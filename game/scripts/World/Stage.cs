using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

public enum Objective
{
    LeaveSafeRoom,
    ReachShutter,
    HoldOut,
    ReachSafehouse,
    CloseSafehouse,
    Cleared,
}

public enum StageState
{
    Playing,
    Dead,
    Cleared,
}

/// <summary>
/// Owns one playable stage: systems (flow field, director, vision, effects), the player, enemies,
/// and the L4D campaign beat: safe room → route → crescendo → safehouse. Geometry comes from
/// <see cref="StageOneLayout"/>; a second stage would be another layout class.
/// </summary>
public partial class Stage : Node3D
{
    public const float WallPad = .45f;

    /// <summary>Set before adding to the tree to bypass the menu selection (tests, captures).</summary>
    public string? CharacterOverride { get; set; }
    public string? ThemeOverride { get; set; }
    public string ThemeId { get; private set; } = "zombie";
    /// <summary>Tests turn this off so the director only spawns on request.</summary>
    public bool DirectorEnabled { get; set; } = true;

    /// <summary>The local, human survivor (camera, HUD, input, objectives).</summary>
    public Player Player { get; private set; } = null!;
    /// <summary>Every survivor in the stage: the local player first, then AI squadmates.</summary>
    public List<Player> Players { get; } = new();
    public IEnumerable<Player> AlivePlayers
    {
        get { foreach (var p in Players) if (IsInstanceValid(p) && !p.IsDead) yield return p; }
    }
    public CameraRig Cam { get; private set; } = null!;
    public Effects Effects { get; private set; } = null!;
    public Sfx Sfx { get; private set; } = null!;
    public HordeDirector Director { get; private set; } = null!;
    public VisionSystem Vision { get; private set; } = null!;
    public FlowField Flow { get; private set; } = null!;
    public CrowdGrid Crowd { get; } = new();
    /// <summary>Route toward the safehouse: how far along the stage a point is (AI never walks ahead of the leader).</summary>
    public FlowField ExitFlow { get; private set; } = null!;
    /// <summary>Route toward the local player, for squadmates finding their way back.</summary>
    public FlowField LeaderFlow { get; private set; } = null!;
    /// <summary>Breadcrumbs of where the local player has walked (1 m apart, newest last).</summary>
    public List<Vector3> Trail { get; } = new();
    /// <summary>AI squadmate count. Null = the menu choice when this is the running game scene, none in test scenes.</summary>
    public int? SquadOverride { get; set; }
    public List<SquadBrain> Squad { get; } = new();
    /// <summary>Bumped whenever walls or doors change, so copies of the flow layout know to refresh.</summary>
    public int FlowVersion { get; private set; }
    /// <summary>Game option "revive". Null = the saved setting when this is the running game scene, off in test scenes.</summary>
    public bool? ReviveOverride { get; set; }
    public bool ReviveEnabled { get; private set; }
    /// <summary>The downed teammate the local player can pick up right now (for the prompt).</summary>
    public Player? RevivePromptTarget { get; private set; }
    private readonly List<(SquadRole Role, int Slot, string Species, float At)> _respawnQueue = new();
    private readonly Dictionary<int, int> _respawnsUsed = new();
    private bool _safehouseReached;
    public Hud Hud { get; private set; } = null!;
    public StageOneLayout Layout => _layout!;

    public List<Enemy> Enemies { get; } = new();
    public List<RigidBody3D> Props { get; } = new();
    /// <summary>Loud devices that go off when shot or caught in a blast (spawn-design.md 4장).</summary>
    public List<AlarmCar> AlarmCars { get; } = new();
    public List<Interactable> Interactables { get; } = new();
    public List<OmniLight3D> FlickerLights { get; } = new();
    /// <summary>Common infected (monsters.csv row "plush"); its look is the zombie version of the player species when mirror_player is on.</summary>
    public MonsterDefinition CommonDefinition { get; private set; } = null!;
    public ThrownItem? Lure { get; private set; }
    public FlowField? LureFlow { get; private set; }

