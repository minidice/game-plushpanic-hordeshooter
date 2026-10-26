using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Test-only current-mixer capture. The capture precedes a quiet monitor effect;
/// no game setting is saved and no candidate replaces a production sound.</summary>
public partial class AudioPilotCapture : Node
{
    private AudioEffectCapture _capture = null!;
    private Stage _stage = null!;
    private readonly List<object> _reports = new();
    private readonly string _out = ProjectSettings.GlobalizePath("res://output/audio-pilot");

    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            await Run();
            GD.Print("AUDIO_PILOT_CAPTURE_PASS " + _reports.Count);
        }
        catch (Exception e) { GD.PushError(e.ToString()); failed = true; }
        finally
        {
            File.WriteAllText(Path.Combine(_out, "capture.json"), JsonSerializer.Serialize(new
            {
                capturedUtc = DateTime.UtcNow, passed = !failed,
                note = "Current Sfx and Stage listener; Ambience muted; capture before -80 dB monitor attenuation. PCM is clamped only on export; pre-PCM peaks are reported. No candidates applied.",
                recordings = _reports
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task Run()
    {
        Directory.CreateDirectory(_out);
        GameSettings.MasterVolume = GameSettings.SfxVolume = 1;
        _stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
        AddChild(_stage);
        for (int i = 0; i < 8; i++) await Frame();
        foreach (var e in _stage.AllEnemies.ToList()) e.QueueFree();
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        _stage.Player.SetControls(Vector2.Zero, _stage.Player.GlobalPosition + Vector3.Forward, false);
        AudioServer.SetBusMute(AudioServer.GetBusIndex(AudioSetup.AmbienceBus), true);

        foreach (string id in new[] { "honk", "groan", "zombie_die", "shot_rifle" })
        {
            if (_stage.Sfx.Stream(id) is not AudioStreamWav wav || wav.Format != AudioStreamWav.FormatEnum.Format16Bits)
                throw new InvalidOperationException("Baseline must be PCM16: " + id);
            WriteWave(Path.Combine(_out, id + ".wav"), wav.Data, wav.MixRate, wav.Stereo ? 2 : 1);
        }

        int master = AudioServer.GetBusIndex("Master");
        _capture = new AudioEffectCapture { BufferLength = 2 };
        AudioServer.AddBusEffect(master, _capture);
        // Last effect only silences the device monitor; the measured upstream mix is unaltered.
        AudioServer.AddBusEffect(master, new AudioEffectAmplify { VolumeDb = -80 });
        await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);

        Vector3 ear = _stage.Player.GlobalPosition + Vector3.Up * 1.5f;
        foreach (string id in new[] { "honk", "groan", "zombie_die" })
            foreach (float distance in new[] { .5f, 3f, 8f })
                await Capture($"{id}-{distance:0.0}m", 1.7, () => _stage.Sfx.PlayAt(id, ear + Vector3.Right * distance));

        await Capture("crowd-deaths", 1.5, () =>
        {
            for (int i = 0; i < 6; i++) _stage.Sfx.PlayAt("zombie_die", ear + new Vector3(2 + i * .5f, 0, i % 2));
        });
        await Capture("crowd-and-rifle", 2, () =>
        {
            for (int i = 0; i < 4; i++) _stage.Sfx.PlayAt("groan", ear + new Vector3(2 + i, 0, 1));
            for (int i = 0; i < 6; i++) _stage.Sfx.PlayAt("zombie_die", ear + new Vector3(2 + i * .5f, 0, -1));
            _stage.Sfx.Play("shot_rifle");
            _stage.Sfx.PlayAt("honk", ear);
        });
        AudioServer.RemoveBusEffect(master, AudioServer.GetBusEffectCount(master) - 1);
        AudioServer.RemoveBusEffect(master, AudioServer.GetBusEffectCount(master) - 1);
    }

    private async Task Capture(string name, double seconds, Action trigger)
    {
        _capture.ClearBuffer();
        var frames = new List<Vector2>();
        ulong start = Time.GetTicksMsec();
        trigger();
        while (Time.GetTicksMsec() - start < seconds * 1000)
        {
            await Frame();
            int count = _capture.GetFramesAvailable();
            if (count > 0) frames.AddRange(_capture.GetBuffer(count));
        }
        int rate = (int)AudioServer.GetMixRate();
        if (frames.Count < seconds * rate * .75) throw new InvalidOperationException("Incomplete capture: " + name);
        float peak = 0;
        double sum = 0;
        int over = 0;
        var pcm = new byte[frames.Count * 4];
        var floats = new byte[frames.Count * 8];
        int offset = 0;
        foreach (var f in frames)
            foreach (float value in new[] { f.X, f.Y })
            {
                if (!float.IsFinite(value)) throw new InvalidOperationException("Non-finite capture: " + name);
                peak = Math.Max(peak, Math.Abs(value));
                sum += value * value;
                if (Math.Abs(value) >= 1) over++;
                BitConverter.GetBytes(value).CopyTo(floats, offset * 2);
                short sample = (short)Math.Clamp((int)(value * 32767), -32768, 32767);
                pcm[offset++] = (byte)(sample & 255);
                pcm[offset++] = (byte)((sample >> 8) & 255);
            }
        if (peak < .0001f) throw new InvalidOperationException("No real audio signal: " + name);
        WriteWave(Path.Combine(_out, name + ".wav"), pcm, rate, 2);
        // Unclamped source for later analysis, even when the original mix exceeds 0 dBFS.
        File.WriteAllBytes(Path.Combine(_out, name + ".f32le"), floats);
        float peakDb = Mathf.LinearToDb(peak);
        _reports.Add(new { name, rate, frames = frames.Count, duration = frames.Count / (double)rate,
            peakDb, rmsDb = 20 * Math.Log10(Math.Sqrt(sum / (frames.Count * 2))), overFullScaleSamples = over });
        GD.Print($"CAPTURE {name} peak={peakDb:0.0} dBFS over={over} frames={frames.Count}");
        await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
    }

    private static void WriteWave(string path, byte[] data, int rate, int channels)
    {
        using var file = File.Create(path);
        using var w = new BinaryWriter(file);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + data.Length);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
        w.Write((short)1); w.Write((short)channels); w.Write(rate); w.Write(rate * channels * 2);
        w.Write((short)(channels * 2)); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(data.Length); w.Write(data);
    }
}
