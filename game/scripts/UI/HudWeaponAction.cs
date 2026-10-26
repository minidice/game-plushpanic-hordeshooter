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
        Blend = Mathf.MoveToward(Blend, Active ? 1 : 0, Mathf.Max(0, dt) / (Active ? .16f : .20f));
        _lastMag = weapon.Mag;
        if (Blend == 0) Kind = ActionKind.None;
    }
}

public partial class Hud
{
    private readonly WeaponActionFeedback _weaponAction = new();
    private Paint _weaponActionGauge = null!;
    private Label _weaponActionLabel = null!;
    private static readonly Color ActionLight = new(.77f, .93f, 1f, .86f);
    // Native vector resources: rounded alpha layers remain sharp at every UI scale.
    private static readonly StyleBoxFlat ActionTrack = ActionPlate(new Color(.015f, .035f, .055f, .50f), new Color(.72f, .88f, 1, .17f), 8);
    private static readonly StyleBoxFlat ActionFill = ActionPlate(new Color(.70f, .89f, 1, .78f), new Color(.88f, .97f, 1, .5f), 5);

    private static StyleBoxFlat ActionPlate(Color fill, Color edge, int radius) => new()
    {
        BgColor = fill, BorderColor = edge,
        BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
        CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        AntiAliasing = true, AntiAliasingSize = 1,
    };

    private bool ShowWeaponAction => Stage.State == StageState.Playing && !Stage.Player.IsDead && !Stage.Player.IsDowned
        && !Paused && !Stage.AllStopped && !InventoryOpen && !CardWindowOpen && !Stage.Player.Weapons.AimingThrow;

    private void BuildWeaponAction()
    {
        _weaponActionGauge = Place(new Paint { Name = "WeaponActionGauge", CustomMinimumSize = new(248, 16), Painter = DrawWeaponActionGauge },
            .5f, 1, new(0, -204), true, true, centerX: true);
        _weaponActionLabel = Place(HudLabel("", 15, ActionLight), .5f, 1, new(0, -226), true, true, centerX: true);
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
        _weaponActionGauge.Visible = _weaponActionLabel.Visible = weight > 0;
        _weaponActionGauge.Modulate = _weaponActionLabel.Modulate = new Color(1, 1, 1, weight);
        string key = _weaponAction.Kind == WeaponActionFeedback.ActionKind.Reload ? "ui_hud_reload"
            : _weaponAction.Kind == WeaponActionFeedback.ActionKind.Recharge ? "hud_action_recharge" : "hud_action_charge";
        _weaponActionLabel.Text = $"{Loc.Text(key)}  ·  {_weaponAction.Percent}%";
        // Leave the existing interaction/status lines below, and move status icons above the action strip.
        _statusEffects.OffsetTop = _statusEffects.OffsetBottom = -200 - 70 * weight;
    }

    private void DrawWeaponActionGauge(Control c)
    {
        c.DrawStyleBox(ActionTrack, new Rect2(Vector2.Zero, c.Size));
        var inside = new Rect2(new Vector2(4, 4), new Vector2((c.Size.X - 8) * _weaponAction.Progress, c.Size.Y - 8));
        if (inside.Size.X <= 0) return;
        c.DrawStyleBox(ActionFill, inside);
        if (inside.Size.X > 8)
            c.DrawLine(inside.Position + new Vector2(4, 1.5f), inside.Position + new Vector2(inside.Size.X - 4, 1.5f), new Color(1, 1, 1, .36f), 1, true);
    }

    private void DrawWeaponActionRing(Control c, Vector2 center)
    {
        float w = _weaponAction.Weight;
        if (w <= 0) return;
        float radius = Mathf.Lerp(23, 19, w);
        c.DrawCircle(center, radius + 4, new Color(.015f, .035f, .055f, .32f * w), true, -1, true);
        c.DrawArc(center, radius, 0, Mathf.Tau, 64, new Color(.55f, .71f, .8f, .20f * w), 3, true);
        if (_weaponAction.Progress > 0)
        {
            float end = -Mathf.Pi / 2 + Mathf.Tau * _weaponAction.Progress;
            c.DrawArc(center, radius, -Mathf.Pi / 2, end, 64, new Color(ActionLight, .12f * w), 7, true);
            c.DrawArc(center, radius, -Mathf.Pi / 2, end, 64, new Color(ActionLight, .86f * w), 2.5f, true);
            c.DrawCircle(center + Vector2.FromAngle(end) * radius, 1.7f, new Color(.94f, .99f, 1, .9f * w), true, -1, true);
        }
    }
}
