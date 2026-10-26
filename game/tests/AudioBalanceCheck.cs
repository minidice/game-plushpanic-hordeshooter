using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>Headless I-64 contracts: authored spatial gains and priority under pool pressure.</summary>
public partial class AudioBalanceCheck : Node
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
        GD.Print($"AUDIO_BALANCE_{(_failures == 0 ? "PASS" : "FAIL")} {_checks} checks, {_failures} failures");
        QuitHygiene.Collect();
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void Run()
    {
        var sfx = new Sfx();
        AddChild(sfx);
        foreach (var def in SoundRecipes.All)
        {
            var voice = sfx.PlayAt(def.Id, Vector3.Zero, -8)!;
            Check(voice != null && voice.MaxDb <= voice.VolumeDb,
                def.Id + " preserves authored/companion attenuation at the listener");
            voice?.Stop();
        }
        foreach (var (id, extra) in new[] { ("explosion", 2f), ("bull_snort", 6f) })
        {
            var voice = sfx.PlayAt(id, Vector3.Zero, extra)!;
            Check(voice.MaxDb <= 3 && voice.MaxDb == voice.VolumeDb,
                id + " largest gameplay bonus respects the previous near ceiling");
            voice.Stop();
        }
        var emitter = new Node3D();
        AddChild(emitter);
        foreach (string id in new[] { "fire_loop", "mortar_whistle", "hunter_bow_draw", "alarm_loop" })
        {
            var attached = sfx.Attach(id, emitter, -8)!;
            Check(attached.MaxDb == attached.VolumeDb, id + " attached emitter retains caller attenuation");
            attached.Stop();
        }
        // One real voice isolates eviction policy from a large pool's spare capacity.
        var tiny = new Sfx { Pool3DSize = 1, Pool2DSize = 1 };
        AddChild(tiny);
        foreach (string warning in new[] { "sonar", "bull_snort", "mortar_whistle" })
        {
            var shot = tiny.PlayAt("shot_rifle", Vector3.Zero)!;
            var cue = tiny.PlayAt(warning, Vector3.Zero);
            Check(cue != null && (string)shot.GetMeta("sound") == warning,
                warning + " can start when the pool is occupied by weapons");
            var noise = tiny.PlayAt("zombie_step", Vector3.Zero);
            Check(noise == null, "crowd footsteps cannot evict " + warning);
            shot.Stop();
        }
        // Repeated footsteps should finish, rather than reset their attack on every crowd request.
        for (int i = 0; i < 6; i++) sfx.PlayAt("zombie_step", Vector3.Zero);
        Check(sfx.PlayAt("zombie_step", Vector3.Zero) == null, "excess crowd footsteps are dropped");
        var voices = sfx.GetChildren().OfType<AudioStreamPlayer3D>().Where(p => p.Playing).ToArray();
        foreach (var voice in voices) voice.Stop();
        for (int i = 0; i < 6; i++) sfx.PlayAt("shot_smg", Vector3.Zero);
        Check(sfx.PlayAt("shot_smg", Vector3.Zero) != null, "rapid fire still restarts its oldest allowed voice");
    }
}
