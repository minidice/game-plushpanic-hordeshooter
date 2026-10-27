using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Weapon overhaul step 1 (doc/mode-weapon-direction.md 1~3장, decisions.md C-24~C-27): per-type move speed,
/// slot 3 special weapons, aiming with right click / Ctrl (range, spread, focus, crit, slower walk, camera),
/// −50% while a throw or a lobbed weapon is aimed, the mortar's set-up and pack-up, and the railgun's charge.
/// With a real window it also saves renders to output/weapons-*.png.
/// </summary>
public partial class WeaponHandlingCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("WEAPON_HANDLING_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("WEAPON_HANDLING_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/weapons-{name}.png"));
        GD.Print($"SHOT: output/weapons-{name}.png");
    }

    private async Task Run()
    {
        Data();
        LoadoutRules();
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false };
        AddChild(_stage);
        await Frames(3);
        foreach (var e in _stage.AllEnemies.ToList()) e.QueueFree();
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        await Speeds(player);
        await AimView(player);
        await Aiming(player);
        await Mortar(player);
        await Railgun(player);
        Check(_stage.Hud.FindChild("SpecialSlot", true, false) is Control { Visible: true }, "the HUD has a slot 3 tile");
    }

    private void Data()
    {
        var db = WeaponDatabase.Current;
        Check(db.Errors.Count == 0, "weapon data loads without errors" + string.Join(" | ", db.Errors));
        var w = db.Weapons;
        Check(w["mp5"].MoveSpeedScale > 1 && w["m4"].MoveSpeedScale == 1 && w["m249"].MoveSpeedScale < 1 && w["awm"].MoveSpeedScale < 1,
            "SMGs walk faster, rifles normal, LMGs and snipers slower");
        Check(w["awm"].AimRangeMultiplier > w["m4"].AimRangeMultiplier && w["m4"].AimRangeMultiplier > w["mp5"].AimRangeMultiplier && w["awm"].CritMultiplier > w["m4"].CritMultiplier,
            "aiming pays most on snipers (range, crit)");
        Check(new[] { "alabong", "mortar", "railgun" }.All(id => w[id].Slot == WeaponSlot.Special) && w.Values.Where(d => d.Slot == WeaponSlot.Special).All(d => !d.Refillable),
            "rocket launcher, mortar and railgun live in slot 3 and never take ammo piles");
        Check(w["mortar"].SetupTime > 0 && w["mortar"].RecoverTime > 0 && !w["mortar"].CanAim, "the mortar sets up and packs up and has no scope aim");
        Check(w["railgun"].ChargeTime > 0 && w["railgun"].Pierce >= 10 && w["railgun"].CanAim, "the railgun charges and pierces everything");
        var m4 = w["m4"];
        Check(m4.FalloffAt(35) < 1 && m4.FalloffAt(35, m4.AimRangeMultiplier) == 1 && Math.Abs(m4.FalloffAt(m4.MaxRange) - m4.FalloffMin) < .001f,
            "falloff is linear to falloff_min at max range, and aiming stretches it (C-27)");

        const string types = "type,name,slot,category,fire_mode,damage,max_range_m,fire_interval_ms,mag_size,max_mags,move_speed_pct,aim_range_mult\n" +
                             "rifle,소총,4,rifle,hitscan,10,40,100,30,5,0,0.5\n";
        var broken = WeaponDatabase.FromText(types, "id,type,name\nbad,rifle,name_weapon_m4\n", new Dictionary<string, string>()).Errors;
        Check(broken.Any(e => e.Contains("1/2/3")) && broken.Any(e => e.Contains("move_speed_pct")) && broken.Any(e => e.Contains("aim_range_mult")),
            "a bad slot, zero speed and an aim range below 1 are reported: " + string.Join(" / ", broken));
    }

    private void LoadoutRules()
    {
        var loadout = ArmoryCatalog.NewLoadout();
        Check(!loadout.SelectSlot(3), "slot 3 is empty at the start");
        loadout.Pickup(ArmoryCatalog.Weapon("m4"));
        var (got, dropped) = loadout.Pickup(ArmoryCatalog.Weapon("alabong"));
        Check(got == PickupResult.Equipped && dropped == null && loadout.ActiveSlot == 3 && loadout.Primary?.Def.Id == "m4" && loadout.Special?.Def.Id == "alabong",
            "a heavy weapon goes to slot 3 and keeps the primary");
        Check(loadout.SelectSlot(2) && loadout.Current.Def.Id == "m4" && loadout.SelectSlot(3) && loadout.Current.Def.Id == "alabong", "1·2·3 switch between them");
        (got, dropped) = loadout.Pickup(ArmoryCatalog.Weapon("railgun"));
        Check(got == PickupResult.Swapped && dropped?.Def.Id == "alabong" && loadout.Special?.Def.Id == "railgun", "another heavy weapon swaps slot 3 only");
    }

    private async Task<float> Walk(Player player, int frames)
    {
        player.TeleportTo(new Vector3(14, 0, 0));
        await Frames(2);
        float start = player.GlobalPosition.X;
        player.SetControls(new Vector2(1, 0), player.GlobalPosition + new Vector3(10, 1, 0), false);
        await Frames(frames);
        player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(10, 1, 0), false);
        return player.GlobalPosition.X - start;
    }

    private async Task Speeds(Player player)
    {
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        float rifle = await Walk(player, 30);
        player.Weapons.Pickup(ArmoryCatalog.Weapon("mp5"), null);
        float smg = await Walk(player, 30);
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m249"), null);
        float lmg = await Walk(player, 30);
        Check(smg > rifle * 1.05f && lmg < rifle * .9f, $"walking speed follows the gun in hand (SMG {smg:0.00} > rifle {rifle:0.00} > LMG {lmg:0.00})");
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        player.Loadout.AddThrowable(ArmoryCatalog.Throwable("frag"));
        player.PressThrow();
        await Frames(2);
        Check(player.Weapons.AimingThrow && Mathf.IsEqualApprox(player.HandlingSpeedScale, Player.LobAimMoveScale), "aiming a throw halves walking speed");
        player.PressCancel();
        await Frames(2);
        Check(!player.Weapons.AimingThrow, "right click cancels the throw aim");
    }

    /// <summary>How far the camera leans (and zooms) toward a far cursor while aiming with <paramref name="weapon"/>.</summary>
    private async Task<(float Lean, float Size)> AimedView(Player player, string weapon)
    {
        player.Weapons.Pickup(ArmoryCatalog.Weapon(weapon), null);
        player.HoldAim(false);
        player.TeleportTo(new Vector3(12, 0, .5f));
        player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(40, 1, 0), false);
        _stage.Cam.SnapTo(player.GlobalPosition + Vector3.Up * .8f);
        var rest = _stage.Cam.GlobalPosition;
        player.HoldAim(true);
        for (int i = 0; i < 90; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var lean = _stage.Cam.GlobalPosition - rest;
        lean.Y = 0;
        float size = _stage.Cam.ViewSize;
        await Shot("aim-" + weapon);
        player.HoldAim(false);
        return (lean.Length(), size);
    }

    /// <summary>Aiming reaches as far as the gun's aim_view: a shotgun not at all, a rifle as before, a sniper ~1.5× (zooming out).</summary>
    private async Task AimView(Player player)
    {
        var shotgun = await AimedView(player, "r870");
        var rifle = await AimedView(player, "m4");
        var sniper = await AimedView(player, "awm");
        float RifleReach(float lean, float size) => lean + size * 16f / 9f / 2f;
        Check(shotgun.Lean < 4f && Mathf.IsEqualApprox(shotgun.Size, CameraRig.WideSize, .05f), $"aiming a shotgun barely moves the view ({shotgun.Lean:0.0} m)");
        Check(rifle.Lean > 8f && Mathf.IsEqualApprox(rifle.Size, CameraRig.WideSize, .05f), $"aiming a rifle leans the view toward the cursor ({rifle.Lean:0.0} m)");
        float ratio = RifleReach(sniper.Lean, sniper.Size) / RifleReach(rifle.Lean, rifle.Size);
        Check(sniper.Size > CameraRig.WideSize * 1.3f && ratio >= 1.45f,
            $"aiming a sniper zooms out and sees about 1.5× further than a rifle ({ratio:0.00}×, size {sniper.Size:0.0})");
        await Frames(40);
        for (int i = 0; i < 60; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(Mathf.IsEqualApprox(_stage.Cam.ViewSize, CameraRig.WideSize, .1f), "letting go zooms back in");
        // A special weapon only slows its holder while it is in hand.
        player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        player.Weapons.Pickup(ArmoryCatalog.Weapon("alabong"), null);
        Check(player.Loadout.ActiveSlot == 3 && player.HandlingSpeedScale < 1, "holding the rocket launcher slows walking");
        player.Weapons.SelectSlot(2);
        Check(Mathf.IsEqualApprox(player.HandlingSpeedScale, ArmoryCatalog.Weapon("m4").MoveSpeedScale), "carrying it in slot 3 while holding the rifle does not");
    }

    private async Task Aiming(Player player)
    {
        var m4 = ArmoryCatalog.Weapon("m4");
        player.Weapons.SelectSlot(2);
        player.TeleportTo(new Vector3(12, 0, .5f));
        _stage.Cam.SnapTo(player.GlobalPosition);
        // 46 m away: past the M4's 40 m max range, inside it when aimed (×1.5).
        var far = new Vector3(58, 0, .5f);
        var target = _stage.SpawnCommon(far, awake: false);
        await Frames(3);
        target.Stun(30);
        float hp = target.Health;
        player.SetControls(Vector2.Zero, far + Vector3.Up, true);
        await Frames(3);
        player.SetControls(Vector2.Zero, far + Vector3.Up, false);
        await Frames(3);
        Check(IsInstanceValid(target) && target.Health == hp, "without aiming a 46 m target is out of the rifle's reach");
        player.HoldAim(true);
        // The cone closes over the M4's aim time (C-30).
        await Frames(Mathf.CeilToInt(m4.AimTime * Engine.PhysicsTicksPerSecond) + 4);
        Check(player.IsAiming && player.AimBlend >= 1 && player.Gun.Spread < m4.BaseSpread, "holding aim with a rifle aims: full blend, tighter than the base cone");
        Check(Mathf.IsEqualApprox(player.HandlingSpeedScale, m4.MoveSpeedScale * m4.AimMoveScale), "aiming slows walking to aim_move_pct");
        await Shot("aim-rifle");
        // Even aimed the cone is about 1 m wide at 46 m: a few shots, one of them lands (reach, not luck, is checked).
        for (int i = 0; i < 5 && IsInstanceValid(target) && target.Health >= hp; i++)
        {
            player.SetControls(Vector2.Zero, far + Vector3.Up, true);
            await Frames(3);
            player.SetControls(Vector2.Zero, far + Vector3.Up, false);
            await Frames(8);
        }
        Check(!IsInstanceValid(target) || target.Health < hp, "aimed, the same shot reaches 46 m");
        player.HoldAim(false);
        await Frames(2);
        Check(!player.IsAiming, "letting go stops aiming");
        player.Weapons.SelectSlot(1);
        player.HoldAim(true);
        await Frames(2);
        Check(!player.IsAiming, "a melee weapon cannot aim");
        player.HoldAim(false);
    }

    private async Task Mortar(Player player)
    {
        var def = ArmoryCatalog.Weapon("mortar");
        player.Weapons.Pickup(def, null);
        player.TeleportTo(new Vector3(16, 0, 0));
        _stage.Cam.SnapTo(player.GlobalPosition);
        var aim = new Vector3(28, 1, 1);
        player.SetControls(Vector2.Zero, aim, false);
        await Frames(4);
        Check(player.Weapons.AimingLob && player.Weapons.Preview != null && Mathf.IsEqualApprox(player.HandlingSpeedScale, def.MoveSpeedScale * Player.LobAimMoveScale),
            "a mortar in hand shows its landing ring and walks at half speed");
        await Shot("mortar-aim");
        player.SetControls(Vector2.Zero, aim, true);
        await Frames(2);
        player.SetControls(new Vector2(1, 0), aim, false);
        var at = player.GlobalPosition;
        await Frames(20);
        Check(player.Weapons.Planted && player.GlobalPosition.DistanceTo(at) < .05f && !_stage.Effects.GetChildren().OfType<MortarShell>().Any(),
            "while the mortar sets up, nothing is fired yet and the survivor cannot walk");
        await Frames((int)(def.SetupTime * 60));
        Check(_stage.Effects.GetChildren().OfType<MortarShell>().Any() && player.Weapons.Planted, "after the set-up the shell leaves, then it packs up");
        await Frames((int)(def.RecoverTime * 60) + 20);
        Check(!player.Weapons.Planted && player.GlobalPosition.DistanceTo(at) > .3f, "packed up, the survivor walks again");
        player.SetControls(Vector2.Zero, aim, false);
        await Frames((int)(def.FlightTime * 60) + 10);
    }

    private async Task Railgun(Player player)
    {
        var def = ArmoryCatalog.Weapon("railgun");
        player.Weapons.Pickup(def, null);
        player.TeleportTo(new Vector3(14, 0, .5f));
        _stage.Cam.SnapTo(player.GlobalPosition + new Vector3(4, 0, 0));
        var line = new List<Enemy>();
        for (int i = 0; i < 4; i++) line.Add(_stage.SpawnCommon(new Vector3(20 + i * 2, 0, .5f), awake: false));
        await Frames(3);
        foreach (var e in line) e.Stun(30);
        var aim = new Vector3(30, 1, .5f);
        player.SetControls(Vector2.Zero, aim, true);
        await Frames(2);
        player.SetControls(Vector2.Zero, aim, false);
        await Frames(Mathf.CeilToInt(def.ChargeTime * 60 * .6f));
        Check(player.Weapons.Charging && line.All(IsInstanceValid) && player.Gun.Mag == 0, "the railgun charges first (the round is taken, nothing hit yet)");
        await Shot("railgun-charge");
        await Frames(Mathf.CeilToInt(def.ChargeTime * 60 * .5f) + 4);
        Check(!player.Weapons.Charging && line.All(e => !IsInstanceValid(e)), "the charged beam pierces the whole line");

        player.PressReload();
        await Frames(Mathf.CeilToInt(def.ReloadTime * 60) + 4);
        Check(player.Gun.Mag == 1, "reloaded");
        player.SetControls(Vector2.Zero, aim, true);
        await Frames(2);
        player.SetControls(Vector2.Zero, aim, false);
        player.PressSlot(1);
        await Frames(2);
        Check(!player.Weapons.Charging && player.Loadout.Special!.Mag == 1, "switching away mid-charge cancels it and gives the round back");
    }
}
