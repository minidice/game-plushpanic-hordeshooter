using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Actual BossEnemy.StartSkill -> FanShot regression; optional real-device output capture.</summary>
public partial class AudioBossGainCheck : Node
{
    [Export] public bool CaptureOutput { get; set; }
    private Stage _stage = null!;
    private string Output => ProjectSettings.GlobalizePath("res://output/audio-boss-gain");
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    private static void Require(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); }

    public override async void _Ready()
    {
        bool passed = false;
        try { await Run(); passed = true; GD.Print("AUDIO_BOSS_GAIN_PASS"); }
        catch (Exception e)
        {
            GD.PushError(e.ToString()); GD.Print("AUDIO_BOSS_GAIN_FAIL " + e.Message);
            if (CaptureOutput)
            {
                Directory.CreateDirectory(Output);
                File.WriteAllText(Path.Combine(Output, "capture.json"), JsonSerializer.Serialize(new
                    { passed = false, capturedUtc = DateTime.UtcNow, failure = e.ToString() }));
            }
        }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        await Frame();
        QuitHygiene.Collect();
        GetTree().Quit(passed ? 0 : 1);
    }

    private async Task Run()
    {
        if (CaptureOutput) Require(AudioServer.GetDriverName() != "Dummy", "Real-device capture cannot use Dummy");
        GameSettings.MasterVolume = GameSettings.SfxVolume = 1;
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false,
            ModeOverride = GameMode.Scenario, SquadOverride = 0, ArrivalOverride = false, NextStageOverride = false };
        AddChild(_stage);
        for (int i = 0; i < 8; i++) await Frame();
        foreach (var enemy in _stage.AllEnemies.ToArray()) enemy.QueueFree();
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        player.TeleportTo(new Vector3(24, .05f, 0));
        player.SetControls(Vector2.Zero, player.Position + Vector3.Right, false);
        AudioServer.SetBusMute(AudioServer.GetBusIndex(AudioSetup.AmbienceBus), true);
        var boss = (BossEnemy)_stage.Spawn(MonsterDatabase.Get(BossEnemy.MonsterId), player.Position + Vector3.Right * 4, true);
        for (int i = 0; i < 4; i++) await Frame();

        AudioEffectCapture? capture = null;
        var frames = new List<Vector2>();
        if (CaptureOutput)
        {
            capture = new AudioEffectCapture { BufferLength = 2 };
            AudioServer.AddBusEffect(0, capture);
            AudioServer.AddBusEffect(0, new AudioEffectAmplify { VolumeDb = -80 });
            await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
            capture.ClearBuffer();
        }
        int shots = _stage.Sfx.Played.GetValueOrDefault("shot_shotgun");
        Require(boss.StartSkill(BossSkillKind.FanShot), "Boss must start the actual fan windup");
        AudioStreamPlayer3D? shot = null;
        float maxDb = float.NaN, volumeDb = float.NaN, pitch = float.NaN;
        ulong start = Time.GetTicksMsec();
        double seconds = CaptureOutput ? 2.4 : 1.8;
        while (Time.GetTicksMsec() - start < seconds * 1000)
        {
            await Frame();
            if (capture != null && capture.GetFramesAvailable() > 0) frames.AddRange(capture.GetBuffer(capture.GetFramesAvailable()));
            if (shot == null && _stage.Sfx.Played.GetValueOrDefault("shot_shotgun") > shots)
            {
                shot = _stage.Sfx.GetChildren().OfType<AudioStreamPlayer3D>()
                    .Single(p => p.Playing && (string)p.GetMeta("sound", "") == "shot_shotgun");
                maxDb = shot.MaxDb; volumeDb = shot.VolumeDb; pitch = shot.PitchScale;
                GD.Print($"BOSS_FAN_VOICE gain={volumeDb:0.0} cap={maxDb:0.0} pitch={pitch:0.000}");
                Require(boss.Uses.GetValueOrDefault(BossSkillKind.FanShot) == 1, "The actual boss fan skill must execute once");
                Require(maxDb <= 3 && Math.Abs(volumeDb - 3) < .01f,
                    $"Boss fan must preserve previous +3 dB near ceiling, got gain {volumeDb}, cap {maxDb}");
                Require(pitch >= .6649f && pitch <= .7351f, "Boss fan keeps its .7 pitch and existing jitter");
                // Keep this short capture on the requested fan: no automatic next skill after its cooldown.
                boss.SetPhysicsProcess(false);
            }
        }
        Require(shot != null, "Actual fan shot was not emitted before timeout");
        Require(SoundRecipes.Find("shot_shotgun")!.VolumeDb == 2, "Player shotgun gain stays +2 dB");
        if (capture == null) return;
        int rate = (int)AudioServer.GetMixRate();
        Require(frames.Count >= seconds * rate * .8, "Incomplete real audio capture");
        Require(capture.GetDiscardedFrames() == 0, "Capture buffer dropped frames");
        float peak = 0;
        double energy = 0;
        int over = 0;
        var pcm = new byte[frames.Count * 4];
        int offset = 0;
        foreach (var frame in frames)
            foreach (float value in new[] { frame.X, frame.Y })
            {
                Require(float.IsFinite(value), "Non-finite actual audio sample");
                peak = Math.Max(peak, Math.Abs(value)); energy += value * value;
                if (Math.Abs(value) >= 1) over++;
                short sample = (short)Math.Clamp((int)(value * 32767), short.MinValue, short.MaxValue);
                pcm[offset++] = (byte)sample; pcm[offset++] = (byte)(sample >> 8);
            }
        float peakDb = Mathf.LinearToDb(Math.Max(peak, .000001f));
        bool passed = peak > .0001 && peakDb <= -.8 && over == 0;
        Directory.CreateDirectory(Output);
        using var wav = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = true, Data = pcm };
        Require(wav.SaveToWav(Path.Combine(Output, "boss-fan.wav")) == Error.Ok, "WAV export failed");
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(ProjectSettings.GlobalizePath("res://" + path)))).ToLowerInvariant();
        File.WriteAllText(Path.Combine(Output, "capture.json"), JsonSerializer.Serialize(new
        {
            passed, capturedUtc = DateTime.UtcNow, driver = AudioServer.GetDriverName(), rate, frames = frames.Count,
            seconds = frames.Count / (double)rate, peakDb, rmsDb = 10 * Math.Log10(energy / (2 * frames.Count)),
            overFullScaleSamples = over, shotGainDb = volumeDb, shotMaxDb = maxDb, shotPitch = pitch,
            soundManifestSha256 = Hash("content/sounds/credits.json"),
            audioSourceSha256 = new[] { "scripts/Enemies/BossEnemy.cs", "scripts/Audio/SoundRecipes.cs", "scripts/Audio/Sfx.cs",
                "scripts/Audio/AudioSetup.cs", "tests/AudioBossGainCheck.cs", "content/data/bosses/boss.json" }.ToDictionary(p => p, Hash),
            note = "Actual BossEnemy.StartSkill/FanShot at 4m from the player; includes windup and fan. Master capture after limiter and before -80dB monitor. No normalization."
        }, new JsonSerializerOptions { WriteIndented = true }));
        AudioServer.RemoveBusEffect(0, AudioServer.GetBusEffectCount(0) - 1);
        AudioServer.RemoveBusEffect(0, AudioServer.GetBusEffectCount(0) - 1);
        Require(passed, "Actual output must contain signal within final ceiling");
        GD.Print($"BOSS_FAN_CAPTURE peak={peakDb:0.0} frames={frames.Count} over={over}");
    }
}
