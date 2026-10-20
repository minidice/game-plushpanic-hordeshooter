using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Headless end-to-end checks that drive a real Stage through its systems.
/// Run: Godot --headless --path . res://tests/Integration.tscn
/// </summary>
public partial class Integration : Node
{
    private readonly List<string> _passed = new();
    private Stage _stage = null!;

    public override async void _Ready()
    {
        try
        {
            CatalogChecks();
            RifleChecks();
            DataChecks();
            MoreDataChecks();
            LoadoutChecks();
            AudioChecks();
            await VoiceChecks();
            await WeaponChecks();
            await StageChecks("test_capsule");
            await SwapCheck("goose");
            Finish(true, "");
        }
        catch (Exception e)
        {
            Finish(false, e.Message + "\n" + e.StackTrace);
        }
    }

    private void Check(bool ok, string name)
    {
        if (!ok) throw new Exception("FAILED: " + name);
        _passed.Add(name);
        GD.Print("PASS: " + name);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    // ---------- pure checks ----------

    private void CatalogChecks()
    {
        var players = CharacterCatalog.LoadAll(CharacterCatalog.PlayerDir);
        Check(players.Count >= 3 && players.Any(p => p.Id == "goose") && players.Any(p => p.Model == null), "catalog lists GLB and placeholder player characters");
        var enemies = CharacterCatalog.LoadAll(CharacterCatalog.EnemyDir).Select(e => e.Id).ToHashSet();
        Check(enemies.SetEquals(new[] { "plush", "balloon", "bull", "humanoid", "crawler", "tether", "leaper", "spitter" }), "catalog lists all eight enemy model definitions");

        var goose = CharacterRig.Create(CharacterCatalog.Player("goose"));
        Check(!goose.IsPlaceholder && goose.Muzzle.Name == "Muzzle" && goose.Head != null && goose.Body.Name == "goose_body", "GLB rig resolves named parts");
        goose.Free();

        var bare = new CharacterDefinition { Id = "bare", Model = new PackedScene(), ColliderHeight = 1.5f };
        var empty = new Node3D { Name = "Empty" };
        bare.Model!.Pack(empty);
        var rig = CharacterRig.Create(bare);
        Check(rig.Muzzle != null && rig.Weapon != null && rig.Body == rig.Pose, "model with no named parts gets generated pivots");
        rig.Animate(.016f, 1, 1, 1, Vector3.One);
        rig.Free();
        empty.Free();
    }

    private void RifleChecks()
    {
        var gun = new WeaponState(ArmoryCatalog.Weapon("m4"));
        Check(gun.Trigger(true, true) && gun.Mag == gun.Def.MagSize - 1 && !gun.Trigger(false, true), "rifle fires once per interval");
        float before = gun.Spread;
        gun.Tick(gun.Def.FireInterval, false);
        gun.Trigger(false, true);
        gun.Tick(0, false);
        Check(gun.Spread > before, "firing blooms the spread");
        gun.Tick(2f, false);
        Check(Math.Abs(gun.Spread - gun.Def.BaseSpread) < .01f, "spread recovers when not firing");
        gun.Tick(0, true);
        Check(gun.Spread > gun.Def.BaseSpread + 1, "moving widens the spread");

        Check(gun.StartReload(), "reload starts with a partial magazine");
        gun.Tick(gun.ReloadTime * .9f, false);
        Check(gun.Reloading && !gun.StartReload(), "reload takes its full time (no timing mini-game)");
        gun.Tick(gun.ReloadTime * .2f, false);
        Check(!gun.Reloading && gun.Mag == gun.Def.MagSize, "reload refills the magazine after reload_ms");

        var pistol = new WeaponState(ArmoryCatalog.Weapon("m1911"));
        Check(pistol.Trigger(true, true) && !pistol.Trigger(false, true), "semi-auto pistol fires once per click");
        pistol.Tick(1, false);
        Check(pistol.Trigger(true, true), "semi-auto pistol fires again on the next click");

        var burst = new WeaponState(ArmoryCatalog.Weapon("m16"));
        int shots = burst.Trigger(true, true) ? 1 : 0;
        for (int i = 0; i < 20; i++)
        {
            burst.Tick(.02f, false);
            if (burst.Trigger(false, true)) shots++;
        }
        Check(shots == 3 && burst.Mag == burst.Def.MagSize - 3, "burst rifle fires exactly three per click");
    }

    /// <summary>Every recipe synthesizes something sane; also exports them to output/sounds for listening.</summary>
    private void AudioChecks()
    {
        var dir = ProjectSettings.GlobalizePath("res://output/sounds");
        System.IO.Directory.CreateDirectory(dir);
        var bad = new List<string>();
        foreach (var def in SoundRecipes.All)
        {
            var samples = def.Build(new Synth(1));
            float peak = 0, energy = 0;
            bool finite = true;
            foreach (var v in samples)
            {
                finite &= float.IsFinite(v);
                peak = Math.Max(peak, Math.Abs(v));
                energy += v * v;
            }
            float rms = MathF.Sqrt(energy / samples.Length);
            float seconds = samples.Length / (float)Synth.Rate;
            if (!finite || peak > 1f || peak < .2f || rms < .01f || seconds < .02f || seconds > 8f) bad.Add($"{def.Id}(peak {peak:0.00} rms {rms:0.000} {seconds:0.00}s)");
            Sfx.Synthesize(def).SaveToWav($"{dir}/{def.Id}.wav");
        }
        Check(bad.Count == 0, "every sound recipe synthesizes a clean, audible clip" + (bad.Count > 0 ? ": " + string.Join(", ", bad) : ""));
        Check(SoundRecipes.All.Select(d => d.Id).Distinct().Count() == SoundRecipes.All.Count, "sound ids are unique");
        Check(ArmoryCatalog.Weapons.Values.All(w => SoundRecipes.Find(w.ShotSound) != null), "every weapon has a firing sound");
        Check(new[] { "ear_ring", "game_over", "stage_clear" }.All(id => SoundRecipes.Find(id)!.Bus != AudioSetup.SfxBus),
            "ear ring and stingers bypass the helium/flash effects");
    }

    private async Task VoiceChecks()
    {
        var sfx = new Sfx { Pool3DSize = 6, Pool2DSize = 1 };
        AddChild(sfx);
        await Frames(1);
        for (int i = 0; i < 6; i++) sfx.PlayAt("zombie_step", Vector3.Zero);
        Check(sfx.PlayAt("explosion", Vector3.Zero) != null, "an important sound takes over a crowd voice when the pool is full");
        var small = new Sfx { Pool3DSize = 8, Pool2DSize = 1 };
        AddChild(small);
        await Frames(1);
        for (int i = 0; i < 4; i++) small.PlayAt("sonar", Vector3.Zero);
        Check(small.PlayAt("sonar", Vector3.Zero) == null, "crowd pings at their limit are skipped, not restarted");
        sfx.QueueFree();
        small.QueueFree();
    }

    private void HeardAll(Stage stage, string[] ids, string name)
    {
        var missing = ids.Where(id => stage.Sfx.Played.GetValueOrDefault(id) == 0).ToList();
        Check(missing.Count == 0, name + (missing.Count > 0 ? " (missing: " + string.Join(", ", missing) + ")" : ""));
    }

    /// <summary>CSV defaults + overrides + trait JSON resolve the way doc/weapon-spec.md says.</summary>
    private void DataChecks()
    {
        var db = WeaponDatabase.Current;
        Check(db.Errors.Count == 0, "weapon data loads without errors" + (db.Errors.Count > 0 ? ": " + string.Join(" | ", db.Errors) : ""));
        var m4 = db.Weapons["m4"];
        var ak = db.Weapons["ak47"];
        var mp5 = db.Weapons["mp5"];
        Check(m4.Damage == 11 && ak.Damage == 7 && mp5.Damage == 6, "weapon rows override their type's damage");
        Check(m4.MaxRange == 40 && m4.EffectiveRange == 30 && mp5.MagSize == 50 && Math.Abs(m4.ReloadTime - 2.2f) < .001f,
            "empty cells fall back to the type default (ms converted to seconds)");
        Check(m4.ReserveMax == 10 * 50 && m4.ReserveStart == 8 * 50 && m4.Refillable, "ammo = magazine size × magazine counts");
        Check(db.Weapons["m1911"].InfiniteReserve && db.Weapons["frying_pan"].InfiniteReserve, "slot 1 weapons never run out");
        Check(db.Weapons["r870"].Pellets == 9 && db.Weapons["double_barrel"].Pellets == 14 && !db.Weapons["double_barrel"].Traits.Contains("buckshot"),
            "type traits are inherited, and a weapon can swap one out with -name");
        Check(db.Weapons["m16"].BurstCount == 3 && db.Weapons["m4"].BurstCount == 1, "a weapon can add its own trait");
        var rocket = db.Weapons["alabong"];
        Check(rocket.Mode == FireMode.Projectile && rocket.Damage == 100 && rocket.ExplosionRadius > 4 && rocket.ExplosionDamage > 0 && !rocket.Refillable,
            "rocket has direct-hit damage plus explosion damage from its warhead trait");
        var dual = db.Weapons["m1911"].DualVariant;
        Check(dual != null && dual.Dual && dual.DisplayName == "쌍 M1911" && dual.MagSize == 30 && dual.FireInterval < db.Weapons["m1911"].FireInterval,
            "dual_wield trait derives the dual version (double magazine, faster)");
        Check(db.Weapons["glock17"].DualVariant?.DisplayName == "쌍 글록 17", "every pistol type gets its own dual version");
        Check(m4.FalloffAt(10) == 1 && Math.Abs(m4.FalloffAt(40) - m4.FalloffMin) < .001f && m4.FalloffAt(35) < 1, "damage falls off between effective and max range");
        Check(db.Weapons["m1911"].RecoilPower <= 10 && db.Weapons["r870"].RecoilPower >= 50, "pistol recoil is tiny, shotgun recoil is big");

        const string types = "type,name,slot,category,fire_mode,damage,max_range_m,fire_interval_ms,mag_size,max_mags,traits\n" +
                             "rifle,소총,2,rifle,hitscan,10,40,100,30,5,\n";
        const string weapons = "id,type,name,damage,max_rnage_m,traits\n" +
                               "ok,rifle,정상,,,\n" +
                               "bad_number,rifle,숫자 오류,abc,,\n" +
                               "bad_type,shotgunn,분류 오류,,,\n" +
                               "bad_trait,rifle,특성 오류,,,missing_trait\n";
        var broken = WeaponDatabase.FromText(types, weapons, new Dictionary<string, string> { ["weird"] = "{\"kind\":\"teleport\"}" });
        string all = string.Join("\n", broken.Errors);
        Check(broken.Weapons.ContainsKey("ok") && all.Contains("max_rnage_m") && all.Contains("abc") && all.Contains("shotgunn") &&
              all.Contains("missing_trait") && all.Contains("teleport") && all.Contains("행"),
            "data mistakes are reported with file, line and column instead of crashing");
    }

    private void MoreDataChecks()
    {
        var throwables = ThrowableDatabase.Current;
        Check(throwables.Errors.Count == 0, "throwable data loads without errors" + (throwables.Errors.Count > 0 ? ": " + string.Join(" | ", throwables.Errors) : ""));
        var frag = throwables.Throwables["frag"];
        var lure = throwables.Throwables["lure"];
        var flash = throwables.Throwables["flashbang"];
        Check(frag.Effects.OfType<ExplodeEffect>().Single().Damage == 160 && frag.AimRadius > 4 && frag.Fuse > 2,
            "frag: explosion damage/radius and fuse come from data");
        Check(lure.Attract != null && lure.DetonationEffects.OfType<ExplodeEffect>().Any(), "lure: attract first, then its explosion");
        Check(flash.Effects.OfType<StunEffect>().Any() && flash.Effects.OfType<BlindSelfEffect>().Any(), "flashbang: stun plus self-blind effects");

        var monsters = MonsterDatabase.Current;
        Check(monsters.Errors.Count == 0, "monster data loads without errors" + (monsters.Errors.Count > 0 ? ": " + string.Join(" | ", monsters.Errors) : ""));
        Check(monsters.Monsters["bull"].Mass > monsters.Monsters["plush"].Mass && monsters.Monsters["balloon"].Mass < 1,
            "monsters have a mass (bull heavy, balloon light)");
        Check(monsters.Monsters["bull"].BullCharge?.Speed > 10 && monsters.Monsters["balloon"].BalloonPop?.SummonCount > 0,
            "special behaviour numbers come from monster_traits JSON");
        Check(ArmoryCatalog.Weapon("r870").HitPower >= 80 && ArmoryCatalog.Weapon("m4").HitPower <= 20, "only shotguns (and explosions) really push");

        var kor = Loc.Table("kor");
        var eng = Loc.Table("eng");
        var missing = kor.Keys.Where(k => !eng.ContainsKey(k)).ToList();
        Check(missing.Count == 0, "every Korean text key has an English line" + (missing.Count > 0 ? ": " + string.Join(", ", missing) : ""));
        // A CSV imported as a Godot translation is not exported as plain text, so Loc could not read it in a build.
        var translated = Loc.Languages.SelectMany(l => DirAccess.GetFilesAt($"{Loc.Dir}/{l}").Where(f => f.EndsWith(".csv")).Select(f => $"{Loc.Dir}/{l}/{f}"))
            .Where(path => !FileAccess.FileExists(path + ".import") || !FileAccess.GetFileAsString(path + ".import").Contains("importer=\"keep\"")).ToList();
        Check(translated.Count == 0, "every language CSV is kept as plain text (importer=keep)" + (translated.Count > 0 ? ": " + string.Join(", ", translated) : ""));
        Check(ArmoryCatalog.Weapon("ak47").DisplayName == "AK47" && ArmoryCatalog.Weapon("m1911").DualVariant!.DisplayName == "쌍 M1911",
            "names resolve through the Korean table");
        Loc.Language = "eng";
        bool english = ArmoryCatalog.Weapon("ak47").DisplayName == "AK-47" && ArmoryCatalog.Weapon("m1911").DualVariant!.DisplayName == "Dual M1911"
                       && MonsterDatabase.Get("bull").DisplayName == "Headbutt Bull" && ArmoryCatalog.Throwable("lure").DisplayName == "Squeaky Lure";
        Loc.Language = "kor";
        Check(english, "switching the language to eng switches every name");
        Check(Loc.Text("no_such_key") == "no_such_key", "a missing key shows the key instead of crashing");
    }

    private void LoadoutChecks()
    {
        var weapons = ArmoryCatalog.Weapons.Values.ToList();
        Check(weapons.Count(w => w.Slot == WeaponSlot.Primary && w.Category == WeaponCategory.Rifle) >= 2
              && weapons.Count(w => w.Category == WeaponCategory.Shotgun) >= 2
              && weapons.Count(w => w.Category == WeaponCategory.Sniper) >= 2
              && weapons.Any(w => w.Mode == FireMode.Projectile) && weapons.Any(w => w.Mode == FireMode.Lob),
            "armory has rifles, shotguns, snipers, rocket launcher and mortar");
        Check(ArmoryCatalog.Throwables.Count >= 4, "four throwable kinds");

        var loadout = ArmoryCatalog.NewLoadout();
        Check(loadout.Secondary.Def.Id == "frying_pan" && loadout.Primary == null && !loadout.SelectSlot(2), "start with only the melee weapon");
        var (r1, d1) = loadout.Pickup(ArmoryCatalog.Weapon("m1911"));
        Check(r1 == PickupResult.Swapped && d1?.Def.Id == "frying_pan" && loadout.Secondary.Def.Id == "m1911", "pistol replaces the melee weapon in slot 1");
        Check(loadout.Pickup(ArmoryCatalog.Weapon("m1911")).result == PickupResult.Upgraded && loadout.Secondary.Def.Id == "m1911_dual", "a second M1911 makes dual M1911");
        Check(loadout.Pickup(ArmoryCatalog.Weapon("m1911")).result == PickupResult.Full, "a third M1911 is useless");
        var other = ArmoryCatalog.NewLoadout();
        other.Pickup(ArmoryCatalog.Weapon("m1911"));
        var (r3, d3) = other.Pickup(ArmoryCatalog.Weapon("glock17"));
        Check(r3 == PickupResult.Swapped && d3?.Def.Id == "m1911" && other.Secondary.Def.Id == "glock17", "a different pistol swaps instead of pairing");
        var dual = loadout.Secondary;
        while (dual.Mag > 0) { dual.Tick(1, false); dual.Trigger(true, true); }
        dual.StartReload();
        dual.Tick(dual.ReloadTime + .1f, false);
        Check(dual.Mag == dual.Def.MagSize && dual.Def.InfiniteReserve, "slot 1 never runs out of ammo");

        Check(loadout.Pickup(ArmoryCatalog.Weapon("m4")).result == PickupResult.Equipped && loadout.ActiveSlot == 2, "primary goes to slot 2 and is drawn");
        var (r2, d2) = loadout.Pickup(ArmoryCatalog.Weapon("r870"));
        Check(r2 == PickupResult.Swapped && d2?.Def.Id == "m4" && loadout.Primary!.Def.Id == "r870", "new primary swaps out the old one");
        Check(loadout.Pickup(ArmoryCatalog.Weapon("r870")).result == PickupResult.Full, "same primary with a full reserve is left on the floor");
        var topUp = ArmoryCatalog.NewLoadout();
        topUp.Pickup(ArmoryCatalog.Weapon("m4"));
        int reserve = topUp.Primary!.Reserve;
        Check(topUp.Pickup(ArmoryCatalog.Weapon("m4")).result == PickupResult.Refilled && topUp.Primary!.Reserve > reserve, "same primary again adds its ammo");
        Check(loadout.SelectSlot(1) && loadout.Current.Def.Id == "m1911_dual" && loadout.SelectSlot(2), "1 and 2 switch weapons");

        var burstLoadout = ArmoryCatalog.NewLoadout();
        burstLoadout.Pickup(ArmoryCatalog.Weapon("m16"));
        var burstGun = burstLoadout.Primary!;
        burstGun.Trigger(true, true);
        burstLoadout.SelectSlot(1);
        burstLoadout.SelectSlot(2);
        int magAfterSwitch = burstGun.Mag;
        bool ghostShot = false;
        for (int i = 0; i < 10; i++)
        {
            burstGun.Tick(.05f, false);
            ghostShot |= burstGun.Trigger(false, false);
        }
        Check(!ghostShot && burstGun.Mag == magAfterSwitch, "switching away mid-burst cancels the rest of the burst");

        var reloadLoadout = ArmoryCatalog.NewLoadout();
        reloadLoadout.Pickup(ArmoryCatalog.Weapon("m4"));
        var ar = reloadLoadout.Primary!;
        ar.Trigger(true, true);
        ar.StartReload();
        var (_, droppedAr) = reloadLoadout.Pickup(ArmoryCatalog.Weapon("r870"));
        Check(droppedAr == ar && !ar.Reloading, "a dropped weapon does not keep reloading");

        var fullLoadout = ArmoryCatalog.NewLoadout();
        fullLoadout.Pickup(ArmoryCatalog.Weapon("m4"));
        var fullAr = fullLoadout.Primary!;
        fullAr.AddReserve(1000);
        fullAr.Trigger(true, true);
        fullAr.StartReload();
        fullAr.Tick(10, false);
        var arDef = ArmoryCatalog.Weapon("m4");
        var partial = new WeaponState(arDef);
        int space = arDef.ReserveMax - fullAr.Reserve;
        var (refill, leftover) = fullLoadout.Pickup(arDef, partial);
        Check(space > 0 && refill == PickupResult.Refilled && leftover != null && leftover.TotalAmmo == partial.TotalAmmo - space,
            "ammo that does not fit stays on the floor");

        var rocket = new WeaponState(ArmoryCatalog.Weapon("alabong"), 0, 0);
        var pump = new WeaponState(ArmoryCatalog.Weapon("r870"), 0, 0);
        Check(rocket.Refill() == 0 && pump.Refill() > 0, "ammo piles refill normal guns but not the rocket launcher");

        var frag = ArmoryCatalog.Throwable("frag");
        var molotov = ArmoryCatalog.Throwable("molotov");
        loadout.AddThrowable(frag);
        loadout.AddThrowable(molotov);
        Check(loadout.CurrentThrowable == molotov, "G uses the most recently picked throwable");
        loadout.AddThrowable(molotov);
        Check(!loadout.AddThrowable(molotov), "throwables stack up to their carry limit");
        Check(loadout.TakeThrowable() == molotov && loadout.TakeThrowable() == molotov && loadout.TakeThrowable() == frag && loadout.TakeThrowable() == null,
            "when one kind runs out, G falls back to the next");
    }

    // ---------- stage ----------

    private async Task<Stage> LoadStage(string character)
    {
        _stage?.QueueFree();
        await Frames(2);
        var stage = new Stage { CharacterOverride = character, DirectorEnabled = false };
        AddChild(stage);
        _stage = stage;
        await Frames(3);
        stage.Player.ManualControl = true;
        return stage;
    }

    private void ClearEnemies()
    {
        foreach (var e in _stage.AllEnemies.ToList())
            if (IsInstanceValid(e)) e.QueueFree();
    }

    private async Task StageChecks(string character)
    {
        var stage = await LoadStage(character);
        var player = stage.Player;
        Check(player.Definition.Id == character && ((CapsuleShape3D)player.GetChild<CollisionShape3D>(0).Shape).Height == player.Definition.ColliderHeight,
            "player body comes from the chosen definition");
        Check(ProjectSettings.GetSetting("physics/3d/physics_engine").AsString() == "Jolt Physics", "Jolt physics configured");
        Check(stage.Enemies.Count >= 7 && stage.Enemies.All(e => !e.Awake), "zombie stage starts with an idle pack waiting in the street (spawn-design.md)");

        // Flow field: the start door blocks the route until opened, the shutter blocks the yard.
        var street = new Vector3(20, 0, 0);
        var yard = new Vector3(100, 0, 0);
        Check(!stage.Flow.Reachable(street), "closed safe-room door blocks the flow field");
        player.SetControls(Vector2.Zero, new Vector3(12, 1, 0), false);
        await Frames(10);
        Check(stage.FocusedInteractable == null, "door prompt only shows when close");
        player.GlobalPosition = new Vector3(8.6f, 0, 0);
        await Frames(4);
        Check(stage.FocusedInteractable?.Prompt == "문 열기", "door prompt shows next to the door");
        stage.FocusedInteractable!.Use();
        await Frames(2);
        Check(stage.Objective == Objective.ReachShutter && stage.Flow.Reachable(street), "opening the door advances the objective and the flow field");
        Check(!stage.Flow.Reachable(yard), "closed shutter keeps the yard unreachable");
        await Frames(2);
        Check(stage.FocusedInteractable == null, "a used door stops prompting");

        // Standing against a thin wall must not move the flow target to the far side of it.
        player.GlobalPosition = new Vector3(52.9f, 0, -4);
        await Frames(2);
        Check(stage.Flow.Distance(new Vector3(55.5f, 0, -4)) < stage.Flow.Distance(new Vector3(50.5f, 0, -4)), "flow target stays on the player's side of a wall");
        player.GlobalPosition = new Vector3(24, 0, -3.8f);
        await Frames(2);
        var around = stage.Flow.Direction(new Vector3(13.6f, 0, -3.6f));
        Check(around != Vector3.Zero && Mathf.Abs(around.Z) > .5f && around.X < .5f, "flow field steers around the parked car");

        // Movement and walls.
        ClearEnemies();
        player.GlobalPosition = new Vector3(14, 0, 0);
        player.SetControls(new Vector2(0, -1), new Vector3(14, 1, -10), false);
        await Frames(120);
        Check(player.GlobalPosition.Z > -6 && player.GlobalPosition.Z < -4.5f, "player walks and stops at the street wall");
        player.SetControls(Vector2.Zero, new Vector3(20, 1, 0), false);

        // Vision: walls hide what is behind them even inside the cone.
        var plush = stage.CommonDefinition;
        player.GlobalPosition = new Vector3(61, 0, -3.5f);
        player.SetControls(Vector2.Zero, new Vector3(61, 1, -12), false);
        var walled = stage.SpawnEnemy<Enemy>(plush, new Vector3(61, 0, -8.5f), awake: false);
        await Frames(10);
        Check(!walled.IsSeen && !walled.Rig.Visible, "enemy behind a shelf is hidden even inside the cone");
        ClearEnemies();

        // Shooting, vision cone, crit focus.
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        player.GlobalPosition = new Vector3(26, 0, .4f);
        var aim = new Vector3(44, 1, .4f);
        player.SetControls(Vector2.Zero, aim, false);
        var front = stage.SpawnEnemy<Enemy>(plush, new Vector3(44, 0, .4f), awake: false);
        var behind = stage.SpawnEnemy<Enemy>(plush, new Vector3(18, 0, -2), awake: false);
        await Frames(40);
        // Behind you it is not shown as it is: hidden in the dark, or only a silhouette in a lamp's glow (decisions.md C-23).
        Check(front.IsSeen && !behind.IsSeen && behind.Visibility != VisionSystem.Visibility.Visible && !behind.Rig.IsSilhouette == !behind.Rig.Visible,
            $"vision cone shows the enemy ahead; the one behind is not shown as it is ({behind.Visibility})");
        Check(!behind.Awake, "idle enemy outside sight stays idle");
        Check(player.CritReady && player.FocusTarget == front, "holding the aim on an enemy readies a crit");
        int kills = stage.Kills;
        int magBefore = player.Gun.Mag;
        player.SetControls(Vector2.Zero, aim, true);
        await Frames(30);
        player.SetControls(Vector2.Zero, aim, false);
        Check(player.Gun.Mag < magBefore, "holding fire spends ammo");
        Check(!IsInstanceValid(front) && stage.Kills > kills, "shots kill the enemy");
        await Frames(2);
        Check(stage.Effects.CorpseCount > 0 && stage.Effects.SplatCount > 0, "dead enemy leaves a physics corpse and a paint splat");
        Check(behind.Awake, "gunfire wakes nearby idle enemies");
        ClearEnemies();

        // Melee damage and healing.
        int hp = player.Health;
        var biter = stage.SpawnEnemy<Enemy>(plush, player.GlobalPosition + new Vector3(1.2f, 0, 0), awake: true);
        await Frames(90);
        Check(player.Health < hp, "awake enemy in reach bites the player");
        biter.Die(Vector3.Right, 3);
        player.Medkits = 1;
        int hurt = player.Health;
        player.PressHeal();
        await Frames((int)(Player.HealDuration * 60) + 10);
        Check(player.Health > hurt && player.Medkits == 0, "medkit heals over time");

        // Balloon: pop near the player = helium + summoned mob.
        ClearEnemies();
        await Frames(2);
        var balloon = stage.SpawnEnemy<BalloonEnemy>(MonsterDatabase.Get("balloon"), player.GlobalPosition + new Vector3(2.5f, 0, 0), awake: true);
        await Frames(2);
        balloon.Pop();
        await Frames(2);
        Check(player.HeliumTime > 0 && stage.Enemies.Count(e => !e.IsDead && e is not BalloonEnemy) >= 6, "close balloon pop applies helium and summons a mob");
        Check(stage.Vision.HalfAngle < VisionSystem.NormalHalfAngle, "helium narrows the vision cone");
        await Frames(2);
        Check(AudioSetup.IsEffectOn(AudioSetup.PitchEffect), "helium squeaks every sound effect up in pitch");

        // Bull: charge launches the player and bowls over commons in the path.
        ClearEnemies();
        await Frames(100);
        player.GlobalPosition = new Vector3(30, 0, 0);
        var bull = stage.SpawnEnemy<BullEnemy>(MonsterDatabase.Get("bull"), new Vector3(22, 0, 0), awake: true);
        var pin = stage.SpawnEnemy<Enemy>(plush, new Vector3(25.5f, 0, 0), awake: false);
        await Frames(2);
        int hpBeforeCharge = player.Health;
        bull.StartWindup(Vector3.Right);
        float peak = 0;
        for (int i = 0; i < 150 && !bull.HitPlayerThisCharge; i++) await Frames(1);
        for (int i = 0; i < 30; i++)
        {
            await Frames(1);
            peak = Mathf.Max(peak, player.GlobalPosition.Y);
        }
        Check(bull.HitPlayerThisCharge && player.Health < hpBeforeCharge, "bull charge hits the player");
        Check(peak > .8f && player.StunTime > 0, "charge launches and stuns the player");
        Check(!IsInstanceValid(pin), "commons in the charge path are bowled over");
        await Frames(150);
        Check(!player.IsStunned, "player recovers from the stun");

        // Barrel: explosion kills nearby commons.
        ClearEnemies();
        var barrel = stage.Props.OfType<ExplosiveBarrel>().First(b => b.GlobalPosition.X > 40);
        var victims = Enumerable.Range(0, 3).Select(i => stage.SpawnEnemy<Enemy>(plush, barrel.GlobalPosition + new Vector3(1.4f, 0, i * .9f + .2f), awake: false)).ToList();
        await Frames(2);
        barrel.Ignite();
        await Frames(15);
        Check(victims.All(v => !IsInstanceValid(v)), "explosive barrel kills enemies in its radius");

        // Shooting a barrel (not calling Ignite) must set it off.
        var target = stage.Props.OfType<ExplosiveBarrel>().First(b => b.GlobalPosition.X > 35 && b.GlobalPosition.X < 38);
        player.Invulnerable = true;
        player.GlobalPosition = new Vector3(31.7f, 0, 5.2f);
        var barrelAim = target.GlobalPosition + Vector3.Up;
        player.SetControls(Vector2.Zero, barrelAim, true);
        await Frames(25);
        player.SetControls(Vector2.Zero, barrelAim, false);
        Check(!IsInstanceValid(target) || target.Exploded, "shooting a barrel blows it up");
        player.Invulnerable = false;
        await Frames(60);

        // Crescendo → shutter → safehouse → clear.
        ClearEnemies();
        stage.Layout.CrescendoSeconds = 3;
        player.GlobalPosition = new Vector3(81.5f, 0, -6.2f);
        await Frames(4);
        Check(stage.FocusedInteractable == stage.Layout.ShutterPanel, "shutter panel prompt near the control panel");
        stage.Layout.ShutterPanel.Use();
        await Frames(2);
        Check(stage.Objective == Objective.HoldOut && stage.Director.CrescendoActive, "panel starts the crescendo");
        player.Invulnerable = true;
        await Frames(60 * 3 + 20);
        Check(stage.Objective == Objective.ReachSafehouse && stage.Layout.Shutter.IsOpen && stage.Flow.Reachable(yard), "crescendo ends and the shutter opens the yard");
        Check(stage.Director.MobsSpawned > 0, "crescendo spawned waves");
        player.Invulnerable = false;

        ClearEnemies();
        Check(!stage.Layout.ShutterPanel.Enabled && !stage.Layout.SafehouseSwitch.Enabled, "used panel is disabled and the safehouse switch waits for the player");
        player.GlobalPosition = new Vector3(119, 0, 0);
        await Frames(4);
        Check(stage.Objective == Objective.CloseSafehouse && stage.Layout.SafehouseSwitch.Enabled, "entering the safehouse asks to close the door");
        var lurker = stage.SpawnEnemy<Enemy>(plush, new Vector3(124, 0, 2), awake: false);
        await Frames(2);
        Check(!stage.TryCloseSafehouse(), "door will not close with an enemy inside");
        lurker.Die(Vector3.Right, 1);
        await Frames(2);
        Check(stage.TryCloseSafehouse() && stage.State == StageState.Cleared && !stage.Layout.SafehouseDoor.IsOpen, "closing the safehouse clears the stage");

        HeardAll(stage, new[]
        {
            "step", "shot_rifle", "zombie_die", "sonar", "honk", "heal", "balloon_pop", "bull_snort",
            "bull_bonk", "alarm_loop", "shutter", "stage_clear", "door", "zombie_step", "explosion",
        }, "footsteps, sonar, zombies, specials and the stage beats are all audible");

        // Death.
        var stage2 = await LoadStage(character);
        stage2.Player.TakeDamage(9999, Vector3.Right);
        await Frames(2);
        Check(stage2.State == StageState.Dead && stage2.Player.Corpse != null && stage2.Player.CollisionLayer == 0, "player death ragdolls and ends the run");
        HeardAll(stage2, new[] { "honk_long", "game_over" }, "dying honks and plays the game-over tune");
    }

    private async Task WeaponChecks()
    {
        var stage = await LoadStage("goose");
        var player = stage.Player;
        var zombie = MonsterDatabase.Get("plush"); // Fixed 30 HP fixture for weapon balance regression checks.
        var zombieLook = stage.LookFor(stage.CommonDefinition);
        Check(zombieLook.Id == "humanoid" && zombieLook.Model != player.Definition.Model, "zombie commons use their dedicated humanoid model");
        var sample = stage.Enemies.First(e => !e.IsSpecial);
        Check(sample.Definition.Id is "humanoid" or "crawler" && !sample.Rig.IsPlaceholder,
            "infected use imported humanoid and crawler models");
        Check(player.Rig.Model.FindChild("goose_weapon_mesh", true, false) is Node3D { Visible: false } && player.Gun.Def.Id == "frying_pan",
            "player holds the starting frying pan instead of the model's gun");

        // Pick up from the safe-room floor with the interact flow.
        player.GlobalPosition = new Vector3(7.3f, 0, -2.3f);
        await Frames(3);
        Check(stage.FocusedInteractable is WeaponPickup { Weapon.Id: "m4" }, "floor weapon shows a pickup prompt");
        stage.FocusedInteractable!.Use();
        await Frames(2);
        Check(player.Loadout.Primary?.Def.Id == "m4" && player.Loadout.ActiveSlot == 2, "picked rifle goes to slot 2");
        player.GlobalPosition = new Vector3(8.4f, 0, -2.3f);
        await Frames(3);
        stage.FocusedInteractable!.Use();
        await Frames(2);
        Check(player.Loadout.Primary?.Def.Id == "r870" &&
              stage.Interactables.OfType<WeaponPickup>().Any(p => p.Weapon?.Id == "m4" && p.State != null),
            "swapping primaries drops the old one with its ammo");

        // Melee arc: front enemies get hit, the one behind does not.
        player.PressSlot(1);
        player.GlobalPosition = new Vector3(4, 0, 0);
        player.SetControls(Vector2.Zero, new Vector3(9, 1, 0), false);
        await Frames(2);
        var front = new[] { new Vector3(5.5f, 0, 0), new Vector3(5.3f, 0, .9f), new Vector3(5.3f, 0, -.9f) }
            .Select(p => stage.SpawnEnemy<Enemy>(zombie, p, awake: false)).ToList();
        var back = stage.SpawnEnemy<Enemy>(zombie, new Vector3(2.4f, 0, 0), awake: false);
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(9, 1, 0), true);
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(9, 1, 0), false);
        await Frames(18); // Let the visible windup reach the single melee impact.
        Check(front.All(e => !IsInstanceValid(e)) && IsInstanceValid(back) && !back.IsDead, "frying pan hits everything in the front arc only");
        ClearEnemies();

