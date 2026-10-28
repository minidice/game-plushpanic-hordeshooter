using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Shared live graphics choices for the main menu and the pause subpage.</summary>
public partial class GraphicsOptions : VBoxContainer
{
    private readonly List<(Button High, Button Low, Func<bool> IsHigh)> _rows = new();

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 18);
        AddRow("Shadow", "settings_shadow_quality", "settings_shadow_hint",
            () => GameSettings.ShadowQuality == GraphicsQualityLevel.High,
            high => GameSettings.ShadowQuality = high ? GraphicsQualityLevel.High : GraphicsQualityLevel.Low);
        AddRow("Surface", "settings_surface_quality", "settings_surface_hint",
            () => GameSettings.SurfaceQuality == GraphicsQualityLevel.High,
            high => GameSettings.SurfaceQuality = high ? GraphicsQualityLevel.High : GraphicsQualityLevel.Low);
        AddRow("Pet", "settings_pet_quality", "settings_pet_hint",
            () => GameSettings.PetQuality == PetModelQuality.High,
            high => GameSettings.PetQuality = high ? PetModelQuality.High : PetModelQuality.Low);
        var hint = UiTheme.Label(Loc.Text(GameSettings.Loaded ? "settings_saved_hint" : "settings_not_saved_hint"), 14, UiTheme.Muted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(hint);
        GameSettings.GraphicsQualityChanged += Refresh;
        GameSettings.PetQualityChanged += PetQualityChanged;
        Refresh();
    }

    private void AddRow(string id, string labelKey, string hintKey, Func<bool> isHigh, Action<bool> choose)
    {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 6);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var label = UiTheme.Label(Loc.Text(labelKey), 17);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(label);
        var group = new ButtonGroup { AllowUnpress = false };
        Button Choice(bool high)
        {
            var button = new Button
            {
                Name = $"Graphics_{id}_{(high ? "High" : "Low")}",
                ToggleMode = true, ButtonGroup = group,
                FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Stop,
                CustomMinimumSize = new Vector2(142, 42),
            };
            button.AddThemeStyleboxOverride("pressed", UiTheme.Panel(new Color("384635"), 10, UiTheme.Accent));
            button.AddThemeStyleboxOverride("hover_pressed", UiTheme.Panel(new Color("43503c"), 10, UiTheme.Accent));
            button.Pressed += () => { choose(high); GameSettings.Save(); Refresh(); };
            row.AddChild(button);
            return button;
        }
        var highButton = Choice(true);
        var lowButton = Choice(false);
        _rows.Add((highButton, lowButton, isHigh));
        section.AddChild(row);
        var hint = UiTheme.Label(Loc.Text(hintKey), 14, UiTheme.Muted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        section.AddChild(hint);
        AddChild(section);
    }

    public void Refresh()
    {
        foreach (var (high, low, isHigh) in _rows)
        {
            bool selectedHigh = isHigh();
            ShowChoice(high, selectedHigh, "settings_quality_high");
            ShowChoice(low, !selectedHigh, "settings_quality_low");
        }
    }

    private static void ShowChoice(Button button, bool selected, string key)
    {
        button.SetPressedNoSignal(selected);
        button.Text = (selected ? "✓  " : "") + Loc.Text(key);
        foreach (var color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(color, selected ? UiTheme.Accent : UiTheme.Muted);
    }

    public void FocusSelected()
    {
        Refresh();
        if (_rows.Count > 0)
        {
            var row = _rows[0];
            (row.IsHigh() ? row.High : row.Low).GrabFocus();
        }
    }

    private void PetQualityChanged(PetModelQuality _) => Refresh();

    public override void _ExitTree()
    {
        GameSettings.GraphicsQualityChanged -= Refresh;
        GameSettings.PetQualityChanged -= PetQualityChanged;
    }
}
