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
        catch (Exception e)
        {
            GD.PushError(e.ToString()); exit = 1;
            Directory.CreateDirectory(_out);
            File.WriteAllText(Path.Combine(_out, "report.json"), JsonSerializer.Serialize(new
                { passed = false, capturedUtc = DateTime.UtcNow, failure = e.ToString() }));
        }
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
        int arenaRecoveries = 0, waveRecenters = 0, intervalShots = 0, intervalHits = 0, intervalDeaths = 0;
        var combatIntervals = new List<object>();
        void CheckCombatInterval(int completedMinute)
        {
            int shots = _stage.Sfx.Played.GetValueOrDefault("shot_rifle");
            int hits = _stage.Sfx.Played.GetValueOrDefault("zombie_hit");
            int deaths = _stage.Sfx.Played.GetValueOrDefault("zombie_die");
            int shotDelta = shots - intervalShots, hitDelta = hits - intervalHits, deathDelta = deaths - intervalDeaths;
            Require(shotDelta > 50 && hitDelta > 5 && deathDelta > 0,
                $"Minute {completedMinute} lost real combat coverage: shots={shotDelta}, hits={hitDelta}, deaths={deathDelta}");
            combatIntervals.Add(new { minute = completedMinute, shots = shotDelta, hits = hitDelta, deaths = deathDelta });
            intervalShots = shots; intervalHits = hits; intervalDeaths = deaths;
        }
        ulong start = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - start < Seconds * 1000)
        {
            double elapsed = (Time.GetTicksMsec() - start) / 1000.0;
            Require(player.Position.IsFinite() && player.Velocity.IsFinite(), "Non-finite arena actor state");
            // This controlled arena has a low fence. Bull launches can clear it even while invulnerable.
            // Recover before freefall; ordinary death below the map is disabled by this fixture's invulnerability.
            var arenaOffset = player.Position - center;
            if (Math.Abs(arenaOffset.X) > 6 || Math.Abs(arenaOffset.Z) > 4.5f || player.Position.Y < -.25f || player.Position.Y > 3)
            {
                GD.Print($"SOAK_ARENA_RECOVERY {elapsed:0.0}s position={player.Position}");
                arenaRecoveries++;
                player.TeleportTo(center);
                _stage.Cam.SnapTo(center + Vector3.Up);
                lastPosition = player.Position;
            }
            if ((int)(elapsed / 12) != wave)
            {
                wave = (int)(elapsed / 12);
                player.TeleportTo(center);
                lastPosition = player.Position; // Authored wave setup is not measured as walking.
                waveRecenters++;
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
                if (minute >= 0) CheckCombatInterval(phase);
                minute = phase;
                if (phase == 2) player.ApplyHelium(55);
                if (phase == 3) player.Blind(55);
                GD.Print($"SOAK_PHASE {phase + 1}/5 elapsed={elapsed:0}s");
            }
            var target = _stage.AllEnemies.Where(e => IsInstanceValid(e) && !e.IsQueuedForDeletion() && !e.IsDead)
                .OrderBy(e => e.Position.DistanceSquaredTo(player.Position)).FirstOrDefault();
            Vector3 destination = center + new Vector3((float)Math.Cos(elapsed * .3) * 3, 0, (float)Math.Sin(elapsed * .3) * 2);
            // SetControls consumes world X/Z. Only ReadInput converts human screen-relative input.
            Vector3 delta = destination - player.Position;
            var move = new Vector2(delta.X, delta.Z).LimitLength();
            player.SetControls(move, target?.Position + Vector3.Up ?? center + Vector3.Right * 10, target != null);
            if (player.Gun.Mag == 0) player.PressReload();
            await Frame();
            Require(player.Position.IsFinite() && player.Position.Y > -1 && player.Position.Y < 4,
                "Actor escaped the controlled combat height");
            distance += new Vector2(player.Position.X - lastPosition.X, player.Position.Z - lastPosition.Z).Length();
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
                    shots = _stage.Sfx.Played.GetValueOrDefault("shot_rifle"), deaths = _stage.Sfx.Played.GetValueOrDefault("zombie_die"),
                    position = new[] { player.Position.X, player.Position.Y, player.Position.Z }, arenaRecoveries });
                GD.Print($"SOAK_PROGRESS {elapsed:0}s shots={_stage.Sfx.Played.GetValueOrDefault("shot_rifle")} deaths={_stage.Sfx.Played.GetValueOrDefault("zombie_die")} peak={Mathf.LinearToDb(afterPeak):0.0} over={afterOver}");
                if (report == 2 || report == 8)
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var screenshot = GetViewport().GetTexture().GetImage();
                    screenshot.SavePng(Path.Combine(_out, $"combat-{report * 30}s.png"));
                }
            }
        }
        CheckCombatInterval(5);
        file.Position = 0;
        Header(wav, rate, checked((int)(frames * 4)));
        wav.Flush();
        AudioSetup.SetHelium(false);
        AudioSetup.SetMuffled(false);
        for (int i = 0; i < 2; i++) AudioServer.RemoveBusEffect(0, AudioServer.GetBusEffectCount(0) - 1);
        AudioServer.RemoveBusEffect(0, 0);
        var played = new Dictionary<string, int>(_stage.Sfx.Played);
        bool passed = frames >= Seconds * rate * .9 && afterPeak > .01 && afterPeak <= Mathf.DbToLinear(-.8f)
            && afterOver == 0 && distance > 30 && distance < 2000 && played.GetValueOrDefault("shot_rifle") > 100
            && played.GetValueOrDefault("zombie_die") > 20 && played.GetValueOrDefault("honk") > 10;
        File.WriteAllText(Path.Combine(_out, "report.json"), JsonSerializer.Serialize(new { passed, seconds = Seconds,
            capturedUtc = DateTime.UtcNow,
            soundManifestSha256 = Fingerprint("content/sounds/credits.json"),
            audioSourceSha256 = new[] { "scripts/Audio/SoundRecipes.cs", "scripts/Audio/Sfx.cs", "scripts/Audio/AudioSetup.cs",
                "scripts/Audio/SurfaceAudio.cs", "scripts/Audio/SurfaceProfiles.cs", "content/data/surface_profiles.json",
                "scripts/Weapons/WeaponController.cs", "scripts/Player/Player.cs", "scripts/Enemies/Enemy.cs",
                "scripts/Enemies/BullEnemy.cs", "scripts/Enemies/BalloonEnemy.cs", "scripts/World/Stage.cs", "tests/AudioCombatSoak.cs" }
                .ToDictionary(path => path, Fingerprint),
            recordedSeconds = frames / (double)rate, rate, frames, beforePeakDb = Mathf.LinearToDb(beforePeak), beforeOver,
            afterPeakDb = Mathf.LinearToDb(afterPeak), afterOver, rmsDb = 10 * Math.Log10(energy / Math.Max(1, frames * 2)),
            distance, arenaRecoveries, waveRecenters, combatIntervals, played, rows,
            note = "Controlled real-time combat in Stage 1; scripted world-XZ controls, invulnerability/healing, periodic enemy replacement/player recenter/ammo refill, bounded arena recovery; planar movement excludes teleports. Each minute requires fresh shots/hits/deaths; ambience enabled. Human audition and campaign difficulty are not assessed." }, new JsonSerializerOptions { WriteIndented = true }));
        Require(passed, "Capture completeness, headroom, movement or repeated-combat coverage failed; see report.json");
    }

    private static string Fingerprint(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        File.ReadAllBytes(ProjectSettings.GlobalizePath("res://" + path)))).ToLowerInvariant();

    private static void Header(BinaryWriter w, int rate, int bytes)
    {
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
        w.Write((short)1); w.Write((short)2); w.Write(rate); w.Write(rate * 4);
        w.Write((short)4); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
    }
}
