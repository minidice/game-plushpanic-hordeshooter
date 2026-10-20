using Godot;

namespace HordeShooter;

public partial class Effects
{
    private int _restTick;

    /// <summary>
    /// Assisted sleep for piles (I-71): every quarter second, every calm common corpse is put to sleep in the same
    /// step, so no still-awake neighbour wakes it again. One that is still moving keeps its pile awake until it calms.
    /// </summary>
    public override void _PhysicsProcess(double delta)
    {
        if (++_restTick < 15 || _corpses.Count == 0) return;
        _restTick = 0;
        foreach (var entry in _corpses)
            if (IsInstanceValid(entry.Body) && entry.Body.ReadyToRest) entry.Body.Sleeping = true;
    }

    /// <summary>One-way contact: cosmetic bodies react, survivor motion never depends on local debris.</summary>
    public void PushCorpses(CollisionShape3D playerShape, Vector3 velocity, bool rolling)
    {
        velocity.Y = 0;
        float speed = velocity.Length();
        if (speed < .1f || CommonCorpseCount == 0) return;
        using var query = new PhysicsShapeQueryParameters3D
        {
            Shape = playerShape.Shape,
            Transform = playerShape.GlobalTransform,
            Margin = .12f,
            CollisionMask = Layers.Corpse,
        };
        var hits = GetWorld3D().DirectSpaceState.IntersectShape(query, 128);
        using var rawHits = (Godot.Collections.Array)hits;
        foreach (var hit in hits)
        {
            using (hit)
            {
                if (hit["collider"].AsGodotObject() is not Corpse body ||
                    body.NamedRecord != null || body.IsQueuedForDeletion()) continue;
                var from = playerShape.GlobalPosition;
                if (!Rays.Clear(this, from, body.GlobalPosition, Layers.World | Layers.Props)) continue;
                var away = body.GlobalPosition - from;
                away.Y = 0;
                var direction = (velocity.Normalized() + away.Normalized() * .5f).Normalized();
                // Match a target speed, rather than adding unlimited force on every touching frame.
                float targetSpeed = rolling ? 12f : Mathf.Min(speed * 1.15f, 7f);
                float kick = Mathf.Max(0, targetSpeed - body.LinearVelocity.Dot(direction));
                if (kick < .05f) continue;
                var impulse = direction * kick + Vector3.Up * (rolling ? kick * .15f : 0);
                body.Kick(impulse, -away.Normalized() * .3f + Vector3.Down * .15f);
            }
        }
    }

    /// <summary>Local blast response. Run after damage so same-frame deaths participate.</summary>
    public void BlastCorpses(Vector3 at, float radius, float power, Node? source = null)
    {
        if (radius <= 0 || power <= 0) return;
        var exclude = source is CollisionObject3D collider ? new[] { collider.GetRid() } : null;
        foreach (var entry in _corpses)
        {
            var body = entry.Body;
            if (!IsInstanceValid(body) || body.IsQueuedForDeletion()) continue;
            var away = body.GlobalPosition - at;
            float distance = away.Length();
            if (distance >= radius) continue;
            if (Rays.Cast(this, at + Vector3.Up * .12f, body.GlobalPosition,
                    Layers.World | Layers.Props, exclude) != null) continue;
            float falloff = 1 - distance / radius;
            away.Y = 0;
            var outward = away.LengthSquared() > .001f ? away.Normalized() : Vector3.Right;
            var impulse = (outward * (2 + 16 * falloff) + Vector3.Up * (1 + 11 * falloff))
                * Mathf.Clamp(power / 150f, 0, 2);
            body.Kick(impulse, -outward * .2f + Vector3.Down * .2f);
        }
    }
}
