using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Real-device I-64 matrix. Uses the Stage listener, Sfx, and contacted floor.
/// Fixed-pitch spatial comparisons isolate distance/gain; volley cases keep gameplay jitter.</summary>
public partial class AudioBalanceCapture : Node
{
    private Stage _stage = null!;
    private readonly Dictionary<string, AudioEffectCapture> _captures = new();
    private readonly List<object> _reports = new();
    private readonly Dictionary<string, float> _near = new();
    private string Output => ProjectSettings.GlobalizePath("res://output/audio-balance");
    private Vector3 Ear => _stage.Player.GlobalPosition + Vector3.Up * 1.5f;

    public override async void _Ready()
    {
        Directory.CreateDirectory(Output);
        bool passed = false;
        string? failure = null;
        try { await Run(); passed = true; GD.Print("AUDIO_BALANCE_CAPTURE_PASS " + _reports.Count); }
        catch (Exception e) { failure = e.ToString(); GD.PushError(failure); GD.Print("AUDIO_BALANCE_CAPTURE_FAIL"); }
        finally
        {
            string[] sources = { "scripts/Audio/SoundRecipes.cs", "scripts/Audio/Sfx.cs", "scripts/Audio/AudioSetup.cs",
                "scripts/Audio/SurfaceAudio.cs", "scripts/Audio/SurfaceProfiles.cs", "content/data/surface_profiles.json",
                "tests/AudioBalanceCapture.cs" };
            string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
                ProjectSettings.GlobalizePath("res://" + path)))).ToLowerInvariant();
            File.WriteAllText(Path.Combine(Output, "capture.json"), JsonSerializer.Serialize(new
            {
                capturedUtc = DateTime.UtcNow, passed, failure, driver = AudioServer.GetDriverName(),
                soundManifestSha256 = Hash("content/sounds/credits.json"),
                audioSourceSha256 = sources.ToDictionary(p => p, Hash),
                note = "Stage listener; Ambience muted; SFX/Cues captured before Master limiter, Master after limiter and before -80 dB monitor. WAV has no normalization; .f32le preserves raw stereo Master samples. Controlled Sfx scenarios do not simulate weapon damage/network events.",
                recordings = _reports
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        QuitHygiene.Collect();
        GetTree().Quit(passed ? 0 : 1);
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    private async Task Run()
    {
        Require(AudioServer.GetDriverName() != "Dummy", "A real audio driver is required; headless/Dummy cannot validate the mix");
        GameSettings.MasterVolume = GameSettings.SfxVolume = 1;
        _stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
        AddChild(_stage);
        for (int i = 0; i < 8; i++) await Frame();
        foreach (var enemy in _stage.AllEnemies.ToList()) enemy.QueueFree();
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        _stage.Player.SetControls(Vector2.Zero, Ear + Vector3.Forward, false);
        AudioServer.SetBusMute(AudioServer.GetBusIndex(AudioSetup.AmbienceBus), true);
        foreach (string bus in new[] { AudioSetup.SfxBus, AudioSetup.CueBus, "Master" })
        {
            var capture = new AudioEffectCapture { BufferLength = 2 };
            AudioServer.AddBusEffect(AudioServer.GetBusIndex(bus), capture);
            _captures.Add(bus, capture);
        }
        AudioServer.AddBusEffect(0, new AudioEffectAmplify { VolumeDb = -80 });
        await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);

        // Explicit variants: comparisons use the largest recorded rifle take, not a lucky random take.
        float rifle = -120;
        for (int take = 1; take <= 3; take++)
        {
            int variant = take;
            var result = await Capture("rifle-reference-" + take, .7, () =>
            {
                var voice = Play("shot_rifle", .5f);
                voice.Stream = GD.Load<AudioStream>("res://content/sounds/shot_rifle" + (variant == 1 ? "" : "_" + variant) + ".wav");
                voice.Play();
            });
            rifle = Math.Max(rifle, result.SfxDb);
        }
        foreach (string id in new[] { "shot_pistol", "shot_smg", "shot_lmg", "shot_shotgun", "shot_sniper", "rocket_launch", "mortar_fire" })
        {
            foreach (float distance in new[] { .5f, 8f, 20f })
            {
                var result = await Capture($"{id}-{distance:0.0}m", 1.2, () => Play(id, distance));
                if (distance == .5f) _near[id] = result.SfxDb;
                else Require(result.SfxDb < _near[id] - (distance == 20 ? 15 : .5f), id + " must attenuate with distance");
            }
            var companion = await Capture(id + "-companion", 1.2, () => Play(id, .5f, -8, 1.15f));
            Require(companion.SfxDb < _near[id] - 6 && companion.SfxDb > _near[id] - 10,
                id + " near companion retains approximately -8 dB caller attenuation");
            int rounds = 0;
            await Capture(id + "-volley-crowd", 2.8, () => { }, elapsed =>
            {
                while (rounds < 8 && elapsed >= rounds * .18)
                {
                    for (int member = 0; member < 4; member++)
                        _stage.Sfx.PlayAt(id, Ear + new Vector3(.5f + member, 0, member * .3f), member == 0 ? 0 : -8, member == 0 ? 1 : 1.15f);
                    for (int enemy = 0; enemy < 6; enemy++)
                    {
                        _stage.Sfx.PlayAt("zombie_hit", Ear + Vector3.Left * 2);
                        _stage.Sfx.PlayAt("groan", Ear + Vector3.Left * 3);
                    }
                    rounds++;
                }
            });
            Require(rounds == 8, "Incomplete volley: " + id);
        }
        Require(_near["shot_shotgun"] > _near["shot_pistol"] + 1 && _near["shot_sniper"] > _near["shot_smg"] + 1,
            "heavy weapons retain their near-level distinction from pistol/SMG");

        foreach (string id in new[] { "bull_snort", "sonar", "mortar_whistle", "lure_beep" })
        {
            var cue = await Capture(id + "-warning", 1.5, () => Play(id, .5f));
            Require(cue.SfxDb > rifle - 24 && cue.SfxDb < rifle + 4, id + " warning remains in the audible combat-cue range");
        }
        await Capture("boss-snort-plus6", 2.3, () => Play("bull_snort", .5f, 6, .45f));
        await Capture("blast-normal", 1.9, () => Play("explosion", .5f));
        await Capture("blast-large-plus2", 1.9, () => Play("explosion", .5f, 2));
        foreach (string id in new[] { "pan_bong", "throw", "roll", "heal", "pickup", "door", "shutter", "glass_break", "flash_bang" })
            await Capture(id + "-near", Math.Max(1.6, _stage.Sfx.Stream(id).GetLength() + .3), () => Play(id, .5f));
        foreach (string id in new[] { "fire_loop", "mortar_whistle" })
        {
            var emitter = new Node3D();
            _stage.AddChild(emitter);
            emitter.GlobalPosition = Ear + Vector3.Right * .5f;
            AudioStreamPlayer3D? attached = null;
            await Capture(id + "-attached-near", 1.6, () =>
            {
                attached = _stage.Sfx.Attach(id, emitter);
                Require(attached != null, "Missing attached voice: " + id);
            });
            attached!.Stop();
            emitter.QueueFree();
        }
        foreach (string cueId in new[] { "ear_ring", "stage_clear", "game_over" })
        {
            double seconds = Math.Max(2.5, _stage.Sfx.Stream(cueId).GetLength() + .3);
            var solo = await Capture(cueId + "-solo", seconds, () => _stage.Sfx.Play(cueId));
            var mix = await Capture(cueId + "-with-blasts", seconds, () =>
            {
                for (int i = 0; i < 5; i++) Play("explosion", .5f + i, 2);
                _stage.Sfx.Play(cueId);
            });
            Require(Math.Abs(solo.CueDb - mix.CueDb) < 1, cueId + " clean cue bus survives explosion pressure");
        }
        // Real contact lookup with controlled floor metadata; the actual actor position and step path remain intact.
        var floor = Rays.Cast(_stage, _stage.Player.GlobalPosition + Vector3.Up * .2f,
            _stage.Player.GlobalPosition + Vector3.Down, Layers.MovementWorld)!.Value.Collider as Node;
        Require(floor != null, "No contacted floor for surface cases");
        bool hadProfile = floor!.HasMeta(SurfaceAudio.ProfileMeta);
        var oldProfile = floor.GetMeta(SurfaceAudio.ProfileMeta, "");
        foreach (string profile in new[] { "asphalt", "metal_sheet", "wood_solid", "sandbag", "soil", "gravel", "grass" })
        {
            floor.SetMeta(SurfaceAudio.ProfileMeta, profile);
            int steps = 0;
            var result = await Capture("steps-" + profile, 2.1, () => { }, elapsed =>
            {
                while (steps < 10 && elapsed >= steps * .17)
                {
                    SurfaceAudio.Step(_stage, _stage.Player, pitch: steps % 2 == 0 ? 1 : 1.1f);
                    steps++;
                }
            });
            Require(steps == 10 && result.SfxDb < rifle - 10 && result.SfxDb > rifle - 35,
                profile + " footsteps remain present and below weapon peaks");
        }
        if (hadProfile) floor.SetMeta(SurfaceAudio.ProfileMeta, oldProfile); else floor.RemoveMeta(SurfaceAudio.ProfileMeta);
        AudioServer.RemoveBusEffect(0, AudioServer.GetBusEffectCount(0) - 1);
        foreach (string bus in _captures.Keys)
        {
            int index = AudioServer.GetBusIndex(bus);
            AudioServer.RemoveBusEffect(index, AudioServer.GetBusEffectCount(index) - 1);
        }
    }

    private AudioStreamPlayer3D Play(string id, float distance, float extraDb = 0, float pitch = 1)
    {
        var voice = _stage.Sfx.PlayAt(id, Ear + Vector3.Right * distance, extraDb, pitch)!;
        Require(voice != null, "Missing voice: " + id);
        voice!.PitchScale = pitch;
        return voice;
    }

    private sealed record Measurement(float SfxDb, float CueDb);
    private async Task<Measurement> Capture(string name, double seconds, Action trigger, Action<double>? tick = null)
    {
        // Stop finite tails too: cases must not inherit previous explosions or ear ringing.
        foreach (var p in _stage.Sfx.GetChildren())
        {
            if (p is AudioStreamPlayer3D p3) p3.Stop();
            if (p is AudioStreamPlayer p2) p2.Stop();
        }
        await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
        var frames = _captures.Keys.ToDictionary(k => k, _ => new List<Vector2>());
        var discarded = _captures.ToDictionary(kv => kv.Key, kv => kv.Value.GetDiscardedFrames());
        foreach (var capture in _captures.Values) capture.ClearBuffer();
        ulong start = Time.GetTicksMsec();
        trigger();
        while (Time.GetTicksMsec() - start < seconds * 1000)
        {
            tick?.Invoke((Time.GetTicksMsec() - start) / 1000.0);
            await Frame();
            foreach (var (bus, capture) in _captures)
            {
                int n = capture.GetFramesAvailable();
                if (n > 0) frames[bus].AddRange(capture.GetBuffer(n));
            }
        }
        int rate = (int)AudioServer.GetMixRate();
        var metrics = new Dictionary<string, object>();
        var peaks = new Dictionary<string, float>();
        foreach (var (bus, samples) in frames)
        {
            Require(samples.Count >= seconds * rate * .8, name + ": incomplete " + bus + " capture");
            Require(_captures[bus].GetDiscardedFrames() == discarded[bus], name + ": dropped " + bus + " frames");
            float peak = 0;
            double energy = 0;
            int over = 0;
            foreach (var frame in samples)
                foreach (float value in new[] { frame.X, frame.Y })
                {
                    Require(float.IsFinite(value), name + ": nonfinite audio");
                    peak = Math.Max(peak, Math.Abs(value)); energy += value * value;
                    if (Math.Abs(value) >= 1) over++;
                }
            peaks[bus] = Mathf.LinearToDb(Math.Max(peak, .000001f));
            metrics[bus] = new { frames = samples.Count, peakDb = peaks[bus],
                rmsDb = 20 * Math.Log10(Math.Max(Math.Sqrt(energy / (2 * samples.Count)), .000001)), overFullScaleSamples = over };
            if (bus == "Master")
            {
                Require(peak > .0001, name + ": no real signal");
                Require(over == 0 && peaks[bus] <= -.8, name + ": final output ceiling exceeded");
            }
        }
        var master = frames["Master"];
        var pcm = new byte[master.Count * 4];
        using (var raw = new BinaryWriter(File.Create(Path.Combine(Output, name + ".f32le"))))
        {
            int i = 0;
            foreach (var frame in master)
                foreach (float value in new[] { frame.X, frame.Y })
                {
                    raw.Write(value);
                    short sample = (short)Math.Clamp((int)(value * 32767), short.MinValue, short.MaxValue);
                    pcm[i++] = (byte)sample; pcm[i++] = (byte)(sample >> 8);
                }
        }
        using var wav = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, Stereo = true, MixRate = rate, Data = pcm };
        Require(wav.SaveToWav(Path.Combine(Output, name + ".wav")) == Error.Ok, name + ": WAV save failed");
        _reports.Add(new { name, rate, duration = master.Count / (double)rate, buses = metrics });
        GD.Print($"BALANCE_CAPTURE {name}: SFX={peaks[AudioSetup.SfxBus]:0.0}, Cues={peaks[AudioSetup.CueBus]:0.0}, Master={peaks["Master"]:0.0}");
        return new Measurement(peaks[AudioSetup.SfxBus], peaks[AudioSetup.CueBus]);
    }
}
