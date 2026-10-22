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
        Check(tuning.Survivor.WeaponPool.Count(id => ArmoryCatalog.Weapon(id).Slot != WeaponSlot.Special) >= 6 &&
              tuning.Survivor.WeaponPool.Any(id => ArmoryCatalog.Weapon(id).Slot == WeaponSlot.Special) &&
              tuning.Survivor.WeaponPool.All(id => ArmoryCatalog.Weapon(id).Mode != FireMode.Melee),
            "the card pool has enough guns for two different slot 1 · 2 offers, special weapons for slot 3, no melee");
        Check(new[] { "honk", "rubber_duck" }.All(id => tuning.Survivor.WeaponPool.Contains(id) && ArmoryCatalog.Weapon(id).Slot == WeaponSlot.Special &&
              tuning.Survivor.SpecialCooldowns.TryGetValue(id, out float own) && own < tuning.Survivor.SpecialCooldown) &&
              ArmoryCatalog.Weapon("honk").Mode == FireMode.Shout && ArmoryCatalog.Throwables.ContainsKey(ArmoryCatalog.Weapon("rubber_duck").ThrowableId),
            "the honk and the rubber duck are slot-3 weapons with their own shorter recharge (the duck throws throwables.csv's rubber duck)");
        var gadgets = new Loadout(ArmoryCatalog.Weapon(ArmoryCatalog.StartingWeapon));
        gadgets.UseOnslaughtAmmo(tuning.Survivor.SpecialCooldown, tuning.Survivor.SpecialCooldowns);
        gadgets.Equip(3, new WeaponState(ArmoryCatalog.Weapon("honk")));
        Check(Mathf.IsEqualApprox(gadgets.Special!.ShotCooldown, tuning.Survivor.SpecialCooldowns["honk"]), "a honk picked up later gets its own recharge too");
        var badCooldowns = ModeTuning.FromText("{\"survivor\": {\"special_cooldowns\": {\"m4\": 5, \"honk\": -1}}}").Errors;
        Check(badCooldowns.Count == 2, "a gun that is not slot 3 and a negative recharge are reported: " + string.Join(" / ", badCooldowns));
        Check(tuning.Survivor.Segments.Count == 4 && tuning.Survivor.Total is > 1000 and < 1200 && tuning.Survivor.SegmentBossHp.Count == 4 &&
              tuning.Survivor.SegmentBossHp[^1] == 1, $"Onslaught runs four boss stretches, about 20 minutes ({tuning.Survivor.Total:0}s)");
        var badSegments = ModeTuning.FromText("{\"survivor\": {\"segments_s\": [60, -1], \"segment_boss_hp\": [1], \"xp_growth\": 0.5}}").Errors;
        Check(badSegments.Count == 3, "a negative stretch, a missing boss share and a shrinking xp curve are reported: " + string.Join(" / ", badSegments));
        var mids = tuning.Survivor.MidBosses;
        Check(mids.Distinct().Count() >= tuning.Survivor.Segments.Count - 1 && tuning.Survivor.FinalBosses.Contains(BossEnemy.MonsterId) &&
              mids.Concat(tuning.Survivor.FinalBosses).All(id => MonsterDatabase.Get(id).Tier == MonsterTier.Boss && BossDatabase.Current.Bosses.ContainsKey(id)),
            $"enough different middle bosses for the three middle stretches ({string.Join(", ", mids)}), the bull chief as the final one");
        Check(mids.All(id => BossDatabase.Get(id).Phases.Count == 3) && mids.Select(id => string.Join("+", BossDatabase.Get(id).Phases[0].Skills)).Distinct().Count() == mids.Count,
            "each middle boss has three phases and opens with its own skills");
        var badBosses = ModeTuning.FromText("{\"survivor\": {\"mid_bosses\": [\"bull\", \"ghost\"]}}").Errors;
        Check(badBosses.Count == 3, "a non-boss, an unknown id and an empty boss list are reported: " + string.Join(" / ", badBosses));

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
        // The Onslaught table runs the whole run clock: every stretch has its own, tougher commons and more named.
        var ends = tuning.Survivor.Segments.Select((_, i) => tuning.Survivor.Segments.Take(i + 1).Sum()).ToList();
        var starts = ends.Select((end, i) => i == 0 ? 0f : ends[i - 1]).ToList();
        var hp = starts.Select(t => onslaught.Rows.First(row => row.Monster == "@common" && row.ActiveAt(t + 1)).HpScale).ToList();
        Check(hp.Zip(hp.Skip(1)).All(p => p.Second > p.First) && hp[0] < 1 && hp[^1] > 1, "commons get tougher stretch by stretch: " + string.Join(" → ", hp));
        var named = starts.Select((t, i) => onslaught.NamedDue(t - .01f, ends[i] - .01f, "zombie").Sum(row => row.Count)).ToList();
        Check(named.All(n => n >= 2) && named.Zip(named.Skip(1)).All(p => p.Second >= p.First) && onslaught.NamedDue(ends[^2], ends[^1], "zombie").Any(row => row.From > ends[^1] - 120),
            "every stretch sends named ones, more each time, up to the last minutes: " + string.Join(", ", named));
        Check(Enumerable.Range(0, 300).Select(_ => onslaught.PickCommon(ends[^1] - 10, "zombie", rng)!.Monster).Contains("crawler"),
            "the last stretch still mixes the theme's fast ones in");
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

        // Arming: four card offers in the card window (slot 1 gun, slot 2 gun, slot 3 special, a companion); each pick fills its slot (M-27).
        var queue = rules.LocalQueue;
        await Until(() => queue.Count == SurvivorRules.ArmingPicks, "the four arming offers wait in the card window", 2);
        Check(queue.Offers.All(o => o.Kind == CardKind.Arming) && rules.Window.IsOpen && !queue.Front!.CanClose && !stage.Interactables.OfType<WeaponPickup>().Any(),
            "the card window opens on the slot 1 offer, cannot be closed, and nothing lies on the floor");
        Check(queue.Offers[0].Choices.Concat(queue.Offers[1].Choices).All(c => r.WeaponPool.Contains(c.Id) && ArmoryCatalog.Weapon(c.Id).Slot != WeaponSlot.Special) &&
              queue.Offers[2].Choices.All(c => ArmoryCatalog.Weapon(c.Id).Slot == WeaponSlot.Special),
            "slots 1 and 2 offer guns from the pool, slot 3 special weapons");
        Check(queue.Offers[3].Choices.Select(c => CompanionSpec.Parse(c.Id)?.Kind).Distinct().Count() == 3 && queue.Offers[3].Choices.All(c => c.Target == CardTarget.Summon),
            "the last offer is a companion: a pet, a drone and a sidekick");
        await Shot("survivor-arming");
        // Rerolls (M-29 plan 16.4): one card at a time, on arming cards too, never the other two or one thrown away.
        Check(r.Rerolls == 5 && rules.LocalRerollsLeft == 5 && rules.Window.RerollsLeft != null, "everyone has 5 rerolls a match (survivor.rerolls)");
        var armingKeys = queue.Front!.Choices.Select(c => c.Key).ToList();
        var slot2Ids = queue.Offers[1].Choices.Select(c => c.Id).ToList();
        rules.Window.Reroll(0);
        var rerolled = queue.Front!.Choices[0];
        Check(rerolled.Key != armingKeys[0] && !armingKeys.Contains(rerolled.Key) && !slot2Ids.Contains(rerolled.Id) &&
              queue.Front.Choices[1].Key == armingKeys[1] && queue.Front.Choices[2].Key == armingKeys[2] && rules.LocalRerollsLeft == 4,
            $"rerolling the first arming card draws another gun ({armingKeys[0]} → {rerolled.Key}, not in the slot 2 offer), the others stay, 4 left");
        await Shot("survivor-arming-reroll");
        string first = queue.Front!.Choices[0].Id;
        rules.Window.Pick(0);
        await Frames(2);
        Check(stage.Player.Loadout.Secondary.Def.Id == first && rules.Arming && queue.Count == 3 && rules.ObjectiveText.Contains("2/4"),
            $"the first card ({first}) becomes slot 1 and the slot 2 offer follows");
        await Seconds(2);
        Check(rules.SpawnedThisMatch == 0 && rules.MatchTime == 0, "nothing comes and the clock waits while arming");
        string second = queue.Front!.Choices[1].Id;
        rules.Window.Pick(1);
        string third = queue.Front!.Choices[2].Id;
        rules.Window.Pick(2);
        await Frames(2);
        Check(rules.Arming && queue.Front?.TitleKey == "ui_card_companion_title", "after slot 3 the companion offer follows; the match still waits");
        await Shot("survivor-companion-card");
        string buddy = queue.Front!.Choices[0].Id;
        rules.Window.Pick(0);
        await Frames(2);
        Check(stage.Player.Loadout.Primary?.Def.Id == second && stage.Player.Loadout.Special?.Def.Id == third && !rules.Arming &&
              stage.Objective == Objective.ModeGoal && !rules.Window.IsOpen, $"slot 2 is the {second}, slot 3 the {third}, and the match starts");
        Check(rules.Companions.Of(stage.Player).Single().Spec.Key == buddy, $"the companion ({buddy}) joins the survivor");
        Check(stage.Player.Loadout.Secondary.HasInfiniteReserve && stage.Player.Loadout.Primary!.HasInfiniteReserve, "both guns have unlimited reserves");
        Check(stage.Hud.FindChild("SpecialSlot", true, false) is Control { Visible: true }, "the slot 3 tile shows in Onslaught now");

        // Experience: every kill fills the team bar; a level-up opens a card.
        Check(rules.Level == 1 && rules.SubBarLabel == Loc.Format("fmt_hud_level", 1) && stage.Hud.FindChild("SubBar", true, false) is Control { Visible: true },
            "the experience bar starts at level 1 under the top bar");
        rules.AddXp(rules.XpNeeded);
        await Frames(2);
        Check(rules.Level == 2 && queue.Count == 1 && queue.Front!.Kind == CardKind.Normal && queue.Front.Choices.Count == 3 && rules.Window.IsOpen,
            "a full bar levels up and opens a level-up card");
        await Shot("survivor-levelup");
        Check(queue.Front.Grade is >= CardCatalog.NormalLow and <= CardCatalog.NormalHigh && queue.Front.Choices.All(c => c.Grade is >= 1 and <= 4),
            $"a level-up window has a grade 1–4 and so do its cards (grade {queue.Front.Grade}: {string.Join(", ", queue.Front.Choices.Select(c => $"{c.Key}={c.Grade}"))})");
        // Rerolls run out (four left after the arming one); a reroll with nothing left to draw changes nothing and costs nothing.
        for (int i = 0; i < 12 && rules.LocalRerollsLeft > 0; i++) rules.Window.Reroll(i % 3);
        var spent = queue.Front.Choices.Select(c => c.Key).ToList();
        int leftNow = rules.LocalRerollsLeft;
        rules.Window.Reroll(0);
        Check(queue.Front.Choices.Select(c => c.Key).SequenceEqual(spent) && rules.LocalRerollsLeft == leftNow && queue.Front.Discarded.Count >= 3,
            $"out of rerolls (or of cards to draw) the cards stay as they are ({leftNow} left, {queue.Front.Discarded.Count} thrown away)");
        var levelCard = queue.Front.Choices[0];
        var applied = levelCard.Inner ?? levelCard;
        rules.Window.Pick(0);
        Check(queue.Count == 0 && (applied.Key.StartsWith("c|") ? rules.Cards.IsOn(Enum.Parse<LabCard>(applied.Id)) : applied.Key[0] is 'r' or 'u'),
            $"picking it applies the card ({levelCard.Key})");

        // Grades (M-29 plan 16.2): repeatable 1, upgrade 2 (last level 4), effect card 3, "?" 4 / 7, specials 5–6.
        Check(CardCatalog.FromKey("r|damage", null)!.Grade == 1 && CardCatalog.FromKey("u|BalloonRadius|1", null)!.Grade == 2 &&
              CardCatalog.FromKey($"u|BalloonRadius|{LabCards.MaxLevel(CardUpgrade.BalloonRadius)}", null)!.Grade == 4 &&
              CardCatalog.FromKey("c|Velcro", null)!.Grade == 3 && CardCatalog.FromKey("s|HeliumParty", null)!.Grade == 5 &&
              CardCatalog.FromKey("s|GlassCannon", null)!.Grade == 6 && CardCatalog.FromKey("q|c|Velcro", null) is { Grade: 4, Inner.Key: "c|Velcro", Name: "?" } &&
              CardCatalog.FromKey("q|s|GlassCannon", null) is { Grade: 7, Inner.Key: "s|GlassCannon" },
            "every card key carries its grade, a \"?\" key its hidden card");
        var gradeRng = new RandomNumberGenerator { Seed = 11 };
        var fresh = stage.AddRemoteSurvivor(CharacterCatalog.Player("duck"), stage.Player.GlobalPosition + new Vector3(2, 0, 0), 995);
        var ones = CardCatalog.LevelUp(rules.Cards, gradeRng, fresh, new float[] { 1, 0, 0, 0 });
        var threes = CardCatalog.LevelUp(rules.Cards, gradeRng, fresh, new float[] { 0, 0, 1, 0 });
        var fours = CardCatalog.LevelUp(rules.Cards, gradeRng, fresh, new float[] { 0, 0, 0, 1 });
        var mystery = fours.Choices.FirstOrDefault(c => c.Inner != null);
        Check(ones.Choices.Count == 3 && ones.Choices.All(c => c.Grade == 1) && threes.Choices.Count == 3 && threes.Choices.All(c => c.Grade == 3),
            "a window draws one grade and all three cards share it when there are enough");
        Check(fours.Grade == 4 && mystery != null && mystery.Inner!.Grade is 3 or 4 && fours.Choices.Count == 3 &&
              fours.Choices.Where(c => c != mystery).All(c => c.Key != mystery.Inner.Key && c.Grade is 3 or 4),
            "a grade 4 window with no grade 4 card yet: a \"?\" hiding a 3–4 and the nearest grade fills the rest, the hidden card not shown twice");
        var specials = CardCatalog.BossReward(rules.Cards, gradeRng, new float[] { 0, 0, 1 });
        // Grade 7 now has real cards (mad scientist; honk beam and static fluff with a teammate) besides the special "?".
        Check(specials.Grade == 7 && specials.Choices.Any(c => c.Grade == 7) && specials.Choices.All(c => c.Inner == null || c.Inner.Grade >= 6) &&
              specials.Choices.All(c => c.Grade >= 5 || c.Key.StartsWith("r|")) && !specials.CanReroll,
            $"a special window has grades 5–7 (a special \"?\" hides a 6–7) and cannot be rerolled ({string.Join(", ", specials.Choices.Select(c => $"{c.Key}={c.Grade}"))})");
        stage.Players.Remove(fresh);
        fresh.QueueFree();
        // A "?" card picked: the slot machine shows what it was, and that card applies.
        queue.Add(new CardOffer { Kind = CardKind.Normal, TitleKey = "ui_card_levelup_title", Grade = 4,
            Choices = new[] { CardCatalog.Mystery(CardCatalog.Lab(LabCard.StuffingBurst, rules.Cards)), CardCatalog.Repeat("heal"), CardCatalog.Repeat("medkit") } });
        await Frames(2);
        rules.Window.Pick(0);
        var reveal = rules.Window.GetNode<MysteryReveal>("CardRoot/MysteryReveal");
        Check(reveal.Playing && rules.Cards.Owns(stage.Player, LabCard.StuffingBurst), "picking a \"?\" card starts its reveal and applies the card it hid");
        await Seconds(MysteryReveal.SpinSeconds + .2f);
        await Shot("survivor-mystery-reveal");
        Check(reveal.Shown == CardCatalog.Lab(LabCard.StuffingBurst, null).Name, $"the reveal stops on that card ({reveal.Shown})");

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
        int levelBefore = rules.Level;
        float xpBefore = rules.Xp;
        foreach (var e in stage.AllEnemies.Where(e => e.IsSpecial).ToList()) e.Burn(99999);
        await Until(() => rules.RestLeft > 0, "once the named are down the rest starts");
        Check(rules.Level > levelBefore || rules.Xp >= xpBefore + r.XpNamed - .01f, "a named kill gives the team its experience");
        Check(!stage.Interactables.OfType<WeaponPickup>().Any(), "and drops nothing on the floor (no racks in Onslaught)");

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
        Check(r.MidBosses.Contains(rules.NextBoss), $"the first stretch drew a middle boss ({rules.NextBoss})");
        // The bull chief has the full skill set the checks below go through.
        rules.NextBoss = BossEnemy.MonsterId;
        stage.AdvanceClockForTest(rules.SegmentLength - 1 - rules.SegmentTime);
        Check(rules.GroupSize > r.GroupStart && rules.Segment == 0, "groups grow through the first stretch");
        await Until(() => rules.BossArrived && rules.Boss != null, "the first stretch's boss arrives on time", 4);
        var boss = rules.Boss!;
        float runAtBoss = rules.RunTime;
        await Seconds(1);
        Check(Mathf.IsEqualApprox(rules.RunTime, runAtBoss) && runAtBoss < r.Segments[0] && runAtBoss > r.Segments[0] - .1f,
            $"the table's run clock stands still at the stretch's end while its boss is out ({runAtBoss:0.00}s)");
        Check(!rules.FinalSegment && Mathf.IsEqualApprox(boss.MaxHealth, boss.Monster.Hp * r.SegmentBossHp[0]) && Mathf.IsEqualApprox(boss.Health, boss.MaxHealth),
            $"it is a middle boss with {r.SegmentBossHp[0]:P0} health");
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
        Check(rules.ObjectiveText == Loc.Text("ui_objective_survivor_midboss") && rules.BarLabel.Contains(Loc.Text("name_monster_boss")), "the objective and bar switch to the boss"); 

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
        // The vote's rule (plan 16.5 table): settled early only when the leader cannot be caught.
        Check(SpecialVote.Settle(new[] { 3, 1, 1 }, 1, false) is [0] && SpecialVote.Settle(new[] { 3, 2, 0 }, 1, false) == null &&
              SpecialVote.Settle(new[] { 4, 0, 0 }, 2, false) is [0] && SpecialVote.Settle(new[] { 0, 3, 0 }, 1, false) is [1] &&
              SpecialVote.Settle(new[] { 3, 0, 0 }, 2, false) is [0] && SpecialVote.Settle(new[] { 2, 2, 2 }, 0, false)!.Count == 3,
            "the vote settles early only when the leader cannot be caught (3·1·1 +1, 4·0 +2, 3·0 +1, 3·0 +2 settle; 3·2 +1 waits; 2·2·2 draws)");
        Check(SpecialVote.Settle(new[] { 0, 0, 0 }, 4, true)!.Count == 3 && SpecialVote.Settle(new[] { 1, 1, 0 }, 3, true)!.SequenceEqual(new[] { 0, 1 }),
            "at time up: no stamps draws from all three, otherwise from the most stamped");
        Check(r.SpecialVote == 60, "the team has 60 seconds to vote (survivor.special_vote_s)");

        // The middle boss down: everyone stops and votes on the special card (M-29), then its experience; the next stretch starts.
        int offersBefore = queue.Count;
        var bystander = stage.AllEnemies.FirstOrDefault(e => e != boss && !e.IsDead);
        boss.Burn(boss.MaxHealth);
        await Frames(2);
        var vote = rules.Vote;
        Check(stage.State == StageState.Playing && rules.Segment == 1 && !rules.BossArrived && vote.Phase == VotePhase.Voting &&
              stage.AllStopped && GetTree().Paused && vote.Offer!.Kind == CardKind.Special && vote.Offer.Choices.Count == 3 &&
              !queue.Offers.Skip(offersBefore).Any(o => o.Kind == CardKind.Special),
            "a middle boss down stops everyone and opens the special card vote (not a card in each queue) and stretch 2");
        await Frames(1);
        await Until(() => vote.Voters == 1 && vote.IsVoter && rules.VoteWindow.GetNode<Control>("VoteRoot").Visible,
            "the only survivor votes; the vote window is up", 2);
        float stretchClock = rules.SegmentTime;
        var bystanderAt = bystander != null && IsInstanceValid(bystander) ? bystander.GlobalPosition : Vector3.Zero;
        await Seconds(1);
        Check(Mathf.IsEqualApprox(rules.SegmentTime, stretchClock) && (bystander == null || !IsInstanceValid(bystander) || bystander.GlobalPosition == bystanderAt) &&
              vote.Left < r.SpecialVote - .5f, "while voting the stretch clock and the monsters hold, the vote's own clock runs");
        await Shot("survivor-vote");
        var won = vote.Offer!.Choices[1];
        vote.StampLocal(1);
        vote.StampLocal(0);
        Check(vote.Stamps[1] == 1 && vote.Stamps[0] == 0 && vote.Winner == 1 && vote.Phase == VotePhase.Spinning,
            "alone, a stamp settles it at once, and a second stamp is ignored");
        await Until(() => vote.Phase == VotePhase.Countdown, "the winner is shown, then counted down", 4);
        Check(rules.Cards.IsOn(Enum.Parse<LabSpecial>(won.Id)) && stage.AllStopped, "the winning card goes on before play resumes");
        await Shot("survivor-vote-countdown");
        await Until(() => !vote.Active && !stage.AllStopped && !GetTree().Paused, "after 3 · 2 · 1 everyone plays on", 5);
        Check(stage.State == StageState.Playing && rules.Segment == 1, "the second stretch runs");
        Check(rules.BarLabel.Contains($"2/{rules.Segments}") && rules.BossIn > rules.SegmentLength - 1, "the bar counts down to the second boss: " + rules.BarLabel);
        Check(Mathf.Abs(rules.RunTime - r.Segments[0]) < .5f, $"the run clock picks up where the first stretch ended ({rules.RunTime:0.0}s)");
        // The other stretches, each with its own middle boss; the last boss is the final one and ends it.
        var met = new List<string>();
        rules.NextBoss = "boss_balloon_king";
        while (!rules.FinalSegment)
        {
            stage.AdvanceClockForTest(rules.SegmentLength - rules.SegmentTime + .1f);
            await Until(() => rules.BossArrived && rules.Boss != null, $"boss {rules.Segment + 1} arrives", 4);
            boss = rules.Boss!;
            met.Add(boss.Monster.Id);
            Check(boss.Definition.ColliderRadius > CharacterCatalog.Enemy(boss.Monster.LookId).ColliderRadius * 1.4f && rules.BarLabel.Contains(boss.Monster.DisplayName) &&
                  stage.Notice == Loc.Format("fmt_survivor_midboss", boss.Monster.DisplayName),
                $"stretch {rules.Segment + 1}: {boss.Monster.DisplayName}, its look scaled up, named on the bar and in the notice");
            if (boss.Monster.Id == "boss_balloon_king")
            {
                // Its summon calls balloons, not the theme's commons.
                await Until(() => !boss.Casting && boss.State == BullEnemy.Mode.Chase, "the balloon king is between skills", 6);
                var call = boss.Data.Skills.Values.First(k => k.Kind == BossSkillKind.Summon);
                int balloons = stage.AllEnemies.Count(e => e.Monster.Id == "balloon");
                int calls = boss.Uses.GetValueOrDefault(BossSkillKind.Summon);
                Check(call.Monster == "balloon" && boss.StartSkill(BossSkillKind.Summon), "the balloon king calls its balloons");
                await Until(() => boss.Uses.GetValueOrDefault(BossSkillKind.Summon) == calls + 1, "the call ends", 3);
                Check(stage.AllEnemies.Count(e => e.Monster.Id == "balloon") >= balloons + call.Count, $"{call.Count} balloons come out");
            }
            if (Windowed)
            {
                // The boss's special cards wait behind the X, so the boss itself is in view.
                rules.Window.Close();
                foreach (var e in stage.AllEnemies.Where(e => e != boss).ToList()) e.QueueFree();
                boss.Stun(3);
                stage.Player.TeleportTo(boss.GlobalPosition + new Vector3(-5.5f, 0, 3f));
                stage.Player.SetControls(Vector2.Zero, boss.GlobalPosition + Vector3.Up, false);
                stage.Cam.Close = true;
                stage.Cam.SnapTo(stage.Player.GlobalPosition);
                await Seconds(1.5f);
                await Shot("survivor-" + boss.Monster.Id);
                stage.Cam.Close = false;
            }
            boss.Burn(boss.MaxHealth);
            await Frames(2);
            Check(rules.Vote.Phase == VotePhase.Voting, $"boss {rules.Segment} down: the vote");
            rules.Vote.StampLocal(0);
            await Until(() => !rules.Vote.Active, "voted, play goes on", 8);
        }
        Check(met.Count == r.Segments.Count - 2 && met.Distinct().Count() == met.Count && met.All(r.MidBosses.Contains),
            "no middle boss comes twice: " + string.Join(", ", met));
        stage.AdvanceClockForTest(rules.SegmentLength - rules.SegmentTime + .1f);
        await Until(() => rules.BossArrived && rules.Boss != null, "the final boss arrives after the last stretch", 4);
        boss = rules.Boss!;
        Check(Mathf.IsEqualApprox(boss.MaxHealth, boss.Monster.Hp) && r.FinalBosses.Contains(boss.Monster.Id) &&
              rules.BarLabel.Contains(Loc.Format("fmt_boss_final", boss.Monster.DisplayName)) &&
              rules.ObjectiveText == Loc.Text("ui_objective_survivor_boss"), "the final boss has full health and its own name on the bar");
        boss.Burn(boss.MaxHealth);
        await Until(() => stage.State == StageState.Cleared, "killing the final boss clears the match", 2);
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
