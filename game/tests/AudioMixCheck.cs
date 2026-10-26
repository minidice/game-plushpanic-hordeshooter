using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// What actually comes out of the SFX bus in a real stage (3D placement, listener, voice pool included):
/// walking footsteps and a car siren up close against a rifle shot (I-62, I-63). Needs a real audio device: run windowed.
/// Run: Godot --path . res://tests/AudioMixCheck.tscn
/// </summary>
public partial class AudioMixCheck : Node
{
    private int _checks;
    private AudioEffectCapture _capture = null!;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("AUDIO_MIX_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("AUDIO_MIX_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        var stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
        AddChild(stage);
        await Frames(5);
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        var player = stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        stage.LeaveSafeRoom();
        player.TeleportTo(new Vector3(14, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        int bus = AudioServer.GetBusIndex(AudioSetup.SfxBus);
        _capture = new AudioEffectCapture { BufferLength = .5f };
        AudioServer.AddBusEffect(bus, _capture);
        await Frames(60);

        float quiet = await Peak(30, () => { });
        int steps = stage.Sfx.Played.GetValueOrDefault("step");
        var before = new Dictionary<string, int>(stage.Sfx.Played);
        float walk = await Peak(150, () => player.SetControls(new Vector2(1, 0), player.GlobalPosition + new Vector3(10, 1, 0), false));
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        int walked = stage.Sfx.Played.GetValueOrDefault("step") - steps;
        await Frames(30);
        // The starting frying pan: one swing at the air.
        bool swung = false;
        float swing = await Peak(40, () =>
        {
            player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(10, 1, 0), !swung);
            swung = true;
        });
        player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(10, 1, 0), false);
        await Frames(30);
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        await Frames(10);
        bool fired = false;
        float shot = await Peak(40, () =>
        {
            player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(10, 1, 0), !fired);
            fired = true;
        });
        GD.Print($"mix peak dB: standing {quiet:0.0}, walking {walk:0.0} ({walked} steps), swing {swing:0.0}, rifle {shot:0.0}");
        Check(walked >= 10, $"the goose took steps while walking ({walked})");
        Check(shot > -30, $"the capture hears the mix (rifle {shot:0.0} dB)");
        // Present but under the swing: -4 felt like stomping, -27 and -35 like nothing at all (I-63).
        Check(walk < swing - 4 && walk > shot - 22, $"walking footsteps are clearly heard, under a pan swing ({walk:0.0} vs swing {swing:0.0}, rifle {shot:0.0})");
        Check(swing < shot - 2 && swing > shot - 10, $"a pan swing is a little under a rifle shot ({swing:0.0} vs {shot:0.0})");

        // Getting hit: the honk is a warning, not a blast (I-74).
        player.SetControls(Vector2.Zero, player.GlobalPosition, false);
        // Two alternating files at both pitch-range endpoints: one random take can miss a quiet variant.
        foreach (float pitch in new[] { .96f, 1.04f })
            for (int take = 0; take < 2; take++)
            {
                await Frames(40);
                player.Heal(player.MaxHealth);
                player.Invulnerable = false;
                float hit = await Peak(40, () =>
                {
                    if (player.Health != player.MaxHealth) return;
                    player.TakeDamage(1, Vector3.Left, 0);
                    foreach (var voice in stage.Sfx.GetChildren().OfType<AudioStreamPlayer3D>())
                        if (voice.Playing && (string)voice.GetMeta("sound", "") == "honk") voice.PitchScale = pitch;
                });
                player.Invulnerable = true;
                GD.Print($"mix peak dB: hit honk {hit:0.0}, pitch={pitch}, take={take}");
                Check(hit < shot - 4 && hit > shot - 14,
                    $"the hit honk sits clearly under a rifle shot ({hit:0.0} vs {shot:0.0}, pitch={pitch}, take={take})");
            }

        // The car siren right next to you: noticeable, never blaring (I-62).
        player.SetControls(Vector2.Zero, player.GlobalPosition, false);
        await Frames(60);
        var car = stage.Layout.AlarmCar;
        player.TeleportTo(car.GlobalPosition + new Vector3(0, -car.GlobalPosition.Y, -2.2f));
        await Frames(5);
        car.Trigger();
        float siren = await Peak(90, () => { });
        GD.Print($"mix peak dB: car siren from 2 m {siren:0.0}");
        Check(car.Sounding && siren < shot - 15 && siren > -60, $"the car siren 2 m away is clearly under a rifle shot ({siren:0.0} vs {shot:0.0})");
        AudioServer.RemoveBusEffect(bus, AudioServer.GetBusEffectCount(bus) - 1);
    }

    /// <summary>Loudest sample on the SFX bus over <paramref name="frames"/> physics frames.</summary>
    private async Task<float> Peak(int frames, Action each)
    {
        _capture.ClearBuffer();
        float peak = 0;
        for (int i = 0; i < frames; i++)
        {
            each();
            await Frames(1);
            int n = _capture.GetFramesAvailable();
            if (n <= 0) continue;
            foreach (var s in _capture.GetBuffer(n)) peak = Mathf.Max(peak, Mathf.Max(Mathf.Abs(s.X), Mathf.Abs(s.Y)));
        }
        return Mathf.LinearToDb(Mathf.Max(peak, 1e-6f));
    }
}
