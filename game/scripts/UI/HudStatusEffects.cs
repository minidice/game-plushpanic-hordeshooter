using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

public partial class Hud
{
    private VBoxContainer _statusEffects = null!;
    private string _statusEffectIds = "";
    private readonly Dictionary<string, Label> _statusEffectTimes = new();

    private void BuildStatusEffects()
    {
        // Bottom left, one small row each, newest at the bottom (decisions.md M-42): out of the way of the bottom row.
        _statusEffects = Place(new VBoxContainer { Name = "StatusEffects" }, 0, 1, new(24, -24), false, true);
        _statusEffects.AddThemeConstantOverride("separation", 4);
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
                var box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
                box.AddThemeConstantOverride("separation", 5);
                var icon = new TextureRect { Texture = StatusVisualIcons.Texture(state.Id), CustomMinimumSize = new(22, 22),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore };
                box.AddChild(icon);
                var label = HudLabel("", 11, HudWhite);
                box.AddChild(Pill(label, PillDark)); _statusEffectTimes[state.Id] = label; _statusEffects.AddChild(box);
            }
        }
        if (states != null) foreach (var state in states)
            _statusEffectTimes[state.Id].Text = StatusVisualCatalog.Current.Get(state.Id).Name
                + (state.Remaining > 0 ? $" {state.Remaining:0.0}s" : "") + (state.Stacks > 1 ? $" ×{state.Stacks}" : "");
    }
}
