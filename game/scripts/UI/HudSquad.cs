using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Teammate list under the run stats: name, health bar and state for every other survivor.</summary>
public partial class Hud
{
    private VBoxContainer? _squadBox;
    private readonly Dictionary<Player, (Label Name, ColorRect Fill, Label State)> _squadRows = new();
    private const float SquadBarWidth = 150f;

    private void UpdateSquadPanel(Stage stage)
    {
        foreach (var survivor in stage.Players)
        {
            if (survivor == stage.Player || !IsInstanceValid(survivor) || _squadRows.ContainsKey(survivor)) continue;
            AddSquadRow(survivor);
        }
        // A respawned squadmate replaces the fallen one in the list.
        foreach (var gone in System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(_squadRows.Keys, k => !IsInstanceValid(k) || !stage.Players.Contains(k))))
        {
            if (IsInstanceValid(_squadRows[gone].Name)) _squadRows[gone].Name.GetParent().QueueFree();
            _squadRows.Remove(gone);
        }
        foreach (var (survivor, row) in _squadRows)
        {
            if (!IsInstanceValid(survivor)) continue;
            float hp = survivor.MaxHealth > 0 ? Mathf.Clamp((float)survivor.Health / survivor.MaxHealth, 0, 1) : 0;
            row.Fill.Size = new Vector2(SquadBarWidth * hp, row.Fill.Size.Y);
            row.Fill.Color = hp > .5f ? UiTheme.Heal : hp > .25f ? UiTheme.Accent : UiTheme.Danger;
            row.State.Text = survivor.IsDead ? Loc.Text("hud_squad_dead")
                : survivor.IsDowned ? Loc.Format("fmt_hud_squad_down", Mathf.CeilToInt(Mathf.Max(0, survivor.BleedLeft)))
                : survivor.IsRestrained ? Loc.Text("hud_squad_held")
                : "";
            // Numbers for the same character can change as people come and go (M-30).
            row.Name.Text = survivor.DisplayLabel.Length > 0 ? survivor.DisplayLabel : Loc.Text(survivor.Definition.DisplayName);
            row.Name.Modulate = survivor.IsDead ? new Color(1, 1, 1, .45f) : Colors.White;
        }
    }

    private void AddSquadRow(Player survivor)
    {
        if (_squadBox == null)
        {
            var panel = Place(new PanelContainer { Name = "SquadPanel" }, 1, 0, new Vector2(-24, 84), true, false);
            panel.AddThemeStyleboxOverride("panel", HudPlate());
            _squadBox = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            _squadBox.AddThemeConstantOverride("separation", 6);
            panel.AddChild(_squadBox);
        }
        var line = new HBoxContainer { Name = "Squad_" + survivor.Name, MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 8);
        var name = HudLabel(survivor.DisplayLabel.Length > 0 ? survivor.DisplayLabel : Loc.Text(survivor.Definition.DisplayName), 14);
        name.CustomMinimumSize = new Vector2(120, 0);
        line.AddChild(name);
        var bar = new Control { CustomMinimumSize = new Vector2(SquadBarWidth, 10), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        bar.AddChild(new ColorRect { Color = new Color(0, 0, 0, .55f), Size = new Vector2(SquadBarWidth, 10), MouseFilter = Control.MouseFilterEnum.Ignore });
        var fill = new ColorRect { Color = UiTheme.Heal, Size = new Vector2(SquadBarWidth, 10), MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddChild(fill);
        line.AddChild(bar);
        var state = HudLabel("", 13, UiTheme.Danger);
        state.CustomMinimumSize = new Vector2(64, 0);
        line.AddChild(state);
        _squadBox.AddChild(line);
        _squadRows[survivor] = (name, fill, state);
    }
}
