using Godot;
using System;
using System.Linq;

namespace HordeShooter;

public enum CompanionKind { Pet, Drone, Sidekick }

/// <summary>
/// What a companion is, as a key the network and the cards share: "pet:dog:twin_blaster", "drone:bee:laser",
/// "sidekick:cadet" (decisions.md M-27 · A-20 · A-21).
/// </summary>
public sealed record CompanionSpec(CompanionKind Kind, string Model, string Gear)
{
    public static readonly string[] PetSpecies = { "dog", "turtle", "rabbit" };
    public static readonly string[] PetGear = { "twin_blaster", "rocket_pod" };
    /// <summary>Drone gear a card can give; the light is a look only and is left out.</summary>
    public static readonly string[] DroneGear = { "blaster", "laser", "support" };

    public string Key => Kind == CompanionKind.Sidekick ? $"sidekick:{Model}" : $"{Kind.ToString().ToLowerInvariant()}:{Model}:{Gear}";

    public static CompanionSpec? Parse(string key)
    {
        var parts = key.Split(':');
        return parts switch
        {
            ["pet", var s, var g] when PetSpecies.Contains(s) && PetGear.Contains(g) => new(CompanionKind.Pet, s, g),
            ["drone", var b, var g] when DroneModel.BodyIds.Contains(b) && DroneModel.EquipmentIds.Contains(g) => new(CompanionKind.Drone, b, g),
            ["sidekick", var id] when SidekickModel.PresetIds.Contains(id) => new(CompanionKind.Sidekick, id, ""),
            _ => null,
        };
    }

    /// <summary>"강아지 펫 · 쌍열 블래스터", "꿀벌 드론 · 레이저", "거위 부사수".</summary>
    public string DisplayName => Kind switch
    {
        CompanionKind.Sidekick => Loc.Text("sidekick_" + Model),
        _ => Loc.Format("fmt_companion_name", Loc.Text($"companion_{Kind.ToString().ToLowerInvariant()}_{Model}"), Loc.Text("companion_gear_" + Gear)),
    };
}

/// <summary>
/// A companion in play (decisions.md M-27 소환수, plan 4장): follows its survivor, fights what comes near, and comes
/// back when the survivor gets too far — a windup it has started (the laser) still goes off first. Pets and drones
/// have no body: monsters, shots and survivors pass through them and nothing can hurt them. A sidekick has no body
/// either, but monsters touching it wear it down; it falls in a burst of stuffing and gets up again beside its
/// survivor a few seconds later. Every PC moves and animates its copy; only the deciding PC deals damage and
/// decides when a sidekick falls (sent with the squad, <see cref="CompanionSquad"/>). Numbers are first guesses.
/// </summary>
public partial class Companion : Node3D
{
    // Following (metres): past Leash it drops the fight and comes back until within Return; past Teleport it jumps.
    public const float Leash = 12f, Return = 5f, Teleport = 28f, FightReach = 6f;
    public const float PetScale = .6f, DroneScale = .4f, DroneHeight = 2.4f;
    public const float SidekickHealth = 60f, SidekickRevive = 8f, ContactTick = .5f, ContactShare = .5f;

    public Stage Stage { get; set; } = null!;
    public Player Survivor { get; set; } = null!;
    public CompanionSpec Spec { get; set; } = null!;
    /// <summary>Its place around the survivor (0, 1, 2…).</summary>
    public int Slot { get; set; }

    public PetModel? Pet { get; private set; }
    public DroneModel? Drone { get; private set; }
    public SidekickModel? Sidekick { get; private set; }

    public bool Returning { get; private set; }
    /// <summary>Winding up an attack (the laser); it is not cut short by the leash.</summary>
    public bool Casting => _castLeft > 0;
    public bool Down { get; private set; }
    public float Health { get; private set; } = SidekickHealth;
    /// <summary>Attacks gone off and damage dealt (checks, lab).</summary>
    public int Attacks { get; private set; }
    public int Casts { get; private set; }
    public int Revives { get; private set; }
    public float Speed { get; private set; }

