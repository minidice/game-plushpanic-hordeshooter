using Godot;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// A parked car with an alarm (spawn-design.md 4장, decisions.md L-8). A small red light blinks on the
/// dash so players learn to spot it. Shooting it or catching it in a blast sets it off once: a siren and
/// flashing lights for <see cref="SpawnRules.AlarmTime"/> seconds, a wake-up noise over the whole area,
/// and hunter waves from the director's entries while it sounds.
/// </summary>
public partial class AlarmCar : StaticBody3D
{
    public bool Triggered { get; private set; }
    public bool Sounding => _left > 0;

    private Stage _stage = null!;
    private StandardMaterial3D _dash = null!, _lamps = null!;
    private OmniLight3D _glow = null!;
    private AudioStreamPlayer3D? _siren;
    private float _left, _clock;

    public static AlarmCar Create(Stage stage, Vector3 center, Vector3 size, string color, Node3D? visual = null)
    {
        var car = new AlarmCar { _stage = stage, Name = "AlarmCar", Position = center, CollisionLayer = Layers.World, CollisionMask = 0 };
        stage.AddChild(car);
        car.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        car._dash = Glow(new Color("ff3b30"), 0);
        car._lamps = Glow(new Color("ffb347"), 0);
        if (visual != null)
        {
            // Collision center stays at half height; imported art has its pivot on the floor.
            visual.Position = Vector3.Down * size.Y / 2;
            car.AddChild(visual);
            car.BindModelLights(visual);
        }
        else
        {
            StageArt.Cargo(car, size, color);
            car.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(.16f, .08f, .16f) },
                Position = new Vector3(size.X * .18f, size.Y / 2 + .04f, 0),
                MaterialOverride = car._dash,
            });
            foreach (float x in new[] { -.5f, .5f })
                car.AddChild(new MeshInstance3D
                {
                    Mesh = new BoxMesh { Size = new Vector3(.12f, .22f, size.Z * .7f) },
                    Position = new Vector3(size.X * x + Mathf.Sign(x) * .04f, .1f, 0),
                    MaterialOverride = car._lamps,
                });
        }
        car._glow = new OmniLight3D { LightColor = new Color("ffb347"), LightEnergy = 0, OmniRange = 7, ShadowEnabled = false, Position = Vector3.Up * 1.2f };
        car.AddChild(car._glow);
        stage.AlarmCars.Add(car);
        return car;
    }

    private void BindModelLights(Node3D model)
    {
        bool red = false, amber = false;
        foreach (var mesh in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
        {
            if (mesh.Mesh.SurfaceGetMaterial(i) is not StandardMaterial3D source) continue;
            bool isRed = source.ResourceName.StartsWith("Red");
            bool isAmber = source.ResourceName.StartsWith("Hazard");
            if (!isRed && !isAmber) continue;
            if (isRed && !red || isAmber && !amber)
            {
                var local = (StandardMaterial3D)source.Duplicate();
                local.EmissionEnabled = true;
                local.Emission = new Color(isRed ? "ff3b30" : "ffb347");
                local.EmissionEnergyMultiplier = 0;
                if (isRed) { _dash = local; red = true; }
                else { _lamps = local; amber = true; }
            }
            mesh.SetSurfaceOverrideMaterial(i, isRed ? _dash : _lamps);
        }
        if (!red || !amber) GD.PushError("Alarm truck needs Red and Hazard light materials");
    }

    private static StandardMaterial3D Glow(Color color, float energy) => new()
    {
        AlbedoColor = color.Darkened(.6f),
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = energy,
    };

    /// <summary>Sets the alarm off. Only once per car.</summary>
    public void Trigger()
    {
        if (Triggered || _stage.State != StageState.Playing || !_stage.Authoritative) return;
        Sound();
    }

    /// <summary>Sets the alarm off for real, or on a client because the host said so.</summary>
    internal void Sound()
    {
        if (Triggered) return;
        Triggered = true;
        _left = _stage.Director.Rules.AlarmTime;
        // Loud for the monsters (noise_alarm_m), quiet for the ears: the siren plays at its plain recipe volume.
        _siren = _stage.Sfx.Attach("alarm_loop", this);
        _stage.Director.StartAlarm(GlobalPosition);
        _stage.ShowNotice(Loc.Text("msg_car_alarm"));
        _stage.Cam.AddShake(.15f);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _clock += dt;
        if (!Triggered)
        {
            // Waiting: a slow red blink on the dash.
            _dash.EmissionEnergyMultiplier = _clock % 1.4f < .18f ? 3f : 0f;
            return;
        }
        if (_left <= 0) return;
        // Cleared or game over: the siren stops with the director.
        _left = _stage.State == StageState.Playing ? _left - dt : 0;
        bool on = _clock % .5f < .25f;
        _lamps.EmissionEnergyMultiplier = on ? 4f : .2f;
        _dash.EmissionEnergyMultiplier = on ? 3f : 0f;
        _glow.LightEnergy = on ? 2.4f : 0f;
        if (_left > 0) return;
        _lamps.EmissionEnergyMultiplier = 0;
        _dash.EmissionEnergyMultiplier = 0;
        _glow.LightEnergy = 0;
        if (_siren != null && IsInstanceValid(_siren)) _siren.QueueFree();
        _siren = null;
    }
}
