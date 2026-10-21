using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Card lab prototype (decisions.md M-27 `[임시]`, doc/onslaught-card-ideas.md 5장): the lab rules (no arming, no
/// boss, monsters on request), each of the six lab cards on its own, and the velcro + balloon chain (a clump floats and
/// pops together). With a real window it also plays a short fight with every card on and saves output/cardlab-*.png.
/// </summary>
public partial class CardLabCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private CardLabRules _rules = null!;
    private LabCards Cards => _rules.Cards;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task Until(Func<bool> done, string what, float seconds = 5)
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
        try { await Run(); GD.Print("CARD_LAB_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("CARD_LAB_FAIL " + e.Message); failed = true; }
        Engine.TimeScale = 1;
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Shot(string name)
    {
        if (!Windowed) return;
        for (int i = 0; i < 3; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/cardlab-{name}.png"));
        GD.Print($"SHOT: output/cardlab-{name}.png");
    }

    /// <summary>A spot about ten metres from the survivor with open ground two metres around it.</summary>
    private Vector3 OpenSpot(float distance)
    {
        var center = _stage.Player.GlobalPosition;
        foreach (var at in _stage.Flow.OpenCellsNear(center, distance, distance + 6))
        {
            bool clear = true;
            for (float x = -2; x <= 2 && clear; x += 1)
                for (float z = -2; z <= 2 && clear; z += 1)
                    clear = !_stage.Flow.IsBlocked(at + new Vector3(x, 0, z));
            if (clear) return new Vector3(at.X, 0, at.Z);
        }
        throw new Exception("no open spot on the field");
    }

    private async Task<Enemy> Common(Vector3 at)
    {
        var e = _stage.SpawnCommon(at, awake: false);
        await Frames(2);
        return e;
    }

    private async Task Idle(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    private async Task Click(Vector2 at)
    {
        var viewport = GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        await Idle(2);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at });
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
        await Idle(2);
    }

    private void Key(Key key)
    {
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
    }

    private static void Tap(Enemy e, float damage = 1) =>
        e.TakeHit(new HitInfo(damage, Vector3.Right, e.GlobalPosition + Vector3.Up));

    private async Task Reset()
    {
        foreach (var c in LabCards.All) Cards.Set(c, false);
        foreach (var s in LabCards.Specials) Cards.Set(s, false);
        _rules.ClearEnemies();
        await Frames(3);
    }

    private async Task Run()
    {
        // ---------- the lab scene ----------
        var lab = new CardLab { Name = "CardLab" };
        AddChild(lab);
        await Frames(3);
        _stage = lab.Stage;
        _rules = lab.Rules;
        var player = _stage.Player;
        player.ManualControl = true;
        Check(_stage.ModeRules == _rules && _stage.Map is OpenFieldMap && _stage.Objective == Objective.ModeGoal,
            "the lab runs its own rules on Onslaught's open field, without arming");
        Check(player.Invulnerable && player.Loadout.Secondary.Def.Id == "m4" && player.Loadout.Primary?.Def.Id == "r870",
            "the survivor cannot be hurt and starts with an M4 and a shotgun");
        Key(Godot.Key.F1);
        Check(Cards.IsOn(LabCard.Balloon) && LabCards.All.Count(Cards.IsOn) == 1, "F1 switches the first card on");
        Key(Godot.Key.Key0);
        Check(LabCards.All.All(Cards.IsOn), "0 switches every card on");
        Key(Godot.Key.Key0);
        Check(!LabCards.All.Any(Cards.IsOn), "and 0 again switches them all off");
        Key(Godot.Key.F9);
        Check(_rules.Trickle, "F9 starts the trickle");
        Key(Godot.Key.F9);
        Key(Godot.Key.F12);
        Check(player.Loadout.Primary?.Def.Id != "r870", "F12 changes the slot-2 gun");
        player.Loadout.SetSlot(2, ArmoryCatalog.Weapon("r870"));
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(lab.FindChild("CardLabPanel", true, false) is Control { Visible: true } panel &&
              panel.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text.Contains(Loc.Text("name_card_balloon"))),
            "the lab panel lists the cards by name");
        await Frames(30);
        Check(!_stage.AllEnemies.Any() && !_stage.Interactables.OfType<WeaponPickup>().Any(), "no monsters and no racks until asked");
        Check(_rules.SpawnRing(8, 7, 11) == 8, "a ring of commons comes on request");
        await Frames(2);
        Check(player.Gun.HasInfiniteReserve, "reserves never run out (Onslaught ammo)");
        foreach (var c in LabCards.All) Check(!Loc.Text(CardLab.CardKey("name_card_", c)).StartsWith("name_") && !Loc.Text(CardLab.CardKey("desc_card_", c)).StartsWith("desc_"),
            $"card {c} has a name and a description in the language table");
        await Reset();

        // ---------- balloon rounds ----------
        Cards.Set(LabCard.Balloon, true);
        var spot = OpenSpot(10);
        var balloon = await Common(spot);
        var neighbour = await Common(spot + new Vector3(1.4f, 0, 0));
        int taps = 0;
        while (!balloon.Floating && taps++ < 15) Tap(balloon);
        Check(balloon.Floating && Cards.Floats == 1 && taps <= 9, $"hits inflate a monster until it floats ({taps} hits)");
        await Frames(30);
        Check(balloon.GlobalPosition.Y > .3f && balloon.Rig.Scale.X > 1.3f, $"it rises and stays puffed up (height {balloon.GlobalPosition.Y:0.00} m)");
        Tap(balloon);
        Check(balloon.IsDead && Cards.Pops == 1 && neighbour.KnockSpeed > 3, $"shooting it pops it and throws its neighbour ({neighbour.KnockSpeed:0.0} m/s)");
        await Reset();

        // ---------- velcro ----------
        Cards.Set(LabCard.Velcro, true);
        spot = OpenSpot(10);
        var sticky = new[] { await Common(spot), await Common(spot + new Vector3(1, 0, 0)), await Common(spot + new Vector3(0, 0, 1)) };
        foreach (var e in sticky) Tap(e);
        Check(sticky.All(e => e.Sticky), "hit monsters turn sticky");
        await Until(() => Cards.ClumpSize(sticky[0]) == 3 && Cards.LargestClump == 3, "three sticky monsters close together form one clump");
        foreach (var e in new[] { await Common(spot + new Vector3(-1, 0, 0)), await Common(spot + new Vector3(0, 0, -1)) }) Tap(e);
        await Until(() => Cards.ClumpBursts >= 1, "a fifth one makes the clump burst");
        Check(sticky.All(e => !IsInstanceValid(e) || !e.Sticky), "the burst clears the stickiness");
        await Reset();

        // ---------- stuffing burst ----------
        Cards.Set(LabCard.StuffingBurst, true);
        spot = OpenSpot(10);
        var victim = await Common(spot);
        neighbour = await Common(spot + new Vector3(1.2f, 0, 0));
        Tap(victim, 100);
        Check(victim.IsDead && Cards.StuffingBursts == 1 && neighbour.KnockSpeed > 2, "a kill bursts into stuffing and pushes the neighbour away");
        await Reset();

        // ---------- billiards ----------
        spot = OpenSpot(10);
        var cue = await Common(spot);
        var ball = await Common(spot + new Vector3(1.3f, 0, 0));
        cue.Shove(Vector3.Right, 150, 0);
        await Frames(20);
        Check(Cards.BilliardHits == 0, "without the card a thrown monster only bumps the other");
        await Reset();
        Cards.Set(LabCard.Billiards, true);
        cue = await Common(spot);
        ball = await Common(spot + new Vector3(1.3f, 0, 0));
        var ballStart = ball.GlobalPosition;
        cue.Shove(Vector3.Right, 150, 0);
        await Until(() => Cards.BilliardHits >= 1, "with billiards a thrown monster passes the hit on", 1);
        await Frames(15);
        Check(ball.GlobalPosition.X - ballStart.X > 1f, $"the one it hit flies on ({ball.GlobalPosition.X - ballStart.X:0.0} m)");
        await Reset();

        // ---------- honk reload ----------
        Cards.Set(LabCard.HonkReload, true);
        var close = await Common(player.GlobalPosition + new Vector3(2, 0, 0));
        var gun = player.Gun;
        gun.SetAmmo(0, gun.Reserve);
        Check(gun.StartReload(), "the rifle starts reloading");
        await Frames(2);
        Check(Cards.Honks == 1 && close.KnockSpeed > 2, "reloading honks and pushes the monster next to the survivor");
        await Frames(Mathf.CeilToInt(gun.Def.ReloadTime * Engine.PhysicsTicksPerSecond) + 5);
        Check(Cards.Honks == 1, "one honk per reload");
        await Reset();

        // ---------- rubber duck ----------
        Cards.Set(LabCard.RubberDuck, true);
        spot = OpenSpot(10);
        var target = await Common(spot);
        for (int i = 0; i < 10; i++) Tap(target);
        Check(Cards.Ducks == 1, "the tenth hit drops a rubber duck");
        await Until(() => _stage.Lure != null, "it lands and starts squeaking (a lure)", 3);
        await Until(() => _stage.Lure == null, "then it pops", 6);
        await Reset();

        // ---------- velcro + balloon: the clump floats and pops together ----------
        Cards.Set(LabCard.Velcro, true);
        Cards.Set(LabCard.Balloon, true);
        spot = OpenSpot(4);
        var clump = new[] { await Common(spot), await Common(spot + new Vector3(1, 0, 0)), await Common(spot + new Vector3(0, 0, 1)) };
        foreach (var e in clump) Tap(e);
        await Until(() => Cards.ClumpSize(clump[0]) == 3, "the three stick together");
        int floats = Cards.Floats;
        taps = 0;
        while (!clump[0].Floating && taps++ < 15) Tap(clump[0]);
        Check(clump.All(e => e.Floating) && Cards.Floats - floats == 3, "when one floats the whole clump floats");
        await Frames(20);
        await Shot("clump-float");
        int pops = Cards.Pops;
        Tap(clump[1]);
        Check(clump.All(e => !IsInstanceValid(e) || e.IsDead) && Cards.Pops - pops == 3, "popping one pops the whole floating clump");
        await Reset();

        // ---------- cheat reset: the arming cards (M-27 plan 13.1) ----------
        var window = lab.Window;
        var hud = _stage.Hud;
        Key(Godot.Key.Home);
        await Idle(3);
        var queue = _rules.Queue;
        Check(queue.Count == 3 && queue.Front!.Kind == CardKind.Arming && window.IsOpen && hud.CardWindowOpen,
            "Home resets and opens the slot 1 · 2 · 3 arming cards, one offer after another");
        Check(player.Loadout.Secondary.Def.Id == ArmoryCatalog.StartingWeapon && player.Loadout.Primary == null && player.Loadout.Special == null && !LabCards.All.Any(Cards.IsOn),
            "the reset leaves only the frying pan and no cards");
        var offers = queue.Offers.ToList();
        Check(offers[0].Choices.Count == 3 && offers[1].Choices.Count == 3 && offers[2].Choices.Count == 3, "each offer has three cards");
        Check(offers[0].Choices.Concat(offers[1].Choices).All(c => ArmoryCatalog.Weapon(c.Id).Slot != WeaponSlot.Special) &&
              offers[2].Choices.All(c => ArmoryCatalog.Weapon(c.Id).Slot == WeaponSlot.Special),
            "slots 1 and 2 offer guns, slot 3 offers special weapons");
        Check(!offers[0].Choices.Select(c => c.Id).Intersect(offers[1].Choices.Select(c => c.Id)).Any(), "slot 2 never offers a gun slot 1 offered");
        Check(window.FindChild("Card1", true, false) is Control { Visible: true } && window.FindChild("CardClose", true, false) is Control { Visible: false },
            "the window shows the cards and no close button for arming");
        window.Close();
        Key(Godot.Key.F);
        Check(window.IsOpen, "arming cards cannot be put off");
        await Shot("arming-1");
        string first = offers[0].Choices[0].Id;
        Key(Godot.Key.Key1);
        await Idle(2);
        Check(player.Loadout.Secondary.Def.Id == first && queue.Count == 2 && window.IsOpen, $"1 picks the first card: slot 1 is the {first}, the slot 2 offer follows");
        window.Pick(1);
        Check(player.Loadout.Primary?.Def.Id == offers[1].Choices[1].Id, "the second offer fills slot 2");
        Key(Godot.Key.Key3);
        await Idle(2);
        Check(player.Loadout.Special?.Def.Id == offers[2].Choices[2].Id && queue.Count == 0 && !window.IsOpen && !hud.CardWindowOpen,
            "the third fills slot 3 and the window closes");
        Check(hud.FindChild("SpecialSlot", true, false) is Control { Visible: true }, "the slot 3 tile shows the special weapon");

        // ---------- level-up cards: put off, counted, same cards again ----------
        Key(Godot.Key.L);
        await Idle(2);
        Check(queue.Count == 1 && queue.Front!.Kind == CardKind.Normal && window.IsOpen, "a level-up opens the window by itself");
        var levelOne = queue.Front!.Choices.Select(c => c.Id).ToList();
        Check(window.FindChild("CardClose", true, false) is Control { Visible: true }, "level-up cards have a close button");
        Key(Godot.Key.F);
        await Idle(2);
        Check(!window.IsOpen && !hud.CardWindowOpen && window.FindChild("CardPlus", true, false) is Control { Visible: true }, "F puts it off onto the + badge");
        Key(Godot.Key.L);
        await Idle(2);
        Check(queue.Count == 2 && !window.IsOpen, "a second level-up while one waits only grows the badge");
        Check(window.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text == "×2"), "the badge counts ×2");
        await Shot("plus-badge");
        Key(Godot.Key.F);
        await Idle(2);
        Check(window.IsOpen && queue.Front!.Choices.Select(c => c.Id).SequenceEqual(levelOne), "F opens the first one again with the same three cards");
        window.Close();
        window.Open();
        Check(queue.Front!.Choices.Select(c => c.Id).SequenceEqual(levelOne), "closing and opening never changes them");
        await Shot("levelup");
        var chosen = queue.Front!.Choices[0];
        window.Pick(0);
        Check(Cards.IsOn(Enum.Parse<LabCard>(chosen.Id)) && queue.Count == 1 && window.IsOpen, $"picking {chosen.Name} turns that card on and shows the next offer");
        window.Pick(0);
        Check(queue.Count == 0 && !window.IsOpen && queue.Picked >= 5, "the last pick closes the window");
        if (Windowed)
        {
            // A real click on a card.
            Key(Godot.Key.L);
            await Idle(3);
            int before = LabCards.All.Count(Cards.IsOn);
            await Click(((Control)window.FindChild("Card2", true, false)!).GetGlobalRect().GetCenter());
            Check(queue.Count == 0 && LabCards.All.Count(Cards.IsOn) == before + 1, "clicking a card picks it");
        }
        await Reset();

        // ---------- special cards (boss-reward cheat) ----------
        Key(Godot.Key.L);
        Key(Godot.Key.K);
        await Idle(2);
        Check(queue.Count == 2 && queue.Offers[0].Kind == CardKind.Normal && queue.Offers[1].Kind == CardKind.Special,
            "L gives a normal card, K a special one, and they wait in the order they came");
        window.Pick(0);
        Check(queue.Front!.Kind == CardKind.Special && queue.Front.Choices.Count == 3 && window.IsOpen, "the special offer follows with three special cards");
        await Shot("special");
        int glass = queue.Front.Choices.ToList().FindIndex(c => c.Id == nameof(LabSpecial.GlassCannon));
        window.Pick(glass);
        Check(Cards.IsOn(LabSpecial.GlassCannon) && Mathf.IsEqualApprox(player.DamageMultiplier, 2), "glass cannon doubles the survivor's damage");
        await Reset();
        Cards.Set(LabSpecial.LastShot, true);
        spot = OpenSpot(10);
        var lastTarget = await Common(spot);
        var lastNeighbour = await Common(spot + new Vector3(1.4f, 0, 0));
        player.Loadout.SelectSlot(2);
        player.Gun.SetAmmo(0, player.Gun.Reserve);
        Tap(lastTarget);
        Check(Cards.LastShots == 1 && lastNeighbour.KnockSpeed > 3, "with an empty magazine the hit explodes (last shot)");
        await Reset();
        Cards.Set(LabSpecial.HeliumParty, true);
        spot = OpenSpot(10);
        var floaty = await Common(spot);
        var bull = _stage.Spawn(MonsterDatabase.Get("bull"), spot + new Vector3(3, 0, 0), awake: false);
        await Frames(2);
        for (int i = 0; i < 80 && !floaty.Floating; i++) Tap(floaty, .01f);
        for (int i = 0; i < 80; i++) Tap(bull, .01f);
        Check(floaty.Floating && !bull.Floating, "helium party floats commons now and then, never a named one");
        Cards.Set(LabSpecial.HeliumParty, false);
        Cards.Set(LabSpecial.GlassCannon, false);
        await Reset();

        // ---------- the training log ----------
        var logAt = OpenSpot(5);
        player.SetControls(Vector2.Zero, logAt + Vector3.Up, false);
        await Frames(2);
        Key(Godot.Key.T);
        await Frames(3);
        var log = _stage.Enemies.OfType<TrainingDummy>().SingleOrDefault();
        Check(log != null && log.GlobalPosition.DistanceTo(new Vector3(player.AimPoint.X, 0, player.AimPoint.Z)) < 1.5f, "T puts a training log at the cursor");
        var logHome = log!.GlobalPosition;
        for (int i = 0; i < 3; i++) Tap(log, 100);
        log.Shove(Vector3.Right, 200, 9999);
        await Frames(20);
        Check(!log.IsDead && IsInstanceValid(log) && log.TotalDamage >= 300 + 9999 && log.Dps > 0, $"it takes any damage without dying (DPS {log.Dps:0})");
        Check(log.GlobalPosition.DistanceTo(logHome) < .05f, "and does not move off its spot");
        Cards.Set(LabCard.Balloon, true);
        for (int i = 0; i < 12; i++) Tap(log);
        Check(!log.IsDead && IsInstanceValid(log), "a balloon pop cannot kill it either");
        Key(Godot.Key.Backspace);
        await Frames(3);
        Check(IsInstanceValid(log) && _stage.Enemies.Contains(log), "clearing the monsters leaves the log");
        if (Windowed)
        {
            // A few real shots at it for the render: damage numbers and DPS.
            player.Loadout.SelectSlot(2);
            for (int t = 0; t < 60; t++)
            {
                player.SetControls(Vector2.Zero, log.GlobalPosition + Vector3.Up, t % 8 == 0);
                await Frames(1);
            }
            await Shot("training-log");
        }
        _rules.RemoveDummies();
        await Frames(3);
        Check(!_stage.AllEnemies.OfType<TrainingDummy>().Any(), "Shift+T clears the logs");
        await Reset();

        // ---------- every card on, a real fight (window only) ----------
        if (!Windowed) return;
        foreach (var c in LabCards.All) Cards.Set(c, true);
        _rules.SpawnRing(18, 6, 10);
        await Frames(10);
        for (int t = 0; t < 240; t++)
        {
            var aim = _stage.Enemies.Where(e => IsInstanceValid(e) && !e.IsDead).OrderBy(e => e.GlobalPosition.DistanceSquaredTo(player.GlobalPosition)).FirstOrDefault();
            // Press and release so the pump shotgun fires too.
            player.SetControls(Vector2.Zero, aim?.GlobalPosition ?? player.GlobalPosition + Vector3.Forward * 5, aim != null && t % 2 == 0);
            if (t == 60) player.Loadout.SelectSlot(2);
            if (t == 110 || t == 200) await Shot($"chaos-{t}");
            await Frames(1);
        }
        GD.Print($"CHAOS: floats {Cards.Floats}, pops {Cards.Pops}, clump {Cards.LargestClump}, bursts {Cards.ClumpBursts}, stuffing {Cards.StuffingBursts}, billiards {Cards.BilliardHits}, honks {Cards.Honks}, ducks {Cards.Ducks}");
    }
}
