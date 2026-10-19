using Godot;
using System;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

// Actual stage/HUD screenshots plus camera and layout acceptance checks.
public partial class CaptureArtDirection : Node
{
    private Stage _stage = null!;
    private int _checks;
    private void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        _checks++; GD.Print("PASS: " + message);
    }

    public override async void _Ready()
    {
        try
        {
            _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false };
            AddChild(_stage);
            _stage.Player.ManualControl = true;
            _stage.Player.Invulnerable = true;
            foreach (var enemy in _stage.AllEnemies) enemy.QueueFree();
            _stage.Player.Weapons.Pickup(ArmoryCatalog.Weapon("m1911"), null);
            _stage.Player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
            _stage.Player.Loadout.AddThrowable(ArmoryCatalog.Throwable("frag"));
            _stage.Player.Medkits = 1;
            await Frames(3);
            var origin = _stage.Cam.UnprojectPosition(_stage.Player.GlobalPosition);
            var right = _stage.Cam.UnprojectPosition(_stage.Player.GlobalPosition + _stage.Cam.GroundMovement(Vector2.Right)) - origin;
            var up = _stage.Cam.UnprojectPosition(_stage.Player.GlobalPosition + _stage.Cam.GroundMovement(Vector2.Up)) - origin;
            Check(right.X > 0 && Mathf.Abs(right.Y) < .1f, "D moves screen-right with diagonal camera");
            Check(up.Y < 0 && Mathf.Abs(up.X) < .1f, "W moves screen-up with diagonal camera");
            Check(Mathf.IsEqualApprox(_stage.Cam.GroundMovement(Vector2.One).Length(), 1), "diagonal input stays normalized");
            foreach (var weapon in ArmoryCatalog.Weapons.Values)
                Check(weapon.LoadIcon() != null, weapon.Id + " inventory icon loads");
            await Place(new Vector3(4.5f, 0, 0), new Vector3(7, 1, 4));
            CheckDock(); await Shot("art-saferoom");
            _stage.LeaveSafeRoom();
            await Place(new Vector3(65.5f, 0, .5f), new Vector3(70, 1, -2));
            await Shot("art-warehouse");
            _stage.Cam.Close = true; await Frames(45); await Shot("art-warehouse-close");
            _stage.Cam.Close = false;
            await Place(new Vector3(26, 0, 0), new Vector3(31, 1, -1));
            await Shot("art-street");
            GetWindow().Size = new Vector2I(1280, 720);
            await Place(new Vector3(65.5f, 0, .5f), new Vector3(70, 1, -2));
            CheckDock(); await Shot("art-warehouse-720p");
            _stage.Player.Weapons.Pickup(ArmoryCatalog.Weapon("double_barrel"), null);
            _stage.Player.Gun.Trigger(true, true);
            _stage.Player.Gun.StartReload();
            await Frames(2);
            Check(((Label)_stage.FindChild("ActiveAmmo", true, false)!).Text == Loc.Text("ui_hud_reload"), "HUD shows live reload state");
            CheckDock();
            _stage.Player.Weapons.BeginThrowAim(); await Frames(2);
            Check(((Label)_stage.FindChild("Status", true, false)!).Text.Contains(ArmoryCatalog.Throwable("frag").DisplayName), "throw aim HUD identifies held grenade");
            _stage.Player.Weapons.EndThrowAim();
            _stage.QueueFree(); await Frames(3);
            GD.Print("ART_DIRECTION_PASS " + _checks);
            QuitHygiene.Collect(); GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }

    private void CheckDock()
    {
        var dock = (Control)_stage.FindChild("EquipmentDock", true, false)!;
        var bounds = dock.GetGlobalRect();
        var viewport = GetViewport().GetVisibleRect();
        Check(bounds.Position.X >= 0 && bounds.End.X <= viewport.Size.X && bounds.End.Y <= viewport.Size.Y,
            "equipment dock fits viewport " + viewport.Size);
        var children = dock.GetChildren();
        for (int i = 1; i < children.Count; i++)
            Check(((Control)children[i - 1]).GetGlobalRect().End.X <= ((Control)children[i]).GetGlobalRect().Position.X,
                "dock items do not overlap " + i);
    }
    private async Task Place(Vector3 at, Vector3 aim)
    {
        _stage.Player.GlobalPosition = at;
        _stage.Player.SetControls(Vector2.Zero, aim, false);
        _stage.Cam.SnapTo(at);
        await Frames(45);
    }
    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private async Task Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/" + name + ".png"));
        Check(error == Error.Ok, "saved " + name);
    }
}
