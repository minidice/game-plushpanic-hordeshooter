using Godot;
using System;
using System.IO;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>Runtime contracts for the recorded sound rollout. Real mix peaks are checked separately.</summary>
public partial class AudioQualityCheck : Node
{
    private int _checks, _failures;
    private void Check(bool ok, string message)
    {
        _checks++;
        if (!ok) _failures++;
        GD.Print((ok ? "PASS: " : "FAIL: ") + message);
    }

    public override void _Ready()
    {
        try { Run(); }
        catch (Exception e) { GD.PushError(e.ToString()); _failures++; }
        GD.Print($"AUDIO_QUALITY_{(_failures == 0 ? "PASS" : "FAIL")} {_checks} checks, {_failures} failures");
        QuitHygiene.Collect();
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void Run()
    {
        var sfx = new Sfx();
        AddChild(sfx);
        var ids = new[] { "honk", "honk_long", "groan", "zombie_die", "balloon_inflate", "balloon_pop", "bull_bonk", "game_over", "stage_clear",
            "shot_rifle", "zombie_hit", "reload_start", "reload_end", "dry_fire" };
        foreach (string id in new[] { "shot_rifle", "zombie_hit", "reload_start", "reload_end", "dry_fire" })
        {
            var def = SoundRecipes.Find(id)!;
            float legacyGain = id == "shot_rifle" ? 0 : id == "reload_end" ? -5 : -6;
            float ceiling = .91f * Mathf.DbToLinear(legacyGain);
            Check(def.Build(new Synth(42)).Max(v => Math.Abs(v)) * Mathf.DbToLinear(def.VolumeDb) <= ceiling,
                id + " synthesis fallback preserves its previous peak budget if the recording is absent");
        }
        foreach (string id in ids)
        {
            Check(sfx.Overridden.Contains(id) && sfx.Stream(id) is AudioStreamWav w && w.MixRate == 48000
                && !w.Stereo && w.Format == AudioStreamWav.FormatEnum.Format16Bits, id + " uses the shipped mono 48 kHz recording/arrangement");
            if (id is "game_over" or "stage_clear") continue;
            var p = sfx.PlayAt(id, Vector3.Zero, -2);
            Check(p != null && p.MaxDb == p.VolumeDb, id + " cannot amplify beyond its authored gain near the listener");
            p?.Stop();
        }
        foreach (string id in new[] { "honk", "groan", "zombie_die", "shot_rifle", "zombie_hit" })
        {
            var first = sfx.PlayAt(id, Vector3.Zero)!;
            var stream = first.Stream;
            first.Stop();
            var second = sfx.PlayAt(id, Vector3.Zero)!;
            Check(second.Stream != stream, id + " avoids repeating the same vocal take");
            second.Stop();
            if (id is "shot_rifle" or "zombie_hit")
            {
                var seen = new System.Collections.Generic.HashSet<AudioStream>();
                AudioStream? previous = null;
                bool repeated = false;
                for (int i = 0; i < 60; i++)
                {
                    var voice = sfx.Play(id)!;
                    repeated |= voice.Stream == previous;
                    previous = voice.Stream;
                    seen.Add(voice.Stream);
                    voice.Stop();
                }
                Check(seen.Count == 3 && !repeated, id + " loads three distinct takes with no immediate repeat in 2D");
            }
        }
        foreach (string id in new[] { "groan", "zombie_die", "zombie_hit" })
        {
            int count = 0;
            for (int i = 0; i < 8; i++) if (sfx.PlayAt(id, Vector3.Zero) != null) count++;
            Check(count <= 3 && count > 0, id + " drops excessive crowd voices instead of restarting them");
        }
        Check(ArmoryCatalog.Weapon("honk").ShotSound == "honk_ability" && SoundRecipes.Find("honk_ability") != null,
            "the honk weapon uses an ability cue instead of the hurt voice");
        int cue = AudioServer.GetBusIndex("Cues");
        Check(cue >= 0 && AudioServer.GetBusEffectCount(cue) == 0 && AudioServer.GetBusSend(cue) == "Master",
            "Cues bypasses helium and flash effects");
        foreach (string id in new[] { "game_over", "stage_clear", "ui_click", "ear_ring" })
            Check(SoundRecipes.Find(id)!.Bus == "Cues", id + " uses the clean effects bus");
        if (cue >= 0)
        {
            GameSettings.SfxVolume = 0;
            GameSettings.ApplyAudio();
            Check(AudioServer.IsBusMute(cue), "effects mute also mutes result cues");
            GameSettings.SfxVolume = .25f;
            GameSettings.ApplyAudio();
            Check(!AudioServer.IsBusMute(cue) && Math.Abs(AudioServer.GetBusVolumeDb(cue) - Mathf.LinearToDb(.25f)) < .01f,
                "result cues follow effects volume");
            GameSettings.SfxVolume = 1;
            GameSettings.ApplyAudio();
        }
        AudioSetup.Ensure();
        AudioSetup.Ensure();
        var limiters = Enumerable.Range(0, AudioServer.GetBusEffectCount(0)).Select(i => AudioServer.GetBusEffect(0, i))
            .OfType<AudioEffectHardLimiter>().ToArray();
        Check(limiters.Length == 1 && limiters[0].CeilingDb <= -1, "repeated setup installs exactly one master limiter with headroom");
    }
}
