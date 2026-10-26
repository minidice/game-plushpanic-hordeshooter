using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Five real-time minutes of controlled Stage combat, not a human audition or campaign playthrough.</summary>
public partial class AudioCombatSoak : Node
{
    private Stage _stage = null!;
    private readonly string _out = ProjectSettings.GlobalizePath("res://output/audio-combat-soak");
    private const double Seconds = 300;
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    private static void Require(bool ok, string label) { if (!ok) throw new Exception(label); }

    public override async void _Ready()
    {
        int exit = 0;
        try { await Run(); GD.Print("AUDIO_COMBAT_SOAK_PASS 300 seconds"); }
        catch (Exception e) { GD.PushError(e.ToString()); exit = 1; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        await Frame();
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    private async Task Run()
    {
        Require(AudioServer.GetDriverName() != "Dummy", "A real audio driver is required");
        Directory.CreateDirectory(_out);
        GameSettings.MasterVolume = GameSettings.SfxVolume = GameSettings.AmbienceVolume = 1;
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false,
            ModeOverride = GameMode.Scenario, SquadOverride = 0, ArrivalOverride = false, NextStageOverride = false };
        AddChild(_stage);
        for (int i = 0; i < 8; i++) await Frame();
        foreach (var e in _stage.AllEnemies.ToArray())
            if (IsInstanceValid(e) && !e.IsQueuedForDeletion()) e.QueueFree();
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        _stage.LeaveSafeRoom();
        var center = new Vector3(24, .05f, 0);
        player.TeleportTo(center);
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        _stage.Cam.SnapTo(center + Vector3.Up);
        await Frame();

        var pre = new AudioEffectCapture { BufferLength = 2 };
        var post = new AudioEffectCapture { BufferLength = 2 };
        AudioServer.AddBusEffect(0, pre, 0);
        AudioServer.AddBusEffect(0, post);
        AudioServer.AddBusEffect(0, new AudioEffectAmplify { VolumeDb = -80 });
        int rate = (int)AudioServer.GetMixRate();
        using var file = File.Create(Path.Combine(_out, "combat.wav"));
        using var wav = new BinaryWriter(file);
        Header(wav, rate, 0);
        var rows = new List<object>();
        long frames = 0, beforeOver = 0, afterOver = 0;
        float beforePeak = 0, afterPeak = 0;
        double energy = 0, distance = 0;
        Vector3 lastPosition = player.Position;
        int wave = -1, hit = -1, minute = -1, lastReport = -1;
        ulong start = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - start < Seconds * 1000)
        {
            double elapsed = (Time.GetTicksMsec() - start) / 1000.0;
            if ((int)(elapsed / 12) != wave)
            {
                wave = (int)(elapsed / 12);
                foreach (var enemy in _stage.AllEnemies.ToArray())
                    if (IsInstanceValid(enemy) && !enemy.IsQueuedForDeletion()) enemy.QueueFree();
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.Tau / 8;
                    _stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,
                        center + new Vector3(Mathf.Cos(angle) * 8, 0, Mathf.Sin(angle) * 5), true);
                }
                _stage.Spawn(MonsterDatabase.Get(wave % 2 == 0 ? "balloon" : "bull"), center + Vector3.Right * 6, true);
                player.Gun.Refill();
            }
            if ((int)(elapsed / 8) != hit)
            {
                hit = (int)(elapsed / 8);
                player.Invulnerable = false;
                player.TakeDamage(1, Vector3.Left, 0);
                player.Invulnerable = true;
                player.Heal(player.MaxHealth);
                if (hit % 2 == 0) player.PressRoll();
            }
            int phase = (int)(elapsed / 60);
            if (phase != minute)
            {
                minute = phase;
                if (phase == 2) player.ApplyHelium(55);
                if (phase == 3) player.Blind(55);
                GD.Print($"SOAK_PHASE {phase + 1}/5 elapsed={elapsed:0}s");
            }
            var target = _stage.AllEnemies.Where(e => IsInstanceValid(e) && !e.IsQueuedForDeletion() && !e.IsDead)
                .OrderBy(e => e.Position.DistanceSquaredTo(player.Position)).FirstOrDefault();
            Vector3 destination = center + new Vector3((float)Math.Cos(elapsed * .3) * 3, 0, (float)Math.Sin(elapsed * .3) * 2);
            // SetControls takes screen-relative movement; convert using the same camera basis as gameplay.
            Vector3 delta = destination - player.Position;
            var right = _stage.Cam.GroundMovement(Vector2.Right);
            var down = _stage.Cam.GroundMovement(Vector2.Down);
            var move = new Vector2(delta.Dot(right), delta.Dot(down)).LimitLength();
            player.SetControls(move, target?.Position + Vector3.Up ?? center + Vector3.Right * 10, target != null);
            if (player.Gun.Mag == 0) player.PressReload();
            await Frame();
            distance += player.Position.DistanceTo(lastPosition);
            lastPosition = player.Position;
            foreach (var sample in pre.GetBuffer(pre.GetFramesAvailable()))
                for (int channel = 0; channel < 2; channel++)
                {
                    float value = sample[channel];
                    Require(float.IsFinite(value), "Non-finite pre-limiter sample");
                    beforePeak = Math.Max(beforePeak, Math.Abs(value));
                    if (Math.Abs(value) >= 1) beforeOver++;
                }
            var output = post.GetBuffer(post.GetFramesAvailable());
            frames += output.Length;
            foreach (var sample in output)
                for (int channel = 0; channel < 2; channel++)
                {
                    float value = sample[channel];
                    Require(float.IsFinite(value), "Non-finite output sample");
                    afterPeak = Math.Max(afterPeak, Math.Abs(value));
                    energy += value * value;
                    if (Math.Abs(value) >= 1) afterOver++;
                    wav.Write((short)Math.Clamp((int)(value * 32767), -32768, 32767));
                }
            int report = (int)(elapsed / 30);
            if (report != lastReport)
            {
                lastReport = report;
                rows.Add(new { elapsed, frames, peakDb = Mathf.LinearToDb(afterPeak), afterOver, distance,
                    helium = AudioSetup.IsEffectOn(AudioSetup.PitchEffect), muffled = AudioSetup.IsEffectOn(AudioSetup.MuffleEffect),
                    shots = _stage.Sfx.Played.GetValueOrDefault("shot_rifle"), deaths = _stage.Sfx.Played.GetValueOrDefault("zombie_die") });
                GD.Print($"SOAK_PROGRESS {elapsed:0}s shots={_stage.Sfx.Played.GetValueOrDefault("shot_rifle")} deaths={_stage.Sfx.Played.GetValueOrDefault("zombie_die")} peak={Mathf.LinearToDb(afterPeak):0.0} over={afterOver}");
                if (report == 2 || report == 8)
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var screenshot = GetViewport().GetTexture().GetImage();
                    screenshot.SavePng(Path.Combine(_out, $"combat-{report * 30}s.png"));
                }
            }
        }
        file.Position = 0;
        Header(wav, rate, checked((int)(frames * 4)));
        wav.Flush();
        AudioSetup.SetHelium(false);
        AudioSetup.SetMuffled(false);
        for (int i = 0; i < 2; i++) AudioServer.RemoveBusEffect(0, AudioServer.GetBusEffectCount(0) - 1);
        AudioServer.RemoveBusEffect(0, 0);
        var played = new Dictionary<string, int>(_stage.Sfx.Played);
        bool passed = frames >= Seconds * rate * .9 && afterPeak > .01 && afterPeak <= Mathf.DbToLinear(-.8f)
            && afterOver == 0 && distance > 30 && played.GetValueOrDefault("shot_rifle") > 100
            && played.GetValueOrDefault("zombie_die") > 20 && played.GetValueOrDefault("honk") > 10;
        File.WriteAllText(Path.Combine(_out, "report.json"), JsonSerializer.Serialize(new { passed, seconds = Seconds,
            recordedSeconds = frames / (double)rate, rate, frames, beforePeakDb = Mathf.LinearToDb(beforePeak), beforeOver,
            afterPeakDb = Mathf.LinearToDb(afterPeak), afterOver, rmsDb = 10 * Math.Log10(energy / Math.Max(1, frames * 2)),
            distance, played, rows, note = "Controlled real-time combat in Stage 1; scripted controls, invulnerability/healing, periodic enemy replacement and ammo refill; ambience enabled. Human audition and campaign difficulty are not assessed." }, new JsonSerializerOptions { WriteIndented = true }));
        Require(passed, "Capture completeness, headroom, movement or repeated-combat coverage failed; see report.json");
    }

    private static void Header(BinaryWriter w, int rate, int bytes)
    {
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
        w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * 4);
        w.Write((short)4); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
    }
}
