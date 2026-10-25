using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Windowed real-device SFX capture, including near-listener attenuation and crowd voice caps.</summary>
public partial class HunterAudioMixCheck : Node3D
{
    private AudioEffectCapture _capture = null!;
    private readonly List<Vector2> _demo = new();
    private readonly List<string> _cueTimes = new();
    private int _checks;
    private int _captureBus = -1;
    private int _captureIndex = -1;

    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("HUNTER_AUDIO_MIX_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("HUNTER_AUDIO_MIX_FAIL " + e.Message); failed = true; }
        finally
        {
            if (_captureBus >= 0 && _captureIndex >= 0) AudioServer.RemoveBusEffect(_captureBus, _captureIndex);
        }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        Check(AudioServer.GetDriverName() != "Dummy", "capture uses a real audio driver (run windowed)");
        var camera = new Camera3D { Position = new Vector3(0, 1.5f, 0), Current = true };
        AddChild(camera);
        var ears = new AudioListener3D { Position = new Vector3(0, 1.5f, 0) };
        AddChild(ears);
        ears.MakeCurrent();
        var sfx = new Sfx();
        AddChild(sfx);
        // Isolated scene: no stage footsteps or ambience. Keep the recipe and 3D pipeline intact.
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(AudioSetup.SfxBus), 0);
        AudioServer.SetBusMute(AudioServer.GetBusIndex(AudioSetup.SfxBus), false);
        _captureBus = AudioServer.GetBusIndex(AudioSetup.SfxBus);
        _capture = new AudioEffectCapture { BufferLength = 1f };
        _captureIndex = AudioServer.GetBusEffectCount(_captureBus);
        AudioServer.AddBusEffect(_captureBus, _capture);
        await WaitFrames(20);

        var source = new Vector3(0, 1.5f, -2);
        var rifle = await Capture("rifle reference", .7f, () => sfx.PlayAt("shot_rifle", source));
        Check(rifle.PeakDb > -30, $"actual capture hears the reference rifle ({rifle.PeakDb:0.0} dBFS)");
        string[] ids = { "hunter_club_raise", "hunter_club_swing", "hunter_club_hit",
            "hunter_knife_ready", "hunter_knife_swing", "hunter_knife_hit",
            "hunter_bow_draw", "hunter_bow_release", "hunter_bow_hit" };
        foreach (string id in ids)
        {
            float duration = (float)sfx.Stream(id).GetLength() + .35f;
            var measured = await Capture(id, duration, () => sfx.PlayAt(id, source));
            // Provisional mix guardrails, not a listening-quality judgment: distinguishable but below gunfire.
            Check(measured.PeakDb > rifle.PeakDb - 32 && measured.RmsDb > -55,
                $"{id} remains audible near the listener (peak {measured.PeakDb:0.0}, RMS {measured.RmsDb:0.0} dBFS)");
            Check(measured.PeakDb < rifle.PeakDb - 7,
                $"{id} stays below the rifle ({measured.PeakDb - rifle.PeakDb:0.0} dB relative peak)");
            Check(measured.Peak < .98f, id + " has no clipped bus samples");
        }

        var hunters = new List<Node3D>();
        var draws = new List<AudioStreamPlayer3D>();
        var swings = new HashSet<AudioStreamPlayer3D>();
        for (int i = 0; i < 8; i++)
        {
            float angle = MathF.Tau * i / 8;
            var hunter = new Node3D { Position = new Vector3(MathF.Sin(angle) * 2, 1.5f, MathF.Cos(angle) * 2) };
            AddChild(hunter);
            hunters.Add(hunter);
        }
        var crowd = await Capture("8 hunters: draw and club swing requests", 1.6f, () =>
        {
            foreach (var hunter in hunters)
            {
                if (sfx.Attach("hunter_bow_draw", hunter) is { } draw) draws.Add(draw);
                if (sfx.PlayAt("hunter_club_swing", hunter.GlobalPosition) is { } swing) swings.Add(swing);
            }
        });
        Check(draws.Count == 3, "eight attached bow-draw requests admit exactly three voices");
        Check(swings.Count == 3, "eight pooled club-swing requests reuse exactly three voices");
        Check(crowd.Peak < .98f, $"the close crowd mix does not clip (peak {crowd.PeakDb:0.0} dBFS)");
        Check(crowd.PeakDb < rifle.PeakDb - 2 && crowd.PeakDb > rifle.PeakDb - 32,
            $"the close crowd remains audible and under gunfire ({crowd.PeakDb - rifle.PeakDb:0.0} dB relative peak)");
        foreach (var hunter in hunters) hunter.QueueFree();

