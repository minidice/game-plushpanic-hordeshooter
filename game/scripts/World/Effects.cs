using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Short-lived juice (tracers, shells, stuffing, paint, sonar, explosions) and corpse bookkeeping.
/// Corpses are local-only scenery; excess distant corpses retire outside every registered view.
/// </summary>
public partial class Effects : Node3D
{
    public const int MaxSplats = 160;
    public const int MaxShells = 40;

    private readonly Queue<Node3D> _splats = new();
    private readonly Queue<Node3D> _shells = new();
    private readonly RandomNumberGenerator _rng = new();
    private static readonly Color[] Paint =
    {
        new("ff8fb8"), new("8fd6ff"), new("ffd36b"), new("b59cff"), new("8effc1"),
    };

    public int CorpseCount => CommonCorpseCount + NamedCorpseCount;
    public int SplatCount => _splats.Count;

    private Stage Stage => GetParent<Stage>();

    // Shared, scaled per use: effects fire every frame in a fight, so avoid a new mesh each time.
    private readonly BoxMesh _unitBox = new() { Size = Vector3.One };
    private readonly CylinderMesh _unitDisc = new() { TopRadius = 1, BottomRadius = 1, Height = .01f, RadialSegments = 14, Rings = 1 };
    private readonly SphereMesh _unitSphere = new() { Radius = 1, Height = 2 };
    private readonly BoxShape3D _shellShape = new() { Size = new Vector3(.05f, .05f, .1f) };
    private readonly System.Collections.Generic.Dictionary<float, BoxMesh> _burstMeshes = new();

    public void Tracer(Vector3 from, Vector3 to, bool crit)
    {
        float length = from.DistanceTo(to);
        if (length < .05f) return;
        float thick = crit ? .07f : .03f;
        var mat = Mat.Glow(crit ? new Color("ffcf4a") : new Color("ffe5a0"), crit ? 4 : 2);
        var beam = Mat.Mesh(this, _unitBox, (from + to) * .5f, mat);
        beam.LookAt(to, Vector3.Up);
        beam.Scale = new Vector3(thick, thick, length);
        Expire(beam, crit ? .08f : .045f);
    }

    public void Shell(Vector3 at, Vector3 right)
    {
        var shell = new RigidBody3D
        {
            Mass = .02f,
            CollisionLayer = 0,
            CollisionMask = Layers.World,
            Position = at,
            PhysicsMaterialOverride = new PhysicsMaterial { Bounce = .4f, Friction = .6f },
        };
        shell.AddChild(new CollisionShape3D { Shape = _shellShape });
        Mat.Mesh(shell, _unitBox, Vector3.Zero, Mat.Solid("d8a94a", .3f)).Scale = new Vector3(.05f, .05f, .1f);
        AddChild(shell);
        shell.LinearVelocity = right * _rng.RandfRange(2, 3.5f) + Vector3.Up * _rng.RandfRange(2.5f, 4f);
        shell.AngularVelocity = new Vector3(_rng.RandfRange(-20, 20), _rng.RandfRange(-20, 20), 0);
        Cap(_shells, shell, MaxShells);
        Expire(shell, 3.5f);
    }

    public void Impact(Vector3 at, Vector3 normal)
    {
        Burst(at, normal, new Color("b9b2a0"), 6, 2.5f, .05f);
    }

    /// <summary>Cute gore: a puff of stuffing instead of blood.</summary>
    public void Stuffing(Vector3 at, Vector3 direction, Color color, int amount)
    {
        Burst(at, direction, color, amount, 5.5f, .09f);
    }

    public void PaintSplat(Vector3 at, float size = 1f)
    {
        var color = Paint[_rng.RandiRange(0, Paint.Length - 1)];
        float radius = _rng.RandfRange(.35f, .8f) * size;
        var splat = Mat.Mesh(this, _unitDisc, new Vector3(at.X, .012f + _rng.Randf() * .006f, at.Z), Mat.Solid(color, .25f));
        splat.Scale = new Vector3(radius, 1, radius * _rng.RandfRange(.6f, 1f));
        splat.RotationDegrees = new Vector3(0, _rng.RandfRange(0, 180), 0);
        Cap(_splats, splat, MaxSplats);
    }

