using Godot;

namespace HordeShooter;

/// <summary>Presentation only. The weapon remains the sole clock for reloads and charging.</summary>
internal sealed class WeaponActionFeedback
{
    internal enum ActionKind { None, Reload, Charge, Recharge }
    public ActionKind Kind { get; private set; }
    public float Blend { get; private set; }
    public float Progress { get; private set; }
    public bool Active { get; private set; }
    public float Weight => Blend * Blend * (3 - 2 * Blend);
    public int Percent => _completed ? 100 : Mathf.Min(99, Mathf.FloorToInt(Progress * 100));
    private bool _completed;
    private WeaponState? _weapon;
    private int _lastMag;

    public void Reset()
    {
        _weapon = null;
        Kind = ActionKind.None;
        Blend = Progress = 0;
        Active = false;
        _completed = false;
    }

    public void Update(WeaponState weapon, bool windingUp, float windup, float dt)
    {
        if (_weapon != weapon) { Reset(); _weapon = weapon; }
        var next = windingUp ? ActionKind.Charge : weapon.Reloading ? ActionKind.Reload
            : weapon.CooldownLeft > 0 ? ActionKind.Recharge : ActionKind.None;
        if (next != ActionKind.None)
        {
            _completed = false;
            Kind = next;
            Progress = Mathf.Clamp(next == ActionKind.Reload ? weapon.ReloadFraction
                : next == ActionKind.Charge ? windup
                : 1 - weapon.CooldownLeft / Mathf.Max(.001f, weapon.ShotCooldown), 0, 1);
        }
        else if (Active)
        {
            // Cancellation keeps the last amount. Completion comes from the weapon; a cancelled charge refunds ammo.
            _completed = Kind == ActionKind.Reload ? weapon.ReloadCompleted
                : Kind == ActionKind.Recharge || Kind == ActionKind.Charge && weapon.Mag <= _lastMag;
            if (_completed) Progress = 1;
        }
        Active = next != ActionKind.None;
        Blend = Mathf.MoveToward(Blend, Active ? 1 : 0, Mathf.Max(0, dt) / (Active ? UiTheme.EnterSeconds : UiTheme.ActionReturnSeconds));
        _lastMag = weapon.Mag;
        if (Blend == 0) Kind = ActionKind.None;
    }
}

public partial class Hud
{
    private readonly WeaponActionFeedback _weaponAction = new();
    private Paint _weaponActionGauge = null!;
    private Label _weaponActionLabel = null!;
    private static readonly Color ActionLight = UiTheme.ActionLight;
    private static readonly Color ActionGlow = UiTheme.ActionGlow;

    private bool ShowWeaponAction => Stage.State == StageState.Playing && !Stage.Player.IsDead && !Stage.Player.IsDowned
        && !Paused && !Stage.AllStopped && !InventoryOpen && !CardWindowOpen && !Stage.Player.Weapons.AimingThrow;

    private void BuildWeaponAction()
    {
        _weaponActionGauge = Place(new Paint { Name = "WeaponActionGauge", CustomMinimumSize = new(248, 22), Painter = DrawWeaponActionGauge },
            .5f, 1, new(0, -228), true, true, centerX: true);
        _weaponActionLabel = Place(HudLabel("", 14, HudWhite), .5f, 1, new(0, -256), true, true, centerX: true);
        _weaponActionLabel.Name = "WeaponActionLabel";
        _weaponActionLabel.CustomMinimumSize = new(248, 0);
        _weaponActionLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _weaponActionLabel.Visible = _weaponActionGauge.Visible = false;
    }

    private void UpdateWeaponAction(float dt)
    {
        var player = Stage.Player;
        if (!ShowWeaponAction) _weaponAction.Reset();
        else _weaponAction.Update(player.Gun, player.Weapons.Charging || player.Weapons.WindupProgress > 0,
            player.Weapons.WindupProgress, dt);
        float weight = _weaponAction.Weight;
        _weaponActionGauge.Visible = _weaponActionLabel.Visible = weight > 0 && !InteractionInDock;
        _weaponActionGauge.Modulate = _weaponActionLabel.Modulate = new Color(1, 1, 1, weight);
        string key = _weaponAction.Kind == WeaponActionFeedback.ActionKind.Reload ? "ui_hud_reload"
            : _weaponAction.Kind == WeaponActionFeedback.ActionKind.Recharge ? "hud_action_recharge" : "hud_action_charge";
        _weaponActionLabel.Text = $"{Loc.Text(key)}  ·  {_weaponAction.Percent}%";
        // Leave the existing interaction/status lines below, and move status icons above the action strip.
        float interactionTop = _worldInteraction.Position.Y - _root.Size.Y;
        _statusEffects.OffsetTop = _statusEffects.OffsetBottom = InteractionInDock ? interactionTop - 86 : -200 - 70 * weight;
        _status.OffsetTop = _status.OffsetBottom = InteractionInDock ? interactionTop - 34 : -166;
    }

    private void DrawWeaponActionGauge(Control c)
    {
        UiTheme.DrawProgress(c, new Rect2(Vector2.Zero, c.Size), _weaponAction.Progress, 4);
    }

    private void DrawWeaponActionRing(Control c, Vector2 center)
    {
        float w = _weaponAction.Weight;
        if (w <= 0) return;
        float radius = Mathf.Lerp(23, 19, w);
        c.DrawArc(center + Vector2.Down, radius, 0, Mathf.Tau, 96, new Color(0, 0, 0, .16f * w), 14, true);
        c.DrawArc(center, radius, 0, Mathf.Tau, 96, new Color(.008f, .016f, .025f, .48f * w), 11, true);
        if (_weaponAction.Progress > 0)
        {
            float end = -Mathf.Pi / 2 + Mathf.Tau * _weaponAction.Progress;
            DrawLightArc(c, center, radius, end, ActionGlow, w);
        }
    }

    // A bounded stack of translucent strokes gives a soft halo without textures or scene bloom.
    private static void DrawLightArc(Control c, Vector2 center, float radius, float end, Color tint, float opacity, float start = -Mathf.Pi / 2)
    {
        for (int i = 10; i >= 1; i--)
        {
            float edge = i / 10f;
            c.DrawArc(center, radius, start, end, 96,
                new Color(tint, (.025f + .045f * (1 - edge)) * opacity), 4 + i * 1.2f, true);
        }
        c.DrawArc(center, radius, start, end, 96, new Color(tint.Lerp(Colors.White, .45f), .8f * opacity), 5, true);
        c.DrawArc(center, radius, start, end, 96, new Color(ActionLight, .98f * opacity), 3.2f, true);
    }
}
