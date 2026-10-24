using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Burning pool from a molotov: hurts every zombie inside each frame, and you too if you stand in it.</summary>
public partial class FireZone : Node3D
{
    private Stage _stage = null!;
    private float _radius, _dps, _playerDps, _life;
    private readonly System.Collections.Generic.Dictionary<Player, float> _playerDebts = new();
    private readonly Dictionary<Enemy, float> _ticks = new();
    private Player? _by;

    public float Radius => _radius;

    /// <param name="by">Who threw the molotov: the burning counts for them.</param>
    public static FireZone Create(Stage stage, Vector3 at, BurnPoolEffect burn, Player? by = null)
    {
        var zone = new FireZone { Name = "Fire", _stage = stage, _radius = burn.RadiusM, _dps = burn.EnemyDps, _playerDps = burn.PlayerDps, _life = burn.Duration, Position = at, _by = by };
        stage.Effects.AddChild(zone);
        zone.AddToGroup(SquadGroups.Hazard);
        zone.SetMeta("radius", burn.RadiusM);
        CombatSurfaceEffect.Create(zone, CombatEffectCatalog.Current.ForSource("fire_pool"), burn.RadiusM, burn.Duration);
        CombatBurstEffect.Play(stage.Effects, CombatEffectCatalog.Current.ForSource("fire_impact"), at, burn.RadiusM);
        var light = new OmniLight3D { OmniRange = burn.RadiusM * 2.2f, LightEnergy = 1.4f, LightColor = new Color("ff7a2a"), Position = Vector3.Up * 1.6f };
        light.AddToGroup(VisionSystem.LightGroup);
        zone.AddChild(light);
        stage.FlickerLights.Add(light);
        light.SetMeta("base_energy", 1.4f);
        stage.Director.MakeNoise(at, stage.Director.Rules.NoiseFire);
        stage.Sfx.Attach("fire_loop", zone);
        return zone;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _life -= dt;
        if (_life <= 0)
        {
            QueueFree();
            return;
        }
        float r2 = _radius * _radius;
        foreach (var enemy in _stage.Enemies)
        {
            if (enemy.IsDead) continue;
            var d = enemy.GlobalPosition - GlobalPosition;
            d.Y = 0;
            if (d.LengthSquared() > r2) continue;
            enemy.Burn(_dps * dt, _by != null && IsInstanceValid(_by) ? _by : null);
            // Occasional sparks so burning zombies read as burning.
            float t = _ticks.GetValueOrDefault(enemy) - dt;
            if (t <= 0)
            {
                t = .35f;
                _stage.Effects.Stuffing(enemy.GlobalPosition + Vector3.Up, Vector3.Up, new Color("ff9a3c"), 3);
            }
            _ticks[enemy] = t;
        }

        foreach (var player in System.Linq.Enumerable.ToList(_stage.AlivePlayers))
        {
            var p = player.GlobalPosition - GlobalPosition;
            p.Y = 0;
            if (p.LengthSquared() > r2) continue;
            // Whole points only, so each survivor carries their own fraction between frames.
            float debt = _playerDebts.GetValueOrDefault(player) + _playerDps * dt;
            int whole = (int)debt;
            _playerDebts[player] = debt - whole;
            if (whole > 0) player.TakeDamage(whole, Vector3.Zero, 0);
        }
    }

    public override void _ExitTree() => _stage.FlickerLights.RemoveAll(l => !IsInstanceValid(l) || l.GetParent() == this);
}