    private float _cooldown = 1f, _castLeft, _downLeft, _contactTimer, _gait, _clock;
    private Enemy? _castTarget;
    private Vector3 _castDir;
    private OmniLight3D? _light;

    private bool IsPet => Spec.Kind == CompanionKind.Pet;
    private bool IsDrone => Spec.Kind == CompanionKind.Drone;
    private bool IsSidekick => Spec.Kind == CompanionKind.Sidekick;
    private float MoveSpeed => IsDrone ? 9f : IsPet ? (Spec.Model == "turtle" ? 6f : 7.5f) : 7f;

    public override void _Ready()
    {
        switch (Spec.Kind)
        {
            case CompanionKind.Pet:
                Pet = PetModel.Create(Spec.Model);
                Pet.Scale = Vector3.One * PetScale;
                AddChild(Pet);
                Pet.SetWeapon(Spec.Gear);
                break;
            case CompanionKind.Drone:
                Drone = DroneModel.Create(Spec.Model);
                Drone.Scale = Vector3.One * DroneScale;
                AddChild(Drone);
                Drone.SetEquipment(Spec.Gear);
                if (Spec.Gear == "light")
                {
                    _light = new OmniLight3D { LightColor = new Color("fff3c4"), OmniRange = 6, LightEnergy = 1.6f };
                    AddChild(_light);
                }
                break;
            default:
                Sidekick = SidekickModel.Create(Spec.Model);
                AddChild(Sidekick);
                break;
        }
        GlobalPosition = Spot();
    }

    /// <summary>Where it stands around its survivor: behind, to the sides, drones above.</summary>
    private Vector3 Spot()
    {
        float angle = Mathf.Pi * (.75f + .5f * (Slot % 3)) + Slot / 3 * .4f;
        var at = Survivor.GlobalPosition + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (1.8f + Slot / 3 * .9f);
        return new Vector3(at.X, IsDrone ? DroneHeight : 0, at.Z);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(Survivor)) return;
        float dt = (float)delta;
        _clock += dt;
        if (Down)
        {
            if (Stage.Authoritative && (_downLeft -= dt) <= 0) Revive();
            return;
        }
        if (Stage.Authoritative && IsSidekick) TakeContact(dt);
        if (Down) return;

        var home = Spot();
        float fromOwner = Flat(GlobalPosition - Survivor.GlobalPosition).Length();
        if (fromOwner > Teleport) GlobalPosition = home;
        Returning = fromOwner > Leash || Returning && fromOwner > Return;
        _cooldown -= dt;

