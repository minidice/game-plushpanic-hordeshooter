using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// RPG-style boss (decisions.md M-24): a big bull whose phases and skills come from content/data/bosses/&lt;id&gt;.json
/// (<see cref="BossDefinition"/>). Each phase starts below a health share, may announce itself, speeds the boss up,
/// shortens its cooldowns and unlocks more skills: the bull charge, a telegraphed slam, a summon roar, a fan of shots.
/// The deciding PC runs the skills; a client's puppet only shows the result.
/// </summary>
public partial class BossEnemy : BullEnemy
{
    public const string MonsterId = "boss";

    public BossDefinition Data { get; private set; } = null!;
    public float MaxHealth => Monster.Hp;
    public int Phase { get; private set; }
    public BossPhase CurrentPhase => Data.Phases[Phase];
    /// <summary>Winding up a slam or a fan of shots (stands still; a flashbang cuts it short).</summary>
    public bool Casting => _casting != null;
    /// <summary>How often each skill has gone off (checks, tuning).</summary>
    public IReadOnlyDictionary<BossSkillKind, int> Uses => _uses;

    private readonly Dictionary<string, float> _cooldowns = new();
    private readonly Dictionary<BossSkillKind, int> _uses = new();
    private BossSkill? _casting;
    private float _castTime;
    private Vector3 _castAt, _castDir;
    private MeshInstance3D? _disc, _fill;
    private StandardMaterial3D? _discMat;
    private readonly List<MeshInstance3D> _lanes = new();

    public override void _Ready()
    {
        Data = BossDatabase.Get(Monster.Id);
        base._Ready();
        // Nothing fires the moment it walks in.
        foreach (var skill in Data.Skills.Values) _cooldowns[skill.Id] = Mathf.Min(skill.Cooldown, 3f) * .5f + 2f;
    }

    protected override bool CanCharge => CurrentPhase.Skills.Contains("charge");

    protected override Vector3 Think(float dt)
    {
        if (Casting)
        {
            TickCast(dt);
            return Vector3.Zero;
        }
        UpdatePhase();
        foreach (var id in _cooldowns.Keys.ToList()) _cooldowns[id] -= dt;
        if (State == Mode.Chase && StunTime <= 0 && Awake && TryStartSkill(dt)) return Vector3.Zero;
        return base.Think(dt);
    }

