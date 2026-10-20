using Godot;

namespace HordeShooter;

/// <summary>
/// Survival's base: a generator the horde walks to and hits (doc/menu-multiplayer-plan.md 3.1).
/// Only monsters damage it; bullets just stop on it. At 0 it blows up and the match is lost.
/// </summary>
public partial class BaseCore : StaticBody3D
{
    public const float Radius = .8f;
    public float MaxHealth { get; private set; }
    public float Health { get; private set; }
    public bool Alive => Health > 0;
    public event System.Action? Destroyed;

    private Stage _stage = null!;
    private StandardMaterial3D _glow = null!;
    private OmniLight3D _light = null!;
    private float _flash, _sparkCooldown, _time;

    public static BaseCore Create(Stage stage, Vector3 at, float health)
    {
        var core = new BaseCore { Name = "BaseCore", Position = at, CollisionLayer = Layers.World, CollisionMask = 0, _stage = stage, MaxHealth = health, Health = health };
        core.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = Radius, Height = 1.6f }, Position = Vector3.Up * .8f });
        stage.AddChild(core);
        core.Build();
        return core;
    }

    private void Build()
    {
        StageArt.Box(this, new Vector3(0, .12f, 0), new Vector3(1.9f, .24f, 1.9f), "3a4148", true);
        StageArt.Box(this, new Vector3(0, .62f, 0), new Vector3(1.3f, .8f, 1.1f), "c99a3b", true);
        StageArt.Box(this, new Vector3(0, 1.1f, 0), new Vector3(1.05f, .22f, .9f), "4b5560", true);
        _glow = Mat.Glow(new Color("5cf0a0"), 2.4f);
        Mat.Box(this, new Vector3(0, .7f, .56f), new Vector3(.7f, .3f, .03f), _glow);
        Mat.Box(this, new Vector3(0, 1.45f, 0), new Vector3(.18f, .5f, .18f), _glow);
        _light = new OmniLight3D { Position = new Vector3(0, 2f, 0), LightColor = new Color("8affc0"), LightEnergy = 2.2f, OmniRange = 7f };
        _light.AddToGroup(VisionSystem.LightGroup);
        AddChild(_light);
        AddChild(new Label3D
        {
            Text = Loc.Text("ui_floor_base"), Position = new Vector3(0, .03f, 1.6f), RotationDegrees = new Vector3(-90, 0, 0),
            FontSize = 64, PixelSize = .012f, Modulate = new Color("9dffc0", .85f), OutlineSize = 0,
        });
    }

    /// <summary>A monster's hit. The host decides; a client only shows the bar it is sent.</summary>
    public void TakeDamage(float amount, Vector3 from)
    {
        if (!Alive || amount <= 0) return;
        Health = Mathf.Max(0, Health - amount);
        _flash = 1;
        if (_sparkCooldown <= 0)
        {
            _sparkCooldown = .15f;
            var dir = new Vector3(from.X - GlobalPosition.X, 0, from.Z - GlobalPosition.Z);
            _stage.Effects.Impact(GlobalPosition + Vector3.Up + dir.Normalized() * Radius, dir.Normalized());
            _stage.Sfx.PlayAt("zombie_hit", GlobalPosition + Vector3.Up, -4, .7f);
        }
        if (Alive) return;
        _stage.Effects.Explosion(GlobalPosition, 4f, new Color("ffb347"));
        _stage.Sfx.PlayAt("explosion", GlobalPosition + Vector3.Up, 2);
        _light.LightColor = new Color("ff5a3a");
        _glow.AlbedoColor = _glow.Emission = new Color("40100a");
        Destroyed?.Invoke();
    }

    /// <summary>Client: the host's health for the core, so the light and the bar match.</summary>
    public void SetHealthFraction(float fraction)
    {
        float health = Mathf.Clamp(fraction, 0, 1) * MaxHealth;
        if (health < Health - .5f) _flash = 1;
        Health = health;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _sparkCooldown -= dt;
        _flash = Mathf.MoveToward(_flash, 0, dt * 4);
        if (!Alive) { _light.LightEnergy = .6f + .4f * Mathf.Sin(_time * 30); return; }
        // Low health: the beacon blinks red faster.
        float low = 1 - Health / MaxHealth;
        var calm = new Color("8affc0").Lerp(new Color("ff6a4a"), low);
        _light.LightColor = calm.Lerp(new Color(1, .2f, .15f), _flash);
        _light.LightEnergy = 2.2f + .8f * Mathf.Sin(_time * (2 + low * 8)) * low + _flash * 2;
    }
}
