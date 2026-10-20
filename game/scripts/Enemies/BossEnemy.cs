using Godot;

namespace HordeShooter;

/// <summary>
/// Survivor's RPG-style boss (doc/menu-multiplayer-plan.md 3.1): a big bull that keeps the bull charge and adds
/// a telegraphed ground slam (red disc fills, then everyone inside is launched) and a summon roar that calls commons
/// around it. Below enrage_hp_pct it gets faster and uses its skills more often. Numbers: mode_rules.json "boss".
/// </summary>
public partial class BossEnemy : BullEnemy
{
    public const string MonsterId = "boss";

    public BossTuning Skills => ModeTuning.Current.Boss;
    public float MaxHealth => Monster.Hp;
    /// <summary>Winding up the slam (it stands still and can be interrupted by a flashbang).</summary>
    public bool Casting => _slamTime >= 0;
    public bool Enraged { get; private set; }
    public int Slams { get; private set; }
    public int Summons { get; private set; }

    private float _slamTime = -1, _slamCooldown = 3f, _summonTimer = 6f;
    private Vector3 _slamAt;
    private MeshInstance3D? _disc, _fill;
    private StandardMaterial3D? _discMat, _fillMat;

    private float CooldownScale => Enraged ? Skills.EnrageCooldownScale : 1;

    protected override Vector3 Think(float dt)
    {
        if (Casting)
        {
            TickSlam(dt);
            return Vector3.Zero;
        }
        _slamCooldown -= dt;
        _summonTimer -= dt;
        if (!Enraged && Health <= MaxHealth * Skills.EnrageHpPct / 100f) Enrage();
        if (State == Mode.Chase && StunTime <= 0 && Awake)
        {
            if (_summonTimer <= 0)
            {
                Summon();
                _summonTimer = Skills.SummonEvery * CooldownScale;
            }
            var target = CurrentTarget(dt);
            if (target != null && _slamCooldown <= 0)
            {
                var to = target.GlobalPosition - GlobalPosition;
                to.Y = 0;
                if (to.Length() < Skills.SlamRange + Radius) { StartSlam(); return Vector3.Zero; }
            }
        }
        return base.Think(dt);
    }

    // ---------- slam ----------

    /// <summary>Exposed for checks and scripted moments.</summary>
    public void StartSlam()
    {
        if (IsDead || Casting) return;
        CancelMelee();
        _slamTime = 0;
        _slamAt = GlobalPosition;
        RevealTo(Target ?? Stage.NearestAlivePlayer(GlobalPosition), Skills.SlamWindup + VisionSystem.RevealAfterHit);
        Stage.Sfx.PlayAt("bull_snort", GlobalPosition + Vector3.Up, 3, .7f);
        ShowDisc();
    }

    private void TickSlam(float dt)
    {
        _slamTime += dt;
        float t = Mathf.Clamp(_slamTime / Skills.SlamWindup, 0, 1);
        Kick = .4f + .6f * t;
        Hurt = Mathf.Max(Hurt, .25f * t);
        if (_fill != null) _fill.Scale = new Vector3(Mathf.Max(.02f, t), 1, Mathf.Max(.02f, t));
        if (_discMat != null) _discMat.AlbedoColor = new Color(1, .2f, .15f, .16f + .14f * Mathf.Abs(Mathf.Sin(_slamTime * 14)));
        if (_slamTime >= Skills.SlamWindup) Slam();
    }