    private void UpdatePhase()
    {
        int phase = Data.PhaseAt(Health / MaxHealth);
        if (phase <= Phase) return;
        Phase = phase;
        SpeedScale = CurrentPhase.SpeedScale;
        if (CurrentPhase.NoticeKey != "")
        {
            Stage.ShowNotice(Loc.Text(CurrentPhase.NoticeKey), 3);
            // Above the summon call, which can come in the same moment.
            Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * (Definition.ColliderHeight + 1.9f), Loc.Text("boss_phase_up"), new Color("ff5b4f"));
        }
        Stage.Cam.AddShake(.3f);
        // A new phase opens with its new skills nearly ready.
        foreach (var id in CurrentPhase.Skills) _cooldowns[id] = Mathf.Min(_cooldowns.GetValueOrDefault(id), 1.5f);
    }

    private bool TryStartSkill(float dt)
    {
        var target = CurrentTarget(dt);
        foreach (var id in CurrentPhase.Skills)
        {
            if (!Data.Skills.TryGetValue(id, out var skill) || skill.Kind == BossSkillKind.Charge || _cooldowns.GetValueOrDefault(id) > 0) continue;
            switch (skill.Kind)
            {
                case BossSkillKind.Summon:
                    Summon(skill);
                    return true;
                case BossSkillKind.Slam when target != null && Flat(target.GlobalPosition - GlobalPosition).Length() < skill.Range + Radius:
                case BossSkillKind.FanShot when target != null && Flat(target.GlobalPosition - GlobalPosition).Length() < skill.Range && HasLineOfSight(dt):
                    StartCast(skill, target);
                    return true;
            }
        }
        return false;
    }

    private void Cooldown(BossSkill skill) => _cooldowns[skill.Id] = skill.Cooldown * CurrentPhase.CooldownScale;

    private void Used(BossSkill skill) => _uses[skill.Kind] = _uses.GetValueOrDefault(skill.Kind) + 1;

    // ---------- casting (slam, fan shot) ----------

    /// <summary>Starts the first skill of that kind in the boss data now (checks and scripted moments).</summary>
    public bool StartSkill(BossSkillKind kind)
    {
        var skill = Data.Skills.Values.FirstOrDefault(s => s.Kind == kind);
        if (skill == null || IsDead || Casting) return false;
        if (kind == BossSkillKind.Summon) { Summon(skill); return true; }
        StartCast(skill, Target ?? Stage.NearestAlivePlayer(GlobalPosition));
        return true;
    }

    private void StartCast(BossSkill skill, Player? target)
    {
        CancelMelee();
        _casting = skill;
        _castTime = 0;
        _castAt = GlobalPosition;
        var to = target != null ? Flat(target.GlobalPosition - GlobalPosition) : Rig.Forward;
        _castDir = to.LengthSquared() > .01f ? to.Normalized() : Vector3.Forward;
        RevealTo(target, skill.Windup + VisionSystem.RevealAfterHit);
        Stage.Sfx.PlayAt("bull_snort", GlobalPosition + Vector3.Up, 3, .7f);
        if (skill.Kind == BossSkillKind.Slam) ShowDisc(skill.Radius);
        else ShowLanes(skill);
    }

    private void TickCast(float dt)
    {
        var skill = _casting!;
        _castTime += dt;
        float t = Mathf.Clamp(_castTime / Mathf.Max(.01f, skill.Windup), 0, 1);
        Kick = .4f + .6f * t;
        Hurt = Mathf.Max(Hurt, .25f * t);
        if (skill.Kind == BossSkillKind.FanShot) FaceOverride = _castDir;
        if (_fill != null) _fill.Scale = new Vector3(Mathf.Max(.02f, t), 1, Mathf.Max(.02f, t));
        if (_discMat != null) _discMat.AlbedoColor = new Color(1, .2f, .15f, .16f + .14f * Mathf.Abs(Mathf.Sin(_castTime * 14)));
        foreach (var lane in _lanes) lane.Scale = new Vector3(1, 1, Mathf.Max(.05f, t));
        if (_castTime < skill.Windup) return;
        _casting = null;
        HideTelegraphs();
        Cooldown(skill);
        Used(skill);
        if (skill.Kind == BossSkillKind.Slam) Slam(skill);
        else FanShot(skill);
    }

    private void Slam(BossSkill skill)
    {
        float r = skill.Radius;
        Stage.Effects.Explosion(_castAt, r * .55f, new Color("ff7a4a"));
        Stage.Effects.PaintSplat(_castAt, 2.4f);
        Stage.Sfx.PlayAt("bull_bonk", _castAt + Vector3.Up, 4, .6f);
        Stage.Cam.AddShake(.6f);
        Kick = 1;
        foreach (var player in Stage.AlivePlayers.ToList())
        {
            var away = Flat(player.GlobalPosition - _castAt);
            if (away.Length() > r) continue;
            var dir = away.LengthSquared() > .01f ? away.Normalized() : Vector3.Right;
            player.TakeDamage(skill.Damage, dir, 0);
            player.Launch(dir * 9f + Vector3.Up * 6f, 1f);
            Stage.Effects.FloatingText(player.GlobalPosition + Vector3.Up * 2.8f, "SLAM!", new Color("ff9a6b"));
        }
        // Its own minions go flying too: the slam clears space as much as it hurts.
        foreach (var other in Stage.Enemies)
        {
            if (other == this || other.IsDead) continue;
            var away = Flat(other.GlobalPosition - _castAt);
            if (away.Length() <= r) other.Shove(away, 140f, other.IsSpecial ? 30f : 999f);
        }
    }

    private void FanShot(BossSkill skill)
    {
        Kick = 1;
        var trait = new RangedTrait(skill.Range, skill.Windup, skill.Cooldown, skill.Speed, 0, 0);
        var from = GlobalPosition + Vector3.Up * (Definition.ColliderHeight * .45f) + _castDir * Radius;
        int count = Mathf.Max(1, skill.Count);
        for (int i = 0; i < count; i++)
        {
            float a = count == 1 ? 0 : Mathf.DegToRad(-skill.SpreadDegrees * .5f + skill.SpreadDegrees * i / (count - 1));
            var dir = _castDir.Rotated(Vector3.Up, a);
            var projectile = new EnemyProjectile();
            projectile.Setup(Stage, from, from + dir * skill.Range, trait, skill.Damage);
            Stage.AddChild(projectile);
            Stage.Net?.OnEnemyProjectile(this, from, from + dir * skill.Range);
        }
        Stage.Sfx.PlayAt("shot_shotgun", from, 3, .7f);
        Stage.Cam.AddShake(.25f);
    }

    /// <summary>Roars and calls commons in a ring around itself.</summary>
    private void Summon(BossSkill skill)
    {
        Cooldown(skill);
        if (IsDead || !Stage.Authoritative) return;
        Used(skill);
        Kick = 1;
        Stage.Effects.SonarPing(GlobalPosition);
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * (Definition.ColliderHeight + .8f), Loc.Text("boss_summon"), new Color("ffcf5c"));
        Stage.Sfx.PlayAt("bull_snort", GlobalPosition + Vector3.Up, 2, .5f);
        int count = Mathf.Min(skill.Count, HordeDirector.MaxCommons - Stage.Director.AliveCommons);
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Tau * i / Mathf.Max(1, count) + Rng.RandfRange(-.2f, .2f);
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var at = GlobalPosition + dir * (Radius + 1.6f);
            if (Stage.Flow.IsBlocked(at)) at = GlobalPosition + dir * (Radius + .6f);
            var minion = Stage.SpawnCommon(at, awake: true);
            minion.ScaleHealth(skill.HpScale);
            minion.LeapDown(dir);
        }
    }

    private void CancelCast()
    {
        if (_casting != null) Cooldown(_casting);
        _casting = null;
        HideTelegraphs();
    }

    // ---------- telegraphs ----------

    private void ShowDisc(float r)
    {
        _discMat = Mat.Fade(new Color(1, .2f, .15f, .2f));
        _disc = Mat.Mesh(Stage.Effects, new CylinderMesh { TopRadius = r, BottomRadius = r, Height = .02f, RadialSegments = 48 }, _castAt + Vector3.Up * .03f, _discMat);
        _fill = Mat.Mesh(Stage.Effects, new CylinderMesh { TopRadius = r, BottomRadius = r, Height = .025f, RadialSegments = 48 }, _castAt + Vector3.Up * .05f,
            Mat.Fade(new Color(1, .35f, .2f, .38f)));
        _fill.Scale = new Vector3(.02f, 1, .02f);
    }

    /// <summary>One red lane per shot on the floor, growing outward as the windup fills.</summary>
    private void ShowLanes(BossSkill skill)
    {
        int count = Mathf.Max(1, skill.Count);
        var mat = Mat.Fade(new Color(1, .25f, .2f, .3f));
        for (int i = 0; i < count; i++)
        {
            float a = count == 1 ? 0 : Mathf.DegToRad(-skill.SpreadDegrees * .5f + skill.SpreadDegrees * i / (count - 1));
            var dir = _castDir.Rotated(Vector3.Up, a);
            var pivot = new MeshInstance3D();
            Stage.Effects.AddChild(pivot);
            pivot.GlobalPosition = _castAt + Vector3.Up * .04f;
            pivot.LookAt(pivot.GlobalPosition + dir, Vector3.Up);
            Mat.Box(pivot, new Vector3(0, 0, -skill.Range * .5f), new Vector3(.35f, .02f, skill.Range), mat);
            pivot.Scale = new Vector3(1, 1, .05f);
            _lanes.Add(pivot);
        }
    }

    private void HideTelegraphs()
    {
        _disc?.QueueFree();
        _fill?.QueueFree();
        _disc = _fill = null;
        _discMat = null;
        foreach (var lane in _lanes) lane.QueueFree();
        _lanes.Clear();
    }

    protected override void OnStunned()
    {
        if (Casting) CancelCast();
        base.OnStunned();
    }

    public override void Die(Vector3 direction, float force)
    {
        HideTelegraphs();
        base.Die(direction, force);
    }

    public override void _ExitTree()
    {
        HideTelegraphs();
        base._ExitTree();
    }

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
