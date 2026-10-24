using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

public partial class Hud
{
    private HBoxContainer _statusEffects = null!;
    private string _statusEffectIds = "";
    private readonly Dictionary<string, Label> _statusEffectTimes = new();

    private void BuildStatusEffects()
    {
        _statusEffects = Place(new HBoxContainer { Name = "StatusEffects" }, .5f, 1, new(0, -200), true, true, centerX: true);
        _statusEffects.AddThemeConstantOverride("separation", 10);
    }

    private void UpdateStatusEffects(Player player)
    {
        var states = player.Rig.GetNodeOrNull<StatusVisualController>("StatusVisuals")?.Active;
        string signature = states == null ? "" : string.Join(',', states.Select(s => s.Id));
        if (signature != _statusEffectIds)
        {
            _statusEffectIds = signature;
            foreach (var child in _statusEffects.GetChildren()) { _statusEffects.RemoveChild(child); child.QueueFree(); }
            _statusEffectTimes.Clear();
            if (states != null) foreach (var state in states)
            {
                var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                var icon = new TextureRect { Texture = StatusVisualIcons.Texture(state.Id), CustomMinimumSize = new(30, 30),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore };
                box.AddChild(icon);
                var label = HudLabel("", 13, HudWhite); label.HorizontalAlignment = HorizontalAlignment.Center;
                box.AddChild(label); _statusEffectTimes[state.Id] = label; _statusEffects.AddChild(box);
            }
        }
        if (states != null) foreach (var state in states)
            _statusEffectTimes[state.Id].Text = StatusVisualCatalog.Current.Get(state.Id).Name
                + (state.Remaining > 0 ? $" {state.Remaining:0.0}s" : "") + (state.Stacks > 1 ? $" ×{state.Stacks}" : "");
    }
}