        var goal = home;
        if (_castLeft > 0)
        {
            // A started windup goes off even if the survivor runs off; it follows afterwards.
            _castLeft -= dt;
            Face(_castDir);
            if (_castLeft <= 0) Fire(_castTarget);
            goal = GlobalPosition;
        }
        else if (!Returning && Target() is { } target)
        {
            var to = Flat(target.GlobalPosition - GlobalPosition);
            Face(to);
            // Ground companions step toward the fight, never further than FightReach from their survivor.
            if (!IsDrone)
            {
                var toward = Survivor.GlobalPosition + Flat(target.GlobalPosition - Survivor.GlobalPosition).LimitLength(FightReach);
                goal = new Vector3(toward.X, 0, toward.Z);
                if (Flat(goal - GlobalPosition).Length() < Range * .6f) goal = GlobalPosition;
            }
            if (_cooldown <= 0) StartAttack(target, to);
        }
        else if (Spec.Gear == "support" && _cooldown <= 0) Support();
        Move(goal, dt);
        Animate(dt);
    }

    private void Move(Vector3 goal, float dt)
    {
        var to = goal - GlobalPosition;
        if (!IsDrone) to.Y = 0;
        float speed = MoveSpeed * (Returning ? 1.35f : 1f);
        var step = to.Length() < .15f ? Vector3.Zero : to.LimitLength(speed * dt);
        GlobalPosition += step;
        if (IsDrone) GlobalPosition = GlobalPosition with { Y = Mathf.Lerp(GlobalPosition.Y, DroneHeight, dt * 4) };
        Speed = dt > 0 ? Flat(step).Length() / dt : 0;
        if (Speed > .3f && _castLeft <= 0) Face(Flat(step));
    }

    private void Face(Vector3 dir)
    {
        dir.Y = 0;
        if (dir.LengthSquared() < .0001f) return;
        // Models face +Z; turn smoothly toward where it goes or aims.
        float yaw = Mathf.Atan2(dir.X, dir.Z);
        Rotation = Rotation with { Y = Mathf.LerpAngle(Rotation.Y, yaw, .25f) };
    }

    /// <summary>Feet move with the ground covered (no sliding); idle when standing.</summary>
    private void Animate(float dt)
    {
        float moving = Mathf.Clamp(Speed / 2.5f, 0, 1);
        // A full gait cycle per ~0.9 m at this size: the pose clock runs with the distance covered.
        _gait += Speed * dt * (Spec.Model == "turtle" ? 1.6f : 1.1f);
        Pet?.Pose(_gait, moving);
        Drone?.Pose(_clock, moving);
        Sidekick?.Animate(dt, moving);
    }

    // ---------- fighting ----------

    private float Range => (Spec.Kind, Spec.Gear) switch
    {
        (CompanionKind.Drone, "laser") => 16f,
        (CompanionKind.Pet, "rocket_pod") => 14f,
        _ => 12f,
    };

    /// <summary>The nearest living monster it can reach and that is near its survivor (it does not wander off).</summary>
    private Enemy? Target()
    {
        if (Spec.Gear is "support" or "light") return null;
        Enemy? best = null;
        float bestD = Range;
        foreach (var e in Stage.Enemies)
        {
            if (!IsInstanceValid(e) || e.IsDead || e is TrainingDummy) continue;
            float d = Flat(e.GlobalPosition - GlobalPosition).Length();
            if (d < bestD && Flat(e.GlobalPosition - Survivor.GlobalPosition).Length() < Leash) { best = e; bestD = d; }
        }
        return best;
    }

    private void StartAttack(Enemy target, Vector3 to)
    {
        float windup = (Spec.Kind, Spec.Gear) switch
        {
            (CompanionKind.Drone, "laser") => .9f,
            (CompanionKind.Pet, "rocket_pod") => .45f,
            _ => 0,
        };
        if (windup <= 0) { Fire(target); return; }
        _castLeft = windup;
        _castTarget = target;
        _castDir = to.Normalized();
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * 1.2f, "!", new Color("6be7ff"));
    }

    private Vector3 Muzzle => GlobalPosition + (IsDrone ? Vector3.Down * .2f : Vector3.Up * (IsPet ? .6f : .8f));

    private void Fire(Enemy? target)
    {
        Attacks++;
        var from = Muzzle;
        var aim = target != null && IsInstanceValid(target) && !target.IsDead ? target.GlobalPosition + Vector3.Up * .8f : from + _castDir * Range;
        var dir = (aim - from).Normalized();
        switch (Spec.Kind, Spec.Gear)
        {
            case (CompanionKind.Drone, "laser"):
                Casts++;
                _cooldown = 4f;
                var end = from + Flat(dir).Normalized() * Range;
                WeaponShotEffect.Play(Stage.Effects, ArmoryCatalog.Weapon("railgun"), from, from, end, false);
                foreach (var e in Stage.Enemies.ToList())
                    if (IsInstanceValid(e) && !e.IsDead && DistanceToSegment(e.GlobalPosition, from, end) < .6f + e.Radius) Hit(e, 70, dir, 90);
                break;
            case (CompanionKind.Pet, "rocket_pod"):
                Casts++;
                _cooldown = 3f;
                Stage.Effects.Explosion(aim, 2.6f, new Color("ffb347"));
                Stage.Sfx.PlayAt("explosion", aim, -6);
                foreach (var e in Stage.Enemies.ToList())
                    if (IsInstanceValid(e) && !e.IsDead && Flat(e.GlobalPosition - aim).Length() < 2.6f + e.Radius) Hit(e, 45, Flat(e.GlobalPosition - aim).Normalized(), 140);
                break;
            default:
                var def = IsSidekick ? ArmoryCatalog.Weapon(Sidekick!.WeaponId) : ArmoryCatalog.Weapon("mp5");
                float damage = IsSidekick ? def.Damage * .5f : IsPet ? 7 : 5;
                _cooldown = IsSidekick ? Mathf.Max(.25f, def.FireInterval * 1.6f) : IsPet ? .35f : .3f;
                WeaponShotEffect.Play(Stage.Effects, def, from, from, aim, false);
                Stage.Sfx.PlayAt(def.ShotSound, from, -8, 1.15f);
                if (target != null && IsInstanceValid(target) && !target.IsDead) Hit(target, damage, dir, 25);
                break;
        }
    }

    /// <summary>Damage only on the deciding PC; the hit names the survivor, so their card effects follow it.</summary>
    private void Hit(Enemy enemy, float damage, Vector3 dir, float power)
    {
        if (!Stage.Authoritative) return;
        enemy.TakeHit(new HitInfo(damage, dir, enemy.GlobalPosition + Vector3.Up, false, power, Survivor, "companion"));
    }

    /// <summary>The support pod mends its survivor a little now and then.</summary>
    private void Support()
    {
        _cooldown = 2.5f;
        if (!Stage.Authoritative || Survivor.IsDead || Survivor.Health >= Survivor.MaxHealth) return;
        Survivor.Heal(4);
        Attacks++;
        Stage.Effects.Puff(Survivor.GlobalPosition + Vector3.Up, .8f, new Color("7dff9a"));
    }

    // ---------- the sidekick falls and gets up ----------

    /// <summary>Monsters touching a sidekick wear it down (it is not anyone's target).</summary>
    private void TakeContact(float dt)
    {
        if ((_contactTimer -= dt) > 0) return;
        _contactTimer = ContactTick;
        float hurt = 0;
        foreach (var e in Stage.Enemies)
            if (IsInstanceValid(e) && !e.IsDead && e is not TrainingDummy && Flat(e.GlobalPosition - GlobalPosition).Length() < .6f + e.Radius)
                hurt += e.Monster.AttackDamage * ContactShare * ContactTick / Mathf.Max(.2f, e.Monster.AttackInterval);
        if (hurt > 0) Damage(hurt);
    }

    /// <summary>A sidekick takes <paramref name="amount"/>; at zero it falls. Pets and drones cannot be hurt.</summary>
    public void Damage(float amount)
    {
        if (!IsSidekick || Down || amount <= 0) return;
        Health -= amount;
        if (Health <= 0) Fall();
    }

    private void Fall()
    {
        SetDown(true);
        Stage.RaiseCompanionDowned(this);
        _downLeft = SidekickRevive;
    }

    private void Revive()
    {
        SetDown(false);
        Revives++;
        GlobalPosition = Spot();
        Stage.Effects.Puff(GlobalPosition + Vector3.Up * .6f, 1f, new Color("fff3c4"));
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * 1.8f, Loc.Text("fx_companion_revive"), new Color("7dff9a"));
    }

    /// <summary>Falls or gets up (also a client copy, from the squad sync).</summary>
    public void SetDown(bool down)
    {
        if (Down == down) return;
        Down = down;
        _castLeft = 0;
        if (down)
        {
            Stage.Effects.Stuffing(GlobalPosition + Vector3.Up * .6f, Vector3.Up, new Color("f3e3c4"), 30);
            Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * 1.6f, Loc.Text("fx_companion_down"), new Color("ffb347"));
        }
        else Health = SidekickHealth;
        Visible = !down;
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);

    private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = Flat(b - a);
        var ap = Flat(p - a);
        float t = ab.LengthSquared() < .0001f ? 0 : Mathf.Clamp(ap.Dot(ab) / ab.LengthSquared(), 0, 1);
        return (ap - ab * t).Length();
    }
}
