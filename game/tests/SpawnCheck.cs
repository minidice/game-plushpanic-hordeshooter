using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Monster arrival and reactions (doc/spawn-design.md): area-by-area pre-placement, dormant / watcher /
/// hunter wake rules, the one-hop alert chain, weapon noise radii, off-screen-first wave entries, the
/// walked-trail entry, ledge drops, protected landings and the car alarm.
/// </summary>
public partial class SpawnCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("SPAWN_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("SPAWN_CHECK_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task<Stage> NewStage(bool director = true)
    {
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(2); }
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = director };
        AddChild(_stage);
        await Frames(3);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        return _stage;
    }

    private void Clear()
    {
        foreach (var e in _stage.AllEnemies.ToList()) if (IsInstanceValid(e)) e.QueueFree();
    }

    private Enemy Place(Vector3 at, Reaction reaction)
    {
        var e = _stage.SpawnCommon(at, false);
        e.Reaction = reaction;
        return e;
    }

    private async Task Run()
    {
        // ---------- data ----------
        var db = SpawnDatabase.Current;
        Check(db.Errors.Count == 0, "spawn rules and stage 1 encounters load without errors" + (db.Errors.Count > 0 ? ": " + string.Join("; ", db.Errors) : ""));
        var set = db.Stage1;
        Check(set.Areas.Select(a => a.Id).SequenceEqual(new[] { "saferoom", "street", "warehouse", "yard", "safehouse" }), "stage 1 has five areas along the route");
        Check(set.Entries.Count(e => e.Kind == EntryKind.Ledge) >= 6 && set.Entries.Count(e => e.Kind == EntryKind.Hidden) >= 20, "stage 1 has hidden spots and ledges to come in from");
        Check(SpawnDatabase.FromText("{\"trail_min_m\": \"far\"}", "{\"areas\": [{\"id\": \"a\", \"x0\": 0, \"x1\": 5}], \"groups\": [{\"area\": \"b\", \"count\": 2, \"rect\": [0, 1, 0, 1]}], \"entries\": [{\"kind\": \"roof\", \"at\": [1, 1]}]}")
            .Errors.Count == 3, "bad numbers, unknown areas and unknown entry kinds are reported");
        var bad = SpawnDatabase.FromText("{\"alarm_wave_every_s\": 0, \"alert_radius\": 6}",
            "{\"areas\": [{\"id\": \"a\", \"x0\": 0, \"x1\": 5}], \"groups\": [{\"area\": \"a\", \"special\": 0, \"at\": [1, 0, 1]}, {\"area\": \"a\", \"count\": 1, \"rect\": [0, 1, 0]}]}").Errors;
        Check(bad.Count == 4, "a zero wave interval, an unknown key and x,y,z or short coordinates are reported: " + string.Join(" / ", bad));
        Check(WeaponDatabase.Current.Weapons.Values.All(w => w.Noise > 0) && ArmoryCatalog.Weapon("m1911").Noise < ArmoryCatalog.Weapon("m4").Noise,
            "every weapon has a noise radius, a pistol is quieter than a rifle");

        // ---------- pre-placement ----------
        var stage = await NewStage();
        var dir = stage.Director;
        Check(dir.EncounterErrors.Count == 0 && dir.Entries.Count == set.Entries.Count,
            "every entry and special fits the built level (ledges land on open floor)" + string.Join("; ", dir.EncounterErrors));
        Check(dir.FilledThrough == 1, "at the start only the safe room and the street are filled");
        var waiting = stage.AllEnemies.ToList();
        Check(waiting.Count >= 7 && waiting.All(e => e.GlobalPosition.X is > 10.4f and < 52.4f && !e.Awake),
            $"the street already holds an idle pack ({waiting.Count}) and nothing waits further on");
        Check(waiting.Any(e => e.Reaction == Reaction.Watcher) && waiting.Any(e => e.Reaction == Reaction.Dormant), "the street mixes dormant ones and watchers");
        if (Windowed) Check(waiting.Count(e => stage.Effects.IsOnScreen(e.GlobalPosition + Vector3.Up, .5f)) <= 1, "the pack was placed off screen");
        stage.LeaveSafeRoom();
        stage.Player.TeleportTo(new Vector3(14, 0, 0));
        await Frames(3);
        Check(dir.FilledThrough == 2 && stage.AllEnemies.Any(e => e.GlobalPosition.X > 52.4f), "stepping into the street fills the warehouse");
        Check(stage.AllEnemies.Any(e => e.IsSpecial && e.GlobalPosition.DistanceTo(new Vector3(66, 0, -8.6f)) < 1.5f && !e.Awake), "the warehouse special waits in its spot");
        stage.Player.TeleportTo(new Vector3(100, 0, 0));
        await Frames(3);
        Check(dir.FilledThrough == 4, "walking further fills the rest, never twice");
        Check(stage.AllEnemies.Any(e => e.IsSpecial && e.Reaction == Reaction.Watcher && e.GlobalPosition.DistanceTo(new Vector3(102, 0, -1.5f)) < 1.5f),
            "the yard special waits as a watcher");

        // ---------- reactions ----------
        Clear();
        await Frames(2);
        var player = stage.Player;
        player.TeleportTo(new Vector3(20, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        var dormant = Place(new Vector3(30, 0, -1.5f), Reaction.Dormant);
        await Frames(60);
        Check(dormant.IsSeen && !dormant.Awake, "a dormant one 10 m away in plain view keeps idling");
        dormant.QueueFree();
        var watcher = Place(new Vector3(30, 0, 1.5f), Reaction.Watcher);
        var hunter = stage.SpawnCommon(new Vector3(48, 0, 0), true);
        await Frames(60);
        Check(watcher.Awake, "a watcher 10 m away with a clear view wakes");
        Check(hunter.Reaction == Reaction.Hunter && hunter.Awake, "director spawns are hunters, awake from the start");
        Clear();
        await Frames(2);
        player.TeleportTo(new Vector3(14, 0, -2));
        player.SetControls(Vector2.Zero, new Vector3(30, 1, -2), false);
        var walled = Place(new Vector3(5, 0, -3.4f), Reaction.Watcher);
        var farWatcher = Place(new Vector3(29.5f, 0, -1), Reaction.Watcher);
        await Frames(60);
        Check(!walled.Awake && !farWatcher.Awake, "watchers behind a wall or beyond sight range stay idle");
        Clear();
        await Frames(2);

        // Alert chain: a hit wakes neighbours once, not the whole street.
        player.TeleportTo(new Vector3(12, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(12, 1, 5), false);
        var a = Place(new Vector3(38, 0, 1), Reaction.Dormant);
        var b = Place(new Vector3(42, 0, 1), Reaction.Dormant);
        var c = Place(new Vector3(46, 0, 1), Reaction.Dormant);
        var far = Place(new Vector3(31, 0, 1), Reaction.Dormant);
        await Frames(2);
        a.TakeHit(new HitInfo(1, Vector3.Right, a.GlobalPosition + Vector3.Up, false, 0));
        await Frames(2);
        Check(a.Awake && b.Awake && !c.Awake && !far.Awake, "a hit wakes the one hit and its close neighbours, one hop only");
        Clear();
        await Frames(2);

        // Weapon noise: the radius comes from weapon_types.csv. "near" stands past every common's wake range.
        foreach (var weaponId in new[] { "m1911", "m4" })
        {
            player.TeleportTo(new Vector3(30, 0, 0));
            var def = ArmoryCatalog.Weapon(weaponId);
            player.Weapons.Pickup(def, null);
            player.Weapons.SelectSlot((int)def.Slot);
            await Frames(2);
            float noise = player.Gun.Def.Noise;
            var near = Place(new Vector3(30 - noise + 3f, 0, 0), Reaction.Dormant);
            Check(noise - 3f > near.Monster.WakeRange + 1, $"{weaponId}: the near one is out of wake range");
            var beyond = Place(new Vector3(30 + noise + 4, 0, 0), Reaction.Dormant);
            await Frames(2);
            int shots = player.ShotsFired;
            player.SetControls(Vector2.Zero, new Vector3(30, 1, -5), true);
            for (int i = 0; i < 40 && player.ShotsFired == shots; i++) await Frames(1);
            player.SetControls(Vector2.Zero, new Vector3(30, 1, -5), false);
            await Frames(30);
            Check(player.Gun.Def.Id == weaponId && player.ShotsFired > shots && near.Awake && !beyond.Awake,
                $"{weaponId}: wakes idle ones inside its {noise:0} m only");
            Clear();
            await Frames(2);
        }

        // ---------- wave entries ----------
        player.TeleportTo(new Vector3(30, 0, 0));
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        await Frames(3);
        int ledge = 0, hiddenOk = 0, total = 0;
        for (int i = 0; i < 40; i++)
        {
            var e = dir.PickEntry(14, 36);
            if (e == null) continue;
            total++;
            if (e.Kind == EntryKind.Ledge) ledge++;
            else if (!dir.OnScreen(e.At) && !stage.Vision.CanSee(e.At)) hiddenOk++;
        }
        Check(total == 40 && ledge + hiddenOk == total, $"waves come from ledges or spots off screen and out of sight ({ledge} ledge, {hiddenOk} hidden)");
        if (Windowed)
        {
            // Independent of Effects.IsOnScreen: project with the camera and test against the window.
            var cam = stage.GetViewport().GetCamera3D();
            var rect = stage.GetViewport().GetVisibleRect();
            bool offScreen = true;
            for (int i = 0; i < 40; i++)
                if (dir.PickEntry(14, 36, ledges: false) is { } e && !cam.IsPositionBehind(e.At) && rect.HasPoint(cam.UnprojectPosition(e.At + Vector3.Up)))
                    offScreen = false;
            Check(offScreen, "with ledges left out, every walk-in entry is really outside the window");
        }

        // Walked trail: once the leader has walked far enough, waves can follow from behind.
        player.TeleportTo(new Vector3(12, 0, 0));
        await Frames(2);
        for (float x = 12; x <= 50; x += 1f) { player.TeleportTo(new Vector3(x, 0, 0)); await Frames(1); }
        player.SetControls(Vector2.Zero, new Vector3(60, 1, 0), false);
        await Frames(2);
        var trail = stage.TrailSpotBehind(dir.Rules.TrailMin, dir.Rules.TrailMax);
        Check(trail is { } t && t.X <= 50 - dir.Rules.TrailMin + .5f && !dir.OnScreen(t), "the walked trail offers a spot well behind and off screen");
        bool trailUsed = false;
        for (int i = 0; i < 80 && !trailUsed; i++) trailUsed = dir.PickEntry(14, 36)?.Kind == EntryKind.Trail;
        Check(trailUsed, "a wave can come from the walked trail");

        // Ledge drop: commons jump off the wall top and land in the street, already hunting.
        var wall = set.Entries.First(e => e.Kind == EntryKind.Ledge && e.At.X < 52);
        int before = stage.AllEnemies.Count();
        dir.SpawnWave(wall, 4);
        await Frames(2);
        var dropped = stage.AllEnemies.Skip(before).ToList();
        Check(dropped.Count == 4 && dropped.All(e => e.GlobalPosition.Y > wall.At.Y - .5f && e.Awake), "a ledge wave starts on the wall top");
        await Frames(90);
        Check(dropped.All(e => e.GlobalPosition.Y < .4f && e.GlobalPosition.Z > -5.9f), "they jump down into the street");
        Clear();
        await Frames(2);

        // Protected landing: nothing appears within the radius.
        set.Protected.Add(new Vector3(30, 0, 0));
        try
        {
            player.TeleportTo(new Vector3(12, 0, 0));
            await Frames(2);
            Check(dir.IsProtected(new Vector3(33, 0, 2)) && !dir.IsProtected(new Vector3(40, 0, 0)), "a protected landing covers its radius");
            bool clean = true;
            for (int i = 0; i < 40; i++)
                if (dir.PickEntry(4, 60) is { } e && e.Ground.DistanceTo(new Vector3(30, 0, 0)) < dir.Rules.ProtectRadius) clean = false;
            Check(clean, "no wave comes in at a protected landing");
        }
        finally { set.Protected.Clear(); }

        // ---------- car alarm ----------
        var car = stage.Layout.AlarmCar;
        Check(stage.AlarmCars.Contains(car) && !car.Triggered, "the street has an alarm car, quiet at first");
        var sleeper = Place(new Vector3(car.GlobalPosition.X + 40, 0, 0), Reaction.Dormant);
        int mobs = dir.MobsSpawned;
        player.TeleportTo(new Vector3(29, 0, -1.5f));
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        var carAim = car.GlobalPosition + Vector3.Up * .4f;
        player.SetControls(Vector2.Zero, carAim, true);
        for (int i = 0; i < 30 && !car.Triggered; i++) await Frames(1);
        player.SetControls(Vector2.Zero, carAim, false);
        await Frames(2);
        Check(car.Triggered && car.Sounding && dir.AlarmRemaining > 0, "shooting the car sets the alarm off");
        Check(sleeper.Awake, "the alarm wakes idle ones 40 m away");
        Check(stage.Sfx.Played.GetValueOrDefault("alarm_loop") > 0, "the siren sounds");
        Check(dir.Rules.NoiseAlarm >= 60, "monsters hear the alarm 60 m away (how loud it plays is AudioLevelCheck's job)");
        await Frames(60 * 2);
        Check(dir.MobsSpawned > mobs && stage.AllEnemies.Count(e => e.Reaction == Reaction.Hunter) >= 6, "hunter waves come while it sounds");
        await Frames((int)(60 * dir.Rules.AlarmTime));
        Check(!car.Sounding && dir.AlarmRemaining <= 0, "the alarm stops after its time");
        car.Trigger();
        await Frames(2);
        Check(!car.Sounding && dir.AlarmRemaining <= 0, "a car alarm goes off only once");

        // A blast next to the car also sets it off; squadmates' bullets do not.
        stage = await NewStage();
        stage.LeaveSafeRoom();
        car = stage.Layout.AlarmCar;
        stage.Player.TeleportTo(new Vector3(20, 0, 0));
        await Frames(2);
        Blast.Explode(stage, car.GlobalPosition + new Vector3(2.5f, 0, -1), 2.5f, 0, new Color("ff8844"));
        await Frames(2);
        Check(car.Triggered, "an explosion beside the car sets the alarm off");

        _stage.QueueFree();
        await Frames(2);
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 1 };
        AddChild(_stage);
        await Frames(3);
        stage = _stage;
        stage.LeaveSafeRoom();
        var bot = stage.Squad[0].Bot;
        stage.Squad[0].ProcessMode = ProcessModeEnum.Disabled;
        await Frames(2);
        car = stage.Layout.AlarmCar;
        bot.TeleportTo(new Vector3(29, 0, -1.5f));
        bot.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        await Frames(2);
        Check(Rays.Cast(bot, bot.GlobalPosition + Vector3.Up * Player.AimHeight, car.GlobalPosition + new Vector3(0, Player.AimHeight - car.GlobalPosition.Y, 0), Layers.World)?.Collider == car,
            "the squadmate has a clear shot at the car");
        for (int i = 0; i < 30; i++) { bot.SetControls(Vector2.Zero, car.GlobalPosition + Vector3.Up * .4f, true); await Frames(1); }
        bot.SetControls(Vector2.Zero, car.GlobalPosition + Vector3.Up * .4f, false);
        Check(bot.ShotsFired > 0 && !car.Triggered, "a squadmate's bullets do not set the alarm off");
    }
}
