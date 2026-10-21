using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Outpost inventory (decisions.md M-25): calibers, the 4 × 5 backpack (stacking, taking, moving), floor weapons into
/// slots or the backpack, equipping and dropping by drag, reloads that spend backpack rounds, ammo piles, medkits that
/// refill Q by themselves, loot spilled by a named monster, and the inventory screen. With a real window it also saves
/// output/inventory-open.png.
/// </summary>
public partial class InventoryCheck : Node
{
    private int _checks;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task Until(Func<bool> done, string what, float seconds = 10)
    {
        for (int i = 0; i < seconds * Engine.PhysicsTicksPerSecond; i++)
        {
            if (done()) { Check(true, what); return; }
            await Frames(1);
        }
        Check(done(), what + " (timed out)");
    }

    /// <summary>Press, move in steps past the drag threshold, release: Godot's own drag and drop runs.</summary>
    private async Task MouseDrag(Vector2 from, Vector2 to)
    {
        var viewport = GetViewport();
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = from, GlobalPosition = from, ButtonMask = MouseButtonMask.Left });
        await Frames(1);
        for (int i = 1; i <= 8; i++)
        {
            var at = from.Lerp(to, i / 8f);
            viewport.PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at, Relative = (to - from) / 8, ButtonMask = MouseButtonMask.Left });
            await Frames(1);
        }
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = to, GlobalPosition = to });
        await Frames(3);
    }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("INVENTORY_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("INVENTORY_CHECK_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        // ---------- data ----------
        var calibers = CaliberDatabase.Current;
        Check(calibers.Errors.Count == 0 && calibers.Order.Count >= 8, "calibers.csv loads without errors" + string.Join("; ", calibers.Errors));
        var guns = ArmoryCatalog.Weapons.Values.Where(w => w.Mode != FireMode.Melee && !w.InfiniteReserve).ToList();
        Check(guns.All(w => CaliberDatabase.Get(w.Caliber) != null), "every gun that runs out of rounds names a known caliber: " +
            string.Join(", ", guns.Where(w => CaliberDatabase.Get(w.Caliber) == null).Select(w => w.Id)));
        Check(ArmoryCatalog.Weapon("ak47").Caliber == "762" && ArmoryCatalog.Weapon("m4").Caliber == "556", "a weapon row can override its type's caliber");
        var bad = CaliberDatabase.FromText("id,name,stack,color\n9mm,name_caliber_9mm,0,\n9mm,name_caliber_9mm,10,\nxx,name_missing,5,\n").Errors;
        Check(bad.Count == 3, "a zero stack, a repeated id and a missing name key are reported: " + string.Join(" / ", bad));

        // ---------- the backpack on its own ----------
        var bag = new Backpack();
        Check(Backpack.Size == 20 && bag.FreeCells == 20, "the backpack has 4 × 5 = 20 cells");
        Check(bag.Add(InvItem.Ammo("9mm", 100)) == 0 && bag.FreeCells == 18 && bag[0]!.Count == 60 && bag[1]!.Count == 40, "100 rounds of 9 mm fill one stack of 60 and start another");
        bag.Add(InvItem.Ammo("9mm", 15));
        Check(bag[1]!.Count == 55 && bag.FreeCells == 18 && bag.Count("9mm") == 115, "more rounds go onto the open stack first");
        Check(bag.Take(ItemKind.Ammo, "9mm", 70) == 70 && bag.Count("9mm") == 45 && bag[1] == null, "taking spends the smaller stack first");
        bag.Add(InvItem.Ammo("9mm", 20));
        bag.Move(1, 0);
        Check(bag[0]!.Count == 60 && bag[1]!.Count == 5, "dragging a stack onto the same kind merges it up to the limit");
        bag.Add(InvItem.Medkit());
        bag.Move(2, 0);
        Check(bag[0]!.Kind == ItemKind.Medkit && bag[2]!.Kind == ItemKind.Ammo, "dragging onto a different item swaps them");
        bag.Clear();
        for (int i = 0; i < Backpack.Size; i++) bag.Add(InvItem.Weapon(new WeaponState(ArmoryCatalog.Weapon("m4"))));
        Check(bag.FreeCells == 0 && bag.Add(InvItem.Ammo("556", 10)) == 10, "a full backpack hands back what does not fit");

        // ---------- in an Outpost match ----------
        var stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", ModeOverride = GameMode.Survival };
        AddChild(stage);
        await Frames(3);
        var player = stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        var rules = (SurvivalRules)stage.ModeRules!;
        await Until(() => player.Backpack != null, "every survivor carries a backpack in Outpost");
        bag = player.Backpack!;
        bag.Clear();
        player.Loadout.Unequip(2);
        player.Loadout.Unequip(3);
        var spot = player.GlobalPosition + Vector3.Right;

        // Floor weapons: the empty slot first, then the backpack; loose rounds become ammo.
        var m4 = WeaponPickup.ForWeapon(stage, spot, ArmoryCatalog.Weapon("m4"), new WeaponState(ArmoryCatalog.Weapon("m4"), 30, 90));
        Check(rules.TakePickup(m4, player) && player.Loadout.Primary?.Def.Id == "m4" && player.Loadout.Primary.Reserve == 0 && bag.Count("556") == 90,
            "a floor rifle goes into the empty slot 2 and its 90 loose rounds into the backpack");
        var ak = WeaponPickup.ForWeapon(stage, spot, ArmoryCatalog.Weapon("ak47"), new WeaponState(ArmoryCatalog.Weapon("ak47"), 20, 40));
        rules.TakePickup(ak, player);
        int akCell = Enumerable.Range(0, Backpack.Size).First(i => bag[i]?.Kind == ItemKind.Weapon);
        Check(player.Loadout.Primary?.Def.Id == "m4" && bag[akCell]!.State!.Def.Id == "ak47" && bag[akCell]!.State!.Mag == 20 && bag.Count("762") == 40,
            "with slot 2 taken, the second rifle goes into the backpack with its magazine; its rounds become 7.62");
        await Frames(2);
        Check(!stage.Interactables.Contains(m4) && !stage.Interactables.Contains(ak), "both left the floor");

        // Drag to equip, swap back, unequip.
        Check(InventoryOps.Drag(player, akCell, InventoryOps.Slot2) && player.Loadout.Primary!.Def.Id == "ak47" && bag[akCell]!.State!.Def.Id == "m4",
            "dragging the AK onto slot 2 equips it and the M4 takes its cell");
        int freeCell = Enumerable.Range(0, Backpack.Size).First(i => bag[i] == null);
        Check(!InventoryOps.Drag(player, InventoryOps.Slot1, freeCell) && player.Loadout.Secondary != null, "slot 1 is never left empty");
        Check(InventoryOps.Drag(player, InventoryOps.Slot2, freeCell) && player.Loadout.Primary == null && bag[freeCell]!.State!.Def.Id == "ak47",
            "dragging slot 2 into an empty cell puts the weapon away");
        InventoryOps.Drag(player, freeCell, InventoryOps.Slot2);
        Check(player.Loadout.Primary!.Def.Id == "ak47", "and back");

        // Reloads spend the backpack's rounds of the right caliber.
        var rifle = player.Loadout.Primary!;
        player.Loadout.SelectSlot(2);
        rifle.SetAmmo(0, 0);
        await Frames(2);
        Check(rifle.Reserve == 40, "the carried 7.62 rounds show as the AK's reserve");
        rifle.StartReload();
        await Until(() => !rifle.Reloading && rifle.Mag > 0, "the AK reloads", rifle.Def.ReloadTime + 2);
        await Frames(2);
        Check(rifle.Mag == Math.Min(rifle.Def.MagSize, 40) && bag.Count("762") == 40 - rifle.Mag && bag.Count("556") == 90,
            $"the reload took {rifle.Mag} rounds of 7.62 out of the backpack and left the 5.56 alone");

        // Ammo piles hand out one stack per carried gun's caliber.
        int before = bag.Count("762");
        stage.RefillAmmo(player);
        Check(bag.Count("762") == before + CaliberDatabase.Get("762")!.Stack, "an ammo pile puts a stack of the AK's rounds in the backpack");

        // Throwables and medkits move between the backpack and G / Q.
        bag.Add(InvItem.Throwable("frag", 2));
        int fragCell = Enumerable.Range(0, Backpack.Size).First(i => bag[i]?.Kind == ItemKind.Throwable);
        int frags = player.Loadout.Count(ArmoryCatalog.Throwables["frag"]);
        Check(InventoryOps.Drag(player, fragCell, InventoryOps.SlotThrow) && player.Loadout.Count(ArmoryCatalog.Throwables["frag"]) > frags,
            "dragging grenades onto G carries them");
        var pipe = WeaponPickup.ForThrowable(stage, spot, ArmoryCatalog.Throwables["molotov"]);
        Check(rules.TakePickup(pipe, player) && bag.CountOf(ItemKind.Throwable, "molotov") == 1, "a floor throwable goes into the backpack");
        player.Medkits = 1;
        bag.Add(InvItem.Medkit(2));
        player.Invulnerable = false;
        player.TakeDamage(40, Vector3.Left, 0);
        player.Invulnerable = true;
        player.PressHeal();
        await Until(() => !player.IsHealing && player.Health > player.MaxHealth - 40, "a medkit is used", Player.HealDuration + 2);
        Check(player.Medkits == 1 && bag.CountOf(ItemKind.Medkit, InvItem.MedkitId) == 1, "the next medkit comes out of the backpack into Q by itself");

        // Drop to the floor and pick it up again.
        int ammoCell = Enumerable.Range(0, Backpack.Size).First(i => bag[i] is { Kind: ItemKind.Ammo, Id: "556" });
        int rounds = bag[ammoCell]!.Count;
        Check(InventoryOps.Drag(player, ammoCell, InventoryOps.Floor) && bag[ammoCell] == null, "dragging out of the backpack drops it");
        var dropped = stage.Interactables.OfType<LootPickup>().Single(l => l.Item is { Kind: ItemKind.Ammo, Id: "556" });
        Check(dropped.Item.Count == rounds && dropped.Prompt.Contains(rounds.ToString()), "it lies on the floor as loot with its count: " + dropped.Prompt);
        dropped.Use(player);
        await Frames(2);
        Check(bag.Count("556") == 90 && !stage.Interactables.Contains(dropped), "E puts it back in the backpack");
        Check(InventoryOps.Drag(player, InventoryOps.Slot2, InventoryOps.Floor) && player.Loadout.Primary == null &&
              stage.Interactables.OfType<WeaponPickup>().Any(p => p.Weapon?.Id == "ak47"), "an equipped weapon dropped lies as a weapon pickup");

        // Part of a pile when the backpack is nearly full.
        while (bag.FreeCells > 1) bag.Add(InvItem.Weapon(new WeaponState(ArmoryCatalog.Weapon("m4"))));
        var big = LootPickup.Spawn(stage, spot, InvItem.Ammo("12g", 30));
        big.Use(player);
        await Frames(2);
        Check(bag.Count("12g") == 12 && big.Item.Count == 18 && stage.Interactables.Contains(big), "a nearly full backpack takes one stack and leaves the rest");

        // A named monster spills loot when it falls.
        bag.Clear();
        player.Loadout.Equip(2, new WeaponState(ArmoryCatalog.Weapon("m4")));
        int lootBefore = stage.Interactables.Count(i => i is LootPickup or WeaponPickup);
        var named = stage.Spawn(MonsterDatabase.Get("bull"), player.GlobalPosition + new Vector3(6, 0, 0), false);
        await Frames(2);
        for (int i = 0; i < 40 && IsInstanceValid(named) && !named.IsDead; i++) named.TakeHit(new HitInfo(500, Vector3.Right, named.GlobalPosition + Vector3.Up));
        await Until(() => rules.LootDrops >= rules.Rules.LootMin, "the named one spills loot");
        int spilled = stage.Interactables.Count(i => i is LootPickup or WeaponPickup) - lootBefore;
        Check(spilled >= rules.Rules.LootMin && spilled <= rules.Rules.LootMax, $"{spilled} items lie around it ({rules.Rules.LootMin}–{rules.Rules.LootMax})");
        var commonDrops = rules.LootDrops;
        var common = stage.SpawnCommon(player.GlobalPosition + new Vector3(-6, 0, 0), false);
        await Frames(2);
        common.TakeHit(new HitInfo(500, Vector3.Left, common.GlobalPosition + Vector3.Up));
        await Frames(5);
        Check(rules.LootDrops == commonDrops, "a common monster drops nothing");

        // ---------- the inventory screen ----------
        bag.Add(InvItem.Weapon(new WeaponState(ArmoryCatalog.Weapon("r870"))));
        bag.Add(InvItem.Ammo("556", 120));
        bag.Add(InvItem.Ammo("12g", 10));
        bag.Add(InvItem.Throwable("molotov", 2));
        bag.Add(InvItem.Medkit(2));
        var hud = stage.Hud;
        Check(!hud.InventoryOpen && Input.MouseMode != Input.MouseModeEnum.Visible || !Windowed, "the backpack screen starts closed");
        hud.SetInventoryOpen(true);
        await Frames(2);
        Check(hud.InventoryOpen && hud.FindChild("Inventory", true, false) is Control { Visible: true } && hud.FindChild("InventoryDropZone", true, false) is Control { Visible: true },
            "I opens the backpack screen and the floor drop zone");
        Check(hud.FindChildren("InvCell*", "", true, false).Count == Backpack.Size + InventoryOps.Slots.Length, "it shows 20 cells and the 1 · 2 · 3 · G · Q slots");
        var tip = Hud.Tooltip(bag.Items.First(i => i.Kind == ItemKind.Weapon));
        Check(tip.Contains(CaliberDatabase.Get("12g")!.DisplayName) && tip.Contains(ArmoryCatalog.Weapon("r870").DisplayName), "hovering a weapon names its caliber: " + tip.Replace('\n', ' '));
        Check(Input.MouseMode == Input.MouseModeEnum.Visible || !Windowed, "the mouse shows while it is open");

        // A real mouse drag through the GUI (window only: headless Godot does no GUI picking): the shotgun's cell onto
        // slot 2, then the 12-gauge stack out of the panel.
        Control CellAt(int place) => (Control)hud.FindChild("InvCell" + place, true, false)!;
        if (Windowed)
        {
            int shotgunCell = Enumerable.Range(0, Backpack.Size).First(i => bag[i]?.State?.Def.Id == "r870");
            await MouseDrag(CellAt(shotgunCell).GetGlobalRect().GetCenter(), CellAt(InventoryOps.Slot2).GetGlobalRect().GetCenter());
            Check(player.Loadout.Primary?.Def.Id == "r870" && bag[shotgunCell]?.State?.Def.Id == "m4", "dragging with the mouse onto slot 2 equips the shotgun");
            int shellCell = Enumerable.Range(0, Backpack.Size).First(i => bag[i] is { Kind: ItemKind.Ammo, Id: "12g" });
            int floorBefore = stage.Interactables.OfType<LootPickup>().Count();
            await MouseDrag(CellAt(shellCell).GetGlobalRect().GetCenter(), new Vector2(200, 300));
            Check(bag[shellCell] == null && stage.Interactables.OfType<LootPickup>().Count() == floorBefore + 1, "dragging with the mouse outside the panel drops it on the floor");
        }
        if (Windowed)
        {
            stage.Cam.Close = true;
            for (int i = 0; i < 6; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
            GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/inventory-open.png"));
            GD.Print("SHOT: output/inventory-open.png");
        }
        hud.SetInventoryOpen(false);
        Check(!hud.InventoryOpen, "and closes again");
    }
}
