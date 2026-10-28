using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class ReticleLabCheck : Node
{
    private int _checks;
    private void Check(bool value, string message) { if (!value) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    public override async void _Ready()
    {
        bool failed = false;
        try
        {
            var lab = new CardLab();
            AddChild(lab);
            await Frames(6);
            var picker = lab.FindChild("ReticleWeaponPicker", true, false) as OptionButton;
            Check(picker != null, "reticle lab offers direct weapon selection");
            var p = lab.Stage.Player;
            p.ManualControl = true;
            foreach (var def in ArmoryCatalog.Weapons.Values.SelectMany(d => d.DualVariant is { } dual ? new[] { d, dual } : new[] { d }))
            {
                int found = Enumerable.Range(0, picker!.ItemCount).FirstOrDefault(i => picker.GetItemMetadata(i).AsString() == def.Id, -1);
                Check(found >= 0, def.Id + " is available including variants");
                picker.Select(found);
                picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)found);
                Check(p.Gun.Def.Id == def.Id && p.Gun.Mag == def.MagSize, "selection equips " + def.Id + " ready to compare");
            }
            string last = p.Gun.Def.Id;
            void Key(Key key) => lab._UnhandledInput(new InputEventKey { Keycode = key, Pressed = true });
            Key(Godot.Key.F12);
            Check(p.Gun.Def.Id != last, "next shortcut wraps the catalogue");
            Key(Godot.Key.F11);
            Check(p.Gun.Def.Id == last, "previous shortcut returns to the last weapon");
            int m4 = Enumerable.Range(0, picker!.ItemCount).Single(i => picker.GetItemMetadata(i).AsString() == "m4");
            picker.Select(m4); picker.EmitSignal(OptionButton.SignalName.ItemSelected, (long)m4);
            p.Gun.SetAmmo(1, 0);
            Check(p.Gun.StartReload(), "unlimited reserve still permits ordinary reload");
            Key(Godot.Key.F12);
            Check(!p.Gun.Reloading && p.Gun.Mag == p.Gun.Def.MagSize, "switching during reload clears old progress");
            p.Gun.SetAmmo(0, 0);
            Key(Godot.Key.Home);
            Check(p.Gun.Mag == p.Gun.Def.MagSize && p.Gun.HasInfiniteReserve && p.Invulnerable, "refill keeps invulnerability and unlimited reserve");
            Check(!lab.Rules.Trickle && !lab.Stage.AllEnemies.Any(), "quiet lab does not spawn threats automatically");
            if (DisplayServer.GetName() != "headless")
            {
                p.ManualControl = false;
                lab.Stage.Hud.CardWindowOpen = false;
                int beforeClick = p.Gun.Mag;
                var click = picker.GetViewportTransform() * picker.GetGlobalRect().GetCenter();
                Input.ParseInputEvent(new InputEventMouseMotion { Position = click, GlobalPosition = click });
                Input.ParseInputEvent(new InputEventMouseButton { Position = click, GlobalPosition = click, ButtonIndex = MouseButton.Left, Pressed = true });
                Input.FlushBufferedEvents();
                Check(lab.Stage.Hud.CardWindowOpen, "first picker click suppresses input before the next frame");
                Check(picker.GetPopup().Visible, "real click opens the weapon picker");
                await Frames(3);
                Input.ParseInputEvent(new InputEventMouseButton { Position = click, GlobalPosition = click, ButtonIndex = MouseButton.Left, Pressed = false });
                Check(p.Gun.Mag == beforeClick, "real picker click does not fire the weapon");
                picker.GetPopup().Hide();
                p.ManualControl = true;
                p.SetControls(Vector2.Zero, p.GlobalPosition + new Vector3(6, 1, 0), false);
                GetViewport().PushInput(new InputEventMouseMotion { Position = new(1000, 300), GlobalPosition = new(1000, 300) });
                await Frames(5);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                image.SavePng(ProjectSettings.GlobalizePath("res://output/reticle-lab.png"));
                picker.ShowPopup();
                await Frames(5);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var list = GetViewport().GetTexture().GetImage();
                list.SavePng(ProjectSettings.GlobalizePath("res://output/reticle-lab-list.png"));
                Check(lab.Stage.Hud.CardWindowOpen, "open weapon list suppresses gameplay input");
                picker.GetPopup().Hide();
                await Frames(15);
            }
            lab.QueueFree(); await Frames(3);
            GD.Print("RETICLE_LAB_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("RETICLE_LAB_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
}