        stage.LeaveSafeRoom();
        player.Invulnerable = true;

        // Shotgun: one blast hits several enemies.
        player.PressSlot(2);
        player.GlobalPosition = new Vector3(14, 0, 0);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(2);
        var group = new[] { -.7f, 0f, .7f }.Select(z => stage.SpawnEnemy<Enemy>(zombie, new Vector3(18.5f, 0, z), awake: false)).ToList();
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), true);
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(2);
        Check(group.Count(e => !IsInstanceValid(e) || e.Health < e.Monster.Hp) >= 2, "shotgun pellets hit several enemies");
        ClearEnemies();
        await Frames(2);

        // Partial pellet hits wound instead of killing: damage is the total of the pellets that land.
        bool partial = false;
        for (int attempt = 0; attempt < 4 && !partial; attempt++)
        {
            var far = stage.SpawnEnemy<Enemy>(zombie, new Vector3(23.5f, 0, 0), awake: false);
            await Frames(2);
            far.Stun(3);
            player.GlobalPosition = new Vector3(14, 0, 0);
            player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), true);
            await Frames(2);
            player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
            await Frames(2);
            player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
            partial = IsInstanceValid(far) && far.Health < far.Monster.Hp;
            ClearEnemies();
            await Frames(60);
        }
        Check(partial, "a partial shotgun hit wounds instead of killing");

        // Recoil: every gun pushes the shooter back by its power; the option scales it.
        float Push(string id, float scale)
        {
            GameSettings.RecoilScale = scale;
            var gun = ArmoryCatalog.Weapon(id);
            return Knockback.Recoil(gun.RecoilPower);
        }
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m1911"), null);
        player.PressSlot(1);
        await Frames(2);
        player.GlobalPosition = new Vector3(20, 0, 0);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(10);
        float x0 = player.GlobalPosition.X;
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), true);
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(30);
        float pistolKick = x0 - player.GlobalPosition.X;
        player.PressSlot(2);
        await Frames(10);
        x0 = player.GlobalPosition.X;
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), true);
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(40);
        float shotgunKick = x0 - player.GlobalPosition.X;
        Check(pistolKick > 0 && pistolKick < .05f && shotgunKick > .6f, $"pistol barely nudges, shotgun slides you back (pistol {pistolKick:0.000}m, shotgun {shotgunKick:0.00}m)");
        Check(Push("r870", 0) == 0 && Push("r870", .5f) * 2 == Push("r870", 1), "recoil option scales the knockback");
        GameSettings.RecoilScale = 1;

        // Sniper: one bullet through a line of enemies.
        player.Weapons.Pickup(ArmoryCatalog.Weapon("awm"), null);
        player.GlobalPosition = new Vector3(14, 0, 0);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(2);
        var line = new[] { 20f, 22f, 24f }.Select(x => stage.SpawnEnemy<Enemy>(zombie, new Vector3(x, 0, 0), awake: false)).ToList();
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), true);
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(2);
        Check(line.All(e => !IsInstanceValid(e)), "sniper bullet pierces a line of enemies");

        // Rocket: explodes on the crowd.
        player.Weapons.Pickup(ArmoryCatalog.Weapon("alabong"), null);
        player.GlobalPosition = new Vector3(14, 0, 0);
        var crowd = Cluster(stage, zombie, new Vector3(24, 0, 0));
        await Frames(2);
        foreach (var e in crowd) e.Stun(3);
        player.SetControls(Vector2.Zero, new Vector3(24, 1, 0), true);
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(24, 1, 0), false);
        await Frames(45);
        Check(crowd.All(e => !IsInstanceValid(e)), "rocket blows up the crowd");
        int rockets = player.Gun.Reserve;
        stage.RefillAmmo();
        Check(player.Gun.Reserve == rockets, "ammo pile does not refill the rocket launcher");

        // Mortar: lands on the aim point after its flight time.
        player.Weapons.Pickup(ArmoryCatalog.Weapon("mortar"), null);
        crowd = Cluster(stage, zombie, new Vector3(26, 0, 0));
        await Frames(2);
        // Shells take a while to land; hold the targets still (a flashbang does this in play).
        foreach (var e in crowd) e.Stun(5);
        player.SetControls(Vector2.Zero, new Vector3(26, 1, 0), true);
        await Frames(2);
        player.SetControls(Vector2.Zero, new Vector3(26, 1, 0), false);
        bool marked = stage.Effects.GetChildren().OfType<MortarShell>().Any(m => m.Target.DistanceTo(new Vector3(26, 0, 0)) < .5f);
        await Frames(30);
        bool earlyAlive = crowd.All(IsInstanceValid);
        await Frames((int)(ArmoryCatalog.Weapon("mortar").FlightTime * 60) + 10);
        Check(marked && earlyAlive && crowd.All(e => !IsInstanceValid(e)), "mortar shell lands on the marked spot after its flight");

        // Throwables.
        await Throws(stage, player, zombie);
        await KnockbackChecks(stage, player, zombie);

        // Dodge roll.
        ClearEnemies();
        player.Invulnerable = false;
        player.GlobalPosition = new Vector3(30, 0, 0);
        player.SetControls(new Vector2(1, 0), new Vector3(40, 1, 0), false);
        await Frames(2);
        var blocker = stage.SpawnEnemy<Enemy>(zombie, new Vector3(32, 0, 0), awake: false);
        await Frames(2);
        blocker.Stun(3);
        float startX = player.GlobalPosition.X;
        int hp = player.Health;
        player.PressRoll();
        await Frames(3);
        player.TakeDamage(20, Vector3.Left);
        Check(player.IsRolling && player.Health == hp, "rolling dodges damage");
        await Frames(30);
        Check(player.GlobalPosition.X > blocker.GlobalPosition.X && player.GlobalPosition.X - startX > 3f, "roll carries the player through a zombie");
        player.PressRoll();
        await Frames(2);
        Check(!player.IsRolling && player.RollCooldownLeft > Player.RollCooldown - 1, "roll has a 5 second cooldown");
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        // Awake zombies nearby moan now and then.
        int groans = stage.Sfx.Played.GetValueOrDefault("groan");
        foreach (var e in Cluster(stage, zombie, player.GlobalPosition + new Vector3(6, 0, 0), awake: true)) e.Stun(6);
        await Frames(60 * 4);
        Check(stage.Sfx.Played.GetValueOrDefault("groan") > groans, "awake zombies nearby groan");
        ClearEnemies();

        HeardAll(stage, new[]
        {
            "melee_swing", "pan_bong", "shot_shotgun", "shot_sniper", "rocket_launch", "mortar_fire", "mortar_whistle",
            "explosion", "throw", "glass_break", "fire_loop", "flash_bang", "ear_ring", "lure_beep", "roll", "door",
            "zombie_hit", "zombie_die", "pickup", "ambience_loop",
        }, "weapons, throwables and rolling all make their sounds");
    }

    private async Task Throws(Stage stage, Player player, MonsterDefinition zombie)
    {
        ClearEnemies();
        player.GlobalPosition = new Vector3(14, 0, 0);
        player.SetControls(Vector2.Zero, new Vector3(22, 1, 0), false);
        await Frames(2);

        player.Loadout.AddThrowable(ArmoryCatalog.Throwable("frag"));
        var crowd = Cluster(stage, zombie, new Vector3(22, 0, 0));
        foreach (var e in crowd) e.Stun(6);
        await Toss(player);
        await Frames(2);
        Check(stage.Effects.GetChildren().OfType<ThrownItem>().Any(), "G throws the grenade");
        await Frames(60 * 3 + 20);
        Check(crowd.All(e => !IsInstanceValid(e)), "frag grenade kills the crowd");

        player.Loadout.AddThrowable(ArmoryCatalog.Throwable("molotov"));
        crowd = Cluster(stage, zombie, new Vector3(22, 0, 0));
        foreach (var e in crowd) e.Stun(6);
        await Toss(player);
        await Frames(60 * 3);
        Check(stage.Effects.GetChildren().OfType<FireZone>().Any() && crowd.All(e => !IsInstanceValid(e)), "molotov leaves fire that burns zombies");
        await Frames(60 * 6);

        player.Loadout.AddThrowable(ArmoryCatalog.Throwable("flashbang"));
        crowd = Cluster(stage, zombie, new Vector3(20, 0, 0), awake: true);
        player.SetControls(Vector2.Zero, new Vector3(19, 1, 0), false);
        await Toss(player);
        await Frames(60 * 2);
        Check(crowd.All(e => IsInstanceValid(e) && e.StunTime > 0), "flashbang stuns nearby zombies");
        Check(player.BlindTime > 0, "watching your own flashbang blinds you");
        Check(AudioSetup.IsEffectOn(AudioSetup.MuffleEffect), "flashbang muffles the sound while blinded");
        ClearEnemies();
        await Frames(200);
        player.Loadout.AddThrowable(ArmoryCatalog.Throwable("flashbang"));
        player.SetControls(Vector2.Zero, new Vector3(19, 1, 0), false);
        await Toss(player);
        await Frames(10);
        player.SetControls(Vector2.Zero, new Vector3(8, 1, 0), false);
        await Frames(110);
        Check(player.BlindTime <= 0, "looking away from a flashbang keeps your eyes");
        await Frames(30);

        player.Loadout.AddThrowable(ArmoryCatalog.Throwable("lure"));
        player.GlobalPosition = new Vector3(30, 0, 0);
        player.SetControls(Vector2.Zero, new Vector3(20, 1, 0), false);
        await Frames(2);
        crowd = Cluster(stage, zombie, new Vector3(38, 0, 0), awake: true);
        await Toss(player);
        await Frames(60);
        var lure = stage.Lure;
        Check(lure != null, "lure starts beeping where it lands");
        float before = crowd.Average(e => e.GlobalPosition.DistanceTo(lure!.GlobalPosition));
        await Frames(120);
        float after = crowd.Where(IsInstanceValid).Average(e => e.GlobalPosition.DistanceTo(lure!.GlobalPosition));
        Check(after < before - 3, "zombies walk to the lure instead of the player");
        await Frames(60 * 5);
        Check(stage.Lure == null && crowd.Count(IsInstanceValid) < crowd.Count, "lure explodes on the crowd it gathered");
        ClearEnemies();

        // Aim mode: G holds the throwable and shows where it lands; click throws, then back to the gun.
        player.GlobalPosition = new Vector3(30, 0, 0);
        player.SetControls(Vector2.Zero, new Vector3(36, 1, 0), false);
        await Frames(2);
        player.Loadout.AddThrowable(ArmoryCatalog.Throwable("frag"));
        player.Loadout.AddThrowable(ArmoryCatalog.Throwable("molotov"));
        int slot = player.Loadout.ActiveSlot;
        player.PressThrow();
        await Frames(2);
        Check(player.Weapons.AimingThrow && player.Weapons.Preview != null && stage.Effects.GetChildren().OfType<ThrownItem>().All(t => t.Def.Id != "molotov"),
            "G starts aiming (nothing is thrown yet) with a landing preview");
        player.SetControls(Vector2.Zero, new Vector3(80, 1, 0), false);
        await Frames(2);
        float reach = new Vector2(player.Weapons.Preview!.Target.X - player.GlobalPosition.X, player.Weapons.Preview.Target.Z - player.GlobalPosition.Z).Length();
        Check(reach <= ArmoryCatalog.Throwable("molotov").MaxRange + .6f, "the landing spot stops at the throwable's max range");
        player.PressCycle(1);
        await Frames(2);
        Check(player.Loadout.CurrentThrowable?.Id == "frag", "mouse wheel switches the throwable kind while aiming");
        player.PressCancel();
        await Frames(2);
        Check(!player.Weapons.AimingThrow && player.Loadout.Count(ArmoryCatalog.Throwable("frag")) == 1 && player.Weapons.Preview == null,
            "right click cancels without throwing");
        player.SetControls(Vector2.Zero, new Vector3(40, 1, 0), false);
        await Toss(player);
        Check(!player.Weapons.AimingThrow && player.Loadout.ActiveSlot == slot && player.Loadout.Count(ArmoryCatalog.Throwable("frag")) == 0,
            "after the click the throwable flies and the previous weapon is back in hand");
        await Frames(60 * 3);
        player.Loadout.TakeThrowable();
    }

    /// <summary>G to aim, click to throw at the current aim point.</summary>
    private async Task Toss(Player player)
    {
        player.PressThrow();
        await Frames(2);
        var aim = player.AimPoint;
        player.SetControls(Vector2.Zero, aim, true);
        await Frames(2);
        player.SetControls(Vector2.Zero, aim, false);
    }

    /// <summary>Hits push by power ÷ mass; small hits are a flinch, big ones a shove.</summary>
    private async Task KnockbackChecks(Stage stage, Player player, MonsterDefinition zombie)
    {
        ClearEnemies();
        player.GlobalPosition = new Vector3(14, 0, 0);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(2);
        var common = stage.SpawnEnemy<Enemy>(zombie, new Vector3(20, 0, -3), awake: false);
        var heavy = stage.Spawn(MonsterDatabase.Get("bull"), new Vector3(20, 0, 3), awake: false);
        await Frames(2);
        common.Stun(3);
        heavy.Stun(3);
        float c0 = common.GlobalPosition.X, h0 = heavy.GlobalPosition.X;
        common.Shove(Vector3.Right, 90, 0);
        heavy.Shove(Vector3.Right, 90, 0);
        await Frames(40);
        float pushed = common.GlobalPosition.X - c0, heavyPushed = heavy.GlobalPosition.X - h0;
        Check(pushed > .8f && heavyPushed < pushed / 3, $"a shotgun-sized push moves a zombie, a heavy bull far less ({pushed:0.00}m vs {heavyPushed:0.00}m)");
        ClearEnemies();
        await Frames(2);

        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        var target = stage.SpawnEnemy<Enemy>(zombie, new Vector3(20, 0, 0), awake: false);
        await Frames(2);
        target.Stun(3);
        float x0 = target.GlobalPosition.X;
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), true);
        await Frames(1);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(1);
        bool flinched = IsInstanceValid(target) && target.FlinchTime > 0;
        await Frames(30);
        float moved = IsInstanceValid(target) ? target.GlobalPosition.X - x0 : 0;
        Check(flinched && moved < .25f, $"a rifle bullet is only a short flinch ({moved:0.00}m)");
        ClearEnemies();
        await Frames(2);

        // Crawlers are shorter than the bullet line; their body still has to stop a flat shot.
        var crawler = stage.Spawn(MonsterDatabase.Get("crawler"), new Vector3(20, 0, 0), awake: false);
        await Frames(2);
        crawler.Stun(3);
        float crawlerHp = crawler.Health;
        bool crawlerIsLow = crawler.Definition.ColliderHeight < Player.AimHeight;
        await Frames(10);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), true);
        await Frames(1);
        player.SetControls(Vector2.Zero, new Vector3(30, 1, 0), false);
        await Frames(2);
        Check(crawlerIsLow && (!IsInstanceValid(crawler) || crawler.IsDead || crawler.Health < crawlerHp),
            "a low crawler still takes a flat rifle shot");
        ClearEnemies();
    }



    private static List<Enemy> Cluster(Stage stage, MonsterDefinition def, Vector3 center, bool awake = false) =>
        new[] { new Vector3(0, 0, 0), new Vector3(1.1f, 0, .6f), new Vector3(1.1f, 0, -.6f), new Vector3(-1f, 0, .5f) }
            .Select(o => stage.SpawnEnemy<Enemy>(def, center + o, awake)).ToList();

    private async Task SwapCheck(string character)
    {
        var stage = await LoadStage(character);
        var player = stage.Player;
        player.SetControls(new Vector2(1, 0), new Vector3(9, 1, 0), true);
        await Frames(40);
        Check(player.Definition.Id == character && !player.Rig.IsPlaceholder && player.ShotsFired > 0, "swapping to the GLB goose plays with the same systems");
    }

    private void Finish(bool ok, string error)
    {
        var report = System.Text.Json.JsonSerializer.Serialize(new
        {
            ok,
            passed = _passed.Count,
            checks = _passed,
            error,
            engine = Engine.GetVersionInfo()["string"].AsString(),
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://output"));
        System.IO.File.WriteAllText(ProjectSettings.GlobalizePath("res://output/integration-report.json"), report);
        GD.Print((ok ? "INTEGRATION_PASS " : "INTEGRATION_FAIL ") + _passed.Count + (ok ? "" : "\n" + error));
        QuitHygiene.Collect();
        GetTree().Quit(ok ? 0 : 1);
    }
}
