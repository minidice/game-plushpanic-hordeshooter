using Godot;

namespace HordeShooter;

/// <summary>Shoot it, ram it, or blow up its neighbour. Kills the crowd, and launches you if you stand too close.</summary>
public partial class ExplosiveBarrel : RigidBody3D
{
    public const string ObjectId = "runtime:barrel";
    public static ObjectInteraction Rule => ObjectInteractions.Current.ForId(ObjectId)
        ?? throw new System.InvalidOperationException("Missing object interaction: " + ObjectId);
    public static float Radius => Rule.Explosion?.Radius ?? 0;
    public ObjectInteraction Interaction { get; private set; } = null!;
    public float Health { get; private set; }
    public bool Exploded { get; private set; }
    public bool Ignited { get; private set; }

    private Stage _stage = null!;
    private float _fuse;
    private Player? _litBy;

    public static ExplosiveBarrel Create(Stage stage, Vector3 at)
    {
        var barrel = new ExplosiveBarrel
        {
            Name = "Barrel",
            _stage = stage,
            Position = at,
            Mass = 3,
            Interaction = Rule,
            Health = Rule.Destruction?.MaxHealth ?? 0,
            CollisionLayer = Rule.BlocksProjectiles ? Layers.Props : Rule.BlocksMovement ? Layers.MovementOnly : 0,
            CollisionMask = Layers.MovementWorld | Layers.Props,
        };
        // Collider is taller than the 1 m mesh so bullets fired at chest height (Player.AimHeight) always hit it.
        barrel.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = .38f, Height = 1.4f }, Position = Vector3.Up * .7f });
        Mat.Mesh(barrel, new CylinderMesh { TopRadius = .38f, BottomRadius = .38f, Height = 1.0f }, Vector3.Up * .5f, Mat.Solid("b8392e", .5f));
        Mat.Mesh(barrel, new CylinderMesh { TopRadius = .39f, BottomRadius = .39f, Height = .12f }, Vector3.Up * .62f, Mat.Solid("e8c547", .5f));
        barrel.NetId = ++stage.NextPropId;
        stage.AddChild(barrel);
        stage.Props.Add(barrel);
        return barrel;
    }

    /// <summary>Same on every PC (build order), so the host can say which barrel blew up.</summary>
    public int NetId { get; private set; }

    /// <param name="by">Who set it off (a shot, or the blast of a barrel they set off): the explosion counts for them.</param>
    public void TakeDamage(float damage, ObjectDamage kind, Player? by = null)
    {
        if (Ignited || Exploded || !_stage.Authoritative || !float.IsFinite(damage) || damage <= 0 ||
            Interaction.Destruction is not { } destruction || !destruction.Accepts(kind)) return;
        Health = Mathf.Max(0, Health - damage);
        if (Health <= 0) Ignite(by: by);
    }

    public void Ignite(float? fuse = null, Player? by = null)
    {
        // On a client the host's explosion arrives as an event and calls Explode directly.
        if (Ignited || Exploded || !_stage.Authoritative || Interaction.Destruction == null) return;
        Ignited = true;
        Health = 0;
        _litBy = by;
        _fuse = fuse ?? Interaction.Explosion?.DelaySeconds ?? 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Ignited || Exploded) return;
        _fuse -= (float)delta;
        if (_fuse <= 0) Explode();
    }

    public void Explode(bool showEffects = true)
    {
        if (Exploded) return;
        Exploded = true;
        _stage.Net?.OnBarrelExplode(this);
        _stage.Props.Remove(this);
        CollisionLayer = 0;
        if (showEffects && Interaction.Explosion is { } explosion)
            Blast.Explode(_stage, GlobalPosition, explosion.Radius, explosion.Damage, new Color("ff9a3c"), this,
                knockbackPower: explosion.Impulse, by: _litBy != null && IsInstanceValid(_litBy) ? _litBy : null, effectSource: "barrel");
        QueueFree();
    }
}
