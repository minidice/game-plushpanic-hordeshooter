using Godot;

namespace HordeShooter;

/// <summary>Shoot it, ram it, or blow up its neighbour. Kills the crowd, and launches you if you stand too close.</summary>
public partial class ExplosiveBarrel : RigidBody3D
{
    public const float Radius = 4.6f;
    public bool Exploded { get; private set; }
    public bool Ignited { get; private set; }

    private Stage _stage = null!;
    private float _fuse;

    public static ExplosiveBarrel Create(Stage stage, Vector3 at)
    {
        var barrel = new ExplosiveBarrel
        {
            Name = "Barrel",
            _stage = stage,
            Position = at,
            Mass = 3,
            CollisionLayer = Layers.Props,
            CollisionMask = Layers.World | Layers.Props,
        };
        // Collider is taller than the 1 m mesh so bullets fired at chest height (Player.AimHeight) always hit it.
        barrel.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = .38f, Height = 1.4f }, Position = Vector3.Up * .7f });
        Mat.Mesh(barrel, new CylinderMesh { TopRadius = .38f, BottomRadius = .38f, Height = 1.0f }, Vector3.Up * .5f, Mat.Solid("b8392e", .5f));
        Mat.Mesh(barrel, new CylinderMesh { TopRadius = .39f, BottomRadius = .39f, Height = .12f }, Vector3.Up * .62f, Mat.Solid("e8c547", .5f));
        stage.AddChild(barrel);
        stage.Props.Add(barrel);
        return barrel;
    }

    public void Ignite(float fuse = .12f)
    {
        if (Ignited || Exploded) return;
        Ignited = true;
        _fuse = fuse;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Ignited || Exploded) return;
        _fuse -= (float)delta;
        if (_fuse <= 0) Explode();
    }

    public void Explode()
    {
        if (Exploded) return;
        Exploded = true;
        Blast.Explode(_stage, GlobalPosition, Radius, 125f, new Color("ff9a3c"), this, knockbackPower: 140f);
        _stage.Props.Remove(this);
        QueueFree();
    }
}
