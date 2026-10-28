using Godot;
using System;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Compare the actual HUD on a frozen stage. A removable black plate isolates UI pixels from animated scenery.</summary>
public partial class ReticleCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private Control _reticle = null!;
    private ColorRect _plate = null!;
    private Vector2 _center;
    private string _suffix = "";
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            if (DisplayServer.GetName() == "headless") throw new Exception("ReticleCheck requires a rendered window");
            bool outdoor = Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--outdoor");
            if (Array.Exists(OS.GetCmdlineUserArgs(), a => a == "--english")) Loc.Language = "eng";
            _suffix = outdoor ? "-outdoor" : "";
            _stage = new Stage { DirectorEnabled = false, CharacterOverride = "goose" };
            AddChild(_stage);
            await Frames(8);
            var p = _stage.Player;
            p.ManualControl = true;
            p.Invulnerable = true;
            if (outdoor) { _stage.LeaveSafeRoom(); p.GlobalPosition = new Vector3(21, 0, .5f); _stage.Cam.SnapTo(p.GlobalPosition); }
            p.SetControls(Vector2.Zero, p.GlobalPosition + new Vector3(6, 1, -2), false);
            await Frames(20);
            p.SetPhysicsProcess(false);
            _stage.ProcessMode = ProcessModeEnum.Disabled;
            _stage.Hud.SetProcess(false);
            _reticle = _stage.Hud.GetNode<Control>("HudRoot/Reticle");
            _center = _stage.Cam.UnprojectPosition(p.AimPoint);
            _plate = new ColorRect { Color = Colors.Black, Position = _center - new Vector2(180, 180), Size = new(360, 360), MouseFilter = Control.MouseFilterEnum.Ignore };
            _reticle.GetParent().AddChild(_plate);
            _reticle.GetParent().MoveChild(_plate, _reticle.GetIndex());

            Equip("m4");
            using var full = await Capture("m4-full");
            // Real magazine consumption, not a second presentation model.
            for (int i = 0; i < 25; i++) { p.Gun.Tick(.11f, false, true); Check(p.Gun.Trigger(true, true), "M4 consumes a round"); }
            p.Gun.Tick(2, false, true);
            using var half = await Capture("m4-half");
            Check(Difference(full, half, true) > 25, "outer ammo arc visibly changes when half the magazine is consumed");
            using var halfScene = await Capture("scene-half", false);
            p.Gun.SetAmmo(0, 100);
            using var empty = await Capture("m4-empty");
            Check(Difference(full, empty, true) > Difference(full, half, true), "empty magazine removes more fill than half magazine while retaining its track and mode");
            _plate.Color = new Color(.88f, .88f, .88f);
            Equip("m4");
            using var bright = await Capture("bright-background");
            _plate.Color = Colors.Black;

            foreach (string id in new[] { "m1911", "mp5", "m4", "m16", "m249", "r870", "benelli_m4", "double_barrel", "awm", "svd", "m82" })
            {
                Equip(id);
                using var shot = await Capture(id, false);
            }
            // Identical minimum-radius cones must still have distinct family silhouettes.
            p.SetControls(Vector2.Zero, p.GlobalPosition + new Vector3(1, 1, 0), false);
            _center = _stage.Cam.UnprojectPosition(p.AimPoint);
            _plate.Position = _center - new Vector2(180, 180);
            Equip("m4");
            using var rifle = await Capture("isolated-rifle");
            foreach (string id in new[] { "m1911", "mp5", "m249", "r870", "awm", "svd" })
            {
                Equip(id);
                using var shape = await Capture("isolated-" + id);
                Check(Difference(rifle, shape, false) > 12, id + " has a distinct family silhouette");
            }
            Equip("m16");
            using var burst = await Capture("isolated-burst");
            Check(Difference(rifle, burst, true) > 10, "same rifle family exposes burst versus automatic glyph");
            Equip("m1911");
            using var pistolFull = await Capture("pistol-full");
            p.Gun.SetAmmo(7, 0);
            using var pistolHalf = await Capture("pistol-half");
            Check(Difference(pistolFull, pistolHalf, true) > 20, "infinite reserve pistol still shows finite magazine fraction");

            Equip("m4");
            p.Gun.SetAmmo(25, 100);
            using var before = await Capture("before-reload");
            Check(p.Gun.StartReload(), "reload starts with a half magazine");
            using var enter = await Capture("reload-enter", true, .04f);
            using var enterScene = await Capture("reload-enter-scene", false, 0);
            Check(Difference(before, enter, true) > 10, "quarter-entry visibly moves the magazine track toward the action ring");
            p.Gun.Tick(p.Gun.ReloadTime * .5f, false, true);
            using var reload = await Capture("reload-half", false);
            using var reloadIsolated = await Capture("reload-half-isolated", true, 0);
            Check(Difference(enter, reloadIsolated, false) > 10, "entry is visually distinct from the completed ring transition");
            p.Gun.CancelReload();
            using var cancel = await Capture("reload-cancel", false, .04f);
            using var restored = await Capture("restored");
            Check(Difference(before, restored, true) == 0, "cancel returns to actual half magazine without a false full arc");
            p.Gun.StartReload();
            _stage.Hud._Process(.2);
            p.Gun.Tick(p.Gun.ReloadTime + .01f, false, true);
            using var done = await Capture("reload-complete", false, .04f);
            using var ready = await Capture("reload-returned", false);
            Check(p.Gun.Mag == p.Gun.Def.MagSize && p.Gun.CanFire, "completed reload returns to a full ready magazine");

            Equip("r870");
            p.Gun.SetAmmo(0, 2);
            p.Gun.StartReload();
            p.Gun.Tick(p.Gun.ReloadTime + .01f, false, true);
            using var shell = await Capture("shell-insert", false);
            p.Gun.CancelReload();
            using var shellReturn = await Capture("shell-cancel", false);
            Check(p.Gun.Mag == 1, "interrupted tube reload retains the inserted round");
            Equip("m16");
            using var switched = await Capture("switch-after-reload");
            Check(Difference(burst, switched, true) == 0, "weapon switch restores its own mode and magazine without stale progress");
            Equip("frying_pan");
            using var melee = await Capture("melee");
            Check(BrightOuter(melee) == 0, "melee has no ammunition or gun mode glyph");
            Equip("m4");
            p.NetRestrained = true;
            using var blocked = await Capture("attack-blocked");
            Check(BrightOuter(blocked) == 0, "attack restriction replaces gun readouts with the unavailable cursor");
            p.NetRestrained = false;
            foreach (string id in new[] { "honk", "rubber_duck" })
            {
                Equip(id);
                using var special = await Capture(id);
                Check(BrightOuter(special) == 0, id + " does not acquire a firearm readout");
            }
            // Cursor aim can reach viewport edges. Auxiliary readouts must remain inside, without shifting aim.
            var size = GetViewport().GetVisibleRect().Size;
            Equip("m4");
            foreach (var edge in new[] { new Vector2(size.X - 8, size.Y * .5f), new Vector2(size.X * .5f, size.Y - 8), size - new Vector2(8, 8) })
            {
                var camera = _stage.Cam;
                var point = new Plane(Vector3.Up, p.GlobalPosition.Y + Player.AimHeight)
                    .IntersectsRay(camera.ProjectRayOrigin(edge), camera.ProjectRayNormal(edge));
                Check(point.HasValue, "edge cursor intersects aim plane");
                p.SetControls(Vector2.Zero, point!.Value, false);
                _center = camera.UnprojectPosition(p.AimPoint);
                _plate.Position = _center - new Vector2(180, 180);
                p.Gun.SetAmmo(50, 100);
                using var edgeFull = await Capture("edge-full-" + (int)edge.X + "-" + (int)edge.Y);
                p.Gun.SetAmmo(25, 100);
                using var edgeHalf = await Capture("edge-half-" + (int)edge.X + "-" + (int)edge.Y);
                Check(OuterDifference(edgeFull, edgeHalf) > 20, "edge aim retains a readable magazine fraction inside the viewport");
            }
            await Frames(15);
            _stage.QueueFree();
            await Frames(3);
            GD.Print("RETICLE_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("RETICLE_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
    private void Equip(string id)
    {
        _stage.Player.Loadout.SetSlot(2, ArmoryCatalog.Weapon(id));
        _stage.Player.Weapons.SelectSlot(2);
        _stage.Player.Gun.Tick(2, false, true);
    }
    private async Task<Image> Capture(string name, bool isolate = true, float dt = .25f)
    {
        _plate.Visible = isolate;
        _stage.Hud._Process(dt);
        _reticle.QueueRedraw();
        await Frames(3);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var im = GetViewport().GetTexture().GetImage();
        im.SavePng(ProjectSettings.GlobalizePath($"res://output/reticle-{name}{_suffix}.png"));
        return im;
    }
    private int Difference(Image a, Image b, bool outer)
    {
        var scale = new Vector2(a.GetWidth(), a.GetHeight()) / GetViewport().GetVisibleRect().Size;
        int count = 0;
        for (int y = 0; y < a.GetHeight(); y++)
        for (int x = 0; x < a.GetWidth(); x++)
        {
            var offset = new Vector2(x / scale.X, y / scale.Y) - _center;
            float r = offset.Length();
            if (outer ? !(r > 40 && r < 160 && offset.X > 0 && offset.Y > -10) : r > 38) continue;
            var ca = a.GetPixel(x, y); var cb = b.GetPixel(x, y);
            if (Mathf.Abs(ca.R - cb.R) + Mathf.Abs(ca.G - cb.G) + Mathf.Abs(ca.B - cb.B) > .25f) count++;
        }
        return count;
    }
    private int BrightOuter(Image im)
    {
        var scale = new Vector2(im.GetWidth(), im.GetHeight()) / GetViewport().GetVisibleRect().Size;
        int count = 0;
        for (int y = 0; y < im.GetHeight(); y++)
        for (int x = 0; x < im.GetWidth(); x++)
        {
            var o = new Vector2(x / scale.X, y / scale.Y) - _center;
            if (o.Length() > 40 && o.Length() < 160 && o.X > 0 && o.Y > -10 && im.GetPixel(x, y).R > .3f) count++;
        }
        return count;
    }
    private int OuterDifference(Image a, Image b)
    {
        var scale = new Vector2(a.GetWidth(), a.GetHeight()) / GetViewport().GetVisibleRect().Size;
        int count = 0;
        for (int y = 0; y < a.GetHeight(); y++)
        for (int x = 0; x < a.GetWidth(); x++)
        {
            float r = (new Vector2(x / scale.X, y / scale.Y) - _center).Length();
            if (r < 40 || r > 160) continue;
            var ca = a.GetPixel(x, y); var cb = b.GetPixel(x, y);
            if (Mathf.Abs(ca.R - cb.R) + Mathf.Abs(ca.G - cb.G) + Mathf.Abs(ca.B - cb.B) > .25f) count++;
        }
        return count;
    }
}
