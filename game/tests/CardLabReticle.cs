using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>Opt-in presentation lab. Reuses the peaceful field, invulnerability and unlimited reserves of CardLab.</summary>
public partial class CardLab
{
    private List<WeaponDefinition> _reticleWeapons = new();
    private OptionButton _weaponPicker = null!;
    private PanelContainer _reticlePanel = null!;
    private int _reticleWeapon;

    private void BuildReticleLab()
    {
        _reticleWeapons = ArmoryCatalog.Weapons.Values
            .SelectMany(d => d.DualVariant is { } dual ? new[] { d, dual } : new[] { d }).ToList();
        var layer = new CanvasLayer { Layer = 11 };
        AddChild(layer);
        _reticlePanel = new PanelContainer { Name = "ReticleLabPanel", Position = new(24, 180) };
        _reticlePanel.AddThemeStyleboxOverride("panel", Hud.HudPlate(pad: 14));
        layer.AddChild(_reticlePanel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        _reticlePanel.AddChild(box);
        box.AddChild(Hud.HudLabel(Loc.Text("reticle_lab_title"), 20, Hud.HudWarm));
        box.AddChild(Hud.HudLabel(Loc.Text("reticle_lab_safe"), 13, Hud.HudMuted));
        _weaponPicker = new OptionButton { Name = "ReticleWeaponPicker", CustomMinimumSize = new(300, 38), FocusMode = Control.FocusModeEnum.None };
        for (int i = 0; i < _reticleWeapons.Count; i++)
        {
            _weaponPicker.AddItem($"{i + 1:00}  {_reticleWeapons[i].DisplayName}");
            _weaponPicker.SetItemMetadata(i, _reticleWeapons[i].Id);
        }
        _weaponPicker.ItemSelected += i => SelectReticleWeapon((int)i);
        _weaponPicker.GetPopup().AboutToPopup += () => SuppressReticleInput(true);
        box.AddChild(_weaponPicker);
        var row = new HBoxContainer();
        box.AddChild(row);
        foreach (var (text, step) in new[] { ("reticle_lab_previous", -1), ("reticle_lab_next", 1) })
        {
            var button = new Button { Text = Loc.Text(text), CustomMinimumSize = new(145, 36), FocusMode = Control.FocusModeEnum.None };
            button.Pressed += () => SelectReticleWeapon(_reticleWeapon + step);
            row.AddChild(button);
        }
        box.AddChild(Hud.HudLabel(Loc.Text("reticle_lab_keys"), 13, Hud.HudWhite));
        SelectReticleWeapon(_reticleWeapons.FindIndex(d => d.Id == "m4"));
    }

    private void SelectReticleWeapon(int index)
    {
        _reticleWeapon = (index % _reticleWeapons.Count + _reticleWeapons.Count) % _reticleWeapons.Count;
        var p = Stage.Player;
        p.Weapons.CancelWindup();
        p.Weapons.EndThrowAim();
        p.SuppressMenuInput();
        p.Recoil.Clear();
        p.Loadout.SetSlot(2, _reticleWeapons[_reticleWeapon]);
        p.Weapons.RefreshVisual();
        _weaponPicker.Select(_reticleWeapon);
    }

    private void UpdateReticleLab()
    {
        // Player input already respects CardWindowOpen. A picker click must not also fire the newly selected gun.
        SuppressReticleInput(_weaponPicker.GetPopup().Visible
            || _reticlePanel.GetGlobalRect().HasPoint(_reticlePanel.GetGlobalMousePosition()));
    }

    public override void _Input(InputEvent e)
    {
        if (!ReticleOnly || _reticlePanel == null || e is not InputEventMouse mouse) return;
        // Input arrives before physics polls the fire action, even on the first frame of pointer entry.
        SuppressReticleInput(_weaponPicker.GetPopup().Visible
            || _reticlePanel.GetGlobalRect().HasPoint(mouse.Position));
    }

    private void SuppressReticleInput(bool blocked)
    {
        Stage.Hud.CardWindowOpen = blocked;
        if (blocked) Stage.Player.SuppressMenuInput();
    }

    private void ReticleLabKey(Key key)
    {
        if (Stage.Hud.Paused) return;
        switch (key)
        {
            case Key.F11: SelectReticleWeapon(_reticleWeapon - 1); break;
            case Key.F12: SelectReticleWeapon(_reticleWeapon + 1); break;
            case Key.Home: SelectReticleWeapon(_reticleWeapon); break;
            case Key.T: Rules.SpawnDummy(Stage.Player.AimPoint); break;
            case Key.F7: Rules.SpawnRing(14, 7, 11); break;
            case Key.Backspace: Rules.ClearEnemies(); Rules.RemoveDummies(); break;
            case Key.F10: Engine.TimeScale = Engine.TimeScale < 1 ? 1 : .35; break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }
}
