using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>World-attached presentation reads the actual interaction target; never chooses a different E action.</summary>
public partial class Hud
{
    private PanelContainer _worldInteraction = null!;
    private Label _interactionTitle = null!, _interactionKey = null!, _interactionHint = null!;
    private Paint _interactionGauge = null!, _interactionMarkers = null!;
    private Node3D? _interactionTarget;
    private Interactable? _submittedHold;
    private float _interactionBlend, _interactionProgress, _interruptLeft, _completeLeft, _markerClock;
    private bool _interactionActive, _reviveWasLocal;
    private readonly List<Interactable> _discoverable = new();
    private readonly StyleBoxFlat _keyPlate = UiTheme.Plate(UiTheme.SelectedSurface, new Color(UiTheme.Selection, .3f), 6, 6);
    private bool ShowInteractions => Stage.State == StageState.Playing && !Stage.Player.IsDead && !Stage.Player.IsDowned
        && !Paused && !Stage.AllStopped && !InventoryOpen && !CardWindowOpen && !Stage.Player.IsRestrained;
    internal int DiscoveryMarkerCount => _discoverable.Count;
    internal Node3D? InteractionTarget => _interactionActive ? _interactionTarget : null;

    private void BuildInteraction()
    {
        _interactionMarkers = new Paint { Name = "InteractionMarkers", Painter = DrawInteractionMarkers };
        _interactionMarkers.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_interactionMarkers);
        _worldInteraction = new PanelContainer { Name = "WorldInteraction", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _worldInteraction.AddThemeStyleboxOverride("panel", UiTheme.Plate(new Color(.025f, .045f, .075f, .92f), new Color(UiTheme.Selection, .22f), 10, 12));
        _root.AddChild(_worldInteraction);
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 5);
        _worldInteraction.AddChild(box);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 12);
        box.AddChild(row);
        _interactionTitle = HudLabel("", 17);
        _interactionTitle.Name = "InteractionTitle";
        _interactionTitle.CustomMinimumSize = new(90, 0);
        _interactionTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _interactionTitle.VerticalAlignment = VerticalAlignment.Center;
        _interactionTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        row.AddChild(_interactionTitle);
        var keyBox = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        keyBox.AddThemeStyleboxOverride("panel", _keyPlate);
        row.AddChild(keyBox);
        _interactionKey = HudLabel("E", 15, UiTheme.Selection);
        _interactionKey.Name = "InteractionKey";
        _interactionKey.HorizontalAlignment = HorizontalAlignment.Center;
        _interactionKey.CustomMinimumSize = new(18, 0);
        keyBox.AddChild(_interactionKey);
        _interactionHint = HudLabel("", 13, HudMuted);
        _interactionHint.Name = "InteractionHint";
        box.AddChild(_interactionHint);
        _interactionGauge = new Paint { Name = "InteractionProgress", CustomMinimumSize = new(0, 12),
            Painter = c => UiTheme.DrawProgress(c, new Rect2(Vector2.Zero, c.Size), _interactionProgress, 2) };
        box.AddChild(_interactionGauge);
        // World labels are below the aiming layer, including the magazine arc and hit feedback.
        _root.MoveChild(_interactionMarkers, _root.GetNode("Reticle").GetIndex());
        _root.MoveChild(_worldInteraction, _root.GetNode("Reticle").GetIndex());
        Stage.Player.Hurt += OnInteractionHurt;
        Stage.HoldSubmitted += item => _submittedHold = item;
    }

    private void OnInteractionHurt()
    {
        // TakeDamage clears the local revive before raising Hurt. Use the last presented owner,
        // and require the actual progress to have stopped; another person's revive is unaffected.
        bool cancelledRevive = _reviveWasLocal && _interactionTarget is Player { ReviveProgress: <= 0 };
        if (_interactionProgress > 0 && (_interactionTarget is Interactable { CancelOnHit: true } || cancelledRevive))
        {
            _interruptLeft = .75f;
            _submittedHold = null;
        }
        _reviveWasLocal = false;
    }

    private static string SurvivorLabel(Player player) => player.DisplayLabel.Length > 0
        ? player.DisplayLabel : Loc.Text(player.Definition.DisplayName);

    private static bool Live(Node3D? target) => target != null && IsInstanceValid(target) && target.IsInsideTree() && !target.IsQueuedForDeletion();

    private void ClearInteraction()
    {
        _interactionTarget = null;
        _submittedHold = null;
        _interactionBlend = _interactionProgress = _interruptLeft = _completeLeft = 0;
        _interactionActive = _reviveWasLocal = false;
        _worldInteraction.Visible = _interactionMarkers.Visible = false;
        _discoverable.Clear();
    }

    private void UpdateInteractionUi(float dt)
    {
        if (!ShowInteractions) { ClearInteraction(); return; }
        var stage = Stage;
        _interactionMarkers.Visible = true;
        _interruptLeft = Mathf.Max(0, _interruptLeft - dt);
        bool finishing = _completeLeft > 0;
        _completeLeft = Mathf.Max(0, _completeLeft - dt);
        if (finishing && _completeLeft == 0) _interactionProgress = 0;
        Node3D? next = stage.RevivePromptTarget ?? (Node3D?)stage.FocusedInteractable;
        if (!Live(next) || next is Interactable { Enabled: false }) next = null;
        if (_interactionTarget != null && !Live(_interactionTarget)) ClearInteraction();
        // Only a real world state change can display completion. A local client request alone cannot.
        if (_completeLeft == 0 && (_interactionProgress > 0 || _submittedHold == _interactionTarget && _submittedHold != null) && Live(_interactionTarget)
            && (_interactionTarget is Interactable { Enabled: false }
                || _interactionTarget is Player { IsDowned: false, IsDead: false }))
        {
            _completeLeft = .3f;
            _submittedHold = null;
        }
        if (_submittedHold != null && (!Live(_submittedHold) || next != _submittedHold)) _submittedHold = null;
        if (next != null && next != _interactionTarget)
        {
            _interactionTarget = next;
            _interactionProgress = _interruptLeft = _completeLeft = 0;
        }
        _interactionActive = next != null && _completeLeft == 0;
        bool retain = _interactionActive || _completeLeft > 0;
        _interactionBlend = Mathf.MoveToward(_interactionBlend, retain ? 1 : 0,
            Mathf.Max(0, dt) / (retain ? UiTheme.EnterSeconds : UiTheme.ExitSeconds));
        if (_interactionBlend == 0) _interactionTarget = null;
        _worldInteraction.Visible = Live(_interactionTarget) && _interactionBlend > 0;
        if (_worldInteraction.Visible)
        {
            bool hold = _interactionTarget is Player || _interactionTarget is Interactable { HoldSeconds: > 0 };
            float progress = _interactionTarget is Player downed ? downed.ReviveProgress : stage.HoldProgress;
            _interactionProgress = _completeLeft > 0 ? 1 : _interactionActive ? progress : 0;
            _interactionTitle.Text = _interactionTarget is Interactable item ? item.Prompt
                : Loc.Format("hud_interaction_revive", SurvivorLabel((Player)_interactionTarget!));
            bool otherReviver = _interactionTarget is Player { RevivedBy: { } owner } && owner != stage.Player;
            bool blocked = !_interactionActive || stage.HoldCancelled || otherReviver;
            _interactionKey.Text = _completeLeft > 0 ? "✓" : "E";
            _interactionKey.Modulate = blocked ? new Color(1, 1, 1, .35f) : Colors.White;
            _keyPlate.BorderColor = new Color(UiTheme.Selection, blocked ? .06f : .35f);
            _interactionHint.Text = _completeLeft > 0 ? Loc.Text("hud_interaction_done")
                : _interruptLeft > 0 ? Loc.Text("hud_interaction_interrupted")
                : otherReviver ? Loc.Text("hud_interaction_other_reviver")
                : _interactionProgress > 0 ? $"{Loc.Text("hud_interaction_hold")}  ·  {Mathf.Min(99, Mathf.FloorToInt(_interactionProgress * 100))}%"
                : blocked ? Loc.Text(stage.HoldCancelled ? "hud_interaction_release" : "hud_interaction_out_of_reach")
                : hold ? Loc.Text("hud_interaction_hold") : "";
            _interactionHint.AddThemeColorOverride("font_color", _interruptLeft > 0 ? UiTheme.Danger : HudMuted);
            _interactionHint.Visible = _interactionHint.Text.Length > 0;
            _interactionGauge.Visible = _interactionProgress > 0;
            float weight = _interactionBlend * _interactionBlend * (3 - 2 * _interactionBlend);
            _worldInteraction.Modulate = new Color(1, 1, 1, weight);
            PlaceInteraction(weight);
        }
        // L-35 keeps the lower percentage while the world label identifies the object.
        if (_interruptLeft > 0) _prompt.Text = Loc.Text("hud_interaction_interrupted");
        else if (_interactionActive && _interactionProgress > 0)
            _prompt.Text = $"{_interactionTitle.Text}  ·  {Mathf.Min(99, Mathf.FloorToInt(_interactionProgress * 100))}%";
        else if (!stage.Player.IsRestrained) _prompt.Text = "";
        _reviveWasLocal = _interactionActive && _interactionTarget is Player reviving
            && reviving.RevivedBy == stage.Player && stage.Player.Reviving == reviving;
        UpdateDiscovery(dt);
    }

    private Vector2 InteractionAnchor(Node3D target) => Stage.Cam.UnprojectPosition(target.GlobalPosition + Vector3.Up * (target is Player ? 1.5f : .75f));

    private void PlaceInteraction(float weight)
    {
        var viewport = _root.Size;
        // Fixed width cap avoids label/layout feedback loops on long translated item names.
        float titleWidth = _interactionTitle.GetThemeFont("font").GetStringSize(_interactionTitle.Text, fontSize: 17).X;
        float width = Mathf.Min(Mathf.Clamp(titleWidth + 78, 190, 330), viewport.X - 32);
        _interactionTitle.CustomMinimumSize = new(width - 75, 0);
        _worldInteraction.ResetSize();
        var size = _worldInteraction.GetCombinedMinimumSize();
        var anchor = InteractionAnchor(_interactionTarget!);
        var at = anchor + new Vector2(22, -size.Y - 8 + (1 - weight) * 5);
        var aim = Stage.Cam.UnprojectPosition(Stage.Player.AimPoint);
        if (new Rect2(at, size).Grow(36).HasPoint(aim)) at.X = anchor.X - size.X - 22;
        at.X = Mathf.Clamp(at.X, 16, Mathf.Max(16, viewport.X - size.X - 16));
        at.Y = Mathf.Clamp(at.Y, 150, Mathf.Max(150, viewport.Y - size.Y - 270));
        _worldInteraction.Position = at;
    }

    private void UpdateDiscovery(float dt)
    {
        _markerClock -= dt;
        if (_markerClock > 0) return;
        _markerClock = .12f;
        _discoverable.Clear();
        var p = Stage.Player;
        foreach (var item in Stage.Interactables)
        {
            if (!Live(item) || !item.Enabled || !item.IsVisibleInTree()) continue;
            var pos = item.GlobalPosition;
            if (pos.DistanceSquaredTo(p.GlobalPosition) > 49 || Mathf.Abs(pos.Y - p.GlobalPosition.Y) > 1.5f) continue;
            if (Stage.Cam.IsPositionBehind(pos) || !Stage.Vision.CanSee(pos)) continue;
            // Discovery must not reveal a neighbouring room through a wall (NearRadius alone bypasses vision rays).
            if (!Rays.Clear(p, p.GlobalPosition + Vector3.Up * 1.1f, pos + Vector3.Up * 1.1f, Layers.SightMask)) continue;
            _discoverable.Add(item);
        }
        _discoverable.Sort((a, b) => a.GlobalPosition.DistanceSquaredTo(p.GlobalPosition).CompareTo(b.GlobalPosition.DistanceSquaredTo(p.GlobalPosition)));
        if (_discoverable.Count > 12) _discoverable.RemoveRange(12, _discoverable.Count - 12);
    }

    private void DrawInteractionMarkers(Control c)
    {
        if (!ShowInteractions) return;
        foreach (var item in _discoverable)
        {
            if (!Live(item) || !item.Enabled || !item.IsVisibleInTree() || item == _interactionTarget) continue;
            var at = InteractionAnchor(item);
            if (!new Rect2(20, 140, c.Size.X - 40, c.Size.Y - 300).HasPoint(at)) continue;
            c.DrawCircle(at, 7, new Color(UiTheme.Glass, .8f), true, -1, true);
            c.DrawArc(at, 5, 0, Mathf.Tau, 24, new Color(UiTheme.SightTint, .65f), 1.4f, true);
            c.DrawCircle(at, 1.8f, UiTheme.Paper, true, -1, true);
        }
        if (!_worldInteraction.Visible || !Live(_interactionTarget)) return;
        var anchor = InteractionAnchor(_interactionTarget!);
        var rect = new Rect2(_worldInteraction.Position, _worldInteraction.Size);
        var edge = new Vector2(Mathf.Clamp(anchor.X, rect.Position.X + 10, rect.End.X - 10), Mathf.Clamp(anchor.Y, rect.Position.Y + 8, rect.End.Y - 8));
        var tint = new Color(UiTheme.Selection, _interactionBlend * (_interactionActive ? .65f : .2f));
        c.DrawLine(anchor, edge, new Color(0, 0, 0, tint.A * .6f), 3, true);
        c.DrawLine(anchor, edge, tint, 1.2f, true);
        c.DrawCircle(anchor, 5, new Color(UiTheme.Glass, _interactionBlend), true, -1, true);
        c.DrawCircle(anchor, 2.5f, tint, true, -1, true);
    }
}
