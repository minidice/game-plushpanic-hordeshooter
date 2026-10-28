using Godot;
using System;
using System.Threading.Tasks;
namespace HordeShooter.Tests;

public partial class InteractionUiCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(ProjectSettings.GlobalizePath("res://output/interaction-ui-" + name + (Loc.Language == "eng" ? "-eng" : "") + (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--bright") ? "-bright" : "") + ".png"));
    }
    private void Tick(float dt = .04f) { _stage._PhysicsProcess(dt); _stage.Hud._Process(dt); }
    private void Advance(float time) { for (float t = 0; t < time; t += .04f) Tick(); }
    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--english")) Loc.Language = "eng";
            _stage = new Stage { DirectorEnabled = false, SquadOverride = 0, CharacterOverride = "goose", ReviveOverride = true };
            AddChild(_stage); await Frames(8);
            var p = _stage.Player;
            p.ManualControl = true; p.Invulnerable = true;
            _stage.LeaveSafeRoom(); p.TeleportTo(new Vector3(21, 0, .5f)); _stage.Cam.SnapTo(p.Position);
            p.SetControls(Vector2.Zero, p.Position + new Vector3(5, 1, -3), false);
            p.Loadout.SetSlot(2, ArmoryCatalog.Weapon("m4")); p.Weapons.SelectSlot(2);
            foreach (var existing in _stage.Interactables) existing.Enabled = false;
            var at = p.Position;
            var item = new Interactable { Position = at + Vector3.Right * 1.2f, Prompt = "ui_use_open_base", HoldSeconds = 3, CancelOnHit = true };
            _stage.AddChild(item); _stage.AddInteractable(item);
            item.OnUse = _ => item.Enabled = false;
            if (PropLibrary.Instance("wall_button_panel") is { } visual) item.AddChild(visual);
            await Frames(20);
            var panel = _stage.Hud.GetNodeOrNull<Control>("HudRoot/WorldInteraction");
            Check(panel is { Visible: true }, "nearby actionable object has a visible world-attached prompt");
            if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--bright"))
                foreach (var child in _stage.GetChildren())
                {
                    if (child is WorldEnvironment world) world.Environment.AmbientLightEnergy = 1.2f;
                    if (child is DirectionalLight3D sun) sun.LightEnergy = 2;
                }
            await Shot("approach");
            _stage.SetPhysicsProcess(false); p.SetPhysicsProcess(false); _stage.Hud.SetProcess(false);
            var hud = _stage.Hud;
            var title = (Label)panel!.FindChild("InteractionTitle", true, false);
            var key = (Label)panel.FindChild("InteractionKey", true, false);
            var hint = (Label)panel.FindChild("InteractionHint", true, false);
            var gauge = (Control)panel.FindChild("InteractionProgress", true, false);
            Check(hud.InteractionTarget == item && title.Text == item.Prompt, "world label identifies the exact E action");
            item.Position = at + Vector3.Right * 3;
            Tick();
            Check(hud.InteractionTarget == null && key.Modulate.A < .5f, "leaving range immediately dims E before the fade ends");
            item.Position = at + Vector3.Right * 1.2f; Tick();
            Check(hud.InteractionTarget == item && panel.Modulate.A > .1f, "boundary re-entry reverses the fade without restarting invisible");
            item.Position = at + Vector3.Right * 3; Advance(.4f);
            Check(!panel.Visible && hud.DiscoveryMarkerCount > 0, "out-of-range label fades back to a discovery marker");
            await Shot("discovery");
            item.Position = at + Vector3.Right * 1.2f; Advance(.3f);
            var second = new Interactable { Position = at + Vector3.Left * .5f, Prompt = "ui_use_medkit" };
            _stage.AddChild(second); _stage.AddInteractable(second); Tick();
            Check(hud.InteractionTarget == second && title.Text == second.Prompt, "a closer target replaces text immediately with no stale E action");
            second.Enabled = false; Tick();
            Check(hud.InteractionTarget == item, "disabled target immediately relinquishes focus");
            _stage.Interactables.Remove(second); second.Free();
            p.ManualControl = false;
            Input.ActionPress(GameInput.Interact); Advance(1.4f);
            Check(_stage.HoldProgress > .4f && _stage.HoldProgress < .55f && gauge.Visible, "actual three-second hold drives world progress");
            Check(hint.Text.Contains("%"), "hold exposes progress without replacing the aim cursor");
            await Shot("hold");
            p.Gun.SetAmmo(20, 90); p.Gun.StartReload(); p.Gun.Tick(p.Gun.ReloadTime / 2, false); hud._Process(.2);
            await Shot("hold-and-reload"); p.Gun.CancelReload();
            p.Invulnerable = false; p.TakeDamage(1, Vector3.Zero, 0); p.Invulnerable = true; Tick();
            Check(_stage.HoldProgress == 0 && !gauge.Visible && hint.Text == Loc.Text("hud_interaction_interrupted"), "damage cancels progress and explains interruption without showing completion");
            await Shot("interrupted");
            Advance(.85f);
            Check(key.Modulate.A < .5f && hint.Text == Loc.Text("hud_interaction_release"), "held key after damage stays inactive until released");
            Input.ActionRelease(GameInput.Interact); Tick(); Input.ActionPress(GameInput.Interact); Advance(.5f);
            Input.ActionRelease(GameInput.Interact); Tick();
            Check(!gauge.Visible && !hint.Text.Contains("100"), "release cancels without false full progress");
            Input.ActionPress(GameInput.Interact); Advance(3.04f); Input.ActionRelease(GameInput.Interact);
            Check(!item.Enabled && hint.Text == Loc.Text("hud_interaction_done"), "real disabled door state shows a short completion acknowledgement");
            await Shot("complete"); Advance(1);
            Check(!panel.Visible, "completion acknowledgement expires instead of restarting forever");
            item.Enabled = true; Advance(.3f);
            // Delay the host's state response after the real client hold dispatch. No success at send time.
            int requests = 0; _stage.Role = NetRole.Client; _stage.RequestUse = _ => requests++;
            Input.ActionPress(GameInput.Interact); Advance(3.04f); Input.ActionRelease(GameInput.Interact); Advance(.3f);
            Check(requests == 1 && item.Enabled && hint.Text != Loc.Text("hud_interaction_done"), "client hold dispatch cannot announce success before host confirmation");
            item.Enabled = false; Tick();
            Check(hint.Text == Loc.Text("hud_interaction_done"), "delayed host confirmation acknowledges the submitted hold after E is released");
            Advance(1); Check(!panel.Visible, "confirmed client completion also expires");
            _stage.Role = NetRole.Offline; _stage.RequestUse = null;
            item.Enabled = true; Advance(.3f);
            hud.SetPaused(true); hud._Process(.04);
            Check(!panel.Visible && hud.DiscoveryMarkerCount == 0, "pause clears prompts and discovery markers immediately");
            hud.SetPaused(false); Advance(.3f); hud.CardWindowOpen = true; hud._Process(.04);
            Check(!panel.Visible, "card overlay suppresses interaction UI");
            hud.CardWindowOpen = false; Advance(.3f);
            var mate = _stage.AddRemoteSurvivor(CharacterCatalog.Player("duck"), at + Vector3.Forward, 2);
            mate.SetPhysicsProcess(false); mate.TakeDamage(9999, Vector3.Zero, 0); Tick();
            Check(mate.IsDowned && hud.InteractionTarget == mate && title.Text == Loc.Format("hud_interaction_revive", Loc.Text(mate.Definition.DisplayName)), "downed teammate prompt has priority over a nearby usable object");
            await Shot("revive");
            var helper = _stage.AddRemoteSurvivor(CharacterCatalog.Player("goose"), at + Vector3.Left * .5f, 3);
            helper.SetPhysicsProcess(false); helper.ManualControl = true;
            helper.SetControls(Vector2.Zero, at + Vector3.Right * 5, false);
            helper.HoldRevive(true); helper._PhysicsProcess(.4); Tick();
            Check(mate.RevivedBy == helper && mate.ReviveProgress > 0, "another survivor owns the real revive progress");
            p.Invulnerable = false; p.TakeDamage(1, Vector3.Zero, 0); p.Invulnerable = true; Tick();
            Check(gauge.Visible && key.Modulate.A < .5f && hint.Text == Loc.Text("hud_interaction_other_reviver"), "local damage cannot claim to interrupt another survivor's revive");
            helper.HoldRevive(false); helper._PhysicsProcess(.04);
            p.ManualControl = true; p.HoldRevive(true); p._PhysicsProcess(.4); Tick();
            Check(mate.RevivedBy == p && mate.ReviveProgress > 0, "local survivor can take over after the previous owner releases");
            p.Invulnerable = false; p.TakeDamage(1, Vector3.Zero, 0); p.Invulnerable = true; Tick();
            Check(mate.ReviveProgress == 0 && hint.Text == Loc.Text("hud_interaction_interrupted"), "local revive damage still explains its actual cancellation");
            p.HoldRevive(false); mate.Revive(); Tick();
            Check(hud.InteractionTarget == item, "revived teammate releases the prompt back to the nearby object");
            _stage.Interactables.Remove(item); item.Free(); Tick();
            Check(!panel.Visible && hud.InteractionTarget == null, "freed interaction leaves no ghost label or invalid reference");
            var high = new Interactable { Position = at + new Vector3(3, 3, 0), Prompt = "ui_use_medkit" };
            _stage.AddChild(high); _stage.AddInteractable(high); Advance(.3f);
            Check(hud.DiscoveryMarkerCount == 0, "discovery does not advertise an object on a different floor");
            high.Position = at + Vector3.Right * 30; Advance(.3f);
            Check(hud.DiscoveryMarkerCount == 0, "distant objects do not fill the screen with discovery markers");
            high.Position = at + Vector3.Right; high.Prompt = new string('W', 90); Advance(.3f); await Frames(3);
            var bounds = panel.GetGlobalRect();
            Check(bounds.Position.X >= 0 && bounds.End.X <= GetViewport().GetVisibleRect().Size.X && bounds.End.Y <= GetViewport().GetVisibleRect().Size.Y, "long action text stays inside viewport bounds");
            await Shot("long-label");
            high.Enabled = false; Advance(.3f);
            // Discovery cannot cross a wall, even inside the nearby-vision radius.
            high.Enabled = true; high.Position = at + Vector3.Right * 2.2f;
            var wall = new StaticBody3D { Position = at + new Vector3(1.1f, 1.1f, 0), CollisionLayer = Layers.SightMask, CollisionMask = 0 };
            wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(.2f, 3, 5) } });
            _stage.AddChild(wall); await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await Frames(2); Advance(.3f);
            Check(hud.DiscoveryMarkerCount == 0, "nearby discovery cannot reveal an item through a wall");
            wall.Free();
            high.Position = at + Vector3.Right; Advance(.3f);
            p.Invulnerable = false; p.TakeDamage(9999, Vector3.Zero, 0); Tick();
            Check(!panel.Visible && hud.DiscoveryMarkerCount == 0, "downed local player cannot retain an active interaction prompt");
            _stage.QueueFree(); await Frames(3);
            GameSettings.Reset(); GameSettings.Path = "user://interaction-ui-settings.cfg";
            var menu = new MainMenu { AppearancePath = "user://interaction-ui-appearance.cfg" };
            AddChild(menu); await Frames(5); await Shot("menu");
            menu.ShowPage(MenuPage.Graphics); await Frames(3);
            var low = (Button)menu.FindChild("Graphics_Shadow_Low", true, false);
            var highButton = (Button)menu.FindChild("Graphics_Shadow_High", true, false);
            low.EmitSignal(BaseButton.SignalName.Pressed);
            Check(low.ButtonPressed && !highButton.ButtonPressed && GameSettings.ShadowQuality == GraphicsQualityLevel.Low, "styled graphics buttons retain exclusive actual quality selection");
            await Shot("graphics");
            highButton.EmitSignal(BaseButton.SignalName.Pressed);
            Check(highButton.ButtonPressed && !low.ButtonPressed, "selected graphics state can switch back");
            menu.QueueFree(); await Frames(3); GameSettings.Reset();
            _stage = new Stage { DirectorEnabled = false, SquadOverride = 0, CharacterOverride = "goose", ModeOverride = GameMode.Survival };
            AddChild(_stage); await Frames(8);
            _stage.Player.ManualControl = true;
            foreach (var existing in _stage.Interactables) existing.Enabled = false;
            var supply = new Interactable { Position = _stage.Player.Position, Prompt = "ui_use_medkit" };
            _stage.AddChild(supply); _stage.AddInteractable(supply); await Frames(12);
            _stage.Hud.SetInventoryOpen(true); _stage.Hud._Process(.04);
            Check(_stage.Hud.InventoryOpen && !_stage.Hud.GetNode<Control>("HudRoot/WorldInteraction").Visible,
                "open inventory clears interaction prompts in survival mode");
            await Shot("inventory");
            _stage.Hud.SetPaused(true); _stage.Hud.OpenGraphicsOptions(); await Frames(3); await Shot("pause-graphics");
            _stage.Hud.SetPaused(false); _stage.QueueFree(); await Frames(3);
            foreach (string path in new[] { "user://interaction-ui-settings.cfg", "user://interaction-ui-appearance.cfg" })
                if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        }
        catch (Exception e) { failed = true; GD.PushError(e.ToString()); }
        finally
        {
            Input.ActionRelease(GameInput.Interact);
            GetTree().Paused = false;
            GD.Print($"InteractionUiCheck: {_checks} checks, {(failed ? "FAILED" : "PASSED")}");
            QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
        }
    }
}
