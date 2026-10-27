using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Low cover (decisions.md C-29): a shot stops at sandbags and barriers unless the shooter is right behind them, when it
/// goes over. "Right behind" is a gap between the shooter's body and the cover smaller than
/// <see cref="ObjectInteractions.ShootOverGapBodies"/> of the shooter's own body widths: two survivors hugging the same
/// sandbag from both sides shoot each other, and one who stands a body width back is shooting into the sandbag. Every
/// shooter goes through here: survivors and squadmates, rockets, monster shots and their line-of-fire checks.
/// </summary>
public static class CoverRule
{
    public static bool IsCover(GodotObject? collider) => collider is CoverScreen || collider is ObjectBody { Interaction.Cover: true };

    /// <summary>The cover hit at <paramref name="hit"/> is close enough to the shooter's body to fire over.</summary>
    public static bool ShootsOver(Vector3 shooter, float radius, Vector3 hit)
    {
        float gap = new Vector2(hit.X - shooter.X, hit.Z - shooter.Z).Length() - radius;
        return gap < ObjectInteractions.Current.ShootOverGapBodies * radius * 2;
    }

    /// <summary>
    /// <see cref="Rays.Cast"/> for a shot from a body at <paramref name="shooter"/>: cover the shooter fires over is
    /// skipped, cover further away stops the shot like a wall.
    /// </summary>
    public static RayHit? Cast(Node3D context, Vector3 shooter, float radius, Vector3 from, Vector3 to, uint mask, IReadOnlyList<Rid>? exclude = null)
    {
        List<Rid>? skip = null;
        for (int i = 0; i < 4; i++)
        {
            var hit = Rays.Cast(context, from, to, mask, skip ?? exclude);
            if (hit is not { } h || !IsCover(h.Collider) || !ShootsOver(shooter, radius, h.Position)) return hit;
            skip ??= exclude == null ? new List<Rid>() : new List<Rid>(exclude);
            skip.Add(h.Rid);
        }
        return Rays.Cast(context, from, to, mask, skip);
    }

    public static bool Clear(Node3D context, Vector3 shooter, float radius, Vector3 from, Vector3 to, uint mask) =>
        Cast(context, shooter, radius, from, to, mask) == null;
}

/// <summary>
/// A placed object whose rules go beyond blocking (object_interactions.json): low cover, and objects that break. Health
/// and what breaks it come from the data; only the host decides a break, and every PC removes the same object by its
/// build-order id (like barrels). A broken object frees its cells in the flow field.
/// </summary>
public partial class ObjectBody : StaticBody3D
{
    public ObjectInteraction Interaction { get; private set; } = null!;
    public float Health { get; private set; }
    public bool Broken { get; private set; }
    /// <summary>Same on every PC (build order); 0 for objects that cannot break.</summary>
    public int NetId { get; private set; }
    private Stage _stage = null!;
    private System.Action? _onBreak;

    /// <summary>The placed object behind a ray hit: the object itself or its cover's shot screen.</summary>
    public static ObjectBody? From(GodotObject? collider) => collider switch
    {
        ObjectBody body => body,
        CoverScreen screen => screen.Body,
        _ => null,
    };

    /// <summary>Low cover: the tall shot screen over <paramref name="size"/> at <paramref name="center"/> (local), at least <see cref="CoverScreen.Height"/>.</summary>
    public void AddShotScreen(Vector3 center, Vector3 size)
    {
        float top = Mathf.Max(center.Y + size.Y / 2, CoverScreen.Height), bottom = center.Y - size.Y / 2;
        var screen = new CoverScreen { Name = "ShotScreen", Body = this, CollisionLayer = Layers.CoverShot, CollisionMask = 0 };
        screen.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(size.X, top - bottom, size.Z) }, Position = new Vector3(center.X, (top + bottom) / 2, center.Z) });
        AddChild(screen);
    }

    public static ObjectBody Create(Stage stage, string name, ObjectInteraction rule, System.Action? onBreak)
    {
        var body = new ObjectBody { Name = name, Interaction = rule, _stage = stage, CollisionLayer = rule.CollisionLayer, CollisionMask = 0, _onBreak = onBreak };
        if (rule.Destruction is { } d)
        {
            body.Health = d.MaxHealth;
            body.NetId = ++stage.NextPropId;
            stage.Breakables.Add(body);
        }
        return body;
    }

    /// <summary>Harm from a shot or a blast; ignored when the data says this object does not break, or not from that.</summary>
    public void TakeDamage(float amount, ObjectDamage kind, Player? by = null)
    {
        if (Broken || !float.IsFinite(amount) || amount <= 0 || Interaction.Destruction is not { } d || !d.Accepts(kind) || !_stage.Authoritative) return;
        Health -= amount;
        if (Health <= 0) Break(by: by);
    }

    /// <summary>Collapses: dust, a thud, gone from physics and the flow field. Clients call it from the host's event.</summary>
    public void Break(bool showEffects = true, Player? by = null)
    {
        if (Broken) return;
        Broken = true;
        Health = 0;
        _stage.Breakables.Remove(this);
        var at = GlobalPosition + Vector3.Up * .4f;
        if (showEffects)
        {
            for (int i = 0; i < 4; i++) _stage.Effects.Impact(at + new Vector3((i - 1.5f) * .4f, 0, 0), Vector3.Up);
            SurfaceAudio.Break(_stage, this, at);
        }
        foreach (var wall in GetChildren().OfType<BreakableWallVisual>().ToArray())
        {
            wall.Reparent(_stage, true);
            if (showEffects) wall.PlayBreak(); else wall.SetProgress(1);
        }
        _stage.Net?.OnObjectBreak(this);
        CollisionLayer = 0;
        foreach (var screen in GetChildren()) if (screen is CoverScreen s) s.CollisionLayer = 0;
        _onBreak?.Invoke();
        if (showEffects && Interaction.Explosion is { } explosion) ObjectDetonation.Schedule(_stage, GlobalPosition, explosion, by);
        QueueFree();
    }
}

/// <summary>Low cover's invisible shot screen (<see cref="Layers.CoverShot"/>); hits on it belong to <see cref="Body"/>.</summary>
public partial class CoverScreen : StaticBody3D
{
    /// <summary>Up to here above the cover's base: over every shot's height (chest 1 m, rockets, raised muzzles).</summary>
    public const float Height = 2.4f;
    public ObjectBody Body { get; set; } = null!;
}
