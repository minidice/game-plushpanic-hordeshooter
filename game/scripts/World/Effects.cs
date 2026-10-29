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

    public int CorpseCount => CommonCorpseCount + NamedCorpseCount;
    public int SplatCount => _splats.Count;

    private Stage Stage => GetParent<Stage>();

    // Shared, scaled per use: effects fire every frame in a fight, so avoid a new mesh each time.
    private readonly BoxMesh _unitBox = new() { Size = Vector3.One };
    private readonly SphereMesh _fluidDrop = new() { Radius = .045f, Height = .09f, RadialSegments = 8, Rings = 4,
        Material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = .35f } };
    private readonly SphereMesh _unitSphere = new() { Radius = 1, Height = 2 };
    private readonly BoxShape3D _shellShape = new() { Size = new Vector3(.05f, .05f, .1f) };
    private readonly System.Collections.Generic.Dictionary<float, BoxMesh> _burstMeshes = new();

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
        // Legacy name used by explosions and ground impacts: they leave dry neutral residue, never blood.
        AddSplat(at, size, BodyFluidSplat.Scorch, false);
    }

    public void DeathSplat(Vector3 at, MonsterDefinition monster, Vector3 direction, float size = 1f)
    {
        var color = BodyFluidSplat.ColorFor(monster);
        AddSplat(at, size, color, true);
        var drops = new CpuParticles3D {
            Position = new Vector3(at.X, Stage.Tiers.GroundY(at) + .12f, at.Z),
            Mesh = _fluidDrop, Color = color, Amount = 6, OneShot = true, Emitting = true,
            Lifetime = .32f, Explosiveness = 1, Direction = direction.LengthSquared() > .01f ? direction.Normalized() + Vector3.Up : Vector3.Up,
            Spread = 38, InitialVelocityMin = .5f, InitialVelocityMax = 1.4f,
            Gravity = new Vector3(0, -14, 0), ScaleAmountMin = .6f, ScaleAmountMax = 1.2f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(drops);
        Expire(drops, .5f);
    }

    private void AddSplat(Vector3 at, float size, Color color, bool fluid)
    {
        float radius = _rng.RandfRange(.35f, .8f) * size;
        var floor = new Vector3(at.X, Stage.Tiers.GroundY(at) + .012f + _rng.Randf() * .006f, at.Z);
        var splat = BodyFluidSplat.Create(this, floor, radius, color, _rng.RandfRange(0, 100), fluid);
        splat.RotationDegrees = new Vector3(0, _rng.RandfRange(0, 180), 0);
        Cap(_splats, splat, MaxSplats);
    }

    /// <summary>Bright hollow crescent following the melee impact and follow-through.</summary>
    public void Swoosh(Vector3 at, Vector3 direction, float range, float arcDegrees)
        => MeleeSwingEffect.Play(this, at, direction, range, arcDegrees);

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

    /// <summary>A soft pop for frequent card effects: a quick fading ball and a few bits, no light or camera shake.</summary>
    public void Puff(Vector3 at, float radius, Color color)
    {
        var mat = Mat.Fade(new Color(color, .55f));
        var ball = Mat.Mesh(this, _unitSphere, at, mat);
        ball.Scale = Vector3.One * .15f;
        var tween = ball.CreateTween().SetParallel();
        tween.TweenProperty(ball, "scale", Vector3.One * radius * .7f, .14f);
        tween.TweenProperty(mat, "albedo_color:a", 0f, .28f);
        tween.Chain().TweenCallback(FreeLater(ball));
        Burst(at, Vector3.Up, color, 14, 5f, .1f);
    }

    public void FloatingText(Vector3 at, string text, Color color)
    {
        var label = new Label3D
        {
            Font = UiFonts.Get(),
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

    /// <summary>Common bodies tumble; named bodies keep a fixed record while their visuals collapse by body shape.</summary>
    public RigidBody3D SpawnCorpse(CharacterRig rig, float radius, float height, Vector3 impulse, Vector3 torque, MonsterDefinition? monster = null, bool critical = false)
    {
        DeathBurst(rig, critical);
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
        var visualOrigin = origin;
        float deathSide = Mathf.Sin(rig.GlobalRotation.Y * 7.3f) >= 0 ? 1 : -1;
        var deathPose = monster?.KeepsCorpse == true ? new CharacterRig.DeathPose(rig, monster.Id, deathSide) : null;
        if (monster?.KeepsCorpse == true)
        {
            var ground = Rays.Cast(this, origin + Vector3.Up * .5f, origin + Vector3.Down * 100, Layers.World);
            if (ground.HasValue) origin = ground.Value.Position;
        }
        body.GlobalPosition = origin + Vector3.Up * height * .5f;
        if (deathPose == null)
        {
            rig.ResetPose();
            rig.ApplyDeathPose(deathSide);
        }
        rig.Visible = true; // corpses are scenery, even if the enemy died outside the vision cone
        rig.ShowAs(VisionSystem.Visibility.Visible);
        rig.Reparent(body);
        rig.Position = Vector3.Down * height * .5f;
        body.Rig = rig;
        TrackCorpse(body, Mathf.Max(3f, height * 2f), monster, origin);
        if (body.NamedRecord == null)
        {
            body.ApplyCentralImpulse(impulse);
            body.ApplyTorqueImpulse(torque);
        }
        else
        {
            // Deterministic resting pose: no local settling or death impulse can move a named body.
            float yaw = rig.GlobalRotation.Y;
            body.Rotation = new Vector3(-Mathf.Pi * .5f, yaw, 0);
            body.GlobalPosition = origin + Vector3.Up * radius;
            rig.Rotation = Vector3.Zero;
            body.FreezeMode = RigidBody3D.FreezeModeEnum.Static;
            body.Freeze = true;
            body.NamedRecord.DeathYaw = yaw;
            Topple(rig, origin, visualOrigin, yaw, deathSide, deathPose!);
        }
        return body;
    }

    /// <summary>A brief death marker, independent of the corpse's lifetime and physics.</summary>
    public void DeathBurst(CharacterRig rig, bool critical = false)
    {
        rig.FlashDeath(critical);
        // Read visibility before SpawnCorpse reveals/reparents the rig. An off-screen kill must not add a new beacon.
        if (!rig.IsVisibleInTree()) return;
        var def = rig.Definition;
        DamageBurstEffect.Play(this, rig.GlobalPosition + Vector3.Up * def.ColliderHeight * .55f,
            def.ColliderHeight, def.ColliderRadius, true, critical);
    }

    public const float NamedDeathMaxSeconds = .8f;

    /// <summary>
    /// The named collision body and record remain fixed (T-16). Only the visuals move,
    /// entering from the current pose and settling their actual surface onto the ground (I-83).
    /// </summary>
    private static void Topple(CharacterRig rig, Vector3 feet, Vector3 visualOrigin, float yaw, float side, CharacterRig.DeathPose pose)
    {
        var facing = Basis.FromEuler(new Vector3(0, yaw, 0));
        rig.GlobalBasis = facing;
        rig.GlobalPosition = visualOrigin;
        var surface = new CorpseSurface(rig);
        float initialBottom = surface.Bottom();
        float initialGap = float.IsFinite(initialBottom) ? Mathf.Max(.02f, initialBottom - feet.Y) : .02f;
        void Pose(float elapsed)
        {
            if (!IsInstanceValid(rig)) return;
            float t = Mathf.Clamp(elapsed / pose.Duration, 0, 1);
            pose.Sample(t);
            rig.GlobalBasis = facing * Basis.FromEuler(pose.Tilt(t));
            float shift = Mathf.SmoothStep(0, 1, Mathf.Clamp((t - .2f) / .65f, 0, 1));
            rig.GlobalPosition = feet + facing * new Vector3(-side * rig.Definition.ColliderHeight * .10f * shift, 0, 0);
            float bottom = surface.Bottom();
            float clearance = Mathf.Lerp(initialGap, .02f, Mathf.SmoothStep(0, 1, Mathf.Min(t / .32f, 1)));
            if (float.IsFinite(bottom)) rig.GlobalPosition += Vector3.Up * (feet.Y + clearance - bottom);
        }
        Pose(0);
        var tween = rig.CreateTween();
        tween.TweenMethod(Callable.From<float>(Pose), 0f, pose.Duration, pose.Duration);
        tween.TweenCallback(Callable.From(() => Pose(pose.Duration)));
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