    private void Slam()
    {
        _slamTime = -1;
        _slamCooldown = Skills.SlamCooldown * CooldownScale;
        Slams++;
        HideDisc();
        float r = Skills.SlamRadius;
        Stage.Effects.Explosion(_slamAt, r * .55f, new Color("ff7a4a"));
        Stage.Effects.PaintSplat(_slamAt, 2.4f);
        Stage.Sfx.PlayAt("bull_bonk", _slamAt + Vector3.Up, 4, .6f);
        Stage.Cam.AddShake(.6f);
        Kick = 1;
        foreach (var player in System.Linq.Enumerable.ToList(Stage.AlivePlayers))
        {
            var away = player.GlobalPosition - _slamAt;
            away.Y = 0;
            if (away.Length() > r) continue;
            var dir = away.LengthSquared() > .01f ? away.Normalized() : Vector3.Right;
            player.TakeDamage(Skills.SlamDamage, dir, 0);
            player.Launch(dir * 9f + Vector3.Up * 6f, 1f);
            Stage.Effects.FloatingText(player.GlobalPosition + Vector3.Up * 2.8f, "SLAM!", new Color("ff9a6b"));
        }
        // Its own minions go flying too: the slam clears space as much as it hurts.
        foreach (var other in Stage.Enemies)
        {
            if (other == this || other.IsDead) continue;
            var away = other.GlobalPosition - _slamAt;
            away.Y = 0;
            if (away.Length() > r) continue;
            other.Shove(away, 140f, other.IsSpecial ? 30f : 999f);
        }
    }

    private void CancelSlam()
    {
        _slamTime = -1;
        _slamCooldown = Skills.SlamCooldown * CooldownScale;
        HideDisc();
    }

    private void ShowDisc()
    {
        float r = Skills.SlamRadius;
        _discMat = Mat.Fade(new Color(1, .2f, .15f, .2f));
        _disc = Mat.Mesh(Stage.Effects, new CylinderMesh { TopRadius = r, BottomRadius = r, Height = .02f, RadialSegments = 48 }, _slamAt + Vector3.Up * .03f, _discMat);
        _fillMat = Mat.Fade(new Color(1, .35f, .2f, .38f));
        _fill = Mat.Mesh(Stage.Effects, new CylinderMesh { TopRadius = r, BottomRadius = r, Height = .025f, RadialSegments = 48 }, _slamAt + Vector3.Up * .05f, _fillMat);
        _fill.Scale = new Vector3(.02f, 1, .02f);
    }

    private void HideDisc()
    {
        _disc?.QueueFree();
        _fill?.QueueFree();
        _disc = _fill = null;
    }

    // ---------- summon and enrage ----------

    /// <summary>Roars and calls summon_count commons in a ring around itself.</summary>
    public int Summon()
    {
        if (IsDead || !Stage.Authoritative) return 0;
        Summons++;
        Kick = 1;
        Stage.Effects.SonarPing(GlobalPosition);
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * (Definition.ColliderHeight + .8f), Loc.Text("boss_summon"), new Color("ffcf5c"));
        Stage.Sfx.PlayAt("bull_snort", GlobalPosition + Vector3.Up, 2, .5f);
        int room = HordeDirector.MaxCommons - Stage.Director.AliveCommons;
        int count = Mathf.Min(Skills.SummonCount, room);
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Tau * i / Mathf.Max(1, count) + Rng.RandfRange(-.2f, .2f);
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var at = GlobalPosition + dir * (Radius + 1.6f);
            if (Stage.Flow.IsBlocked(at)) at = GlobalPosition + dir * (Radius + .6f);
            var minion = Stage.SpawnCommon(at, awake: true);
            minion.ScaleHealth(ModeTuning.Current.Survivor.CommonHpScale);
            minion.LeapDown(dir);
        }
        return count;
    }

    private void Enrage()
    {
        Enraged = true;
        SpeedScale = Skills.EnrageSpeed;
        _summonTimer = Mathf.Min(_summonTimer, 1.5f);
        Stage.ShowNotice(Loc.Text("msg_boss_enraged"), 3);
        Stage.Effects.FloatingText(GlobalPosition + Vector3.Up * (Definition.ColliderHeight + .8f), Loc.Text("boss_enraged"), new Color("ff5b4f"));
        Stage.Cam.AddShake(.3f);
    }

    protected override void OnStunned()
    {
        if (Casting) CancelSlam();
        base.OnStunned();
    }

    public override void Die(Vector3 direction, float force)
    {
        HideDisc();
        base.Die(direction, force);
    }

    public override void _ExitTree()
    {
        HideDisc();
        base._ExitTree();
    }
}
