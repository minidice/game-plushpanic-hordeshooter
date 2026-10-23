using Godot;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// A thrown item. Flies on a fixed-time arc to the aimed spot, then runs its data-defined effects:
/// on impact (molotov), after its fuse (frag, flash), or after an attract phase (lure).
/// </summary>
public partial class ThrownItem : RigidBody3D
{
    private Player? _thrower;
    /// <summary>Who threw it, if they are still around: its blast and fire count for them.</summary>
    private Player? Thrower => _thrower != null && IsInstanceValid(_thrower) ? _thrower : null;
    public ThrowableDefinition Def { get; private set; } = null!;
    public bool Landed { get; private set; }
    public bool Done { get; private set; }
    /// <summary>While beeping: how far it pulls common zombies from.</summary>
    public float AttractRadius => Def.Attract?.RadiusM ?? 0;

    private Stage _stage = null!;
    private float _age, _fuse, _attractLeft, _beep;
    private bool _attracting;
    private OmniLight3D? _light;

    // ---------- trajectory (shared with the aim preview) ----------

    /// <summary>Where a throw toward <paramref name="target"/> actually lands, given the item's max range.</summary>
    public static Vector3 ClampTarget(Vector3 from, Vector3 target, float maxRange)
    {
        var flat = new Vector3(target.X - from.X, 0, target.Z - from.Z);
        if (flat.Length() > maxRange) target = from + flat.Normalized() * maxRange;
        target.Y = .2f;
        return target;
    }

    /// <summary>Fixed-time arcs, so near and far throws both look like a lob.</summary>
    public static float FlightTime(Vector3 from, Vector3 target) =>
        Mathf.Clamp(new Vector2(target.X - from.X, target.Z - from.Z).Length() / 14f, .45f, 1.05f);

    private static float Gravity => (float)ProjectSettings.GetSetting("physics/3d/default_gravity", 9.8);

    public static Vector3 LaunchVelocity(Vector3 from, Vector3 target)
    {
        float time = FlightTime(from, target);
        var v = (target - from) / time;
        v.Y = (target.Y - from.Y + .5f * Gravity * time * time) / time;
        return v;
    }

    /// <summary>Position along the arc at <paramref name="t"/> seconds (for drawing the preview).</summary>
    public static Vector3 ArcPoint(Vector3 from, Vector3 target, float t)
    {
        var v = LaunchVelocity(from, target);
        return from + v * t + Vector3.Down * (.5f * Gravity * t * t);
    }