    public Objective Objective { get; private set; } = Objective.LeaveSafeRoom;
    public StageState State { get; private set; } = StageState.Playing;
    public float Elapsed { get; private set; }
    public int Kills { get; private set; }
    public int SpecialKills { get; private set; }
    public Interactable? FocusedInteractable { get; private set; }
    public string Notice { get; private set; } = "";
    public float NoticeTime { get; private set; }

    private StageOneLayout? _layout;
    private Node3D _enemyRoot = null!;
    private AudioStreamPlayer? _alarm;
    private float _groanTimer = 2f;
    private readonly List<Enemy> _pending = new();
    private readonly List<Vector3> _flowTargets = new();
    private readonly RandomNumberGenerator _rng = new();
    private float _flickerTime;

    public override void _Ready()
    {
        GameInput.Ensure();
        Flow = new FlowField(-2, -12, 128, 12);
        Sfx = new Sfx();
        AddChild(Sfx);
        Sfx.Bed("ambience_loop", AudioSetup.AmbienceBus);
        Effects = new Effects { Name = "Effects" };
        Director = new HordeDirector { Name = "Director" };
        Vision = new VisionSystem { Name = "Vision" };
        AddChild(Director);
        AddChild(Vision);
        AddChild(Effects);
        BuildEnvironment();

        ReviveEnabled = ReviveOverride ?? (GetTree().CurrentScene == this && GameSettings.ReviveEnabled);
        _layout = StageOneLayout.Build(this);

        var def = CharacterCatalog.Player(CharacterOverride ?? GameSession.SelectedCharacterId);
        Player = new Player();
        Player.Setup(this, def);
        Player.Position = Layout.PlayerStart;
        AddChild(Player);
        Players.Add(Player);
        Player.Died += CheckWipe;
        ThemeId = StageTheme.Normalize(ThemeOverride ?? GameSession.SelectedTheme);
        CommonDefinition = MonsterDatabase.Get(StageTheme.Commons(ThemeId)[0]);

        Cam = new CameraRig { Name = "Camera", Target = Player, Player = Player };
        AddChild(Cam);
        Cam.SnapTo(Layout.PlayerStart);
        Effects.RegisterCorpseObserver(Player, Cam);

        _enemyRoot = new Node3D { Name = "Enemies" };
        AddChild(_enemyRoot);
        Flow.Update(Player.GlobalPosition);
        RefreshRouteFlows();
        Director.UseEncounters(SpawnDatabase.Current.Stage1);
        Trail.Add(Player.GlobalPosition);
        // The menu's squad size applies to the real game scene; tests ask for squadmates explicitly.
        SpawnSquad(SquadOverride ?? (GetTree().CurrentScene == this ? GameSession.SquadSize : 0));

        Hud = new Hud { Name = "Hud" };
        AddChild(Hud);
        ShowNotice(Loc.Text("msg_stage_start"));
    }