        SaveDemo();
        sfx.QueueFree();
        await WaitFrames(2);
    }

    private async Task<(float Peak, float PeakDb, float RmsDb)> Capture(string name, float seconds, Action play)
    {
        _capture.ClearBuffer();
        long discardedBefore = _capture.GetDiscardedFrames();
        _cueTimes.Add($"{_demo.Count / AudioServer.GetMixRate():0.000}s {name}");
        play();
        double energy = 0;
        float peak = 0;
        int count = 0;
        for (int i = 0; i < (int)Math.Ceiling(seconds * Engine.PhysicsTicksPerSecond); i++)
        {
            await WaitFrames(1);
            int available = _capture.GetFramesAvailable();
            if (available <= 0) continue;
            foreach (var sample in _capture.GetBuffer(available))
            {
                if (!float.IsFinite(sample.X) || !float.IsFinite(sample.Y)) throw new Exception(name + ": nonfinite bus sample");
                peak = MathF.Max(peak, MathF.Max(MathF.Abs(sample.X), MathF.Abs(sample.Y)));
                energy += sample.X * sample.X + sample.Y * sample.Y;
                count++;
                _demo.Add(sample);
            }
        }
        Check(count > AudioServer.GetMixRate() * seconds * .5f, name + " captures real-time stereo samples");
        Check(_capture.GetDiscardedFrames() == discardedBefore, name + " captures without buffer overflow");
        float peakDb = Mathf.LinearToDb(MathF.Max(peak, 1e-6f));
        float rmsDb = Mathf.LinearToDb(MathF.Max((float)Math.Sqrt(energy / (2 * count)), 1e-6f));
        GD.Print($"hunter mix {name}: peak {peakDb:0.0} dBFS, RMS {rmsDb:0.0} dBFS, frames {count}");
        return (peak, peakDb, rmsDb);
    }

    private void SaveDemo()
    {
        byte[] pcm = new byte[_demo.Count * 4];
        for (int i = 0; i < _demo.Count; i++)
        {
            // The reference rifle retains its existing +3 dB near cap. Fixed export headroom prevents
            // that reference from clipping PCM16; relative levels and measured raw bus values are unchanged.
            short left = (short)Math.Clamp((int)(_demo[i].X * .5f * 32767), short.MinValue, short.MaxValue);
            short right = (short)Math.Clamp((int)(_demo[i].Y * .5f * 32767), short.MinValue, short.MaxValue);
            pcm[i * 4] = (byte)left;
            pcm[i * 4 + 1] = (byte)(left >> 8);
            pcm[i * 4 + 2] = (byte)right;
            pcm[i * 4 + 3] = (byte)(right >> 8);
        }
        using var wav = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = (int)AudioServer.GetMixRate(), Stereo = true, Data = pcm };
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        Check(wav.SaveToWav("res://output/hunter-audio-demo.wav") == Error.Ok, "captured stereo demo is saved");
        System.IO.File.WriteAllLines(ProjectSettings.GlobalizePath("res://output/hunter-audio-demo.txt"),
            new[] { "Actual SFX bus capture at 2 m; rifle, nine hunter cues, then eight hunter requests.",
                "Fixed -6.02 dB WAV export gain, no normalization. Measurements use raw bus levels.",
                "Bus capture excludes the operating system and speaker volume." }.Concat(_cueTimes));
    }

    private async Task WaitFrames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
}