    /// <param name="thrower">Who threw it: only they can be blinded by watching their own flashbang.</param>
    public static ThrownItem Throw(Stage stage, ThrowableDefinition def, Vector3 from, Vector3 target, Player? thrower = null)
    {
        target = ClampTarget(from, target, def.MaxRange);
        // Height tiers (L-15): aim for the floor of the tier it lands on.
        if (stage.Tiers.Any) target.Y = stage.Tiers.GroundY(target) + .2f;
        var item = new ThrownItem
        {
            Name = "Thrown_" + def.Id,
            Def = def,
            _stage = stage,
            _thrower = thrower,
            Mass = .4f,
            ContinuousCd = true,
            ContactMonitor = true,
            MaxContactsReported = 2,
            CollisionLayer = 0,
            CollisionMask = Layers.World | Layers.Props,
            PhysicsMaterialOverride = new PhysicsMaterial { Bounce = def.DetonateOnImpact ? 0 : .35f, Friction = .8f },
            LinearDamp = .2f,
            AngularDamp = .5f,
        };
        item.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = .14f } });
        BuildLook(item, def);
        stage.Effects.AddChild(item);
        item.GlobalPosition = from;
        item.LinearVelocity = LaunchVelocity(from, target);
        item.AngularVelocity = new Vector3(8, 3, 0);
        item._fuse = def.Fuse;
        return item;
    }

    public static void BuildLook(Node3D parent, ThrowableDefinition def)
    {
        if (def.LoadModel() is { } scene)
        {
            parent.AddChild(scene.Instantiate());
            return;
        }
        var color = Mat.Solid(def.Color, .5f);
        switch (def.Kind)
        {
            case ThrowableKind.Fire:
                Mat.Mesh(parent, new CylinderMesh { TopRadius = .06f, BottomRadius = .1f, Height = .26f }, Vector3.Zero, Mat.Solid(new Color(.55f, .75f, .45f), .1f));
                Mat.Mesh(parent, new SphereMesh { Radius = .05f, Height = .1f }, new Vector3(0, .16f, 0), Mat.Glow(new Color("ff9a3c"), 3));
                break;
            case ThrowableKind.Lure:
                Mat.Mesh(parent, new SphereMesh { Radius = .15f, Height = .3f }, Vector3.Zero, color);
                Mat.Mesh(parent, new SphereMesh { Radius = .05f, Height = .1f }, new Vector3(0, .14f, .06f), Mat.Glow(new Color("ff2d55"), 3));
                break;
            default:
                Mat.Mesh(parent, new SphereMesh { Radius = .12f, Height = .26f }, Vector3.Zero, color);
                Mat.Box(parent, new Vector3(0, .14f, 0), new Vector3(.05f, .06f, .05f), Mat.Solid("b9b9a8", .3f));
                break;
        }
    }

    // ---------- lifecycle ----------

    public override void _PhysicsProcess(double delta)
    {
        if (Done) return;
        float dt = (float)delta;
        _age += dt;
        if (!Landed && (GetContactCount() > 0 || _age > 1.6f)) Land();
        if (Done) return;

        if (_attracting)
        {
            _attractLeft -= dt;
            _beep -= dt;
            var attract = Def.Attract!;
            if (_beep <= 0)
            {
                // Beeps speed up and rise in pitch as the end approaches.
                float left = Mathf.Clamp(_attractLeft / attract.Duration, 0, 1);
                _beep = Mathf.Max(.12f, attract.BeepInterval * left);
                _stage.Effects.FloatingText(GlobalPosition + Vector3.Up * .6f, Loc.Text("msg_lure_squeak"), new Color("ff9ccc"));
                _stage.Sfx.PlayAt("lure_beep", GlobalPosition, 0, 1 + .35f * (1 - left));
                if (_light != null) _light.LightEnergy = _light.LightEnergy > 0 ? 0 : 3;
            }
            if (_attractLeft <= 0) Detonate();
            return;
        }
        if (Def.Fuse > 0)
        {
            _fuse -= dt;
            if (_fuse <= 0) Detonate();
        }
    }

    private void Land()
    {
        Landed = true;
        // Thud: kill most of the throw speed so it settles near the cursor instead of rolling away.
        LinearVelocity = new Vector3(LinearVelocity.X * .2f, Mathf.Min(LinearVelocity.Y, 1.5f), LinearVelocity.Z * .2f);
        LinearDamp = 3f;
        if (Def.DetonateOnImpact)
        {
            Detonate();
            return;
        }
        if (Def.Attract is { } attract && Def.Fuse <= 0)
        {
            _attracting = true;
            _attractLeft = attract.Duration;
            _light = new OmniLight3D { OmniRange = 6, LightEnergy = 3, LightColor = new Color("ff2d55"), Position = Vector3.Up * .4f };
            AddChild(_light);
            _stage.SetLure(this);
        }
    }

    private void Detonate()
    {
        if (Done) return;
        var at = GlobalPosition;
        var ground = new Vector3(at.X, 0, at.Z);
        bool flashed = false;
        foreach (var effect in Def.DetonationEffects)
        {
            switch (effect)
            {
                case ExplodeEffect e:
                    Blast.Explode(_stage, at, e.RadiusM, e.Damage, Def.Kind == ThrowableKind.Lure ? new Color("ff7ab8") : new Color("ffb347"),
                        knockbackPower: e.KnockbackPower, selfShare: e.SelfDamageShare, by: Thrower);
                    break;
                case BurnPoolEffect b:
                    FireZone.Create(_stage, ground, b, Thrower);
                    _stage.Sfx.PlayAt("glass_break", at);
                    _stage.Effects.Stuffing(at, Vector3.Up, new Color("b8e0b0"), 14);
                    break;
                case StunEffect s:
                    if (!flashed) Flash(at);
                    flashed = true;
                    foreach (var enemy in _stage.Enemies)
                        if (!enemy.IsDead && enemy.GlobalPosition.DistanceTo(at) <= s.RadiusM && Clear(this, at, enemy.GlobalPosition))
                            enemy.Stun(s.Duration);
                    break;
                case BlindSelfEffect blind:
                    if (!flashed) Flash(at);
                    flashed = true;
                    BlindThrower(at, blind);
                    break;
            }
        }
        Finish();
    }

    private void Flash(Vector3 at)
    {
        _stage.Effects.Explosion(at, 2.5f, new Color("ffffff"));
        _stage.Sfx.PlayAt("flash_bang", at + Vector3.Up);
        _stage.Director.MakeNoise(at, _stage.Director.Rules.NoiseFlash);
    }

    /// <summary>Watching your own flashbang is a classic: close + looking at it = white screen.</summary>
    private void BlindThrower(Vector3 at, BlindSelfEffect blind)
    {
        var player = _thrower != null && IsInstanceValid(_thrower) ? _thrower : _stage.Player;
        if (!player.IsLocal) return; // a teammate's flashbang never whites out your screen for their mistake
        var to = at - player.GlobalPosition;
        to.Y = 0;
        float dist = to.Length();
        if (player.IsDead || dist > blind.RadiusM || !Clear(this, at, player.GlobalPosition)) return;
        float t = blind.FacingThreshold;
        float facing = dist < 1.2f ? 1 : Mathf.Clamp((player.AimDirection.Dot(to / dist) - t) / (1 - t), 0, 1);
        player.Blind(blind.MaxSeconds * (.5f + .5f * (1 - dist / blind.RadiusM)) * facing);
    }

    private static bool Clear(Node3D context, Vector3 a, Vector3 b) =>
        Rays.Clear(context, a + Vector3.Up, new Vector3(b.X, a.Y + 1, b.Z), Layers.SightMask);

    private void Finish()
    {
        Done = true;
        if (_stage.Lure == this) _stage.ClearLure();
        QueueFree();
    }
}
