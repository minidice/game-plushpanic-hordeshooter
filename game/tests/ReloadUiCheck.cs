using Godot;
using System;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class ReloadUiCheck : Node
{
    private int _checks;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private bool Outdoor => Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--outdoor");
    private bool Sequence => Windowed && !Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--still-only");
    private int _frame;
    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(ProjectSettings.GlobalizePath($"res://output/reload-ui-{(Outdoor ? "outdoor-" : "")}{name}.png"));
    }
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }
    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--english")) Loc.Language = "eng";
            var stage = new Stage { DirectorEnabled = false, CharacterOverride = "goose" };
            AddChild(stage);
            await Frames(5);
            stage.Player.ManualControl = true;
            var label = stage.Hud.GetNodeOrNull<Label>("HudRoot/WeaponActionLabel");
            Check(label != null, "HUD has a shared bottom weapon progress label");
            CheckFeedback();
            var player = stage.Player;
            player.Invulnerable = true;
            player.Loadout.SetSlot(2, ArmoryCatalog.Weapon("m4"));
            player.Weapons.SelectSlot(2);
            if (Outdoor)
            {
                stage.LeaveSafeRoom();
                player.GlobalPosition = new Vector3(21, 0, .5f);
                stage.Cam.SnapTo(player.GlobalPosition);
            }
            player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(5, 1, -3), false);
            await Frames(20);
            // Freeze only the weapon simulation; sample known real weapon states through the actual HUD.
            player.SetPhysicsProcess(false);
            stage.Hud.SetProcess(false);
            var gun = player.Gun;
            var ammo = (Label)stage.Hud.FindChild("ActiveAmmo", true, false);
            gun.SetAmmo(2, 90);
            stage.Hud._Process(.25);
            Check(!label!.Visible, "no progress strip when ready");
            Check(ammo.GetThemeColor("font_color") == UiTheme.Danger, "ready weapon still warns about low ammo");
            await Shot("ready");
            Check(gun.StartReload(), "real M4 starts reloading");
            for (int i = 0; i <= 60; i++)
            {
                if (i > 0) gun.Tick(gun.ReloadTime / 60, false);
                stage.Hud._Process(1.0 / 30);
                if (Sequence) { await Frames(1); await Shot($"frame-{_frame++:000}"); }
                if (i == 3) await Shot("enter");
                if (i == 1) await Shot("small");
                if (i == 59) await Shot("almost");
                if (i == 30)
                {
                    Check(label.Visible && (label.Text.Contains("49%") || label.Text.Contains("50%")), "bottom label follows the actual half reload");
                    Check(ammo.GetThemeColor("font_color") != UiTheme.Danger, "reload status does not compete with danger warnings");
                    Check(ammo.GetThemeFontSize("font_size") < 28, "reload status is quieter than the ready ammo count");
                    if (Windowed) await CheckReloadReticle(stage);
                    int rounds = gun.Mag;
                    Check(!gun.Trigger(true, true) && gun.Mag == rounds, "trigger cannot shoot during magazine reload");
                    await Shot("half");
                    float before = gun.ReloadElapsed;
                    stage.Hud.SetPaused(true);
                    stage.Hud._Process(.2);
                    Check(!label.Visible && gun.ReloadElapsed == before, "pause hides feedback without advancing weapon time");
                    stage.Hud.SetPaused(false);
                    stage.Hud._Process(.2);
                    stage.Hud.CardWindowOpen = true;
                    stage.Hud._Process(.2);
                    Check(!label.Visible, "card window suppresses weapon feedback");
                    stage.Hud.CardWindowOpen = false;
                    stage.Hud._Process(.2);
                }
            }
            gun.Tick(.001f, false);
            stage.Hud._Process(1.0 / 30);
            Check(!gun.Reloading && gun.CanFire && label.Text.Contains("100%"), "completion displays 100 percent and restores firing");
            await Shot("complete");
            for (int i = 0; i < 9; i++)
            {
                stage.Hud._Process(1.0 / 30);
                if (Sequence) { await Frames(1); await Shot($"frame-{_frame++:000}"); }
            }
            Check(!label.Visible, "completed strip fades away");
            gun.SetAmmo(1, 90);
            gun.StartReload();
            gun.Tick(gun.ReloadTime * .4f, false);
            stage.Hud._Process(.2);
            gun.CancelReload();
            stage.Hud._Process(.02);
            Check(label.Visible && !label.Text.Contains("100%"), "cancellation never claims completion");
            Check(ammo.GetThemeColor("font_color") == UiTheme.Danger && ammo.GetThemeFontSize("font_size") == 28,
                "cancelling reload immediately restores the readable low ammo warning");
            await Shot("cancel");
            // Stable fully visible samples exercise tiny fills independently of the entrance fade.
            gun.StartReload();
            stage.Hud._Process(.2);
            await Shot("zero");
            gun.Tick(gun.ReloadTime * .01f, false);
            stage.Hud._Process(.2);
            await Shot("one-percent");
            gun.CancelReload();
            player.Weapons.SelectSlot(1);
            stage.Hud._Process(.02);
            Check(!label.Visible, "switching weapons removes the previous weapon progress immediately");
            // A real controller charge, including cancellation/refund, shares the presentation.
            player.Loadout.SetSlot(2, ArmoryCatalog.Weapon("railgun"));
            player.Weapons.SelectSlot(2);
            player.SetPhysicsProcess(true);
            player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(7, 1, -3), true);
            for (int i = 0; i < 60 && !player.Weapons.Charging; i++) await Frames(1);
            player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(7, 1, -3), false);
            Check(player.Weapons.Charging, "real railgun controller enters charge");
            player.SetPhysicsProcess(false);
            stage.Hud._Process(.2);
            Check(label.Visible && label.Text.StartsWith(Loc.Text("hud_action_charge")), "charge uses the common progress strip with its own label");
            await Shot("charge");
            player.Weapons.CancelWindup();
            stage.Hud._Process(.02);
            Check(!label.Text.Contains("100%"), "cancelled controller charge is not shown as complete");
            player.Gun.ShotCooldown = 4;
            player.Gun.SetAmmo(1, player.Gun.Reserve);
            player.Gun.Tick(2, false);
            Check(player.Gun.Trigger(true, true), "special weapon starts an actual recharge");
            stage.Hud._Process(.2);
            Check(player.Gun.CooldownLeft > 0 && label.Text.StartsWith(Loc.Text("hud_action_recharge")), "recharge is active during the hit mark check");
            if (Windowed)
            {
                player.PressRoll();
                player._PhysicsProcess(.016);
                stage.Hud._Process(.016);
                Check(player.RollCooldownLeft > 0, "leap cooldown render samples an actual roll");
                await Shot("leap-cooldown");
                stage.ProcessMode = ProcessModeEnum.Disabled;
                var reticle = stage.Hud.GetNode<Control>("HudRoot/Reticle");
                reticle.QueueRedraw();
                await Frames(3);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var before = GetViewport().GetTexture().GetImage();
                stage.Hud.ShowHitMarker(true);
                await Frames(6);
                reticle.QueueRedraw();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var after = GetViewport().GetTexture().GetImage();
                var center = stage.Cam.UnprojectPosition(player.AimPoint);
                // canvas_items scales the viewport's logical 1600x900 coordinates into the window image.
                var logical = GetViewport().GetVisibleRect().Size;
                center *= new Vector2(after.GetWidth() / logical.X, after.GetHeight() / logical.Y);
                int brighter = 0;
                for (int y = Mathf.Max(0, (int)center.Y - 100); y < Mathf.Min(after.GetHeight(), (int)center.Y + 100); y++)
                for (int x = Mathf.Max(0, (int)center.X - 100); x < Mathf.Min(after.GetWidth(), (int)center.X + 100); x++)
                {
                    var a = after.GetPixel(x, y); var b = before.GetPixel(x, y);
                    if (a.R - b.R > .25f && a.G - b.G > .15f) brighter++;
                }
                Check(brighter > 40, $"hit marks remain visibly rendered during recharge ({brighter} brightened pixels)");
                await Shot("recharge-hit");
            }
            stage.QueueFree();
            await Frames(2);
            GD.Print("RELOAD_UI_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("RELOAD_UI_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task CheckReloadReticle(Stage stage)
    {
        // Compare the same frozen world with/without the real reticle; scenery cannot masquerade as UI dots.
        var previousMode = stage.ProcessMode;
        stage.ProcessMode = ProcessModeEnum.Disabled;
        var reticle = stage.Hud.GetNode<Control>("HudRoot/Reticle");
        reticle.Visible = false;
        await Frames(2);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var background = GetViewport().GetTexture().GetImage();
        reticle.Visible = true;
        reticle.QueueRedraw();
        await Frames(2);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var visible = GetViewport().GetTexture().GetImage();
        var scale = new Vector2(visible.GetWidth(), visible.GetHeight()) / GetViewport().GetVisibleRect().Size;
        var center = stage.Cam.UnprojectPosition(stage.Player.AimPoint);
        int dots = 0, ring = 0;
        for (int y = Mathf.Max(0, (int)((center.Y - 36) * scale.Y)); y < Mathf.Min(visible.GetHeight(), (int)((center.Y + 36) * scale.Y)); y++)
        for (int x = Mathf.Max(0, (int)((center.X - 36) * scale.X)); x < Mathf.Min(visible.GetWidth(), (int)((center.X + 36) * scale.X)); x++)
        {
            var offset = new Vector2(x / scale.X, y / scale.Y) - center;
            float radius = offset.Length();
            var a = visible.GetPixel(x, y); var b = background.GetPixel(x, y);
            bool brightened = a.R - b.R > .1f && a.G - b.G > .1f;
            if (brightened && radius > 28 && radius < 35 && Mathf.Abs(Mathf.Abs(offset.X) - Mathf.Abs(offset.Y)) < 5) dots++;
            if (brightened && radius > 16 && radius < 22) ring++;
        }
        stage.ProcessMode = previousMode;
        Check(ring > 20, $"reload progress arc remains visible ({ring} pixels)");
        Check(dots == 0, $"reload ring has no decorative diagonal dots ({dots} pixels)");
    }

    private void CheckFeedback()
    {
        var gun = new WeaponState(ArmoryCatalog.Weapon("m4"), 0, 90);
        var feedback = new WeaponActionFeedback();
        gun.StartReload();
        feedback.Update(gun, false, 0, .04f);
        Check(feedback.Weight > 0 && feedback.Weight < 1, "reload enters through intermediate morph weights");
        gun.Tick(gun.ReloadTime * .5f, false);
        feedback.Update(gun, false, 0, .2f);
        Check(Mathf.IsEqualApprox(feedback.Progress, .5f) && feedback.Percent == 50, "ring and percent sample half reload without display lag");
        gun.CancelReload();
        feedback.Update(gun, false, 0, .04f);
        Check(feedback.Weight > 0 && feedback.Weight < 1 && feedback.Percent == 50, "cancel fades at last progress instead of flashing success");
        feedback.Update(gun, false, 0, .2f);
        Check(feedback.Weight == 0, "cancel finishes fading");
        gun.StartReload();
        gun.Tick(gun.ReloadTime * .996f, false);
        feedback.Update(gun, false, 0, .2f);
        gun.CancelReload();
        feedback.Update(gun, false, 0, .01f);
        Check(feedback.Percent < 100, "late cancellation at 99.6 percent never rounds to success");
        gun.StartReload();
        feedback.Update(gun, false, 0, .2f);
        gun.Tick(gun.ReloadTime, false);
        feedback.Update(gun, false, 0, .01f);
        Check(feedback.Percent == 100 && gun.CanFire, "successful reload reaches 100 as weapon unlocks");
        gun.SetAmmo(gun.Def.MagSize - 1, 90);
        gun.StartReload();
        feedback.Update(gun, false, 0, .2f);
        gun.Tick(gun.ReloadTime, false);
        Check(gun.Trigger(true, true), "held trigger fires on the reload completion tick");
        feedback.Update(gun, false, 0, .01f);
        Check(feedback.Percent == 100, "reload completion is retained when a shot consumes the inserted round immediately");
        var shell = new WeaponState(ArmoryCatalog.Weapon("r870"), 0, 8);
        shell.StartReload();
        shell.Tick(shell.ReloadTime * .9f, false);
        feedback.Update(shell, false, 0, .2f);
        Check(feedback.Progress > .89f, "tube reload shows first shell nearing insertion");
        shell.Tick(shell.ReloadTime * .1f + .01f, false);
        feedback.Update(shell, false, 0, .01f);
        Check(shell.Mag == 1 && feedback.Progress < .1f && feedback.Weight == 1, "next shell restarts progress without flickering the ring");
        Check(shell.Trigger(true, true) && !shell.Reloading, "tube reload retains its existing fire-to-interrupt rule");
        feedback.Update(shell, false, 0, .01f);
        Check(feedback.Percent < 100, "interrupted tube reload does not flash a full magazine");
        shell = new WeaponState(shell.Def, shell.Def.MagSize - 1, 8);
        shell.StartReload();
        feedback.Update(shell, false, 0, .2f);
        shell.Tick(shell.ReloadTime + .001f, false);
        Check(shell.Trigger(true, true), "tube weapon can fire as its final shell finishes");
        feedback.Update(shell, false, 0, .01f);
        Check(feedback.Percent == 100, "final shell completion survives same-tick firing");
        var limited = new WeaponState(ArmoryCatalog.Weapon("m4"), 0, 1);
        limited.StartReload();
        feedback.Update(limited, false, 0, .2f);
        limited.Tick(limited.ReloadTime, false);
        feedback.Update(limited, false, 0, .01f);
        Check(limited.Mag == 1 && feedback.Percent == 100, "limited reserve completes the action even without a full magazine");
        var recharge = new WeaponState(ArmoryCatalog.Weapon("alabong"), 1) { ShotCooldown = 4 };
        recharge.Trigger(true, true);
        recharge.TickCooldown(1);
        feedback.Update(recharge, false, 0, .2f);
        Check(feedback.Percent == 25 && feedback.Kind == WeaponActionFeedback.ActionKind.Recharge, "recharge reads elapsed share of the actual cooldown");
        recharge.TickCooldown(3);
        feedback.Update(recharge, false, 0, .01f);
        Check(feedback.Percent == 100, "recharge completion also reaches 100");
        feedback.Update(new WeaponState(ArmoryCatalog.Weapon("m1911")), false, 0, .01f);
        Check(feedback.Weight == 0 && feedback.Kind == WeaponActionFeedback.ActionKind.None, "new ready weapon inherits no stale ring");
    }
}
