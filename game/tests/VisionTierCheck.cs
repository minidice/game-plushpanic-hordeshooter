using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Visible / silhouette / hidden (decisions.md C-23, I-67): the cone and touching distance show enemies, a nearby
/// shape or one in lamplight is a silhouette, walls still hide, squadmates' flashlights show what they light,
/// attackers are never invisible, and a hit leaves a direction arc. Windowed runs also save output/vision-*.png.
/// </summary>
public partial class VisionTierCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("VISION_TIER_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("VISION_TIER_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task<Stage> NewStage(int squad = 0)
    {
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(2); }
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = squad };
        AddChild(_stage);
        await Frames(3);
        foreach (var e in _stage.AllEnemies.ToList()) e.QueueFree();
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        _stage.LeaveSafeRoom();
        await Frames(2);
        return _stage;
    }

    private Enemy Idle(Vector3 at) => _stage.SpawnEnemy<Enemy>(_stage.CommonDefinition, at, awake: false);

    private static bool Silhouetted(Enemy e) =>
        e.Rig.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>().Any(m => m.MaterialOverride == CharacterRig.SilhouetteMaterial);

    private async Task Run()
    {
        var stage = await NewStage();
        var player = stage.Player;
        player.TeleportTo(new Vector3(30, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        var ahead = Idle(new Vector3(38, 0, 0));
        var touching = Idle(new Vector3(28.2f, 0, 0));
        var near = Idle(new Vector3(25, 0, .6f));
        var far = Idle(new Vector3(19, 0, .5f));
        await Frames(20);
        Check(ahead.Visibility == VisionSystem.Visibility.Visible && ahead.Rig.Visible && !Silhouetted(ahead), "an enemy in the cone is fully visible");
        Check(touching.Visibility == VisionSystem.Visibility.Visible, "an enemy right behind you (touching distance) is fully visible");
        Check(near.Visibility == VisionSystem.Visibility.Silhouette && near.Rig.Visible && Silhouetted(near) && !near.IsSeen,
            "an enemy 5 m behind you is a silhouette (shape, no detail, no aim assist)");
        Check(far.Visibility == VisionSystem.Visibility.Hidden && !far.Rig.Visible, "an enemy 11 m behind you in the dark stays hidden");
        if (Windowed) await Shot("vision-tiers");

        // Turning around: the silhouette becomes the real model again.
        player.SetControls(Vector2.Zero, new Vector3(20, 1, 0), false);
        await Frames(20);
        Check(near.Visibility == VisionSystem.Visibility.Visible && !Silhouetted(near), "turning toward a silhouette shows it as it is (materials restored)");
        near.TakeHit(new HitInfo(1, Vector3.Right, near.GlobalPosition + Vector3.Up));
        await Frames(1);
        Check(near.Rig.FindChild("HitFlash", true, false) != null, "the hit flash still works on a once-silhouetted enemy");
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        await Frames(2);

        // Appearing is never a hard pop: the shape fills in, then the colors (decisions.md C-23).
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        var comer = Idle(new Vector3(19, 0, .5f));
        await Frames(10);
        var bases = comer.Rig.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>()
            .Select(m => (m, m.MaterialOverlay)).ToList();
        Check(comer.Visibility == VisionSystem.Visibility.Hidden, "a newcomer starts hidden in the dark");
        comer.GlobalPosition = new Vector3(25, 0, .6f);
        for (int i = 0; i < 20 && comer.Visibility == VisionSystem.Visibility.Hidden; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(comer.Visibility == VisionSystem.Visibility.Silhouette && comer.Rig.ShapeFill < 1, $"stepping close, its shape fills in rather than popping ({comer.Rig.ShapeFill:0.00})");
        if (Windowed) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await Shot("vision-shape-fill"); }
        await Frames(20);
        Check(comer.Rig.ShapeFill >= 1, "the silhouette is complete a moment later");
        player.SetControls(Vector2.Zero, new Vector3(20, 1, 0), false);
        for (int i = 0; i < 30 && comer.Visibility != VisionSystem.Visibility.Visible; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(comer.Visibility == VisionSystem.Visibility.Visible && comer.Rig.ColorFill < 1, $"turning to it, the colors fill in rather than popping ({comer.Rig.ColorFill:0.00})");
        if (Windowed) await Shot("vision-color-fill");
        comer.Rig.FlashHit(); // a hit in the middle of the fade must not leave anything stuck
        await Frames(30);
        Check(comer.Rig.ColorFill >= 1 && !Silhouetted(comer) && bases.All(b => !IsInstanceValid(b.m) || b.m.MaterialOverlay == b.MaterialOverlay),
            "after the fade (and a hit flash during it) every mesh is back to its own materials");
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        comer.GlobalPosition = new Vector3(19, 0, .5f);
        for (int i = 0; i < 30 && comer.Visibility != VisionSystem.Visibility.Hidden; i++) await Frames(1);
        Check(!comer.Rig.Visible, "disappearing is immediate");
        comer.QueueFree();

        // Walls still hide: close, but on the other side of the safe room wall.
        player.TeleportTo(new Vector3(14, 0, -2));
        player.SetControls(Vector2.Zero, new Vector3(30, 1, -2), false);
        var walled = Idle(new Vector3(8.5f, 0, -3));
        await Frames(20);
        Check(walled.Visibility == VisionSystem.Visibility.Hidden, "a close enemy behind a wall stays hidden");
        walled.QueueFree();

        // Lamplight: the yard lamp shows a silhouette far outside the 7 m ring.
        player.TeleportTo(new Vector3(110, 0, 2));
        player.SetControls(Vector2.Zero, new Vector3(120, 1, 2), false);
        var lit = Idle(new Vector3(98, 0, -4.5f));
        var dark = Idle(new Vector3(92, 0, 4.5f));
        await Frames(20);
        Check(lit.Visibility == VisionSystem.Visibility.Silhouette, "an enemy in a lamp's glow is a silhouette even 13 m away");
        Check(dark.Visibility == VisionSystem.Visibility.Hidden, "the same distance away from any light stays hidden");
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        await Frames(2);

        // Attackers: a shooter behind you in the dark shows as a silhouette from the windup.
        player.TeleportTo(new Vector3(30, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        // Placed idle so it does not start shooting before we look.
        var shooter = (RangedEnemy)stage.Spawn(MonsterDatabase.Get("armed_animal"), new Vector3(18, 0, 0), false);
        await Frames(5);
        Check(shooter.Visibility == VisionSystem.Visibility.Hidden && !shooter.WindingUp, "a shooter far behind starts out hidden");
        shooter.StartWindup();
        await Frames(2);
        Check(shooter.Visibility == VisionSystem.Visibility.Silhouette, "a shooter aiming at you from the dark becomes a silhouette at once");
        shooter.QueueFree();
        var faded = Idle(new Vector3(19, 0, .5f));
        await Frames(2);
        faded.RevealTo(player, .4f);
        await Frames(2);
        Check(faded.Visibility == VisionSystem.Visibility.Silhouette, "a revealed attacker shows while the reveal lasts");
        await Frames(60);
        Check(faded.Visibility == VisionSystem.Visibility.Hidden, "and fades back into the dark after it");
        faded.QueueFree();

        // Hit arc: something bites you from behind and the HUD points at it.
        int hits = 0;
        player.HitFrom += _ => hits++;
        player.Invulnerable = false;
        var biter = stage.SpawnEnemy<Enemy>(stage.CommonDefinition, new Vector3(28.9f, 0, 0), awake: true);
        for (int i = 0; i < 120 && hits == 0; i++) await Frames(1);
        player.Invulnerable = true;
        Check(hits > 0, "a bite from behind reports where it came from");
        Check(biter.Visibility != VisionSystem.Visibility.Hidden, "the biter is never invisible");
        if (Windowed)
        {
            await Frames(2);
            var arcs = stage.Hud.HitArcAngles;
            // The camera looks toward −Z with the +X route up-right, so a hit from −X shows on the lower left.
            Check(arcs.Count > 0 && arcs.All(a => Mathf.Cos(a) < 0 && Mathf.Sin(a) > 0), $"a red arc shows on the side of the hit ({string.Join(", ", arcs.Select(a => Mathf.RadToDeg(a).ToString("0")))}°)");
            await Shot("vision-hit-arc");
        }
        biter.QueueFree();
        await Frames(2);

        // Squadmates' flashlights are shared: what they light, you see.
        stage = await NewStage(squad: 1);
        player = stage.Player;
        var brain = stage.Squad[0];
        brain.ProcessMode = ProcessModeEnum.Disabled;
        var bot = brain.Bot;
        player.TeleportTo(new Vector3(30.5f, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        bot.TeleportTo(new Vector3(29, 0, 1.5f));
        bot.SetControls(Vector2.Zero, new Vector3(10, 1, 1.5f), false);
        var behind = Idle(new Vector3(18, 0, 1.5f));
        await Frames(20);
        Check(behind.Visibility == VisionSystem.Visibility.Visible && !Silhouetted(behind), "an enemy in a squadmate's beam is fully visible to you");
        bot.SetControls(Vector2.Zero, new Vector3(40, 1, 1.5f), false);
        await Frames(20);
        Check(behind.Visibility == VisionSystem.Visibility.Hidden, "once the beam turns away it is hidden again");
        if (Windowed) await Shot("vision-squad-light");
    }

    private async Task Shot(string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/{name}.png"));
    }
}
