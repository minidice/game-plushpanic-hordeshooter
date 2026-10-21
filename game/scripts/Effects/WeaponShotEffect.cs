using Godot;

namespace HordeShooter;

/// <summary>Weapon presentation only. Damage, ammunition and projectile motion stay in their existing systems.</summary>
public partial class WeaponShotEffect : Node3D
{
    private const float InitialHead = .9f, BeamSeconds = .18f;
    // Art tuning, not ballistics. Keep M4's accepted first-pass proportions as the rifle default.
    private readonly record struct Style(float Seconds, float Length, float Width, float Speed, float Tail, float Line, float Light)
    {
        public static Style For(WeaponDefinition def) => def.Mode switch
        {
            FireMode.Projectile => new(.085f, 1.05f, .7f, 0, 0, 0, 2.0f),
            FireMode.Lob => new(.085f, .85f, .75f, 0, 0, 0, 1.8f),
            _ when def.ChargeTime > 0 => new(.07f, .85f, .65f, 0, 0, .20f, 1.7f),
            _ => def.Category switch
            {
                WeaponCategory.Pistol => new(.045f, .45f, .32f, 125, 1.7f, .065f, 1),
                WeaponCategory.Smg => new(.035f, .5f, .30f, 150, 2, .055f, .8f),
                WeaponCategory.Lmg => new(.045f, .8f, .50f, 175, 3, .075f, 1.35f),
                WeaponCategory.Shotgun => new(.07f, 1.05f, .85f, 140, 1.5f, .045f, 2),
                WeaponCategory.Sniper when def.TypeId == "dmr" => new(.055f, .85f, .48f, 200, 3.5f, .075f, 1.6f),
                WeaponCategory.Sniper when def.Id == "m82" => new(.08f, 1.3f, .7f, 240, 5, .11f, 2.4f),
                WeaponCategory.Sniper => new(.065f, 1.05f, .55f, 220, 4.5f, .09f, 2),
                _ => new(.055f, .7f, .5f, 160, 2.8f, .075f, 1.6f),
            },
        };
    }
    private static readonly QuadMesh Quad = new() { Size = Vector2.One };
    private static readonly Shader ShotShader = GD.Load<Shader>("res://shaders/weapon_shot.gdshader");
    private static readonly ShaderMaterial NormalTracer = Material(false, 2.4f);
    private static readonly ShaderMaterial CritTracer = Material(false, 3.2f);
    private MeshInstance3D? _flash, _tracer;
    private ShaderMaterial? _flashMaterial, _beamMaterial;
    private OmniLight3D? _light;
    private Vector3 _muzzle, _from, _direction;
    private float _distance, _elapsed;
    private bool _crit, _beam, _flashOnly, _showMuzzle = true;
    private Style _style;

    public static void Play(Node3D parent, WeaponDefinition weapon, Vector3 muzzle, Vector3 from, Vector3 end, bool crit, bool muzzleFlash = true)
    {
        var delta = end - from;
        parent.AddChild(new WeaponShotEffect
        {
            Name = "WeaponShot",
            _style = Style.For(weapon),
            _beam = weapon.ChargeTime > 0,
            _showMuzzle = muzzleFlash,
            _muzzle = muzzle,
            _from = from,
            _direction = delta.LengthSquared() > .000001f ? delta.Normalized() : Vector3.Right,
            _distance = delta.Length(),
            _crit = crit,
        });
    }

    public static void Flash(Node3D parent, WeaponDefinition weapon, Vector3 muzzle, Vector3 direction)
    {
        parent.AddChild(new WeaponShotEffect
        {
            Name = "WeaponLaunchFlash", _style = Style.For(weapon), _flashOnly = true,
            _muzzle = muzzle, _direction = direction.Normalized(),
        });
    }

    public override void _Ready()
    {
        if (_showMuzzle)
        {
            _flashMaterial = Material(true, 3.5f, _beam);
            _flash = Surface("MuzzleFlash", _flashMaterial);
            _light = new OmniLight3D { OmniRange = 2.2f, LightColor = new Color(_beam ? "92e7ff" : "ffcf78"), ShadowEnabled = false };
            AddChild(_light);
            _light.GlobalPosition = _muzzle;
        }
        if (!_flashOnly)
        {
            if (_beam) _beamMaterial = Material(false, 4, true);
            _tracer = Surface("Tracer", _beamMaterial ?? (_crit ? CritTracer : NormalTracer));
        }
        UpdateLook();
    }

    private static ShaderMaterial Material(bool muzzle, float brightness, bool beam = false)
    {
        var material = new ShaderMaterial { Shader = ShotShader };
        material.SetShaderParameter("muzzle", muzzle);
        material.SetShaderParameter("brightness", brightness);
        if (beam)
        {
            material.SetShaderParameter("edge_color", new Color("2da9ff"));
            material.SetShaderParameter("core_color", new Color("dcfaff"));
        }
        return material;
    }

    private MeshInstance3D Surface(string name, Material material)
    {
        var mesh = new MeshInstance3D
        {
            Name = name, Mesh = Quad, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(mesh);
        return mesh;
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        bool traceDone = _flashOnly || (_beam ? _elapsed >= BeamSeconds : InitialHead + _elapsed * _style.Speed - _style.Tail >= _distance);
        if (_elapsed >= _style.Seconds && traceDone)
        {
            QueueFree();
            return;
        }
        UpdateLook();
    }

    private void UpdateLook()
    {
        float flash = Mathf.Max(0, 1 - _elapsed / _style.Seconds);
        // Face the camera while keeping the long axis exactly on the shot direction.
        var view = GetViewport().GetCamera3D()?.GlobalBasis.Z ?? Vector3.Up;
        var side = view.Cross(_direction);
        if (side.LengthSquared() < .001f)
            side = (Mathf.Abs(_direction.Y) < .9f ? Vector3.Up : Vector3.Right).Cross(_direction);
        side = side.Normalized();
        var normal = _direction.Cross(side);
        if (_flash != null)
        {
            _flash.Visible = flash > 0;
            _flashMaterial!.SetShaderParameter("strength", flash * flash);
            _light!.LightEnergy = _style.Light * flash * flash;
            float flashLength = _style.Length * (.75f + .25f * flash);
            _flash.GlobalTransform = new Transform3D(new Basis(_direction * flashLength, side * _style.Width, normal),
                _muzzle + _direction * flashLength * .5f);
        }

        if (_tracer == null) return;
        float head = _beam ? _distance : InitialHead + _elapsed * _style.Speed;
        float tail = _beam ? 0 : Mathf.Clamp(head - _style.Tail, 0, _distance);
        head = Mathf.Min(head, _distance);
        float length = head - tail;
        _tracer.Visible = length > .001f;
        if (!_tracer.Visible) return;
        float fade = _beam ? Mathf.Max(0, 1 - _elapsed / BeamSeconds) : 1;
        _beamMaterial?.SetShaderParameter("strength", fade * fade);
        _tracer.GlobalTransform = new Transform3D(new Basis(_direction * length, side * _style.Line * (_crit ? 1.27f : 1) * (_beam ? .3f + .7f * fade : 1), normal),
            _from + _direction * (head + tail) * .5f);
    }
}
