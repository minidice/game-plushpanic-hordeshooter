using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Survival and Survivor first versions (doc/menu-multiplayer-plan.md 3.1): mode data, the warehouse arena,
/// Survival's timed waves and generator (monsters hit it, it breaks, lasting the waves clears),
/// Survivor's swarm and skill boss (slam, summon, enrage, kill clears), the HUD bar and the end states.
/// With a real window it also saves renders to output/mode-*.png.
/// </summary>
public partial class ModeCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private Task Seconds(float s) => Frames(Mathf.CeilToInt(s * Engine.PhysicsTicksPerSecond));

    private async Task Until(Func<bool> done, string what, float seconds = 10)
    {
        for (int i = 0; i < seconds * Engine.PhysicsTicksPerSecond; i++)
        {
            if (done()) { Check(true, what); return; }
            await Frames(1);
        }
        Check(done(), what + " (timed out)");
    }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("MODE_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("MODE_CHECK_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task<Stage> NewStage(GameMode mode)
    {
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(2); }
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = mode };
        AddChild(_stage);
        await Frames(3);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        return _stage;
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/mode-{name}.png"));
        GD.Print($"SHOT: output/mode-{name}.png");
    }

    private async Task Run()
    {
        // ---------- data ----------
        Check(GameSession.PlayableModes.SequenceEqual(new[] { GameMode.Scenario, GameMode.Survival, GameMode.Survivor }),
            "all three modes are playable (the menu and the room follow this list)");
        var tuning = ModeTuning.Current;
        Check(tuning.Errors.Count == 0, "mode_rules.json loads without errors" + string.Join("; ", tuning.Errors));
        var bad = ModeTuning.FromText("{\"survival\": {\"core_hp\": -1, \"waves\": [{\"every_s\": \"often\"}], \"spare\": 1}, \"survivor\": {\"spawn_min_m\": 9, \"spawn_max_m\": 4}}").Errors;
        Check(bad.Count == 4, "a negative core, a non-number, an unknown key and a backwards spawn ring are reported: " + string.Join(" / ", bad));
        Check(MonsterDatabase.Current.Errors.Count == 0, "monsters.csv (with the boss row) loads without errors" + string.Join("; ", MonsterDatabase.Current.Errors));
        // ---------- Onslaught ammo: unlimited reserves, slot-3 kinds recharge (M-26) ----------
        var kit = ArmoryCatalog.NewLoadout();
        kit.UseOnslaughtAmmo(tuning.Survivor.SpecialCooldown);
        kit.SetSlot(1, ArmoryCatalog.Weapon("m4"));
        var rifle = kit.Secondary;
        while (rifle.Mag > 0) { rifle.Trigger(true, true); rifle.Tick(1, false); }
        Check(rifle.HasInfiniteReserve && rifle.StartReload(), "a rifle in slot 1 reloads from unlimited reserves");
        rifle.Tick(10, false);
        Check(rifle.Mag == rifle.Def.MagSize && !rifle.IsEmpty, "and is full again");
        kit.SetSlot(2, ArmoryCatalog.Weapon("alabong"));
        var rocket = kit.Primary!;
        Check(rocket.Trigger(true, false) && rocket.Mag == 0 && rocket.CooldownLeft > 0 && !rocket.CanFire && !rocket.StartReload(),
            $"a rocket launcher recharges for {tuning.Survivor.SpecialCooldown}s after a shot instead of reloading");
        kit.SelectSlot(1);
        for (int i = 0; i < 26; i++) kit.TickIdle(1);
        Check(rocket.CooldownLeft == 0 && rocket.Mag == rocket.Def.MagSize && rocket.CanFire, "it recharges while put away too");
        Check(tuning.Survivor.WeaponPool.Count >= tuning.Survivor.RackSize && tuning.Survivor.WeaponPool.All(id => ArmoryCatalog.Weapon(id).Mode != FireMode.Melee),
            "the rack pool lists guns only");

        var bossDef = MonsterDatabase.Get(BossEnemy.MonsterId);
        Check(bossDef.Behavior == MonsterBehavior.Boss && bossDef.BullCharge != null && bossDef.IsSpecial && bossDef.Tier == MonsterTier.Boss,
            "the boss is a boss-tier special with a bull charge");
        Check(bossDef.KeepsCorpse && MonsterDatabase.Get("bull").KeepsCorpse && !MonsterDatabase.Get("humanoid").KeepsCorpse,
            "boss and named corpses follow the same policy, commons their own (T-21)");

        // ---------- mode monster tables (M-24) ----------
        var outpost = ModeSpawnTable.For(GameMode.Survival);
        var onslaught = ModeSpawnTable.For(GameMode.Survivor);
        Check(outpost.Errors.Count == 0 && onslaught.Errors.Count == 0, "both mode tables load without errors" + string.Join("; ", outpost.Errors.Concat(onslaught.Errors)));
        var rng = new RandomNumberGenerator { Seed = 7 };
        var wave1 = Enumerable.Range(0, 60).Select(_ => outpost.PickCommon(1, "zombie", rng)!.Monster).ToList();
        var wave3 = Enumerable.Range(0, 200).Select(_ => outpost.PickCommon(3, "zombie", rng)!.Monster).ToList();
        Check(wave1.All(m => m == "@common") && wave3.Contains("armed_animal") && wave3.Contains("@common"), "wave 1 sends only commons, wave 3 mixes in armed animals");
        Check(outpost.NamedDue(0, 1, "zombie").Count() == 0 && outpost.NamedDue(1, 3, "zombie").Count() == 2, "named ones open waves 2 and 3, not wave 1");
        Check(onslaught.PickCommon(30, "armed", rng)!.Monster == "@common" && Enumerable.Range(0, 100).Any(_ => onslaught.PickCommon(80, "armed", rng)!.Monster == "armed_animal"),
            "theme rows only join when they match the stage theme and time");
        var badTable = ModeSpawnTable.FromText("test", "monster,role,theme,weight,count,from,until,hp_scale,damage_scale,extra\n" +
            "ghost,common,any,1,,0,,1,1,\n@common,boss,any,1,,0,,1,1,\n@common,common,space,0,,5,2,0,1,\n").Errors;
        Check(badTable.Count >= 6, "a table with an unknown column, monster, role and theme, zero weight and scale, and until before from is reported: " + string.Join(" / ", badTable));

        // ---------- boss phases and skills (M-24) ----------
        Check(BossDatabase.Current.Errors.Count == 0, "boss data loads without errors" + string.Join("; ", BossDatabase.Current.Errors));
        var bossData = BossDatabase.Get(BossEnemy.MonsterId);
        Check(bossData.Phases.Count == 3 && bossData.PhaseAt(1) == 0 && bossData.PhaseAt(.6f) == 1 && bossData.PhaseAt(.2f) == 2,
            "three phases by health: 100 / 66 / 33%");
        Check(bossData.Skills.Values.Select(k => k.Kind).Distinct().Count() == 4 && bossData.Phases[2].Skills.Count > bossData.Phases[0].Skills.Count,
            "four skill kinds; later phases use more skills");
        var badBoss = BossDatabase.FromText(new Dictionary<string, string>
        {
            ["test"] = "{\"phases\": [{\"from_hp_pct\": 90, \"skills\": [\"nope\"]}, {\"from_hp_pct\": 95}], \"skills\": {\"x\": {\"kind\": \"laser\"}, \"s\": {\"kind\": \"slam\", \"radius\": 3}}}",
        }).Errors;
        Check(badBoss.Count == 5, "a missing skill, a first phase below 100, phases going up, an unknown kind and an unknown key are reported: " + string.Join(" / ", badBoss));
        var badPose = BossDatabase.FromText(new Dictionary<string, string>
        {
            ["test"] = "{\"phases\": [{\"from_hp_pct\": 100, \"skills\": [\"s\"], \"pose\": \"moonwalk\", \"pose_s\": -1}], \"skills\": {\"s\": {\"kind\": \"slam\", \"pose\": \"twirl\", \"leap_m\": -2}}}",
        }).Errors;
        Check(badPose.Count == 4, "an unknown phase pose, a negative pose_s, an unknown skill pose and a negative leap are reported: " + string.Join(" / ", badPose));
        Check(bossData.Skills["slam"].Pose == "crouch_leap" && bossData.Skills["slam"].Leap > 0 && bossData.Skills["summon"].Windup > 0 &&
              bossData.Skills["charge"].RunPose == "gallop" && bossData.Phases[1].Pose != "" && bossData.Phases[1].PoseTime > 0,
            "the boss data names its motions: leaping slam, roaring summon, galloping charge, a roar at each phase");
        Check(BossEnemy.MonsterId == bossData.Id && bossDef.Hp >= 8000, $"the boss is built for a 2–3 minute fight ({bossDef.Hp} HP)");

        // ---------- scenario is unchanged ----------
        var stage = await NewStage(GameMode.Scenario);
        Check(stage.ModeRules == null && stage.Objective == Objective.LeaveSafeRoom && stage.Director.Encounters != null && stage.AllEnemies.Any(),
            "scenario still starts in the safe room with the street pre-filled");
        Check(stage.Hud.FindChild("ModeBar", true, false) is Control { Visible: false }, "scenario has no mode bar");

        await Survival();
        await Survivor();
    }

    private async Task Survival()
    {
        var stage = await NewStage(GameMode.Survival);
        var rules = stage.ModeRules as SurvivalRules;
        Check(rules != null && stage.Objective == Objective.ModeGoal, "survival runs its own rules");
        var r = rules!.Rules;
        Check(stage.Player.GlobalPosition.DistanceTo(stage.Map.PlayerStart) < .5f && stage.Layout.Shutter.IsOpen && !stage.Layout.StartDoor.IsOpen,
            "survival starts in the warehouse with the shutter raised");
        Check(stage.Director.Encounters == null && !stage.AllEnemies.Any(), "nothing is pre-placed");
        var core = rules.Core!;
        Check(core.Alive && core.GlobalPosition.DistanceTo(stage.Map.CoreSpot) < .01f && core.Health == r.CoreHp, "the generator stands in the hall at full health");
        Check(stage.Hud.FindChild("ModeBar", true, false) is Control { Visible: true } && rules.BarLabel.Contains("100"), "the HUD shows the base bar at 100%");
        Check(rules.Phase(0) == (-1, false, r.Prep) && rules.Phase(r.Prep + 1).Wave == 0 && rules.Phase(r.Prep + 1).Running,
            "the clock starts with preparation, then wave 1");
        Check(rules.Phase(r.Prep + r.Waves[0].Duration + 1) is { Wave: 0, Running: false } && rules.Phase(r.Prep + r.Waves[0].Duration + r.Break + 1) is { Wave: 1, Running: true },
            "a break follows each wave");
        Check(r.Total is > 150 and < 210, $"one match lasts about three minutes ({r.Total:0}s)");
        Check(!rules.ObjectiveText.StartsWith("fmt_") && rules.ObjectiveText.Length > 0, "the objective text is translated: " + rules.ObjectiveText);

        // Waves spawn by themselves through the doorways.
        stage.AdvanceClockForTest(r.Prep + .1f);
        await Until(() => rules.SpawnedThisMatch > 0, "wave 1 sends a group");
        Check(stage.Notice == Loc.Format("fmt_survival_wave_start", 1, r.Waves.Count), "wave 1 is announced");
        Check(stage.AllEnemies.All(e => e.GlobalPosition.X is < 52 or > 84), "groups come in from the street or the yard, not inside the hall");
        for (int i = 0; i < 6; i++) rules.SpawnGroup(r.Waves[2], 2);
        Check(stage.AllEnemies.Any(e => e.Monster.Id is "armed_animal"), "the last wave brings armed animals");
        stage.AdvanceClockForTest(r.Prep + r.Waves[0].Duration + r.Break + .2f - stage.Elapsed);
        await Until(() => rules.NamedSent >= 1 && stage.AllEnemies.Any(e => e.IsSpecial), "wave 2 opens with a named monster from the table");

        // Away from the generator, the horde goes for it instead of the player.
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        await Frames(2);
        stage.Player.GlobalPosition = new Vector3(4, 0, 0); // inside the closed safe room
        for (int i = 0; i < 6; i++) stage.SpawnCommon(stage.Map.CoreSpot + new Vector3(4 + i * .4f, 0, i % 2 == 0 ? .8f : -.8f), awake: true);
        await Until(() => core.Health < core.MaxHealth, "commons walk to the generator and hit it");
        await Shot("survival-core");
        core.TakeDamage(core.MaxHealth, stage.Map.CoreSpot + Vector3.Right);
        await Frames(2);
        Check(!core.Alive && stage.State == StageState.Dead && rules.ResultText.Length > 0, "a broken generator loses the match");
        await Frames(10);
        await Shot("survival-lost");

        // Lasting every wave clears it.
        stage = await NewStage(GameMode.Survival);
        rules = (SurvivalRules)stage.ModeRules!;
        if (Windowed)
        {
            // A real look at a wave in progress for the render review.
            stage.AdvanceClockForTest(r.Prep + r.Waves[0].Duration + r.Break + .1f);
            await Seconds(9);
            await Shot("survival-wave");
        }
        stage.AdvanceClockForTest(r.Total + 1);
        await Frames(2);
        Check(stage.State == StageState.Cleared && stage.Player.Invulnerable && rules.ObjectiveText == Loc.Text("ui_objective_survival_cleared"),
            "lasting to the end of the last wave clears the match");
        await Frames(10);
        await Shot("survival-clear");
    }

    private async Task Survivor()
    {
        var stage = await NewStage(GameMode.Survivor);
        var rules = stage.ModeRules as SurvivorRules;
        Check(rules != null && stage.Objective == Objective.ModeArming && rules.Arming && !stage.AllEnemies.Any(), "Onslaught opens with arming and no swarm");
        var r = rules!.Rules;
        Check(stage.Map is OpenFieldMap && stage.Map.TitleKey == "ui_map_open_field", "Onslaught plays its own open field");
        Check(rules.GroupSize == r.GroupStart && rules.BarLabel.Length > 0 && !rules.BossArrived, "the bar counts down to the boss");

        // Arming: two racks of rack_size random weapons; each pick goes to its slot with unlimited reserves.
        Check(rules.RackItems.Count == r.RackSize && rules.RackItems.All(i => r.WeaponPool.Contains(i.Weapon!.Id)), $"a rack of {r.RackSize} weapons from the pool lies on the floor");
        await Shot("survivor-rack");
        var first = rules.RackItems[0].Weapon!;
        rules.RackItems[0].Use(stage.Player);
        await Frames(2);
        Check(stage.Player.Loadout.Secondary.Def == first && stage.Player.Loadout.ActiveSlot == 1 && rules.Arming && rules.RackItems.Count == r.RackSize,
            $"the first pick ({first.Id}) becomes slot 1 and a second rack opens");
        await Seconds(2);
        Check(rules.SpawnedThisMatch == 0 && rules.MatchTime == 0, "nothing comes and the clock waits while arming");
        var second = rules.RackItems[1].Weapon!;
        rules.RackItems[1].Use(stage.Player);
        await Frames(2);
        Check(stage.Player.Loadout.Primary?.Def == second && !rules.Arming && stage.Objective == Objective.ModeGoal && rules.RackItems.Count == 0,
            $"the second pick ({second.Id}) becomes slot 2 and the match starts");
        Check(stage.Player.Loadout.Secondary.HasInfiniteReserve && stage.Player.Loadout.Primary!.HasInfiniteReserve, "both picks have unlimited reserves");

        await Until(() => rules.SpawnedThisMatch > 0, "the swarm starts once armed");
        var start = stage.Player.GlobalPosition;
        var commons = stage.AllEnemies.Where(e => !e.IsSpecial).ToList();
        Check(commons.All(e => e.GlobalPosition.DistanceTo(start) is > 10 and < 26), "the swarm arrives on a ring around the player");
        float weak = ModeSpawnTable.For(GameMode.Survivor).Rows.First(row => row.Monster == "@common").HpScale;
        Check(commons.All(e => e.Health <= e.Monster.Hp * weak + .01f), $"survivor commons get the table's hp_scale ({weak})");

        // A named wave, then a rest once they are down (M-24).
        stage.AdvanceClockForTest(45.2f - rules.MatchTime);
        await Until(() => rules.NamedSent >= 1, "a named monster arrives at its table time");
        Check(rules.ObjectiveText == Loc.Text("ui_objective_survivor_named"), "the objective asks to take it down");
        foreach (var e in stage.AllEnemies.Where(e => e.IsSpecial).ToList()) e.Burn(99999);
        await Until(() => rules.RestLeft > 0, "once the named are down the rest starts");
        Check(rules.UpgradeRacks >= 1 && rules.RackItems.Count == r.RackSize, "a fallen named drops an upgrade rack");
        var upgrade = rules.RackItems[0].Weapon!;
        stage.Player.TeleportTo(rules.RackItems[0].GlobalPosition);
        rules.RackItems[0].Use(stage.Player);
        await Frames(2);
        Check(stage.Player.Loadout.Current.Def == upgrade && stage.Player.Loadout.Current.HasInfiniteReserve && rules.RackItems.Count == 0,
            $"the upgrade ({upgrade.Id}) replaces the weapon in hand and the rack is gone");

        // Teammates off screen get an arrow on the screen edge (drawn only with a real window, like the hit arcs).
        if (Windowed)
        {
            // Aim at your own feet so the camera centres on you (a mortar in hand leans it far toward the cursor).
            stage.Player.SetControls(Vector2.Zero, stage.Player.GlobalPosition + Vector3.Up, false);
            stage.Cam.SnapTo(stage.Player.GlobalPosition);
            await Frames(20);
            var mate = stage.AddSurvivor(CharacterCatalog.Player("duck"), stage.Player.GlobalPosition + new Vector3(30, 0, 0), true, "FarMate", new AppearanceProfile());
            mate.ManualControl = true;
            mate.Invulnerable = true;
            mate.DisplayLabel = "FarMate";
            for (int i = 0; i < 4; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Check(stage.Hud.OffscreenArrows.Contains("FarMate"), "a teammate off screen gets an edge arrow with the name");
            await Shot("survivor-arrow");
            mate.TeleportTo(stage.Player.GlobalPosition + new Vector3(1.5f, 0, 0));
            // The arrows are worked out while drawing: wait for drawn frames, not physics ticks.
            for (int i = 0; i < 4; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Check(!stage.Hud.OffscreenArrows.Contains("FarMate"), "on screen, the arrow goes away");
            stage.Players.Remove(mate);
            mate.QueueFree();
        }
        int spawnedAtRest = rules.SpawnedThisMatch;
        await Seconds(2);
        Check(rules.SpawnedThisMatch == spawnedAtRest && rules.ObjectiveText.Length > 0 && !rules.ObjectiveText.StartsWith("fmt_"), "no swarm comes during the rest: " + rules.ObjectiveText);
        if (Windowed)
        {
            // Let a mid-match swarm build up for the render review.
            stage.AdvanceClockForTest(90 - rules.MatchTime);
            await Seconds(12);
            await Shot("survivor-swarm");
        }
        stage.AdvanceClockForTest(r.BossAt - 1 - rules.MatchTime);
        Check(rules.GroupSize == r.GroupEnd || rules.GroupSize == r.GroupEnd - 1, "groups grow toward the boss");
        await Until(() => rules.BossArrived && rules.Boss != null, "the boss arrives on time", 4);
        var boss = rules.Boss!;
        Check(boss.Definition.ColliderRadius > CharacterCatalog.Enemy("bull").ColliderRadius * 1.5f && boss.Rig.Model.FindChild("boss_leftArm", true, false) != null,
            "the boss wears its own model (boss.glb, arms kept), scaled up");
        if (Windowed)
        {
            // Stand a few metres off, facing it, with the swarm cleared, so the close camera frames the boss model.
            foreach (var e in stage.AllEnemies.Where(e => e != boss).ToList()) e.QueueFree();
            boss.Stun(3);
            stage.Player.TeleportTo(boss.GlobalPosition + new Vector3(-4.5f, 0, 2.5f));
            stage.Player.SetControls(Vector2.Zero, boss.GlobalPosition + Vector3.Up, false);
            stage.Cam.Close = true;
            stage.Cam.SnapTo(stage.Player.GlobalPosition);
            await Seconds(1.2f);
            await Shot("survivor-boss-close");
            stage.Cam.Close = false;
        }
        Check(rules.ObjectiveText == Loc.Text("ui_objective_survivor_boss") && rules.BarLabel.Contains(Loc.Text("name_monster_boss")), "the objective and bar switch to the boss");

        // Slam: a red disc, then everyone inside is hit and launched.
        Check(boss.Phase == 0 && !boss.CurrentPhase.Skills.Contains("summon"), "phase 1 has no summon yet");
        await Until(() => !boss.Casting && boss.State == BullEnemy.Mode.Chase, "the boss is between skills", 6);
        foreach (var e in stage.AllEnemies.Where(e => e != boss).ToList()) e.QueueFree();
        boss.GlobalPosition = stage.Player.GlobalPosition + new Vector3(2.5f, 0, 0);
        stage.Player.Invulnerable = false;
        int before = stage.Player.Health, slams = boss.Uses.GetValueOrDefault(BossSkillKind.Slam);
        var slam = boss.Data.Skills.Values.First(k => k.Kind == BossSkillKind.Slam);
        var leapFrom = boss.GlobalPosition;
        var targetAt = stage.Player.GlobalPosition;
        Check(boss.StartSkill(BossSkillKind.Slam) && boss.Casting, "the slam winds up");
        await Frames(Mathf.CeilToInt(slam.Windup * .6f * Engine.PhysicsTicksPerSecond));
        await Shot("survivor-slam");
        await Until(() => boss.Leaping, "then it jumps", 2);
        float peak = 0;
        bool airShot = false;
        while (boss.Casting)
        {
            peak = Mathf.Max(peak, boss.LeapHeight);
            if (!airShot && boss.LeapHeight > slam.LeapHeight * .9f) { airShot = true; await Shot("survivor-slam-air"); }
            await Frames(1);
        }
        await Shot("survivor-slam-land");
        await Until(() => boss.Uses.GetValueOrDefault(BossSkillKind.Slam) == slams + 1, "the slam lands after its windup", 3);
        float landed = Flat(boss.GlobalPosition - targetAt).Length();
        Check(peak > slam.LeapHeight * .8f && landed < boss.Radius + stage.Player.Definition.ColliderRadius + .5f && Flat(boss.GlobalPosition - leapFrom).Length() > 1f,
            $"it leaps {peak:0.0} m high onto where the target stood and lands there ({landed:0.0} m off, bodies cannot overlap)");
        Check(stage.Player.Health < before, $"the slam hurts a player inside the disc ({before} → {stage.Player.Health})");
        stage.Player.Invulnerable = true;

        // Phase 2 below 66%: the summon joins.
        boss.Burn(boss.MaxHealth * .4f);
        await Until(() => boss.Phase == 1 && boss.CurrentPhase.Skills.Contains("summon"), "below 66% the second phase starts", 3);
        Check(stage.Notice == Loc.Text("msg_boss_phase2"), "the new phase is announced");
        await Until(() => boss.Roaring, "it stops to roar at the new phase", 2);
        await Shot("survivor-phase-roar");
        await Until(() => !boss.Casting, "the boss is free to summon", 4);
        var summon = boss.Data.Skills.Values.First(k => k.Kind == BossSkillKind.Summon);
        int alive = stage.AllEnemies.Count();
        int summons = boss.Uses.GetValueOrDefault(BossSkillKind.Summon);
        Check(boss.StartSkill(BossSkillKind.Summon) && boss.Casting && stage.AllEnemies.Count() == alive, "the summon starts with a roar, nobody comes yet");
        await Frames(Mathf.CeilToInt(summon.Windup * .5f * Engine.PhysicsTicksPerSecond));
        await Shot("survivor-summon-roar");
        await Until(() => boss.Uses.GetValueOrDefault(BossSkillKind.Summon) == summons + 1, "then the roar ends", 3);
        Check(stage.AllEnemies.Count() >= alive + summon.Count, "and a ring of commons bursts out");
        if (Windowed)
        {
            // The charge's pawing and gallop for the render review.
            await Until(() => !boss.Casting && boss.State == BullEnemy.Mode.Chase, "the boss is free to charge", 6);
            boss.StartWindup((stage.Player.GlobalPosition - boss.GlobalPosition).Normalized());
            await Seconds(.5f);
            await Shot("survivor-charge-paw");
            await Until(() => boss.State == BullEnemy.Mode.Charge, "it charges", 3);
            await Seconds(.25f);
            await Shot("survivor-charge-gallop");
        }

        // Phase 3 below 33%: faster, and the fan of shots.
        boss.Burn(boss.MaxHealth * .3f);
        await Until(() => boss.Phase == 2 && boss.Speed > boss.Monster.MoveSpeed * 1.2f, "below 33% the last phase speeds it up", 3);
        Check(rules.BarFraction < .34f, "the bar follows the boss's health");
        await Until(() => !boss.Casting && boss.State == BullEnemy.Mode.Chase, "the boss is free to fire", 6);
        var fan = boss.Data.Skills.Values.First(k => k.Kind == BossSkillKind.FanShot);
        int fans = boss.Uses.GetValueOrDefault(BossSkillKind.FanShot);
        Check(boss.StartSkill(BossSkillKind.FanShot) && boss.Casting, "the fan of shots winds up");
        await Frames(Mathf.CeilToInt(fan.Windup * .6f * Engine.PhysicsTicksPerSecond));
        await Shot("survivor-fan");
        await Until(() => boss.Uses.GetValueOrDefault(BossSkillKind.FanShot) == fans + 1, "the fan goes off after its windup", 3);
        Check(stage.GetChildren().OfType<EnemyProjectile>().Count() >= fan.Count, $"it fires {fan.Count} shots in a fan");
        await Seconds(1.5f);
        await Shot("survivor-boss");
        boss.Burn(boss.MaxHealth);
        await Until(() => stage.State == StageState.Cleared, "killing the boss clears the match", 2);
        Check(rules.ResultText == Loc.Text("msg_result_survivor_clear") && stage.SpecialKills >= 1, "the end screen names the boss kill");
        await Frames(10);
        await Shot("survivor-clear");

        // Falling ends it too.
        stage = await NewStage(GameMode.Survivor);
        stage.Player.Invulnerable = false;
        stage.Player.TakeDamage(999, Vector3.Right);
        await Frames(2);
        Check(stage.State == StageState.Dead && ((SurvivorRules)stage.ModeRules!).ResultText.Length > 0, "a fallen survivor ends the match");
    }
}