    private void BuildEnvironment()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("080e20"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("a9c0df"),
            AmbientLightEnergy = .24f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            TonemapMode = Godot.Environment.ToneMapper.Agx,
            TonemapExposure = 1.1f,
            SsaoEnabled = true,
            SsaoRadius = 1.2f,
            SsaoIntensity = 1.4f,
            GlowEnabled = true,
            GlowIntensity = .65f,
            GlowBloom = .05f,
        };
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(new DirectionalLight3D
        {
            Name = "Moon",
            RotationDegrees = new Vector3(-62, -28, 0),
            LightColor = new Color("a9c7ed"),
            LightEnergy = .38f,
            ShadowEnabled = true,
            ShadowBlur = 2f,
            DirectionalShadowMaxDistance = 60,
        });
    }

    // ---------- spawning ----------

    /// <summary>
    /// Enemies spawned mid-frame are queued and joined to <see cref="Enemies"/> at the start of the
    /// next physics frame, so code iterating the list never sees it change underneath it.
    /// </summary>
    public T SpawnEnemy<T>(MonsterDefinition monster, Vector3 at, bool awake) where T : Enemy, new()
    {
        var enemy = new T();
        enemy.Setup(this, monster, LookFor(monster), awake);
        // Ledge entries start on a wall top; everything else stands on the floor.
        enemy.Position = new Vector3(at.X, Mathf.Max(at.Y, .05f), at.Z);
        _enemyRoot.AddChild(enemy);
        if (Player != null) Vision.Apply(enemy);
        _pending.Add(enemy);
        return enemy;
    }

    /// <summary>Spawn with the class that matches the monster's behavior.</summary>
    public Enemy Spawn(MonsterDefinition monster, Vector3 at, bool awake) => monster.Behavior switch
    {
        MonsterBehavior.Balloon => SpawnEnemy<BalloonEnemy>(monster, at, awake),
        MonsterBehavior.Bull => SpawnEnemy<BullEnemy>(monster, at, awake),
        MonsterBehavior.Tether or MonsterBehavior.Leaper => SpawnEnemy<CaptureEnemy>(monster, at, awake),
        MonsterBehavior.Spitter or MonsterBehavior.Shooter => SpawnEnemy<RangedEnemy>(monster, at, awake),
        _ => SpawnEnemy<Enemy>(monster, at, awake),
    };

    private int _commonIndex, _specialIndex;
    public Enemy SpawnCommon(Vector3 at, bool awake)
    {
        var pool = StageTheme.Commons(ThemeId);
        return Spawn(MonsterDatabase.Get(pool[_commonIndex++ % pool.Length]), at, awake);
    }

    public Enemy SpawnThemeSpecial(Vector3 at, bool awake)
    {
        var pool = StageTheme.Specials(ThemeId);
        return Spawn(MonsterDatabase.Get(pool[_specialIndex++ % pool.Length]), at, awake);
    }

    public Enemy SpawnInitialSpecial(int index, Vector3 at)
    {
        var pool = StageTheme.Specials(ThemeId);
        _specialIndex = Math.Max(_specialIndex, index + 1);
        return Spawn(MonsterDatabase.Get(pool[index % pool.Length]), at, false);
    }

    private readonly Dictionary<string, CharacterDefinition> _looks = new();

    /// <summary>The look for a monster, built once per stage (zombie looks copy the player's species).</summary>
    public CharacterDefinition LookFor(MonsterDefinition monster)
    {
        if (_looks.TryGetValue(monster.Id, out var look)) return look;
        var baseLook = CharacterCatalog.Enemy(monster.LookId);
        if (monster.Id is "wild_animal" or "armed_animal" or "armed_veteran")
        {
            look = (CharacterDefinition)CharacterCatalog.Player(monster.Id == "wild_animal" ? "raccoon" : "redpanda").Duplicate();
            look.Role = CharacterRole.Enemy;
        }
        else look = monster.MirrorPlayer ? CharacterDefinition.MakeZombie(Player.Definition, baseLook) : baseLook;
        _looks[monster.Id] = look;
        return look;
    }

    private void RefreshRouteFlows()
    {
        // The route counts doors as open: progress and "forward" must exist before the shutter opens.
        ExitFlow = Flow.CopyLayout();
        Layout.ApplyFlow(ExitFlow, doors: false);
        ExitFlow.Update(Layout.SafehouseCenter);
        LeaderFlow = Flow.CopyLayout();
        LeaderFlow.Update(Player.GlobalPosition);
    }

    /// <summary>Metres left to the safehouse along the route (smaller = further ahead). Off the route: by the +X axis.</summary>
    public float Progress(Vector3 at)
    {
        int cost = ExitFlow.Distance(at);
        return cost == FlowField.Unreachable ? Layout.SafehouseCenter.X - at.X : cost / 10f;
    }

    /// <summary>Direction the route continues from <paramref name="at"/> (the level runs along +X, level-design-rules.md).</summary>
    public Vector3 RouteForward(Vector3 at)
    {
        var dir = ExitFlow.Direction(at);
        return dir == Vector3.Zero ? Vector3.Right : dir;
    }

    /// <summary>
    /// The nearest breadcrumb the local player already walked that is at least <paramref name="min"/> metres
    /// behind them along the trail, off every screen and not inside a wall (searched up to <paramref name="max"/>).
    /// Null if none fits yet.
    /// </summary>
    public Vector3? TrailSpotBehind(float min, float max)
    {
        float walked = 0;
        for (int i = Trail.Count - 1; i > 0; i--)
        {
            walked += Trail[i].DistanceTo(Trail[i - 1]);
            if (walked < min) continue;
            if (walked > max) break;
            var spot = Trail[i - 1];
            if (Flow.IsBlocked(spot) || Effects.IsOnScreen(spot + Vector3.Up, 1.2f)) continue;
            // Backtracking can put old breadcrumbs ahead on the route: never appear there.
            if (!Player.IsDead && Progress(spot) < Progress(Player.GlobalPosition) - .5f) continue;
            return spot;
        }
        return null;
    }

    private void SpawnSquad(int count)
    {
        var roles = SquadDatabase.Current.Roles;
        if (count <= 0 || roles.Count == 0) return;
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var species = new List<string> { "goose", "duck", "raccoon", "redpanda" };
        species.Remove(Player.Definition.Id);
        var offsets = new[] { new Vector3(-1.4f, 0, -1.3f), new Vector3(-1.4f, 0, 1.3f), new Vector3(-2.6f, 0, 0) };
        for (int i = 0; i < count; i++)
        {
            // Three squadmates take one role each; a single one gets a random role.
            var role = count >= roles.Count ? roles[i % roles.Count] : roles[rng.RandiRange(0, roles.Count - 1)];
            var def = CharacterCatalog.Player(species[i % species.Count]);
            var at = Layout.PlayerStart + offsets[i % offsets.Length];
            if (Flow.IsBlocked(at)) at = Layout.PlayerStart;
            var bot = AddSurvivor(def, at, true, $"Squad{i + 1}", new AppearanceProfile());
            Squad.Add(SquadBrain.Attach(this, bot, role, i));
            WatchSquadmate(bot, role, i);
        }
    }

    private void WatchSquadmate(Player bot, SquadRole role, int slot)
    {
        bot.Died += () =>
        {
            CheckWipe();
            // With revive on, a fallen squadmate walks back in from the walked route after a while (M-17, M-18).
            var rules = SquadDatabase.Current.Rules;
            if (!ReviveEnabled || State != StageState.Playing || _respawnsUsed.GetValueOrDefault(slot) >= rules.RespawnsPerStage) return;
            _respawnsUsed[slot] = _respawnsUsed.GetValueOrDefault(slot) + 1;
            _respawnQueue.Add((role, slot, bot.Definition.Id, Elapsed + rules.RespawnDelay));
        };
    }

    public int RespawnsUsed(int slot) => _respawnsUsed.GetValueOrDefault(slot);

    /// <summary>Checks only: makes queued squadmate respawns due now instead of after respawn_delay_s.</summary>
    public void ExpediteRespawnsForTest()
    {
        for (int i = 0; i < _respawnQueue.Count; i++) _respawnQueue[i] = _respawnQueue[i] with { At = Elapsed };
    }
    public int PendingRespawns => _respawnQueue.Count;

    private void UpdateRespawns()
    {
        if (_respawnQueue.Count == 0) return;
        var rules = SquadDatabase.Current.Rules;
        for (int i = _respawnQueue.Count - 1; i >= 0; i--)
        {
            var (role, slot, species, at) = _respawnQueue[i];
            if (Elapsed < at || Player.IsDead) continue;
            // Off screen, on a spot the leader already walked; wait if there is none yet.
            if (TrailSpotBehind(rules.CatchupBehindMin, rules.CatchupBehindMax) is not { } spot) continue;
            _respawnQueue.RemoveAt(i);
            var old = Squad.Find(b => b.Slot == slot);
            if (old != null)
            {
                Squad.Remove(old);
                Players.Remove(old.Bot);
                // The corpse is its own node; the dead survivor (and its brain) can go.
                if (IsInstanceValid(old.Bot)) old.Bot.QueueFree();
            }
            var bot = AddSurvivor(CharacterCatalog.Player(species), spot, true, $"Squad{slot + 1}_{RespawnsUsed(slot)}", new AppearanceProfile());
            bot.SetSpawnHealth(rules.RespawnHealth);
            Squad.Add(SquadBrain.Attach(this, bot, role, slot));
            WatchSquadmate(bot, role, slot);
            ShowNotice(Loc.Format("fmt_squad_returned", Loc.Text(bot.Definition.DisplayName)), 3);
        }
    }

    /// <summary>
    /// By prompt, not node name: Godot renames same-named siblings, so a second "Medkit" was not one (I-61).
    /// </summary>
    public static bool IsMedkit(Interactable item) => item.PromptKey == "ui_use_medkit";

    /// <summary>A squadmate takes an ammo pile or a medkit (they never take weapons, decisions.md M-13).</summary>
    public bool TakePickup(Interactable item, Player who)
    {
        if (!item.Enabled || !item.Visible) return false;
        if (IsMedkit(item))
        {
            if (who.Medkits > 0) return false;
            who.Medkits = 1;
            item.Enabled = false;
            item.Visible = false;
            Sfx.PlayAt("pickup", who.GlobalPosition + Vector3.Up);
            return true;
        }
        if (item.PromptKey == "ui_use_ammo") { RefillAmmo(who); return true; }
        return false;
    }

    /// <summary>Adds another survivor (an AI squadmate) at <paramref name="at"/>.</summary>
    public Player AddSurvivor(CharacterDefinition def, Vector3 at, bool bot, string name, AppearanceProfile? appearance = null)
    {
        // Appearance must be set before AddChild: _Ready applies it.
        var survivor = new Player { IsBot = bot, ManualControl = bot, Appearance = appearance };
        survivor.Setup(this, def);
        survivor.Name = name;
        survivor.Position = at;
        AddChild(survivor);
        Players.Add(survivor);
        return survivor;
    }

    /// <summary>The living survivor closest to <paramref name="from"/> in a straight line, or null when all are down.</summary>
    public Player? NearestAlivePlayer(Vector3 from)
    {
        Player? best = null;
        float bestD = float.MaxValue;
        foreach (var p in AlivePlayers)
        {
            float d = p.GlobalPosition.DistanceSquaredTo(from);
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    public int PendingEnemies => _pending.Count;
    public IEnumerable<Enemy> AllEnemies => System.Linq.Enumerable.Concat(Enemies, _pending);

    /// <summary>Re-stamp the whole flow grid (doors opening or closing). Layout is null while it is still being built.</summary>
    public void RebuildFlow()
    {
        _layout?.ApplyFlow();
        FlowVersion++;
        if (ExitFlow != null) RefreshRouteFlows();
        if (Lure != null && LureFlow != null)
        {
            LureFlow = Flow.CopyLayout();
            LureFlow.Update(Lure.GlobalPosition);
        }
    }

    public void OnEnemyDied(Enemy enemy)
    {
        Kills++;
        if (enemy.IsSpecial) SpecialKills++;
    }

    // ---------- frame ----------

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Enemies.AddRange(_pending);
        _pending.Clear();
        Enemies.RemoveAll(e => e.IsDead || !IsInstanceValid(e) || e.IsQueuedForDeletion());
        Crowd.Rebuild(Enemies);
        _flowTargets.Clear();
        foreach (var p in AlivePlayers) _flowTargets.Add(p.GlobalPosition);
        if (_flowTargets.Count > 0) Flow.Update(_flowTargets);
        if (!Player.IsDead)
        {
            LeaderFlow.Update(Player.GlobalPosition);
            if (Trail.Count == 0 || Trail[^1].DistanceTo(Player.GlobalPosition) >= 1f)
            {
                Trail.Add(Player.GlobalPosition);
                if (Trail.Count > 600) Trail.RemoveAt(0);
            }
        }
        NoticeTime = Mathf.Max(0, NoticeTime - dt);

        // Areas fill one ahead of the furthest survivor. Tests that switch the director off keep only the start fill.
        if (DirectorEnabled) Director.UpdateAreas();

        UpdateCrowdAudio(dt);
        AudioSetup.SetHelium(!Player.IsDead && Player.HeliumTime > 0);
        AudioSetup.SetMuffled(Player.BlindTime > 0);

        if (State != StageState.Playing) return;
        Elapsed += dt;
        UpdateObjective();
        UpdateInteraction();
        UpdateRespawns();
    }

    /// <summary>Somebody out there is always moaning. Picks a random awake zombie near the player.</summary>
    private void UpdateCrowdAudio(float dt)
    {
        _groanTimer -= dt;
        if (_groanTimer > 0 || Enemies.Count == 0) return;
        _groanTimer = _rng.RandfRange(1.2f, 3.2f);
        var near = Enemies.FindAll(e => !e.IsDead && e.Awake && !e.IsSpecial && e.GlobalPosition.DistanceTo(Player.GlobalPosition) < 22f);
        if (near.Count == 0) return;
        var who = near[_rng.RandiRange(0, near.Count - 1)];
        Sfx.PlayAt("groan", who.GlobalPosition + Vector3.Up, 0, _rng.RandfRange(.85f, 1.2f));
    }

    public override void _ExitTree()
    {
        AudioSetup.SetHelium(false);
        AudioSetup.SetMuffled(false);
    }

    public override void _Process(double delta)
    {
        _flickerTime -= (float)delta;
        if (_flickerTime > 0) return;
        _flickerTime = .07f;
        foreach (var light in FlickerLights)
        {
            var baseEnergy = (float)light.GetMeta("base_energy", 1f);
            light.LightEnergy = _rng.Randf() < .12f ? baseEnergy * _rng.RandfRange(0, .3f) : baseEnergy;
        }
    }

    private void UpdateObjective()
    {
        switch (Objective)
        {
            case Objective.HoldOut:
                Layout.Shutter.SetProgress(1 - Director.CrescendoRemaining / Director.CrescendoDuration);
                if (!Director.CrescendoActive)
                {
                    _alarm?.QueueFree();
                    _alarm = null;
                    Sfx.PlayAt("shutter", Layout.Shutter.GlobalPosition + Vector3.Up * 1.5f);
                    Layout.Shutter.SetOpen(true, 1.2f);
                    Objective = Objective.ReachSafehouse;
                    ShowNotice(Loc.Text("msg_stage_shutter_open"));
                }
                break;
            case Objective.ReachSafehouse:
                if (Layout.InSafehouse(Player.GlobalPosition))
                {
                    if (!_safehouseReached)
                        foreach (var survivor in AlivePlayers) if (!survivor.IsDowned) survivor.ResetDowns();
                    _safehouseReached = true;
                    Objective = Objective.CloseSafehouse;
                    Layout.SafehouseSwitch.Enabled = true;
                    ShowNotice(Loc.Text("msg_stage_close_to_clear"));
                }
                break;
            case Objective.CloseSafehouse:
                if (!Layout.InSafehouse(Player.GlobalPosition))
                {
                    Objective = Objective.ReachSafehouse;
                    Layout.SafehouseSwitch.Enabled = false;
                }
                break;
        }
    }

    private void UpdateInteraction()
    {
        FocusedInteractable = null;
        RevivePromptTarget = null;
        if (Player.IsDead || Player.IsDowned || Player.IsStunned || Player.IsRestrained) return;
        RevivePromptTarget = Player.Reviving ?? Player.DownedTeammateInReach();
        float best = float.MaxValue;
        foreach (var item in Interactables)
        {
            if (!item.Enabled || !item.Visible) continue;
            float d = item.GlobalPosition.DistanceTo(Player.GlobalPosition);
            if (d < item.Radius && d < best)
            {
                best = d;
                FocusedInteractable = item;
            }
        }
        // E next to a downed teammate means "pick them up", never also "use the thing on the floor".
        if (FocusedInteractable != null && RevivePromptTarget == null && !Player.ManualControl && Input.IsActionJustPressed(GameInput.Interact))
            FocusedInteractable.Use();
    }

    // ---------- objective beats (called by layout interactables) ----------

    public void LeaveSafeRoom()
    {
        if (Objective != Objective.LeaveSafeRoom) return;
        Layout.StartDoor.SetOpen(true);
        Layout.StartDoorSwitch.Enabled = false;
        Sfx.PlayAt("door", Layout.StartDoor.GlobalPosition + Vector3.Up);
        Objective = Objective.ReachShutter;
        if (DirectorEnabled) Director.Begin();
        ShowNotice(Loc.Text("msg_stage_find_shutter"));
    }

    public void StartCrescendo()
    {
        if (Objective != Objective.ReachShutter) return;
        Objective = Objective.HoldOut;
        Layout.ShutterPanel.Enabled = false;
        _alarm = Sfx.Bed("alarm_loop", AudioSetup.SfxBus);
        Director.Begin();
        Director.StartCrescendo(Layout.CrescendoSeconds);
        Cam.AddShake(.3f);
        ShowNotice(Loc.Text("msg_stage_alarm"));
    }

    public bool TryCloseSafehouse()
    {
        if (Objective != Objective.CloseSafehouse) return false;
        foreach (var e in AllEnemies)
        {
            if (!e.IsDead && Layout.NearSafehouse(e.GlobalPosition))
            {
                ShowNotice(Loc.Text("msg_stage_enemies_inside"));
                return false;
            }
        }
        Layout.SafehouseDoor.SetOpen(false);
        Layout.SafehouseSwitch.Enabled = false;
        Sfx.PlayAt("door", Layout.SafehouseDoor.GlobalPosition + Vector3.Up);
        Sfx.Play("stage_clear");
        Objective = Objective.Cleared;
        State = StageState.Cleared;
        Director.Stop();
        Player.Invulnerable = true;
        ShowNotice(Loc.Text("ui_end_clear"));
        return true;
    }

    /// <summary>
    /// Game over when every human is dead, or when every survivor (squad included) is down or dead —
    /// a downed player can still be picked up by a squadmate (doc/ai-squad-plan.md 6.3).
    /// </summary>
    private void CheckWipe()
    {
        if (State != StageState.Playing) return;
        bool humanAlive = false, anyoneStanding = false;
        foreach (var p in Players)
        {
            if (!IsInstanceValid(p)) continue;
            if (!p.IsBot && !p.IsDead) humanAlive = true;
            if (!p.IsDead && !p.IsDowned) anyoneStanding = true;
        }
        if (!humanAlive || !anyoneStanding) OnPlayerDied();
    }

    public void OnSurvivorDowned(Player who)
    {
        if (who == Player) ShowNotice(Loc.Text("msg_you_are_down"), 3);
        CheckWipe();
    }

    private void OnPlayerDied()
    {
        foreach (var p in Players)
        {
            if (!IsInstanceValid(p)) continue;
            p.ReleaseRestraint();
            p.EndDowned();
        }
        State = StageState.Dead;
        Director.Stop();
        _alarm?.QueueFree();
        _alarm = null;
        Sfx.Play("game_over");
        ShowNotice(Loc.Text("msg_stage_down"));
    }

    // ---------- lure and pickups ----------

    public void SetLure(ThrownItem lure)
    {
        Lure = lure;
        LureFlow = Flow.CopyLayout();
        LureFlow.Update(lure.GlobalPosition);
        Director.MakeNoise(lure.GlobalPosition, lure.AttractRadius);
    }

    public void ClearLure()
    {
        Lure = null;
        LureFlow = null;
    }

    public void PickUp(WeaponPickup pickup)
    {
        var at = Player.GlobalPosition + Vector3.Up * 2.6f;
        if (pickup.Throwable is { } throwable)
        {
            if (!Player.Loadout.AddThrowable(throwable))
            {
                ShowNotice(Loc.Format("fmt_pickup_throwable_max", throwable.DisplayName, throwable.MaxCarry), 2);
                return;
            }
            Effects.FloatingText(at, Loc.Format("fmt_pickup_throwable", throwable.DisplayName), new Color("8dff9c"));
            Sfx.Play("pickup");
            RemovePickup(pickup);
            return;
        }
        if (pickup.Weapon is not { } weapon) return;
        var (result, dropped) = Player.Weapons.Pickup(weapon, pickup.State);
        switch (result)
        {
            case PickupResult.Full:
                ShowNotice(Loc.Text("msg_pickup_full"), 2);
                return;
            case PickupResult.Upgraded:
                Effects.FloatingText(at, Loc.Format("fmt_pickup_upgraded", Player.Gun.Def.DisplayName), new Color("6fb6ff"));
                break;
            case PickupResult.Refilled:
                Effects.FloatingText(at, Loc.Text("msg_pickup_ammo"), new Color("ffd36b"));
                if (dropped != null)
                {
                    // Only part of it fit: leave the rest where it was lying.
                    var spot = pickup.GlobalPosition;
                    RemovePickup(pickup);
                    WeaponPickup.ForWeapon(this, spot, dropped.Def, dropped);
                    return;
                }
                break;
            default:
                Effects.FloatingText(at, weapon.DisplayName, new Color("ffb347"));
                break;
        }
        Sfx.Play("pickup");
        RemovePickup(pickup);
        if (dropped != null)
        {
            var back = -Player.AimDirection * .9f;
            var spot = Player.GlobalPosition + back;
            if (Flow.IsBlocked(spot)) spot = Player.GlobalPosition;
            WeaponPickup.ForWeapon(this, spot, dropped.Def, dropped);
        }
    }

    private void RemovePickup(WeaponPickup pickup)
    {
        Interactables.Remove(pickup);
        pickup.QueueFree();
    }

    /// <summary>Ammo piles refill slot 2 only, and never the rocket launcher or mortar.</summary>
    public void RefillAmmo() => RefillAmmo(Player);

    public void RefillAmmo(Player who)
    {
        bool local = who == Player;
        var at = who.GlobalPosition + Vector3.Up * 2.6f;
        var primary = who.Loadout.Primary;
        if (primary == null)
        {
            if (local) ShowNotice(Loc.Text("msg_refill_no_primary"), 2);
            return;
        }
        if (!primary.Def.Refillable)
        {
            if (local) ShowNotice(Loc.Format("fmt_refill_not_refillable", primary.Def.DisplayName), 2.5f);
            return;
        }
        int got = primary.Refill();
        if (got > 0) Sfx.PlayAt("reload_end", who.GlobalPosition + Vector3.Up);
        Effects.FloatingText(at, got > 0 ? Loc.Format("fmt_refill_got", got) : Loc.Text("msg_refill_full"), new Color("ffd36b"));
    }

    public void ShowNotice(string text, float seconds = 4f)
    {
        Notice = text;
        NoticeTime = seconds;
    }

    public string ObjectiveText => Objective switch
    {
        Objective.LeaveSafeRoom => Loc.Text("ui_objective_leave_saferoom"),
        Objective.ReachShutter => Loc.Text("ui_objective_reach_shutter"),
        Objective.HoldOut => Loc.Format("fmt_objective_hold_out", Mathf.CeilToInt(Mathf.Max(0, Director.CrescendoRemaining))),
        Objective.ReachSafehouse => Loc.Text("ui_objective_reach_safehouse"),
        Objective.CloseSafehouse => Loc.Text("ui_objective_close_safehouse"),
        _ => Loc.Text("ui_objective_cleared"),
    };

    public void Restart() => GetTree().ReloadCurrentScene();

    public void BackToMenu() => GetTree().ChangeSceneToFile("res://scenes/Menu.tscn");

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) QuitHygiene.Collect();
    }
}