    /// <summary>Flat fading fan showing a melee swing.</summary>
    public void Swoosh(Vector3 at, Vector3 direction, float range, float arcDegrees)
    {
        var mesh = new ArrayMesh();
        var verts = new System.Collections.Generic.List<Vector3>();
        int steps = 10;
        float half = Mathf.DegToRad(arcDegrees) * .5f;
        for (int i = 0; i < steps; i++)
        {
            float a0 = -half + 2 * half * i / steps, a1 = -half + 2 * half * (i + 1) / steps;
            verts.Add(Vector3.Zero);
            verts.Add(new Vector3(Mathf.Sin(a1), 0, Mathf.Cos(a1)) * range);
            verts.Add(new Vector3(Mathf.Sin(a0), 0, Mathf.Cos(a0)) * range);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        var mat = Mat.Fade(new Color(1, 1, .85f, .35f));
        var fan = Mat.Mesh(this, mesh, at, mat);
        fan.Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
        var tween = fan.CreateTween();
        tween.TweenProperty(mat, "albedo_color:a", 0f, .16f);
        tween.TweenCallback(FreeLater(fan));
    }

    public void SonarPing(Vector3 at)
    {
        var mat = Mat.Fade(new Color(1f, .35f, .35f, .75f));
        var ring = Mat.Mesh(this, new TorusMesh { InnerRadius = .55f, OuterRadius = .65f, Rings = 24, RingSegments = 4 },
            new Vector3(at.X, .05f, at.Z), mat);
        ring.Scale = Vector3.One * .3f;
        var tween = ring.CreateTween().SetParallel();
        tween.TweenProperty(ring, "scale", Vector3.One * 1.5f, .8f);
        tween.TweenProperty(mat, "albedo_color:a", 0f, .8f);
        tween.Chain().TweenCallback(FreeLater(ring));
    }

    public void Explosion(Vector3 at, float radius, Color color)
    {
        var mat = Mat.Fade(new Color(color, .85f));
        var ball = Mat.Mesh(this, _unitSphere, at + Vector3.Up * .6f, mat);
        ball.Scale = Vector3.One * .2f;
        var tween = ball.CreateTween().SetParallel();
        tween.TweenProperty(ball, "scale", Vector3.One * radius * .8f, .18f);
        tween.TweenProperty(mat, "albedo_color:a", 0f, .35f);
        tween.Chain().TweenCallback(FreeLater(ball));

        var light = new OmniLight3D { Position = at + Vector3.Up * 1.5f, OmniRange = radius * 3, LightEnergy = 6, LightColor = color };
        AddChild(light);
        var fade = light.CreateTween();
        fade.TweenProperty(light, "light_energy", 0f, .4f);
        fade.TweenCallback(FreeLater(light));

        Burst(at + Vector3.Up * .5f, Vector3.Up, color, 40, 9f, .14f);
        Stage.Cam.AddShake(.55f);
    }

    public void FloatingText(Vector3 at, string text, Color color)
    {
        var label = new Label3D
        {
            Text = text,
            Position = at,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 64,
            OutlineSize = 14,
            PixelSize = .01f,
            Modulate = color,
            NoDepthTest = true,
        };
        AddChild(label);
        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "position", at + Vector3.Up * 1.2f, .8f);
        tween.TweenProperty(label, "modulate:a", 0f, .8f).SetDelay(.3f);
        tween.Chain().TweenCallback(FreeLater(label));
    }

    /// <summary>Move a character's visuals onto a tumbling rigid body (the "bowling pin" death).</summary>
    public RigidBody3D SpawnCorpse(CharacterRig rig, float radius, float height, Vector3 impulse, Vector3 torque, MonsterDefinition? monster = null)
    {
        var body = new Corpse
        {
            Name = "Corpse",
            Mass = 1,
            CollisionLayer = Layers.Corpse,
            CollisionMask = Corpse.SceneryCollisionMask,
            LinearDamp = .5f,
            AngularDamp = .9f,
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = .8f, Bounce = .25f },
        };
        body.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Radius = radius, Height = Mathf.Max(height, radius * 2.05f) } });
        AddChild(body);
        var origin = rig.GlobalPosition;
        body.GlobalPosition = origin + Vector3.Up * height * .5f;
        rig.ResetPose();
        rig.Visible = true; // corpses are scenery, even if the enemy died outside the vision cone
        rig.Reparent(body);
        rig.Position = Vector3.Down * height * .5f;
        body.Rig = rig;
        TrackCorpse(body, Mathf.Max(3f, height * 2f), monster, origin);
        body.ApplyCentralImpulse(impulse);
        body.ApplyTorqueImpulse(torque);
        return body;
    }

    private void Burst(Vector3 at, Vector3 direction, Color color, int amount, float speed, float size)
    {
        if (!_burstMeshes.TryGetValue(size, out var mesh))
        {
            mesh = new BoxMesh { Size = Vector3.One * size, Material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = .9f } };
            _burstMeshes[size] = mesh;
        }
        var particles = new CpuParticles3D
        {
            Position = at,
            Emitting = true,
            OneShot = true,
            Amount = amount,
            Lifetime = .9f,
            Explosiveness = .95f,
            Mesh = mesh,
            Direction = direction.LengthSquared() > .01f ? direction.Normalized() + Vector3.Up * .6f : Vector3.Up,
            Spread = 55,
            InitialVelocityMin = speed * .4f,
            InitialVelocityMax = speed,
            Gravity = new Vector3(0, -14, 0),
            ScaleAmountMin = .6f,
            ScaleAmountMax = 1.4f,
            AngularVelocityMin = -400,
            AngularVelocityMax = 400,
            Color = color,
            HueVariationMin = -.04f,
            HueVariationMax = .04f,
        };
        AddChild(particles);
        Expire(particles, 1.4f);
    }

    private static void Expire(Node node, float seconds)
    {
        // Tween bound to the node itself: it dies with the node, and uses a native callable so no
        // C# delegate is left dangling if the game quits mid-effect.
        var tween = node.CreateTween();
        tween.TweenInterval(seconds);
        tween.TweenCallback(FreeLater(node));
    }

    private static Callable FreeLater(Node node) => new(node, Node.MethodName.QueueFree);

    private static void Cap(Queue<Node3D> queue, Node3D node, int max)
    {
        queue.Enqueue(node);
        while (queue.Count > max)
        {
            var old = queue.Dequeue();
            if (IsInstanceValid(old)) old.QueueFree();
        }
        // Drop references to nodes that already expired so counts stay meaningful.
        while (queue.Count > 0 && !IsInstanceValid(queue.Peek())) queue.Dequeue();
    }
}
