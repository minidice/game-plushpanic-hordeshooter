using Godot;

namespace HordeShooter;

/// <summary>
/// Fan-shaped vision (doc/idea.md 5.3 ①) with three steps (decisions.md C-23, I-67):
///   Visible    — in your cone or within touching distance, or lit by any survivor's flashlight (lights are shared).
///   Silhouette — not lit, but close by with nothing in between, in the glow of a lamp or fire you can see,
///                or attacking you (windup to a moment after the hit). A dark shape: where and how big, no detail.
///   Hidden     — everything else. Hidden enemies that move nearby still leave sonar ripples.
/// </summary>
public partial class VisionSystem : Node
{
    public enum Visibility { Hidden, Silhouette, Visible }

    /// <summary>Closer than this (with a clear line) you sense a shape even outside the cone.</summary>
    public const float SilhouetteRadius = 7f;
    /// <summary>Lamps and fires join this group; enemies inside part of their range show as silhouettes.</summary>
    public const string LightGroup = "vision_light";
    public const float LightReach = .6f;
    /// <summary>An attacker stays a silhouette this long after its attack on you lands.</summary>
    public const float RevealAfterHit = 2f;

    public const float NormalHalfAngle = 57f;
    public const float HeliumHalfAngle = 30f;
    public const float Range = 24f;
    public const float NearRadius = 2.6f;
    public const float SonarRadius = 15f;
    public const float EyeHeight = 1.3f;

    private Stage Stage => GetParent<Stage>();
    private int _cursor;

    public float HalfAngle => Stage.Player.HeliumTime > 0 ? HeliumHalfAngle : NormalHalfAngle;

    public bool CanSee(Vector3 point)
    {
        var player = Stage.Player;
        var eye = player.GlobalPosition + Vector3.Up * EyeHeight;
        var to = point - player.GlobalPosition;
        to.Y = 0;
        float dist = to.Length();
        if (dist < NearRadius) return true;
        if (dist > Range) return false;
        if (Mathf.RadToDeg(player.AimDirection.AngleTo(to / dist)) > HalfAngle) return false;
        return Rays.Clear(player, eye, new Vector3(point.X, eye.Y, point.Z), Layers.SightMask);
    }

    /// <summary>In another living survivor's flashlight cone with nothing in between (shared lights, C-23).</summary>
    public bool LitBySquad(Vector3 point)
    {
        foreach (var p in Stage.AlivePlayers)
        {
            if (p == Stage.Player || p.IsDowned) continue;
            var to = point - p.GlobalPosition;
            to.Y = 0;
            float dist = to.Length();
            if (dist > Range || dist < .01f) continue;
            if (Mathf.RadToDeg(p.AimDirection.AngleTo(to / dist)) > NormalHalfAngle) continue;
            var eye = p.GlobalPosition + Vector3.Up * EyeHeight;
            if (Rays.Clear(p, eye, new Vector3(point.X, eye.Y, point.Z), Layers.SightMask)) return true;
        }
        return false;
    }

    public Visibility Classify(Enemy e)
    {
        var player = Stage.Player;
        if (player.IsDead) return Visibility.Visible;
        var at = e.GlobalPosition + Vector3.Up * .8f;
        if (CanSee(at) || LitBySquad(at)) return Visibility.Visible;
        // Whoever is attacking you is never invisible, even outside the cone.
        if (e.RevealTime > 0 || (e is CaptureEnemy captor && captor.Held == player)) return Visibility.Silhouette;
        var to = at - player.GlobalPosition;
        to.Y = 0;
        float dist = to.Length();
        if (dist > Range) return Visibility.Hidden;
        bool near = dist < SilhouetteRadius, lit = !near && InLamplight(at);
        if (!near && !lit) return Visibility.Hidden;
        var eye = player.GlobalPosition + Vector3.Up * EyeHeight;
        return Rays.Clear(player, eye, new Vector3(at.X, eye.Y, at.Z), Layers.SightMask) ? Visibility.Silhouette : Visibility.Hidden;
    }

    private bool InLamplight(Vector3 point)
    {
        foreach (var node in _lights)
            if (node is OmniLight3D light && IsInstanceValid(light) && light.IsInsideTree() && light.LightEnergy > .05f &&
                point.DistanceTo(light.GlobalPosition) < light.OmniRange * LightReach) return true;
        return false;
    }

    /// <summary>Applies the step to the model: hidden, a dark silhouette, or as it is.</summary>
    public void Apply(Enemy e)
    {
        if (_lights.Count == 0 && IsInsideTree()) _lights = GetTree().GetNodesInGroup(LightGroup);
        var v = Classify(e);
        e.Visibility = v;
        e.IsSeen = v == Visibility.Visible;
        e.Rig.ShowAs(v);
    }

    private Godot.Collections.Array<Node> _lights = new();

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        var enemies = Stage.Enemies;
        if (enemies.Count == 0) return;
        bool dead = Stage.Player.IsDead;
        _lights = GetTree().GetNodesInGroup(LightGroup);
        // Re-test a third of the crowd per frame; plenty at 60 Hz and cheap with 70+ enemies.
        int batch = Mathf.Max(1, enemies.Count / 3 + 1);
        for (int i = 0; i < batch; i++)
        {
            _cursor = (_cursor + 1) % enemies.Count;
            var e = enemies[_cursor];
            if (!e.IsDead) Apply(e);
        }
        // Attackers are re-tested every frame so the warning shows the moment the windup starts.
        foreach (var e in enemies)
            if (!e.IsDead && e.RevealTime > 0 && e.Visibility == Visibility.Hidden) Apply(e);

        if (dead) return;
        var playerPos = Stage.Player.GlobalPosition;
        foreach (var e in enemies)
        {
            if (e.IsDead || e.Visibility != Visibility.Hidden || !e.Awake) continue;
            e.PingTimer -= dt;
            if (e.PingTimer > 0) continue;
            e.PingTimer = e.IsSpecial ? .7f : 1.2f;
            if (e.GlobalPosition.DistanceTo(playerPos) < SonarRadius && e.Velocity.LengthSquared() > .2f)
            {
                Stage.Effects.SonarPing(e.GlobalPosition);
                Stage.Sfx.PlayAt("sonar", e.GlobalPosition + Vector3.Up, 0, e.IsSpecial ? .7f : 1f);
            }
        }
    }
}
