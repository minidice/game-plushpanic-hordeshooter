using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class Stage2BridgeCheck : Node
{
    private Stage _stage = null!;
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        if (GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/bridge-" + name + ".png")) != Error.Ok)
            throw new Exception("could not save bridge render " + name);
    }
    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            _stage = new Stage { MapOverride = new LayoutMap(encounters: null, supplies: null), ModeOverride = GameMode.Scenario,
                CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0 };
            AddChild(_stage);
            await Frames(4);
            _stage.Map.Shutter!.SetOpen(true, 0);
            var p = _stage.Player;
            p.ManualControl = true;
            p.Invulnerable = true;
            int route = 0;
            foreach (var pair in new[] {
                (new Vector3(186.75363f, 2.45f, -45.372902f), new Vector3(196, 2.4f, -45)),
                (new Vector3(214.1517f, 2.45f, -43.878563f), new Vector3(217, 2.4f, -45)),
                (new Vector3(183, 2.45f, -22), new Vector3(217, 2.4f, -45)) })
            {
                p.TeleportTo(pair.Item1);
                p.SetControls(Vector2.Zero, p.GlobalPosition + Vector3.Right, false);
                await Frames(2);
                await Shot(++route + "-before");
                var flow = _stage.Flow.CopyLayout(); flow.Riders = true; flow.Update(pair.Item2);
                for (int i = 0; i < 1800 && p.GlobalPosition.DistanceTo(pair.Item2) > .65f; i++)
                {
                    var dir = Rays.FlowDirection(p, flow);
                    p.SetControls(new Vector2(dir.X, dir.Z), p.GlobalPosition + Vector3.Right * 5, false);
                    await Frames(1);
                }
                if (p.GlobalPosition.DistanceTo(pair.Item2) > .65f) throw new Exception($"bridge stuck {p.GlobalPosition}, goal {pair.Item2}");
                p.SetControls(Vector2.Zero, p.GlobalPosition + Vector3.Right, false);
                await Shot(route + "-after");
                GD.Print("PASS: bridge crosses from " + pair.Item1);
            }
            p.TeleportTo(new Vector3(186.75f, 2.45f, -45.37f));
            p.Loadout.SetSlot(2, ArmoryCatalog.Weapon("m249")); p.PressSlot(2);
            p.SetControls(Vector2.Zero, p.GlobalPosition + Vector3.Right * 5, false);
            await Frames(2);
            for (int x = 188; x <= 212; x += 2)
                foreach (float z in new[] { -46f, -44f }) _stage.SpawnCommon(new Vector3(x, 2.45f, z), false);
            var goal = new Vector3(217, 2.4f, -45);
            await Shot("combat-before");
            var combatFlow = _stage.Flow.CopyLayout(); combatFlow.Update(goal);
            for (int i = 0; i < 3600 && p.GlobalPosition.DistanceTo(goal) > .65f; i++)
            {
                if (p.Gun.Mag == 0) p.PressReload();
                var enemy = _stage.AllEnemies.Where(e => Stage2RouteCheck.CanTarget(p, e))
                    .OrderBy(e => e.GlobalPosition.DistanceSquaredTo(p.GlobalPosition))
                    .FirstOrDefault();
                var dir = Rays.FlowDirection(p, combatFlow);
                p.SetControls(new Vector2(dir.X, dir.Z), enemy?.GlobalPosition + Vector3.Up ?? p.GlobalPosition + Vector3.Right * 5, enemy != null);
                if (i == 240) await Shot("combat-middle");
                await Frames(1);
            }
            if (p.GlobalPosition.DistanceTo(goal) > .65f) throw new Exception("bridge combat stuck " + p.GlobalPosition);
            p.SetControls(Vector2.Zero, p.GlobalPosition + Vector3.Right, false);
            await Shot("combat-after");
            GD.Print("PASS: bridge crosses through active crowd");
            p.TeleportTo(new Vector3(255.90784f, 2.227f, -52.8774f));
            p.Loadout.SetSlot(2, ArmoryCatalog.Weapon("m249")); p.PressSlot(2);
            p.SetControls(Vector2.Zero, p.GlobalPosition + Vector3.Right, false);
            var low = _stage.SpawnCommon(new Vector3(256.60904f, 1.20004f, -53.401634f), false);
            low.Stun(30);
            await Frames(2);
            if (!Stage2RouteCheck.CanTarget(p, low)) throw new Exception("route input ignores lower-tier enemy physically blocking descent");
            await Shot("lower-blocker");
            int lowKills = _stage.Kills;
            for (int i = 0; i < 180 && IsInstanceValid(low) && !low.IsDead; i++)
            {
                p.SetControls(Vector2.Zero, low.GlobalPosition + Vector3.Up * Player.AimHeight, true);
                await Frames(1);
            }
            if (_stage.Kills <= lowKills) throw new Exception("selected lower-tier blocker was not hit by real gunfire");
            p.SetControls(Vector2.Zero, p.GlobalPosition + Vector3.Right, false);
            await Shot("lower-cleared");
            GD.Print("PASS: lower-tier blocker is selected and killed by real gunfire");
            GD.Print("STAGE2_BRIDGE_PASS");
        }
        catch (Exception e) { GD.Print("STAGE2_BRIDGE_FAIL " + e); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
}
