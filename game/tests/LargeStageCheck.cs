using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// The large campaign (decisions.md L-30 · L-31 · L-32, doc/large-stages.md): for the urban Stage 1, the forest Stage 2 and
/// the size draft as Stage 3 it checks the launch wiring, the layout data (scale, alternating large/small zones, ladders
/// climbing north or east), that every ladder and button can be seen from the play camera (a ray from it toward the
/// camera meets no wall), supplies on their floors, a monster way onto every raised space, then plays the stage through
/// with no monsters: the start door (Stage 1), the mission panel and its crescendo, the walk to the safehouse, the
/// door, and the hatch to the next stage. Run with --fixed-fps 60. Prints MEASURE lines.
/// </summary>
public partial class LargeStageCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private LayoutMap _map = null!;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private static void Measure(string what) => GD.Print("MEASURE: " + what);
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("LARGE_STAGE_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("LARGE_STAGE_FAIL " + e.Message); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        GameSession.LargeStages = false;
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        // ---------- launch wiring ----------
        GameSession.LargeStages = true;
        GameSession.StartCampaign();
        Check(GameSession.ScenarioStage == 1 && Enumerable.Range(1, 3).All(n => StageMap.Create(GameMode.Scenario, n) is LayoutMap { StageNumber: var s } && s == n)
            && StageMap.Create(GameMode.Survival) is StageOneLayout && StageMap.Create(GameMode.Survivor) is OpenFieldMap,
            "with --large-stages the scenario runs the large Stage 1 → 2 → 3, other modes keep their maps");
        Check(LayoutMap.ForStage(1).Layout.NextStage == 2 && LayoutMap.ForStage(2).Layout.NextStage == 3 && LayoutMap.ForStage(3).Layout.NextStage == 0,
            "each large stage's safehouse hatch leads to the next, Stage 3 is the last for now");
        // Level test start (--zone=2:D): the option alone runs the large campaign from that stage.
        GameSession.LargeStages = false;
        GameSession.StartZone = "D";
        GameSession.StartZoneStage = 2;
        GameSession.StartCampaign();
        Check(GameSession.LargeCampaign && GameSession.ScenarioStage == 2 && StageMap.Create(GameMode.Scenario, 2) is LayoutMap { StageNumber: 2 },
            "--zone=2:D starts the large campaign on Stage 2");
        GameSession.StartZone = "";
        GameSession.StartZoneStage = 1;
        GameSession.StartCampaign();
        Check(StageMap.Create(GameMode.Scenario, 1) is StageOneLayout && StageMap.Create(GameMode.Scenario, 2) is StageTwoMap, "without the option today's Stage 1 and 2 stay");
        Check(PropLibrary.Errors.Count == 0 && new[] { "safehouse_b", "wall_button_panel", "shutter_leaf", "urban_rowhouse", "urban_shop", "urban_warehouse", "car_wreck", "bus_wreck", "dumpster" }
            .All(id => PropLibrary.Find(id) != null), "the large stage kit (Codex) is registered" + string.Join("; ", PropLibrary.Errors));

        for (int n = 1; n <= 3; n++)
        {
            await CheckStage(n);
            _stage.Free();
            _stage = null!;
            await Frames(2);
        }
        await CheckZoneStart();
        await CheckSharedSupplies();
    }

    /// <summary>A level test run started at Stage 1's D (--zone=D): the player, the open safe room and the running director.</summary>
    private async Task CheckZoneStart()
    {
        var map = LayoutMap.ForStage(1);
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false, SquadOverride = 0, StartZoneOverride = "d" };
        AddChild(_stage);
        await Frames(3);
        var entry = map.ZoneEntry("D");
        var at = _stage.Player.GlobalPosition;
        Check(entry is { } e && at.DistanceTo(e) < 1 && map.Layout.ZoneAt(new Vector2(at.X, at.Z))?.Id == "D" && _stage.StartedZone == "D",
            $"--zone=d starts the player at D's entry on the main route ({at}, entry {entry})");
        Check(_stage.Objective != Objective.LeaveSafeRoom && map.StartDoor is { IsOpen: true },
            $"a zone start is past the safe room: its door is open and the objective moved on ({_stage.Objective})");
        _stage.Free();
        _stage = null!;
        await Frames(2);
        var none = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = LayoutMap.ForStage(1), DirectorEnabled = false, SquadOverride = 0, StartZoneOverride = "Z" };
        _stage = none;
        AddChild(_stage);
        await Frames(3);
        Check(none.StartedZone == "" && none.Player.GlobalPosition.DistanceTo(none.Map.PlayerStart) < 1 && none.Objective == Objective.LeaveSafeRoom,
            "an unknown zone falls back to the safe-room start");
        _stage.Free();
        _stage = null!;
        await Frames(2);
    }

    /// <summary>
    /// Shared tables and per-person supplies (C-33, M-35) in the tutorial Stage 1 for a party of three: a medkit and a molotov
    /// each, the same gun refills from its table, another gun swaps in and the table keeps its gun.
    /// </summary>
    private async Task CheckSharedSupplies()
    {
        var map = LayoutMap.ForStage(1);
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false, SquadOverride = 0, PartyOverride = 3 };
        AddChild(_stage);
        await Frames(3);
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        int Near(System.Func<Interactable, bool> what, Vector2 at, float r) =>
            _stage.Interactables.Count(i => i.Enabled && what(i) && new Vector2(i.GlobalPosition.X, i.GlobalPosition.Z).DistanceTo(at) < r);
        Check(_stage.HumanCount == 3 && Near(i => i.Pickup == PickupKind.Medkit, new Vector2(140, -52), 3) == 3
              && Near(i => i is WeaponPickup { Throwable: not null }, new Vector2(4, 1.6f), 3) == 3,
            "three people: base C1 lays out three medkits, base P6 three molotovs");
        var mp5 = _stage.Interactables.OfType<WeaponPickup>().Single(w => w.Infinite && w.Weapon!.Id == "mp5");
        mp5.Use(player);
        await Frames(2);
        var gun = player.Loadout.Primary!;
        int full = gun.Reserve;
        gun.AddReserve(-full / 2);
        mp5.Use(player);
        await Frames(2);
        Check(gun.Def.Id == "mp5" && player.Loadout.Primary == gun && gun.Reserve == gun.Def.ReserveMax && gun.Mag == gun.Def.MagSize && IsInstanceValid(mp5) && mp5.Enabled,
            $"the MP5 table: taking it again fills the same gun to the top ({gun.Reserve}/{gun.Def.ReserveMax}, was {full - full / 2}), nothing dropped, the table keeps its gun");
        int dropsBefore = _stage.Interactables.OfType<WeaponPickup>().Count(w => !w.Infinite && w.Weapon?.Id == ArmoryCatalog.StartingWeapon);
        var pistol = _stage.Interactables.OfType<WeaponPickup>().Single(w => w.Infinite && w.Weapon!.Id == "glock17");
        pistol.Use(player);
        await Frames(2);
        int dropsAfter = _stage.Interactables.OfType<WeaponPickup>().Count(w => !w.Infinite && w.Weapon?.Id == ArmoryCatalog.StartingWeapon);
        Check(player.Loadout.Secondary.Def.Id == "glock17" && dropsAfter == dropsBefore + 1 && IsInstanceValid(pistol) && pistol.Enabled,
            "the pistol table swaps the pistol in, the frying pan drops on the floor, the table keeps its pistol");
        _stage.Free();
        _stage = null!;
        await Frames(2);
    }

    private async Task CheckStage(int n)
    {
        var map = _map = LayoutMap.ForStage(n);
        var layout = map.Layout;
        string name = $"Stage {n}";
        Check(layout.Errors.Count == 0, $"{name}: the layout loads without errors (ladder directions included) " + string.Join("; ", layout.Errors));
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Scenario, MapOverride = map, DirectorEnabled = false, SquadOverride = 0, NextStageOverride = true };
        AddChild(_stage);
        await Frames(3);
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        await ClearEnemies();
        var tiers = _stage.Tiers;

        // ---------- scale and zones (L-30, L-31) ----------
        var exit = _stage.ExitFlow;
        if (map.Shutter is { } gate) gate.SetOpen(true, 0);
        // The exit route counts doors as open (Stage.RefreshRouteFlows), so it runs past the mission door already.
        var path = new List<Vector3> { exit.Center(exit.Index(map.PlayerStart)) };
        var goal = map.RouteGoal with { Y = 0 };
        for (int i = 0; i < 6000 && path[^1].DistanceTo(goal) > 1.5f; i++)
        {
            int next = exit.NextCell(path[^1]);
            if (next < 0) break;
            path.Add(exit.Center(next));
        }
        Check(path[^1].DistanceTo(goal) <= 1.5f, $"{name}: the exit route reaches the safehouse (mission door open, {path.Count} cells)");
        float walked = 0;
        var zones = new List<StageLayout.Zone>();
        for (int i = 1; i < path.Count; i++)
        {
            var d = path[i] - path[i - 1];
            if (d.Length() <= 1.5f) walked += d.Length();
            if (layout.ZoneAt(new Vector2(path[i].X, path[i].Z)) is { Branch: false } z && (zones.Count == 0 || zones[^1] != z)) zones.Add(z);
        }
        Measure($"{name}: exit route walked {walked:0} m (pure walking {walked / player.Definition.MoveSpeed / 60:0.0} min), zones {string.Join("-", zones.Select(z => z.Id))}");
        // Stage 1 is the base campaign's tutorial (L-34): a short prologue of small zones and bases runs before the street.
        bool tutorial = n == 1;
        Check(walked is > 500 && walked < (tutorial ? 950 : 800), $"{name}: the route is at the Stage 3 scale ({walked:0} m, L-30 ~550-650 m on the plan{(tutorial ? ", plus the tutorial prologue" : "")})");
        int large = layout.Zones.Count(z => z.Size == "large"), small = layout.Zones.Count(z => z.Size == "small" && !z.Branch);
        Check(large is >= 3 and <= 4 && small >= 4 && small <= (tutorial ? 12 : 6), $"{name}: {large} large and {small} small zones on the route (L-31: 3-4 and 4-6{(tutorial ? ", tutorial prologue and bases up to 12" : "")})");
        var pairs = zones.Zip(zones.Skip(1)).Where(p => p.First.Size == "large" && p.Second.Size == "large").Select(p => $"{p.First.Id}-{p.Second.Id}").ToList();
        Check(pairs.Count == 0, $"{name}: the walked route never goes from a large zone straight into another (L-31) " + string.Join(", ", pairs));
        if (map.Shutter is { } shut) shut.SetOpen(false, 0);

        // ---------- visibility of ladders and buttons (L-32) ----------
        foreach (var ladder in _stage.Ladders)
        {
            var climb = ladder.Top - ladder.Bottom;
            var flat = new Vector2(climb.X, climb.Z);
            bool northOrEast = Mathf.Abs(flat.X) >= Mathf.Abs(flat.Y) ? flat.X > 0 : flat.Y < 0;
            Check(northOrEast, $"{name}: ladder at {ladder.Bottom} climbs north or east ({flat})");
            var face = ladder.Bottom.Lerp(ladder.Top, .5f) with { Y = (ladder.Bottom.Y + ladder.Top.Y) / 2 + .6f };
            Check(Seen(face - new Vector3(flat.X, 0, flat.Y).Normalized() * .35f), $"{name}: the camera sees the ladder at {ladder.Bottom}");
        }
        var buttons = new (string What, Interactable? Item, float Front)[] {
            ("start door switch", map.StartDoorSwitch, .05f), ("mission device", map.ShutterPanel, .05f), ("safehouse switch", map.SafehouseSwitch, .1f) };
        foreach (var (what, item, front) in buttons)
        {
            if (item == null) continue;
            Check(Seen(item.GlobalPosition + Vector3.Back * front), $"{name}: the camera sees the {what} at {item.GlobalPosition}");
        }
        var room = layout.FindMarker("safehouse")!.Pos;
        // Control: the old safehouse button spot on the west wall's inner face (screen lower-left) is hidden by that wall.
        var old = new Vector3(room.X, map.SafehouseSwitch!.GlobalPosition.Y, room.Y) + new Vector3(-6.5f + .1f, 0, 1.8f);
        Check(!Seen(old), $"{name}: the old west-wall button spot would be hidden from the camera (the ray check tells them apart)");
        var local = map.SafehouseSwitch!.GlobalPosition - new Vector3(room.X, map.SafehouseSwitch.GlobalPosition.Y - LayoutMap.SafehouseSwitchLocal.Y, room.Y);
        Check(local.Z < -LayoutMap.SafehouseSize.Y / 2 + 1, $"{name}: the safehouse switch hangs on the north wall, the screen's upper-left wall ({local})");

        // Level test starts (--zone): every zone a route crosses has an open entry inside it.
        var noEntry = new List<string>();
        foreach (var zone in layout.Zones)
        {
            var entry = map.ZoneEntry(zone.Id);
            if (entry is not { } at || layout.ZoneAt(new Vector2(at.X, at.Z))?.Id != zone.Id || _stage.Flow.IsBlocked(at)) noEntry.Add(zone.Id);
        }
        Check(noEntry.Count == 0, $"{name}: every zone has a level test start (a route entry, or the open middle of a branch no route crosses; none: {string.Join(", ", noEntry)})");

        // ---------- supplies and raised spaces ----------
        Check(_stage.Director.EncounterErrors.Count == 0, $"{name}: every wave entry and named monster stands on open floor " + string.Join("; ", _stage.Director.EncounterErrors));
        if (n == 1)
        {
            // The tutorial (L-34) teaches alarms in F only: the yard truck stays, the alley car went.
            var alarms = map.AlarmDevices;
            Check(alarms.Count == 1 && _stage.AlarmCars.Contains(alarms[0]) && map.AlarmDevice == alarms[0] && alarms[0].GlobalPosition.X > 450,
                $"{name}: the yard truck is the stage's one alarm ({string.Join(", ", alarms.Select(a => a.GlobalPosition))})");
            // Tutorial supplies (L-34, C-33, M-35): no start kit, one pistol in the first yard, shared tables in the bases.
            var shared = _stage.Interactables.OfType<WeaponPickup>().Where(w => w.Infinite).Select(w => w.Weapon!.Id).OrderBy(id => id).ToList();
            Check(shared.SequenceEqual(new[] { "glock17", "m4", "mp5" }) && map.BaseEntries.Count == 4 && map.BaseExits.Count == 3
                  && map.BaseEntries.All(e => e.HoldSeconds == LayoutMap.BaseDoorHold && e.CancelOnHit),
                $"{name}: shared tables {string.Join(", ", shared)}; {map.BaseEntries.Count} hold doors (three bases and the safehouse)");
            // The first base cannot be walked around (log 014): with its doors shut the back alley is cut off from the first yard.
            var alley = new Vector3(-75, 0, 0);
            var around = _stage.Flow.CopyLayout();
            around.Update(alley);
            Check(!around.Reachable(new Vector3(-112, 0, 0)), $"{name}: the only way from the first yard to the back alley runs through base P3");
            // The street is lanes now (L-36, level log 016): base C1 is the only way from the street on to the alley.
            var alleyC = new Vector3(170, 0, -64);
            var street = _stage.Flow.CopyLayout();
            street.Update(alleyC);
            Check(!_stage.Flow.IsBlocked(alleyC) && !street.Reachable(new Vector3(100, 0, -20)), $"{name}: the only way from the street to the alley runs through base C1");
            // The urban ground (A-30): roads on the lanes, concrete in the yards, a dashed centre line, urban bits and no grass.
            var bits = map.UrbanBits;
            Check(map.UrbanRoadAt(new Vector2(80, -16)) && map.UrbanRoadAt(new Vector2(260, -84)) && !map.UrbanRoadAt(new Vector2(-114, 0)) && !map.UrbanRoadAt(new Vector2(140, -54))
                  && map.RoadDashes > 40 && bits.Values.Sum() > 300 && new[] { "bit_manhole", "bit_drain", "bit_litter", "bit_crack_weed" }.All(id => bits.GetValueOrDefault(id) > 0)
                  && !map.GroundBits.ContainsKey("bit_grass_tuft") && !map.GroundBits.ContainsKey("bit_grass_clump"),
                $"{name}: urban ground — roads on the lanes, concrete in P2 and base C1, {map.RoadDashes} centre-line dashes, bits {string.Join(", ", bits.Select(b => $"{b.Key} {b.Value}"))}, no grass");
            // G's barrels (L-33 #4) blow up: data props "explosive_barrel" are real barrels, the kit "barrel" is only dressing (I-131).
            // (The ramp nook's breach barrel is a destruction placement's, not one of these.)
            var gBarrels = _stage.Props.OfType<ExplosiveBarrel>().Count(b => b.GlobalPosition.X is > 522 and < 592 && !b.HasMeta("destruction_placement"));
            Check(gBarrels == 3, $"{name}: G's three barrels are explosive ({gBarrels})");
            // The depot's pallet lane (level log 016): a shutter shuts its east alley, a button inside the lane raises it.
            var shutter = map.Doors.FirstOrDefault(d => new Vector2(d.GlobalPosition.X - 324.5f, d.GlobalPosition.Z + 70).Length() < 1);
            var raise = _stage.Interactables.FirstOrDefault(i => i.Enabled && new Vector2(i.GlobalPosition.X - 316, i.GlobalPosition.Z + 63.6f).Length() < 1.5f);
            bool shutBefore = shutter is { IsOpen: false } && _stage.Flow.IsBlocked(shutter.GlobalPosition);
            raise?.Use(player);
            Check(shutBefore && shutter!.IsOpen && raise is { Enabled: false }, $"{name}: the pallet lane's shutter is shut and its button inside the lane raises it ({shutter?.GlobalPosition}, button {raise?.GlobalPosition})");
            var yard = _stage.Interactables.OfType<WeaponPickup>().Where(w => !w.Infinite && w.GlobalPosition.X < -100).ToList();
            Check(yard.Count == 1 && yard[0].Weapon!.Id == "glock17" && player.Loadout.Primary == null && player.Loadout.Secondary.Def.Id == ArmoryCatalog.StartingWeapon,
                $"{name}: a run starts with the frying pan only, one pistol lies in the first yard");
            // B wayfinding landmark (L-33): the warehouse sign north of the alley exit; its legs block, the road in front stays open.
            var sign = map.KitProps.FirstOrDefault(p => p.GetNodeOrNull("warehouse_sign") != null);
            var signAt = sign?.GlobalPosition ?? Vector3.Zero;
            Check(sign != null && _stage.Flow.IsBlocked(signAt + new Vector3(1.85f, 0, 0)) && _stage.Flow.IsBlocked(signAt - new Vector3(1.85f, 0, 0))
                  && !_stage.Flow.IsBlocked(signAt + new Vector3(0, 0, 4)),
                $"{name}: the warehouse sign stands at the alley exit, legs solid, the road in front open ({signAt})");

            // Pressure (pacing-pressure-plan.md 4.2): timed mobs in every large zone, bigger, with a rest after a peak.
            var raids = map.Encounters!.Raids!;
            Check(new[] { "street", "depot", "yard" }.All(raids.Areas.Contains) && raids.MobCountMin >= 12 && raids.MobMax <= 75
                  && raids.RestMin >= 25 && raids.FirstInAreaMax > 0,
                $"{name}: timed mobs in B, D and F, {raids.MobCountMin}-{raids.MobCountMax} every {raids.MobMin}-{raids.MobMax} s, rest {raids.RestMin}-{raids.RestMax} s");
            // A wave comes from two sides of the leader and a few at a time, not in one file.
            var director = _stage.Director;
            player.TeleportTo(new Vector3(90, tiers.GroundY(new Vector3(90, 0, -20)) + .05f, -20));
            await Frames(3);
            int before = _stage.AllEnemies.Count(e => !e.IsDead);
            int sent = director.SpawnMob(16);
            int now = _stage.AllEnemies.Count(e => !e.IsDead) - before;
            var lead = player.GlobalPosition;
            var one = director.LastEntry;
            var two = director.LastSecondEntry;
            bool apart = one != null && two != null && (one.Ground - lead).Normalized().Dot((two.Ground - lead).Normalized()) < 0;
            Check(sent == 16 && apart && now <= HordeDirector.BatchMax * 2 && director.PendingCommons == sent - now,
                $"{name}: a 16-monster wave splits between two entries on opposite sides ({one?.Ground} / {two?.Ground}), {now} out at once, {director.PendingCommons} waiting");
            await ToSignal(GetTree().CreateTimer(HordeDirector.BatchGapMax + .5f), SceneTreeTimer.SignalName.Timeout);
            int later = _stage.AllEnemies.Count(e => !e.IsDead) - before;
            Check(later > now, $"{name}: the waiting ones follow a few seconds later ({now} -> {later})");
            await ClearEnemies();
            player.TeleportTo(map.PlayerStart);
            await Frames(3);
        }
        var pickups = _stage.Interactables.Where(i => i is WeaponPickup || i.Pickup is PickupKind.Medkit or PickupKind.Ammo).ToList();
        // A shared gun lies on its base's table (C-33): on the table top, reached from the floor in front of it.
        static bool OnTable(Interactable i) => i is WeaponPickup { Infinite: true };
        Vector3 Stand(Interactable i) => OnTable(i) ? i.GlobalPosition + new Vector3(0, -StageMap.TableTop, 1.6f) : i.GlobalPosition;
        var off = pickups.Where(i => Mathf.Abs(i.GlobalPosition.Y - tiers.GroundY(i.GlobalPosition) - (OnTable(i) ? StageMap.TableTop : 0)) > .3f || _stage.Flow.IsBlocked(Stand(i)))
            .Select(i => $"{i.Name} at {i.GlobalPosition}").ToList();
        Check(pickups.Count > 10 && off.Count == 0, $"{name}: {pickups.Count} pickups lie on their floors, off walls " + string.Join("; ", off));
        if (map.Shutter is { } open) open.SetOpen(true, 0);
        map.StartDoor?.SetOpen(true, 0);
        await Frames(2);
        var reach = _stage.Flow.CopyLayout();
        reach.Riders = true;
        // Breakable gates (the barrel lesson) count as open here: "walkable from the start" once the way is cleared.
        map.ApplyFlow(reach, doors: false);
        var cut = new List<string>();
        foreach (var pickup in pickups)
        {
            reach.Update(Stand(pickup));
            if (!reach.Reachable(map.PlayerStart)) cut.Add($"{pickup.Name} {pickup.GlobalPosition}");
        }
        Check(cut.Count == 0, $"{name}: every pickup can be walked to from the start " + string.Join(", ", cut));
        foreach (var region in layout.Regions.Where(r => r.Tier > 0))
        {
            var c = region.Poly.Aggregate(Vector2.Zero, (s, p) => s + p) / region.Poly.Length;
            if (layout.TierAt(c) != region.Tier) continue;
            var at = new Vector3(c.X, tiers.GroundY(new Vector3(c.X, 0, c.Y)), c.Y);
            bool any = false;
            foreach (var climb in new[] { MonsterClimb.Ladder, MonsterClimb.Wall, MonsterClimb.Jump })
            {
                var field = _stage.Flow.CopyLayout();
                map.ApplyFlow(field, doors: false);
                field.Mover = climb;
                field.Update(at);
                any |= field.Reachable(map.PlayerStart);
            }
            int own = map.Encounters!.Entries.Count(e => Geometry2D.IsPointInPolygon(new Vector2(e.At.X, e.At.Z), region.Poly));
            Check(any || own > 0, $"{name}: monsters can get onto {region.Id} (tier {region.Tier})");
        }
        // The mission door is the only way on: a run cannot walk past it and skip the crescendo.
        if (map.Shutter is { } mission)
        {
            mission.SetOpen(false, 0);
            await Frames(2);
            var skip = _stage.Flow.CopyLayout();
            skip.Riders = true;
            skip.Update(map.RouteGoal);
            Check(!skip.Reachable(map.PlayerStart), $"{name}: with the mission door shut the safehouse cannot be reached from the start");
        }
        if (map.Shutter is { } close) close.SetOpen(false, 0);
        map.StartDoor?.SetOpen(false, 0);
        await Frames(2);

        // ---------- play it through ----------
        player.TeleportTo(map.PlayerStart);
        await Frames(2);
        if (map.StartDoorSwitch is { } doorSwitch)
        {
            Check(_stage.Objective == Objective.LeaveSafeRoom && !map.StartDoor!.IsOpen, $"{name}: the run starts shut in the safe room");
            var stand = doorSwitch.GlobalPosition + new Vector3(0, -1.2f, .4f);
            var (s0, stuck0, _) = await Walk(Toward(stand), stand, .35f, 1);
            Check(!stuck0 && _stage.FocusedInteractable == doorSwitch, $"{name}: the door switch on the north wall is in reach ({s0:0} s)");
            doorSwitch.Use(player);
            await Frames(3);
            Check(map.StartDoor!.IsOpen && _stage.Objective == Objective.ReachShutter, $"{name}: the switch opens the safe room door");
        }
        var panel = map.ShutterPanel!;
        if (n == 1)
        {
            // The bases on the way (L-35): hold E at the entry, a hit starts it over; a plain E opens the exit.
            for (int b = 0; b < map.BaseExits.Count; b++)
            {
                var entry = map.BaseEntries[b];
                // The walk is about the way and the doors: a pack woken on the way (P5's sleepers) would only stand in it.
                await ClearEnemies();
                var (sb, stuckB, _) = await Walk(Toward(entry.GlobalPosition), entry.GlobalPosition, .6f, 3);
                Check(!stuckB, $"{name}: a survivor walks up to base {b + 1}'s door ({sb:0} s, at {player.GlobalPosition})");
                if (b == 0)
                {
                    player.ManualControl = false;
                    player.Invulnerable = false;
                    Input.ActionPress(GameInput.Interact);
                    await Frames(60);
                    float half = _stage.HoldProgress;
                    player.TakeDamage(1, Vector3.Right);
                    await Frames(2);
                    bool broken = _stage.HoldProgress == 0 && _stage.HoldCancelled;
                    await Frames(30);
                    bool stays = _stage.HoldProgress == 0;
                    Input.ActionRelease(GameInput.Interact);
                    await Frames(2);
                    Input.ActionPress(GameInput.Interact);
                    await Frames((int)(60 * LayoutMap.BaseDoorHold) + 20);
                    Input.ActionRelease(GameInput.Interact);
                    player.ManualControl = true;
                    player.Invulnerable = true;
                    Check(half is > .25f and < .5f && broken && stays && !entry.Enabled,
                        $"{name}: the first base door fills over 3 s ({half:0.00} after 1 s), a hit breaks it until E is let go, a full hold opens it");
                }
                else entry.Use(player);
                await Frames(3);
                var leave = map.BaseExits[b];
                var (sx, stuckX, _) = await Walk(Toward(leave.GlobalPosition), leave.GlobalPosition, .5f, 2);
                Check(!stuckX, $"{name}: inside base {b + 1}, a survivor reaches the exit door ({sx:0} s)");
                leave.Use(player);
                await Frames(3);
            }
            // The barrel lesson (pacing-pressure-plan.md 4.4): the alley mouth is shut until its barrel goes off.
            var alleyGate = DestructionPlacements.Load("stage1_large").Single(p => p.Id == "alley_gate");
            var gateWall = _stage.Breakables.Single(b => b.HasMeta("destruction_placement") && b.GetMeta("destruction_placement").AsString() == alleyGate.Id);
            var gateBarrel = _stage.Props.OfType<ExplosiveBarrel>().Single(b => b.HasMeta("destruction_placement") && b.GetMeta("destruction_placement").AsString() == alleyGate.Id);
            Check(alleyGate.Gate && !Toward(panel.GlobalPosition).Reachable(player.GlobalPosition) && _stage.ExitFlow.Reachable(map.PlayerStart),
                $"{name}: the alley gate shuts the way to the mission until it breaks (the route still counts it open)");
            var normal = new Basis(Vector3.Up, Mathf.DegToRad(alleyGate.Yaw)) * Vector3.Back;
            var standAt = alleyGate.Barrel - normal * 6;
            var (sg, stuckG, _) = await Walk(Toward(standAt), standAt, .6f, 2);
            Check(!stuckG, $"{name}: a survivor walks up to the barricade ({sg:0} s, at {player.GlobalPosition})");
            gateBarrel.TakeDamage(gateBarrel.Health, ObjectDamage.Gun, player);
            await Frames(80);
            Check(!IsInstanceValid(gateWall) && Toward(panel.GlobalPosition).Reachable(player.GlobalPosition),
                $"{name}: shooting the barrel blows the gate open, the way to the mission is clear");
            await ClearEnemies();
        }
        var (s1, stuck1, uses1) = await Walk(Toward(panel.GlobalPosition), panel.GlobalPosition, panel.Radius - .1f, 4);
        await Frames(2);
        Check(!stuck1 && _stage.FocusedInteractable == panel, $"{name}: a survivor walks to the mission device ({s1 / 60:0.0} min, {uses1}, at {player.GlobalPosition})");
        panel.Use(player);
        await Frames(2);
        Check(_stage.Objective == Objective.HoldOut, $"{name}: the device starts the crescendo");
        await ClearEnemies();
        _stage.Director.EndCrescendoForTest();
        await Frames(3);
        Check(map.Shutter!.IsOpen && _stage.Objective == Objective.ReachSafehouse, $"{name}: the way on opens when the crescendo is held out");
        _stage.Director.Stop();
        await ClearEnemies();
        // The safehouse door opens with a held E from outside (L-35); the checks skip the waiting.
        var safehouseEntry = map.BaseEntries[^1];
        var (sh, stuckH, _) = await Walk(Toward(safehouseEntry.GlobalPosition), safehouseEntry.GlobalPosition, .6f, 8);
        Check(!stuckH && map.SafehouseDoor is { IsOpen: false }, $"{name}: a survivor reaches the shut safehouse door ({sh / 60:0.0} min)");
        safehouseEntry.Use(player);
        await Frames(3);
        var (s2, stuck2, uses2) = await Walk(exit, map.RouteGoal, 2f, 8);
        Measure($"{name}: mission device → safehouse {s2 / 60:0.0} min simulated, {uses2}");
        Check(!stuck2 && player.GlobalPosition.DistanceTo(map.RouteGoal) <= 2f, $"{name}: a survivor walks on to the safehouse ({player.GlobalPosition})");
        var shutSwitch = map.SafehouseSwitch!;
        // Stand on the floor inside, in front of the button (the button itself is in the wall's flow cell).
        var inFront = shutSwitch.GlobalPosition + new Vector3(0, -LayoutMap.SafehouseSwitchLocal.Y, .6f);
        var (_, stuck3, _) = await Walk(Toward(inFront), inFront, .35f, 1);
        await Frames(3);
        Check(!stuck3 && _stage.Objective == Objective.CloseSafehouse && shutSwitch.Enabled && _stage.FocusedInteractable == shutSwitch,
            $"{name}: the close switch on the north wall is in reach inside the safehouse (stuck {stuck3}, objective {_stage.Objective}, enabled {shutSwitch.Enabled}, "
            + $"focused {_stage.FocusedInteractable?.Name}, player {player.GlobalPosition}, switch {shutSwitch.GlobalPosition})");
        shutSwitch.Use(player);
        await Frames(3);
        if (layout.NextStage > 0)
            Check(_stage.Objective == Objective.TakeExit && map.StageExit is { Enabled: true } && !map.SafehouseDoor!.IsOpen,
                $"{name}: shutting the door opens the floor hatch to Stage {layout.NextStage}");
        else
            Check(_stage.State == StageState.Cleared && !map.SafehouseDoor!.IsOpen, $"{name}: shutting the door clears the last stage");
    }

    // ---------- helpers ----------

    /// <summary>
    /// A ray from <paramref name="point"/> back along the play camera's view meets no wall, floor or prop body within 3 m
    /// above it — the height of room walls and cliffs that hide a device. Higher up the only bodies are the invisible 8 m
    /// walls around the map, which hide nothing.
    /// </summary>
    private bool Seen(Vector3 point)
    {
        var back = GetViewport().GetCamera3D().GlobalBasis.Z.Normalized();
        var to = point + back * (3f / Mathf.Max(back.Y, .1f));
        var hit = _stage.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(point, to, Layers.World));
        if (hit.Count > 0) GD.Print($"  ray from {point} hits {((Node)hit["collider"]).Name} at {hit["position"]}");
        return hit.Count == 0;
    }

    private FlowField Toward(Vector3 at)
    {
        var flow = _stage.Flow.CopyLayout();
        flow.Riders = true;
        flow.Update(at);
        return flow;
    }

    /// <summary>Walks the survivor along <paramref name="flow"/>, taking ladders and ziplines (SizeDraftCheck's walker, no gondola here).</summary>
    private async Task<(float Seconds, bool Stuck, string Uses)> Walk(FlowField flow, Vector3 goal, float near, float minutes)
    {
        var player = _stage.Player;
        var tiers = _stage.Tiers;
        int frames = 0, ladderUses = 0, ziplineUses = 0, gondolaRides = 0, lastMoved = 0;
        bool stuck = false;
        var last = player.GlobalPosition;
        var gondola = _map.GondolaStation;
        while (frames < 60 * 60 * minutes && player.GlobalPosition.DistanceTo(goal) > near)
        {
            await Frames(1);
            frames++;
            if (player.OnLadder || player.OnZipline || (gondola?.Riding ?? false)) { player.SetControls(Vector2.Zero, player.AimPoint, false); lastMoved = frames; continue; }
            var here = player.GlobalPosition;
            int cell = tiers.Index(here);
            int next = flow.NextCell(here);
            if (gondola != null && gondola.Aboard(player) && next >= 0 && flow.Center(next).DistanceTo(here with { Y = 0 }) > 3f)
            {
                if (gondola.TryDepart()) gondolaRides++;
                continue;
            }
            var zip = _stage.Ziplines.FirstOrDefault(z => z.StartCell == cell && next == z.EndCell);
            if (zip != null && player.RideZipline(zip)) { ziplineUses++; continue; }
            var ladder = _stage.Ladders.FirstOrDefault(l => (l.BottomCell == cell && next == l.TopCell) || (l.TopCell == cell && next == l.BottomCell));
            if (ladder != null && player.ClimbLadder(ladder, up: ladder.BottomCell == cell)) { ladderUses++; continue; }
            var dir = next < 0 ? (goal - here) with { Y = 0 } : flow.Direction(here);
            if (dir.LengthSquared() > 1) dir = dir.Normalized();
            player.SetControls(new Vector2(dir.X, dir.Z), here + dir * 5 + Vector3.Up, false);
            if (here.DistanceTo(last) > .5f) { last = here; lastMoved = frames; }
            if (frames - lastMoved > 180)
            {
                // What holds the walker: monsters next to it, and what a knee-high ray along its way hits.
                var close = _stage.AllEnemies.Where(e => IsInstanceValid(e) && e.GlobalPosition.DistanceTo(here) < 3).Select(e => $"{e.Name}@{e.GlobalPosition}");
                var block = _stage.GetWorld3D().DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(here + Vector3.Up * .5f, here + Vector3.Up * .5f + dir * 1.5f));
                GD.Print($"  stuck at {here} toward {dir} (next cell {next}): monsters [{string.Join(", ", close)}], ray hits {(block.Count > 0 ? ((Node)block["collider"]).GetPath() + " at " + block["position"] : "nothing")}");
                stuck = true;
                break;
            }
        }
        player.SetControls(Vector2.Zero, player.AimPoint, false);
        return (frames / 60f, stuck, $"ladders {ladderUses}, ziplines {ziplineUses}, gondola {gondolaRides}");
    }

    private async Task ClearEnemies()
    {
        foreach (var e in _stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
        await Frames(2);
    }
}
