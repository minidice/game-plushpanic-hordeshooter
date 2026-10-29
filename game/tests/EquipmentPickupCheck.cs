using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Gear on the floor and in the HUD (decisions.md C-34 · C-36 · M-41, equipment-implementation-plan.md Task 3): an empty
/// slot takes a piece, a full one swaps and drops the old piece with its option for a teammate, bad or repeated requests
/// change nothing, only the host equips, Outpost (mode 2) has no gear, and the option stays hidden everywhere unless
/// developer mode is on. Windowed it also saves output/gear-hud-normal.png and gear-hud-dev.png.
/// </summary>
public partial class EquipmentPickupCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private string _suffix = "";
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("EQUIPMENT_PICKUP_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("EQUIPMENT_PICKUP_FAIL " + e.Message); failed = true; }
        GameSession.DeveloperMode = false;
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private static GearItem Gear(string id) => EquipmentDatabase.Current.Create(id, new RandomNumberGenerator { Seed = 1 }) ?? throw new Exception(id);

    private EquipmentPickup Floor(string id, Vector3 at) => EquipmentPickup.Spawn(_stage, at, Gear(id));

    private List<EquipmentPickup> OnFloor() => _stage.Interactables.OfType<EquipmentPickup>().Where(p => IsInstanceValid(p) && !p.IsQueuedForDeletion()).ToList();

    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/gear-hud-{name}{_suffix}.png"));
    }

    private T Ui<T>(string name) where T : Node => _stage.Hud.FindChild(name, true, false) as T ?? throw new Exception($"no HUD node {name}");

    private async Task Run()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--english")) { Loc.Language = "eng"; _suffix = "-eng"; }
        GameSession.DeveloperMode = false;
        await Modes();
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0, ReviveOverride = true };
        AddChild(_stage);
        await Frames(4);
        var me = _stage.Player;
        me.ManualControl = true;
        me.Invulnerable = true;
        var here = me.GlobalPosition;
        var mate = _stage.AddSurvivor(CharacterCatalog.Player("goose"), here + new Vector3(0, 0, 1.5f), false, "mate");
        mate.ManualControl = true;
        mate.Invulnerable = true;
        await Frames(2);

        // ---------- taking and swapping ----------
        var cap = Floor("keen_cap", here + new Vector3(.6f, 0, 0));
        Check(cap.Item.Gear.Id == "keen_cap" && OnFloor().Count == 1, "a hat lies on the floor with its option already chosen");
        Check(_stage.TryEquipPickup(cap, me) && me.Gear.Head?.Gear.Id == "keen_cap" && OnFloor().Count == 0, "an empty head slot takes it and the floor is clear");
        Check(!_stage.TryEquipPickup(cap, me) && OnFloor().Count == 0, "the same request again does nothing (no copy)");
        var keenOption = me.Gear.Head!.Option.Id;
        var goggles = Floor("scope_goggles", here + new Vector3(.6f, 0, 0));
        Check(_stage.TryEquipPickup(goggles, me) && me.Gear.Head?.Gear.Id == "scope_goggles", "a second hat swaps in");
        var dropped = OnFloor();
        Check(dropped.Count == 1 && dropped[0].Item.Gear.Id == "keen_cap" && dropped[0].Item.Option.Id == keenOption,
            "the old hat drops with the same option");
        Check(Mathf.Abs(dropped[0].GlobalPosition.Y - _stage.Tiers.GroundY(dropped[0].GlobalPosition)) < .05f && dropped[0].GlobalPosition.DistanceTo(me.GlobalPosition) < 1.5f,
            "it lands on the floor next to the wearer");
        mate.TeleportTo(dropped[0].GlobalPosition + new Vector3(0, 0, .6f));
        Check(_stage.TryEquipPickup(dropped[0], mate) && mate.Gear.Head?.Gear.Id == "keen_cap" && mate.Gear.Head.Option.Id == keenOption && OnFloor().Count == 0,
            "a teammate picks the dropped hat up, option and all");
        var vest = Floor("brace_vest", here + new Vector3(.6f, 0, 0));
        Check(_stage.TryEquipPickup(vest, me) && me.Gear.Armor?.Gear.Id == "brace_vest" && me.Gear.Head?.Gear.Id == "scope_goggles" && OnFloor().Count == 0,
            "an armor fills its own slot without touching the hat");

        // ---------- refused ----------
        var same = EquipmentPickup.Spawn(_stage, here + new Vector3(.6f, 0, 0), me.Gear.Head!);
        Check(!_stage.TryEquipPickup(same, me) && OnFloor().Contains(same) && me.Gear.Head?.Gear.Id == "scope_goggles", "the very same piece is not swapped for itself");
        same.QueueFree();
        _stage.Interactables.Remove(same);
        var far = Floor("lucky_cap", here + new Vector3(9, 0, 0));
        Check(!_stage.TryEquipPickup(far, me) && OnFloor().Contains(far), "out of reach is refused");
        far.Enabled = false;
        far.GlobalPosition = here + new Vector3(.6f, 0, 0);
        Check(!_stage.TryEquipPickup(far, me), "a disabled piece is refused");
        far.Enabled = true;
        _stage.Role = NetRole.Client;
        Check(!_stage.TryEquipPickup(far, me) && OnFloor().Contains(far), "a client never equips by itself (the host decides)");
        _stage.Role = NetRole.Offline;
        mate.Invulnerable = false;
        mate.TakeDamage(9999, Vector3.Left, 0);
        Check(mate.IsDowned, "the teammate is down");
        far.GlobalPosition = mate.GlobalPosition + new Vector3(.4f, 0, 0);
        Check(!_stage.TryEquipPickup(far, mate) && OnFloor().Contains(far) && mate.Gear.Head?.Gear.Id == "keen_cap", "someone downed cannot equip");

        // ---------- what the HUD shows ----------
        await Frames(3);
        var hidden = EquipmentText.Describe(me.Gear.Head!.Option);
        Check(Ui<Control>("GearPanel").Visible && Ui<HudEquipmentSlot>("HeadSlot").Title == Loc.Text(me.Gear.Head.Gear.NameKey)
              && Ui<HudEquipmentSlot>("ArmorSlot").Title == Loc.Text("name_gear_brace_vest"), "the HUD tiles carry both pieces (name as tooltip)");
        Check(!Ui<Label>("HeadGearOption").Visible && !Ui<Label>("ArmorGearOption").Visible, "normally no option line");
        Check(Texts(_stage.Hud).All(t => !t.Contains(hidden)) && !far.Prompt.Contains(EquipmentText.Describe(far.Item.Option)),
            "normally the option text is nowhere: HUD, floor prompt");
        _stage.Hud.SetPaused(true);
        await Frames(2);
        Check(Texts(_stage.Hud).All(t => !t.Contains(hidden)), "not on the pause screen either");
        _stage.Hud.SetPaused(false);
        far.GlobalPosition = me.GlobalPosition + new Vector3(.8f, 0, 0);
        await Frames(4);
        Dock();
        await Shot("normal");

        GameSession.DeveloperMode = true;
        await Frames(3);
        var headLine = Ui<Label>("HeadGearOption");
        Check(headLine.Visible && headLine.Text.Contains(hidden) && Ui<Label>("ArmorGearOption").Text.Contains(EquipmentText.Describe(me.Gear.Armor!.Option)),
            $"developer mode shows each worn option ({headLine.Text})");
        Check(far.Prompt.Contains(EquipmentText.Describe(far.Item.Option)) && far.Prompt.Contains(hidden),
            $"developer mode compares the floor piece with the worn one in its prompt ({far.Prompt})");
        Check(headLine.Text.Contains(EquipmentText.Off + " "), "an aiming option reads off from the hip");
        me.Loadout.SetSlot(2, ArmoryCatalog.Weapon("m4"));
        me.Weapons.SelectSlot(2);
        me.HoldAim(true);
        await Frames(20);
        Check(me.IsAiming && Ui<Label>("HeadGearOption").Text.Contains(EquipmentText.On + " "), "and on while aiming");
        await Shot("dev");
        me.HoldAim(false);
        // An empty magazine turns the active weapon's pills red (M-42).
        GameSession.DeveloperMode = false;
        me.Gun.SetAmmo(0, 60);
        await Frames(3);
        Check(Ui<Label>("ActiveWeaponName").Text == Loc.Text("hud_ammo_unloaded") && Ui<Label>("ActiveAmmo").Text.StartsWith("00"),
            "an empty magazine says it is not loaded");
        await Shot("empty");
        me.Gun.SetAmmo(0, 0);
        await Frames(2);
        Check(Ui<Label>("ActiveWeaponName").Text == Loc.Text("hud_ammo_out"), "no ammo at all says so");
        GameSession.DeveloperMode = false;
        _stage.QueueFree();
        await Frames(2);
    }

    /// <summary>Gear exists in Scenario and Onslaught, not in Outpost (mode 2, C-34).</summary>
    private async Task Modes()
    {
        foreach (var (mode, on) in new[] { (GameMode.Scenario, true), (GameMode.Survivor, true), (GameMode.Survival, false) })
        {
            var stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false, SquadOverride = 0, ModeOverride = mode };
            AddChild(stage);
            await Frames(4);
            _stage = stage;
            stage.Player.ManualControl = true;
            var piece = Floor("keen_cap", stage.Player.GlobalPosition + new Vector3(.6f, 0, 0));
            bool took = stage.TryEquipPickup(piece, stage.Player);
            var panel = stage.Hud.FindChild("GearPanel", true, false) as Control;
            Check(stage.GearEnabled == on && took == on && (panel?.Visible ?? false) == on, $"{mode}: gear {(on ? "on" : "off")}, HUD slots {(on ? "shown" : "hidden")}");
            stage.QueueFree();
            await Frames(2);
        }
    }

    /// <summary>
    /// The bottom row (M-42) fits the screen with no tile over another, and the active weapon's pills sit above its tile
    /// and below the interaction prompt (CaptureArtDirection's dock check cannot run while I-139 is open).
    /// </summary>
    private void Dock()
    {
        var view = GetViewport().GetVisibleRect();
        var dock = Ui<Control>("EquipmentDock").GetGlobalRect();
        Check(dock.Position.X >= 0 && dock.End.X <= view.Size.X && dock.End.Y <= view.Size.Y, $"the bottom row fits the screen ({dock})");
        var tiles = Ui<Control>("EquipmentDock").FindChildren("*", "", true, false).OfType<HudEquipmentSlot>()
            .Where(t => t.IsVisibleInTree()).Select(t => t.TileRect).OrderBy(r => r.Position.X).ToList();
        Check(tiles.Count == 7 && tiles.Zip(tiles.Skip(1)).All(p => p.First.End.X <= p.Second.Position.X), $"seven tiles side by side, none over another ({tiles.Count})");
        var info = Ui<Control>("ActiveInfo").GetGlobalRect();
        var prompt = Ui<Control>("InteractionPrompt").GetGlobalRect();
        Check(info.End.Y <= tiles[0].Position.Y && info.Position.Y >= prompt.End.Y - 1, $"the weapon pills sit between the tiles and the prompt ({info} / {prompt})");
    }

    private static IEnumerable<string> Texts(Node root)
    {
        foreach (var node in root.FindChildren("*", "", true, false))
            if (node is Label { Visible: true } label && label.IsVisibleInTree()) yield return label.Text;
            else if (node is RichTextLabel { Visible: true } rich && rich.IsVisibleInTree()) yield return rich.Text;
    }
}
