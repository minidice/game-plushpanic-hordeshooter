using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>One continuous draft playthrough, using movement and real E input, with no test teleports or mission skips.</summary>
public partial class Stage2RouteCheck : Node
{
    private Stage _stage = null!;
    private LayoutMap _map = null!;
    private int _checks, _ticks, _ladderUses, _zipUses, _peakEnemies;
    private bool _firePulse;
    private Enemy? _target;
    internal static bool CanTarget(Player player, Enemy enemy)
    {
        if (enemy.IsDead) return false;
        var from = player.GlobalPosition + Vector3.Up * Player.AimHeight;
        var to = enemy.GlobalPosition with { Y = from.Y };
        if (from.DistanceTo(to) >= Math.Min(22, player.Gun.Def.MaxRange)) return false;
        var tiers = player.Stage.Tiers;
        if (player.Gun.IsMelee)
        {
            if (!tiers.MeleeReach(player.GlobalPosition, enemy.GlobalPosition)) return false;
        }
        else if (tiers.TierOf(enemy) >= tiers.TierOf(player))
        {
            float height = Mathf.Max(Enemy.MinHitHeight, Mathf.Max(enemy.Definition.ColliderHeight, enemy.Radius * 2.05f));
            if (from.Y < enemy.GlobalPosition.Y || from.Y > enemy.GlobalPosition.Y + height) return false;
        }
        // Hitscan also hits lower tiers on the floor plan (WeaponController.LowerTierTargets).
        // Test visibility at the actual shot height, not on a sloping ray into the cliff/deck.
        return Rays.Clear(player, from, to, Layers.World | Layers.Props);
    }
    private Player P => _stage.Player;
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }
    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            _ticks++;
            _peakEnemies = Math.Max(_peakEnemies, _stage.AllEnemies.Count(e => !e.IsDead));
        }
    }
    private void Drive(Vector3 dir)
    {
        if (P.Gun.Mag == 0) P.PressReload();
        if (!P.Gun.HasInfiniteReserve && P.Gun.TotalAmmo == 0) P.PressSlot(1);
        var enemy = _stage.AllEnemies.Where(e => CanTarget(P, e))
            .OrderBy(e => e.GlobalPosition.DistanceSquaredTo(P.GlobalPosition))
            .FirstOrDefault();
        // Small crowd movements should not turn the scripted aim back and forth every frame.
        // Keep a valid target until another threat is materially closer.
        if (_target != null && IsInstanceValid(_target) && CanTarget(P, _target) &&
            (enemy == null || P.GlobalPosition.DistanceTo(_target.GlobalPosition) <= P.GlobalPosition.DistanceTo(enemy.GlobalPosition) + 2f)) enemy = _target;
        bool fire = enemy != null;
        _target = enemy;
        if (!P.Gun.Def.Automatic) { bool press = fire && !_firePulse; _firePulse = fire && press; fire = press; }
        P.SetControls(new Vector2(dir.X, dir.Z), enemy != null ? enemy.GlobalPosition + Vector3.Up * Player.AimHeight
            : P.GlobalPosition + (dir.LengthSquared() > .01f ? dir : Vector3.Right) * 5 + Vector3.Up, fire);
    }
    private async Task Shot(string name)
    {
        GD.Print($"ROUTE: {name} at {P.GlobalPosition}, {_ticks / 60f:0.0}s, kills {_stage.Kills}");
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/route-" + name + ".png")) == Error.Ok, "render " + name);
    }
    private async Task Use(Interactable item, string label)
    {
        P.SetControls(Vector2.Zero, item.GlobalPosition, false);
        await Frames(3);
        Check(_stage.FocusedInteractable == item, label + " is the actual focused E prompt");
        P.ManualControl = false;
        Input.ParseInputEvent(new InputEventAction { Action = GameInput.Interact, Pressed = true });
        await Frames(2);
        Input.ParseInputEvent(new InputEventAction { Action = GameInput.Interact, Pressed = false });
        P.ManualControl = true;
        await Frames(2);
    }
    // Godot renames repeated LadderUp/LadderDown nodes; position is the stable identity here.
    private Interactable At(Vector3 at) => _stage.Interactables.Single(i => i.GlobalPosition.DistanceTo(at) < .1f);
    private async Task Walk(Vector3 goal, string label, float near = .65f)
    {
        var flow = _stage.Flow.CopyLayout();
        flow.Riders = true;
        flow.Update(goal);
        Check(flow.Reachable(P.GlobalPosition), label + " has a route from the current position");
        var last = P.GlobalPosition;
        int moved = _ticks, began = _ticks;
        while (P.GlobalPosition.DistanceTo(goal) > near && _ticks - began < 60 * 180)
        {
            if (P.OnLadder || P.OnZipline || _map.GondolaStation!.Riding)
            {
                P.SetControls(Vector2.Zero, P.AimPoint, false);
                await Frames(1);
                moved = _ticks;
                continue;
            }
            int cell = _stage.Tiers.Index(P.GlobalPosition), next = flow.NextCell(P.GlobalPosition);
            var ladder = _stage.Ladders.FirstOrDefault(l => (l.BottomCell == cell && next == l.TopCell) || (l.TopCell == cell && next == l.BottomCell));
            if (ladder != null)
            {
                bool up = cell == ladder.BottomCell;
                await Use(At(up ? ladder.Bottom : ladder.Top), label + " ladder");
                Check(P.OnLadder, label + " E starts climbing");
                _ladderUses++;
                await Shot("ladder-" + _ladderUses);
                continue;
            }
            var zip = _stage.Ziplines.FirstOrDefault(z => z.StartCell == cell && next == z.EndCell);
            if (zip != null)
            {
                await Use(At(zip.Start), "tower zipline");
                Check(P.OnZipline, "E starts the tower zipline");
                _zipUses++;
                await Frames(30);
                await Shot("zipline-departure");
                await Frames(100);
                await Shot("zipline-mid");
                continue;
            }
            var delta = goal - P.GlobalPosition;
            var dir = (delta with { Y = 0 }).Length() < 1.6f ? (delta with { Y = 0 }).Normalized() : Rays.FlowDirection(P, flow);
            // Combat can push the scripted player into a physically open pocket omitted by the coarse grid.
            // Like a player looking at the doorway, walk toward the visible goal; never cross a wall or skip movement.
            if (dir == Vector3.Zero && Rays.Clear(P, P.GlobalPosition + Vector3.Up * .5f,
                goal + Vector3.Up * .5f, Layers.World | Layers.Props)) dir = (delta with { Y = 0 }).Normalized();
            Drive(dir);
            if (_ticks - moved > 120 && _ticks % 30 == 0)
            {
                GD.Print($"BRIDGE_TRACE {_ticks}: p {P.GlobalPosition} dir {dir} vel {P.Velocity} blocked {flow.IsBlocked(P.GlobalPosition)} cost {flow.Distance(P.GlobalPosition)} stun {P.StunTime}/{P.DazeTime} planted {P.Weapons.Planted} target {_target?.GlobalPosition} ammo {P.Gun.Mag}/{P.Gun.Reserve} shots {P.ShotsFired} kills {_stage.Kills}");
                for (int c = 0; c < P.GetSlideCollisionCount(); c++)
                {
                    using var hit = P.GetSlideCollision(c);
                    GD.Print($"BRIDGE_CONTACT: {hit.GetCollider()} normal {hit.GetNormal()} at {hit.GetPosition()}");
                    if (hit.GetCollider() is Enemy e) GD.Print($"CONTACT_ENEMY: {e.Name} pos {e.GlobalPosition} health {e.Health} climb {e.Climbing} seen {e.IsSeen}");
                }
            }
            await Frames(1);
            if (P.GlobalPosition.DistanceTo(last) > .4f) { last = P.GlobalPosition; moved = _ticks; }
            if (_ticks - moved > 600)
            {
                await Shot("stuck");
                throw new Exception($"{label}: stuck at {P.GlobalPosition}, goal {goal}, next {next}, enemies {_stage.AllEnemies.Count()}, restrained {P.IsRestrained}");
            }
        }
        Drive(Vector3.Zero);
        Check(P.GlobalPosition.DistanceTo(goal) <= near, label + $" reached by actual movement at {P.GlobalPosition} ({P.Gun.Def.Id} {P.Gun.Mag}/{P.Gun.Reserve}, kills {_stage.Kills})");
    }
    private Vector3 Ground(float x, float z) => new(x, _stage.Tiers.GroundY(new Vector3(x, 0, z)), z);
    private async Task Visit(float x, float z, string label)
    {
        await Walk(Ground(x, z), label);
        for (int i = 0; i < 35; i++) { Drive(Vector3.Zero); await Frames(1); }
        await Shot(label);
    }
    private async Task Supply(float x, float z, string label)
    {
        var item = _stage.Interactables.First(i => (i is WeaponPickup || i.Pickup == PickupKind.Ammo) && ((i.GlobalPosition - new Vector3(x, 0, z)) with { Y = 0 }).Length() < .2f);
        var weaponId = (item as WeaponPickup)?.Weapon?.Id;
        bool ammo = item.Pickup == PickupKind.Ammo;
        await Walk(item.GlobalPosition, label, .3f);
        await Use(item, label);
        if (weaponId != null) Check(P.Loadout.Primary?.Def.Id == weaponId, label + " equips the actual primary weapon");
        if (ammo && P.Loadout.Primary is { } primary) Check(primary.Reserve == primary.Def.ReserveMax, label + " refills primary reserve");
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        bool failed = false;
        try { await Run(); GD.Print($"STAGE2_ROUTE_PASS {_checks}; seconds {_ticks / 60f:0.0}; ladders {_ladderUses}; ziplines {_zipUses}; kills {_stage.Kills}; peak enemies {_peakEnemies}; shots {P.ShotsFired}"); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("STAGE2_ROUTE_FAIL " + e.Message); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        _map = new LayoutMap();
        _stage = new Stage { MapOverride = _map, ModeOverride = GameMode.Scenario, CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = true, SquadOverride = 0 };
        AddChild(_stage);
        await Frames(4);
        P.ManualControl = true;
        P.Invulnerable = true; // Functional route/combat check, not a difficulty or survival-rate test.
        Check(_stage.Objective == Objective.ReachShutter && !_map.Shutter!.IsOpen, "fresh stage starts with raised bridge");
        await Shot("01-hatch");
        await Supply(8, 6.5f, "start-m4");
        P.PressSlot(2);
        await Visit(95, -22, "02-forest");
        await Visit(118, -64, "03-tower-foot");
        await Visit(120, -73, "04-tower-top");
        await Supply(119, -75, "tower-cache");
        await Visit(176, -56, "05-zipline-arrival");
        Check(_zipUses == 1, "reward branch returns by zipline");
        await Supply(171, -37, "rim-ammo");
        await Visit(197, -45, "06-raised-bridge");
        Check(!_map.Shutter!.IsOpen, "bridge remains closed before pump");
        await Visit(184, -30, "07-drop-edge");
        await Visit(194, -27, "08-canyon");
        var pump = _map.ShutterPanel!;
        await Walk(pump.GlobalPosition, "pump-valve", pump.Radius - .1f);
        await Shot("09-pump-valve");
        await Use(pump, "pump valve");
        Check(_stage.Objective == Objective.HoldOut && _stage.Director.CrescendoActive, "real E begins the 50-second defense");
        Check(_stage.ObjectiveText.Contains("다리") || _stage.ObjectiveText.Contains("bridge"), "pump defense HUD names the bridge");
        int defenseStart = _ticks;
        while (_stage.Objective == Objective.HoldOut && _ticks - defenseStart < 3600)
        {
            Drive(Vector3.Zero);
            await Frames(1);
            if (_ticks - defenseStart == 1500) await Shot("10-pump-defense");
        }
        Check(_ticks - defenseStart >= 2900 && _map.Shutter!.IsOpen && _stage.Objective == Objective.ReachSafehouse, "full defense timer opens bridge without test skip");
        await Visit(200, -10, "pump-exit");
        await Supply(205, -7, "pump-reward");
        P.PressSlot(2);
        await Visit(196, -45, "11-bridge-return");
        await Visit(217, -45, "12-bridge-crossed");
        await Visit(232, -52, "13-watch-post");
        await Supply(234, -50, "watch-ammo");
        await Visit(240, -52, "14-watch-east-exit");
        foreach (var (x, z) in new[] { (279f, -75f), (329f, -81f), (375f, -86f) })
        {
            await Visit(x, z, "container-top-" + x);
            await Visit(x, z + 9, "container-down-" + x);
        }
        await Visit(318, -40, "15-alarm-truck");
        var truck = _map.AlarmDevice!;
        for (int i = 0; i < 180 && !truck.Triggered; i++)
        {
            P.SetControls(Vector2.Zero, truck.GlobalPosition + Vector3.Up * Player.AimHeight, true);
            if (P.Gun.Mag == 0) P.PressReload();
            await Frames(1);
        }
        Check(truck.Triggered, "actual player shooting triggers alarm truck");
        await Shot("16-truck-alarm");
        await Visit(350, -94, "17-crane");
        await Supply(360, -12, "workshop-reward");
        await Shot("18-workshop");
        P.PressSlot(2);
        await Supply(432, -64, "yard-ammo");
        await Visit(465, -92, "19-highland");
        await Visit(516, -119, "20-bunker-gate");
        await Visit(524, -125, "21-gate-bypass");
        var gondola = _map.GondolaStation!;
        await Visit(526, -128, "22-gondola-entry");
        await Walk(gondola.GlobalPosition, "gondola boarding", .2f);
        Check(gondola.Aboard(P), "walked through cabin side opening onto platform");
        await Use(At(gondola.GlobalPosition), "gondola departure");
        Check(gondola.Riding && _stage.AllStopped, "E starts gondola and pauses combat");
        await Frames(250);
        await Shot("23-gondola-ride");
        for (int i = 0; i < 1000 && gondola.Riding; i++) await Frames(1);
        Check(gondola.Completed && !_stage.AllStopped, "natural gondola arrival restores play");
        await Visit(594, -140, "24-gondola-disembark");
        await Visit(622, -145, "25-safehouse-approach");
        await Visit(633, -145, "26-safehouse-inside");
        await Supply(636, -141, "arrival-ammo");
        // safehouse_b: the button is on the north wall's inner face (L-32); stand on the floor in front of it.
        await Walk(Ground(628.4f, -149.9f), "safehouse-button", .2f);
        for (int i = 0; i < 3600 && _stage.AllEnemies.Any(e => !e.IsDead && _map.NearSafehouse(e.GlobalPosition)); i++) { Drive(Vector3.Zero); await Frames(1); }
        await Shot("27-safehouse-button");
        await Use(_map.SafehouseSwitch!, "safehouse close");
        await Frames(60);
        Check(_stage.State == StageState.Cleared && !_map.SafehouseDoor!.IsOpen, "same continuous playthrough ends by closing the safehouse");
        Check(_ladderUses >= 8 && _stage.Kills > 0 && P.ShotsFired > 0, "both branches and three container ladders plus live combat exercised");
        Check(PropLibrary.Errors.Count == 0, "all prop resources present throughout route");
        await Shot("28-cleared");
    }
}
