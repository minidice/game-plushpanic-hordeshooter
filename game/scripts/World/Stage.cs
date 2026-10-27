using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

public enum Objective
{
    LeaveSafeRoom,
    ReachShutter,
    HoldOut,
    ReachSafehouse,
    CloseSafehouse,
    Cleared,
    /// <summary>Survival / Survivor: the mode's own rules give the goal (<see cref="Stage.ModeRules"/>).</summary>
    ModeGoal,
    /// <summary>Onslaught: picking weapons from the floor racks before the swarm starts.</summary>
    ModeArming,
    /// <summary>Scenario: the safehouse is shut and its hatch leads on to the next stage (decisions.md L-14).</summary>
    TakeExit,
}

public enum StageState
{
    Playing,
    Dead,
    Cleared,
}

/// <summary>
/// Owns one playable stage: systems (flow field, director, vision, effects), the player, enemies,
/// and the L4D campaign beat: safe room → route → crescendo → safehouse → the hatch to the next stage. Geometry and the
/// doors, panel and safehouse of those beats come from <see cref="StageMap"/> (Stage 1 is <see cref="StageOneLayout"/>,
/// Stage 2 <see cref="StageTwoMap"/>; I-79).
/// </summary>
public partial class Stage : Node3D
{
    public const float WallPad = .45f;

    /// <summary>Set before adding to the tree to bypass the menu selection (tests, captures).</summary>
    public string? CharacterOverride { get; set; }
    public string? ThemeOverride { get; set; }
    public string ThemeId { get; private set; } = "zombie";
    /// <summary>Set before adding to the tree to bypass the menu's mode (tests, captures).</summary>
    public GameMode? ModeOverride { get; set; }
    /// <summary>The mode being played: the menu or room choice in the running game scene, Scenario in test scenes.</summary>
    public GameMode Mode { get; private set; } = GameMode.Scenario;
    /// <summary>Set before adding to the tree to play another map than the mode's own (checks, a generated map).</summary>
    public StageMap? MapOverride { get; set; }
    /// <summary>
    /// A zone id to start a scenario at instead of the safe room (level tests). Null = <see cref="GameSession.StartZone"/>
    /// in the running game scene on its first stage; "" = the normal start.
    /// </summary>
    public string? StartZoneOverride { get; set; }
    /// <summary>The zone this stage started at for a level test, or "" (the normal start).</summary>
    public string StartedZone { get; private set; } = "";
    /// <summary>
    /// Whether shutting a scenario safehouse opens the hatch to the next stage. Null = offline in the running game scene
    /// (a network match ends at the first safehouse for now, I-90); test scenes stop at the clear unless they ask.
    /// </summary>
    public bool? NextStageOverride { get; set; }
    public bool NextStageEnabled { get; private set; }
    /// <summary>Whether this stage takes over a stage change in progress (camera ease, carried kit, the opening circle). Null = the running game scene.</summary>
    public bool? ArrivalOverride { get; set; }
    /// <summary>How long the interact key has been held on a hold-to-use thing (0..1).</summary>
    public float HoldProgress { get; private set; }
    /// <summary>The last hold was broken by a hit; cleared when the key is let go (so it does not restart under fire by itself).</summary>
    public bool HoldCancelled { get; private set; }
    private Interactable? _holding;
    /// <summary>Set before adding to the tree to run other rules than the mode's own (the card lab).</summary>
    public ModeRules? RulesOverride { get; set; }
    /// <summary>Survival / Survivor rules (null in Scenario).</summary>
    public ModeRules? ModeRules { get; private set; }
    /// <summary>Tests turn this off so the director only spawns on request.</summary>
    public bool DirectorEnabled { get; set; } = true;
    /// <summary>Offline by default. Set before the stage enters the tree (multiplayer-sync-design.md).</summary>
    public NetRole Role { get; set; } = NetRole.Offline;
    /// <summary>This PC decides the game: offline or the host. A client only shows the host's decisions.</summary>
    public bool Authoritative => Role != NetRole.Client;
    /// <summary>Raised by the host when an enemy dies, with the direction and force of its fall.</summary>
    public event System.Action<Enemy, Vector3, float>? EnemyKilled;
    /// <summary>Raised by the deciding PC when a bullet, pellet or swing hits an enemy (before it may die).</summary>
    public event System.Action<Enemy, HitInfo>? EnemyHit;
    /// <summary>A client asks the host to use an interactable instead of using it itself.</summary>
    public System.Action<Interactable>? RequestUse { get; set; }
    private int _nextInteractableId;
    internal int NextPropId;
    /// <summary>The network link when this stage is part of a match (null offline).</summary>
    public StageNet? Net { get; private set; }

    /// <summary>Registers a usable thing with the next id in build order (the same on every PC).</summary>
    public void AddInteractable(Interactable item)
    {
        if (item.NetId == 0) item.NetId = ++_nextInteractableId;
        else _nextInteractableId = System.Math.Max(_nextInteractableId, item.NetId);
        Interactables.Add(item);
    }

    internal void RaiseEnemyHit(Enemy enemy, HitInfo hit) => EnemyHit?.Invoke(enemy, hit);

    internal void RaiseEnemyKilled(Enemy enemy, Vector3 direction, float force)
    {
        EnemyKilled?.Invoke(enemy, direction, force);
        Net?.OnEnemyKilled(enemy, direction, force);
    }

    /// <summary>Another person's survivor (multiplayer). Never a bot: it counts as a human for every rule.</summary>
    public Player AddRemoteSurvivor(CharacterDefinition def, Vector3 at, int peer)
    {
        var survivor = new Player { IsRemote = true, ManualControl = true, PeerId = peer };
        survivor.Setup(this, def);
        survivor.Name = "Peer" + peer;
        survivor.Position = at;
        AddChild(survivor);
        Players.Add(survivor);
        survivor.Died += CheckWipe; // the last human dying ends the match, whoever it is (I-73)
        return survivor;
    }

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
    /// <summary>Height tiers on the flow field's cells (decisions.md L-15). Flat unless the map paints some.</summary>
    public TierMap Tiers { get; private set; } = null!;
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
    /// <summary>
    /// People for the party table (decisions.md M-5, M-31). Null = counted when this is the running game scene; test scenes
    /// keep the data's own numbers (the 4-person row) unless they set one.
    /// </summary>
    public float? PartyOverride { get; set; }
    /// <summary>How much one AI squadmate weighs in <see cref="PartySize"/> (decisions.md M-34).</summary>
    public const float SquadmateWeight = .5f;
    /// <summary>
    /// How many people play: each human 1, each AI squadmate half (M-34: alone with one squadmate is 1.5). A host counts
    /// everyone in its room too, so friends still loading at the start already count for the first fill.
    /// </summary>
    public float PartySize
    {
        get
        {
            if (PartyOverride is { } forced) return forced;
            int humans = Players.Count(p => IsInstanceValid(p) && !p.IsBot);
            int bots = Players.Count(p => IsInstanceValid(p) && p.IsBot);
            float people = humans + bots * SquadmateWeight;
            return Role == NetRole.Host && NetLaunch.Lobby is { } lobby ? System.Math.Max(people, lobby.Members.Count) : people;
        }
    }
    /// <summary>
    /// People (no AI squadmates) for things a base lays out one each, like its medkits (decisions.md M-35): the party override
    /// in checks, the room's members in a match (host and clients count the same room), else one.
    /// </summary>
    public int HumanCount => PartyOverride is { } forced ? Mathf.Max(1, Mathf.FloorToInt(forced))
        : Role != NetRole.Offline && NetLaunch.Lobby is { } lobby ? Mathf.Max(1, lobby.Members.Count) : 1;
    /// <summary>The party table's row in effect now (the 4-person row, i.e. the data as written, in test scenes).</summary>
    public PartyScale Party => PartyOverride != null || GetTree().CurrentScene == this ? PartyScaling.For(PartySize) : PartyScale.Baseline;
    private float _commonCarry;

    /// <summary>
    /// <paramref name="count"/> commons scaled for the party. The fraction carries to the next call, so small waves still
    /// add up to the table's share over a stage.
    /// </summary>
    public int ScaleCommons(int count)
    {
        if (count <= 0) return 0;
        _commonCarry += count * Party.CommonCount;
        int n = Mathf.FloorToInt(_commonCarry + .0001f);
        _commonCarry -= n;
        return n;
    }

    /// <summary>Game option "revive". Null = the saved setting when this is the running game scene, off in test scenes.</summary>
    public bool? ReviveOverride { get; set; }
    public bool ReviveEnabled { get; private set; }
    /// <summary>The downed teammate the local player can pick up right now (for the prompt).</summary>
    public Player? RevivePromptTarget { get; private set; }
    private readonly List<(SquadRole Role, int Slot, string Species, float At)> _respawnQueue = new();
    private readonly Dictionary<int, int> _respawnsUsed = new();
    private bool _safehouseReached;
    public Hud Hud { get; private set; } = null!;
    /// <summary>The ground being played (any mode).</summary>
    public StageMap Map => _map!;
    /// <summary>Stage 1's doors, panels and safehouse, for the scenario beats (and checks) that need them.</summary>
    public StageOneLayout Layout => _map as StageOneLayout ?? throw new System.InvalidOperationException("this stage is not played on Stage 1");

    public List<Enemy> Enemies { get; } = new();
    public List<RigidBody3D> Props { get; } = new();
    /// <summary>Placed objects that can still break (object_interactions.json <c>destruction</c>, C-29).</summary>
    public List<ObjectBody> Breakables { get; } = new();
    /// <summary>Loud devices that go off when shot or caught in a blast (spawn-design.md 4장).</summary>
    public List<AlarmCar> AlarmCars { get; } = new();
    public List<Interactable> Interactables { get; } = new();
    /// <summary>Ladders up tier cliffs (L-16), ziplines and gondolas (L-18).</summary>
    public List<Ladder> Ladders { get; } = new();
    public List<Zipline> Ziplines { get; } = new();
    public List<Gondola> Gondolas { get; } = new();
    public List<OmniLight3D> FlickerLights { get; } = new();
    /// <summary>Common infected (monsters.csv row "plush"); its look is the zombie version of the player species when mirror_player is on.</summary>
    public MonsterDefinition CommonDefinition { get; private set; } = null!;
    public ThrownItem? Lure { get; private set; }
    public FlowField? LureFlow { get; private set; }

    public Objective Objective { get; private set; } = Objective.LeaveSafeRoom;
    public StageState State { get; private set; } = StageState.Playing;

    /// <summary>
    /// Everyone stopped for a team decision (the special card vote, M-29): monsters, clocks, bodies and physics hold
    /// on every PC; the network, the HUD and the vote keep running. Online too, unlike the pause menu.
    /// </summary>
    public bool AllStopped { get; private set; }

    public void SetAllStop(bool on)
    {
        if (AllStopped == on) return;
        AllStopped = on;
        GetTree().Paused = on || (Hud != null && Hud.Paused && Role == NetRole.Offline);
        // The link keeps talking so votes and the host's word get through.
        if (Net != null) Net.ProcessMode = on ? ProcessModeEnum.Always : ProcessModeEnum.Inherit;
    }
    public float Elapsed { get; private set; }
    public int Kills { get; private set; }
    public int SpecialKills { get; private set; }
    public Interactable? FocusedInteractable { get; private set; }
    public string Notice { get; private set; } = "";
    public float NoticeTime { get; private set; }

    private StageMap? _map;
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
        Mode = ModeOverride ?? (GetTree().CurrentScene == this ? GameSession.Mode : GameMode.Scenario);
        // The map sizes the flow field before it builds into it.
        bool running = GetTree().CurrentScene == this;
        var map = MapOverride ?? StageMap.Create(Mode, running ? GameSession.ScenarioStage : 1);
        var area = map.Area;
        Flow = new FlowField(area.Position.X, area.Position.Y, area.End.X, area.End.Y);
        Tiers = new TierMap(Flow);
        Flow.Tiers = Tiers;
        Sfx = new Sfx();
        AddChild(Sfx);
        Sfx.Bed("ambience_loop", AudioSetup.AmbienceBus);
        Effects = new Effects { Name = "Effects" };
        Director = new HordeDirector { Name = "Director" };
        Vision = new VisionSystem { Name = "Vision" };
        AddChild(Director);
        AddChild(Vision);
        AddChild(Effects);
        BuildEnvironment(map);

        if (Role == NetRole.Offline && GetTree().CurrentScene == this) Role = NetLaunch.Pending;
        ReviveEnabled = ReviveOverride ?? (GetTree().CurrentScene == this && GameSettings.ReviveEnabled);
        NextStageEnabled = NextStageOverride ?? (running && Role == NetRole.Offline);
        // A random field per match offline; in a match everyone builds the host's (GameSession.MapSeed from the room; 1 without a room).
        if (MapOverride == null && GetTree().CurrentScene == this)
            map.Seed = Role == NetRole.Offline ? (int)(GD.Randi() % 1000000) + 1 : GameSession.MapSeed != 0 ? GameSession.MapSeed : 1;
        map.Build(this, Mode);
        _map = map;

        // Level test start (--zone): the zone's entry on the route; arriving down a hatch keeps the next stage's normal start.
        string zone = StartZoneOverride ?? (running && Mode == GameMode.Scenario && !StageTransition.Arriving
            && GameSession.ScenarioStage == GameSession.StartZoneStage ? GameSession.StartZone : "");
        var start = Map.PlayerStart;
        if (zone != "")
        {
            if (Map is LayoutMap layoutMap && layoutMap.ZoneEntryOf(zone) is { } entry) { start = entry.At; StartedZone = entry.Id; }
            else GD.PushWarning($"[zone] stage {Map.StageNumber} has no zone '{zone}' on a route; starting in the safe room");
        }

        var def = CharacterCatalog.Player(CharacterOverride ?? GameSession.SelectedCharacterId);
        Player = new Player();
        Player.Setup(this, def);
        Player.Position = start;
        AddChild(Player);
        Players.Add(Player);
        Player.Died += CheckWipe;
        // A hit cancels a hold-to-use (a base door, L-35): it starts over from nothing.
        Player.Hurt += () => { if (_holding is { CancelOnHit: true }) { _holding = null; HoldProgress = 0; HoldCancelled = true; } };
        ThemeId = StageTheme.Normalize(ThemeOverride ?? GameSession.SelectedTheme);
        CommonDefinition = CommonRoster.Current.Compose(CommonRoster.Current.CreatePack(DefaultCommonPack(ThemeId), 1)[0]);

        Cam = new CameraRig { Name = "Camera", Target = Player, Player = Player, View = Map.View, Bounds = Map.CameraBounds };
        if (_environment.FogEnabled && _environment.FogMode == Godot.Environment.FogModeEnum.Depth) Cam.Haze = _environment;
        // Coming down the last stage's hatch: the view eases from that stage's look into this one (L-14).
        bool arriving = (ArrivalOverride ?? running) && StageTransition.Arriving;
        if (arriving) Cam.Arrive(StageTransition.FromView, StageTransition.ArriveSeconds);
        AddChild(Cam);
        Cam.SnapTo(start);
        Effects.RegisterCorpseObserver(Player, Cam);

        _enemyRoot = new Node3D { Name = "Enemies" };
        AddChild(_enemyRoot);
        Flow.Update(Player.GlobalPosition);
        PrepareClimbSteps();
        RefreshRouteFlows();
        // Scenario fills areas ahead and sends waves from the stage's entries; the other modes spawn by their own rules.
        if (Mode == GameMode.Scenario && Map.Encounters is { } encounters) Director.UseEncounters(encounters);
        if (Mode == GameMode.Scenario) Objective = Map.ScenarioStart;
        ModeRules = RulesOverride ?? ModeRules.Create(Mode);
        if (ModeRules != null)
        {
            Objective = ModeRules.InitialObjective;
            AddChild(ModeRules);
        }
        // A client starts in the first friend's place; the host's roster moves it to its own one (M-30).
        if (Role == NetRole.Client) Player.Position = StageNet.ClientStart(this, 1);
        Trail.Add(Player.GlobalPosition);
        // The menu's squad size applies to the real game scene; tests ask for squadmates explicitly.
        // No AI squadmates in a network match (M-11).
        SpawnSquad(SquadOverride ?? (GetTree().CurrentScene == this && Role == NetRole.Offline ? GameSession.SquadSize : 0), start);

        // Multiplayer: after the level and the start fill exist, so ids and the welcome list are complete.
        if (Role != NetRole.Offline) Net = StageNet.Attach(this, Role);

        Hud = new Hud { Name = "Hud" };
        AddChild(Hud);
        if (ModeRules == null) ShowNotice(Loc.Text(Objective == Objective.LeaveSafeRoom ? "msg_stage_start" : Map.RouteStartKey));
        // A stage that starts out on the route (no safe-room door to open) wakes the director at once.
        if (ModeRules == null && Objective != Objective.LeaveSafeRoom && DirectorEnabled) Director.Begin();
        // A zone start is past the safe room: open its door and go on as if the switch was used.
        if (StartedZone != "" && Objective == Objective.LeaveSafeRoom) LeaveSafeRoom();
        if (arriving)
        {
            if (GameSession.Carry is { } carry) carry.ApplyTo(Player);
            GameSession.Carry = null;
            StageTransition.Reveal(this);
        }
    }

    private Godot.Environment _environment = null!;

    private void BuildEnvironment(StageMap map)
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
        var key = new DirectionalLight3D
        {
            Name = "Moon",
            RotationDegrees = new Vector3(-62, -28, 0),
            LightColor = new Color("a9c7ed"),
            LightEnergy = .38f,
            ShadowEnabled = true,
            ShadowBlur = 2f,
            DirectionalShadowMaxDistance = 60,
        };
        // Stage 1 keeps the night above; an outdoor map brings its own sky, haze and key light.
        map.Light(env, key);
        _environment = env;
        AddChild(new WorldEnvironment { Environment = env });
        AddChild(key);
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
        // Health and damage for the party, fixed at birth so a join or a leave never changes a monster already out.
        if (Authoritative)
        {
            var party = Party;
            if (!Mathf.IsEqualApprox(party.Hp(monster.Tier), 1)) enemy.ScaleHealth(party.Hp(monster.Tier));
            if (!Mathf.IsEqualApprox(party.Damage(monster.Tier), 1)) enemy.ScaleDamage(party.Damage(monster.Tier));
        }
        // Ledge entries start on a wall top; everything else stands on the floor.
        enemy.Position = new Vector3(at.X, Mathf.Max(at.Y, Tiers.GroundY(at) + .05f), at.Z);
        _enemyRoot.AddChild(enemy);
        if (Player != null) Vision.Apply(enemy);
        Net?.OnEnemySpawned(enemy);
        _pending.Add(enemy);
        return enemy;
    }

    /// <summary>Spawn with the class that matches the monster's behavior.</summary>
    public Enemy Spawn(MonsterDefinition monster, Vector3 at, bool awake) => monster.Behavior switch
    {
        MonsterBehavior.Balloon => SpawnEnemy<BalloonEnemy>(monster, at, awake),
        MonsterBehavior.Bull => SpawnEnemy<BullEnemy>(monster, at, awake),
        MonsterBehavior.Boss => SpawnEnemy<BossEnemy>(monster, at, awake),
        MonsterBehavior.Tether or MonsterBehavior.Leaper => SpawnEnemy<CaptureEnemy>(monster, at, awake),
        MonsterBehavior.Spitter or MonsterBehavior.Shooter => SpawnEnemy<RangedEnemy>(monster, at, awake),
        _ => SpawnEnemy<Enemy>(monster, at, awake),
    };

    private int _commonIndex, _specialIndex;
    public Enemy SpawnCommon(Vector3 at, bool awake)
    {
        var pack = CommonRoster.Current.Packs[CommonPackAt(at)];
        int index = _commonIndex++;
        var identity = new CommonIdentity(pack.Faction, pack.Species, pack.Palette, pack.Weapons[index % pack.Weapons.Length], index % 10,
            pack.Faction == "armed" && index % 3 == 0 ? CommonGear.Vest : CommonGear.None);
        return SpawnCommonIdentity(RandomCommonAppearance(identity), at, awake);
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
        if (monster.CommonIdentity is { } identity) return CommonRoster.Current.LookFor(identity);
        if (_looks.TryGetValue(monster.Id, out var look)) return look;
        var baseLook = CharacterCatalog.Enemy(monster.LookId);
        if (monster.Id is "wild_animal" or "armed_animal" or "armed_veteran")
        {
            look = (CharacterDefinition)CharacterCatalog.Player(monster.Id == "wild_animal" ? "raccoon" : "redpanda").Duplicate();
            look.Role = CharacterRole.Enemy;
        }
        else if (monster.Behavior == MonsterBehavior.Boss)
        {
            // The boss is its look scaled up (bosses/<id>.json scale), collider included.
            look = (CharacterDefinition)baseLook.Duplicate();
            float scale = BossDatabase.Get(monster.Id).Scale;
            look.ModelScale *= scale;
            look.ColliderRadius *= scale;
            look.ColliderHeight *= scale;
        }
        else look = monster.MirrorPlayer ? CharacterDefinition.MakeZombie(Player.Definition, baseLook) : baseLook;
        _looks[monster.Id] = look;
        return look;
    }

    private void RefreshRouteFlows()
    {
        // The route counts doors as open: progress and "forward" must exist before the shutter opens.
        // After a door the same fields take the new walls and patch their step costs (no new grids to allocate, I-109).
        if (ExitFlow == null) { ExitFlow = Flow.CopyLayout(); ExitFlow.Riders = true; }
        else ExitFlow.CopyWallsFrom(Flow);
        Map.ApplyFlow(ExitFlow, doors: false);
        ExitFlow.Update(Map.RouteGoal);
        if (LeaderFlow == null) { LeaderFlow = Flow.CopyLayout(); LeaderFlow.Riders = true; }
        else LeaderFlow.CopyWallsFrom(Flow);
        LeaderFlow.Update(Player.GlobalPosition);
    }

    /// <summary>
    /// Metres left to the route's goal (smaller = further ahead). Off the route: by the +X axis.
    /// A map without a route (open field) has no ahead or behind: every spot is 0.
    /// </summary>
    public float Progress(Vector3 at)
    {
        if (!Map.HasRoute) return 0;
        int cost = ExitFlow.Distance(at);
        return cost == FlowField.Unreachable ? Map.RouteGoal.X - at.X : cost / 10f;
    }

    /// <summary>Direction the route continues from <paramref name="at"/> (the level runs along +X, level-design-rules.md).</summary>
    public Vector3 RouteForward(Vector3 at)
    {
        // Without a route, "forward" is where the local player is looking.
        if (!Map.HasRoute) return Player.AimDirection;
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

    private void SpawnSquad(int count, Vector3 start)
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
            var at = start + offsets[i % offsets.Length];
            if (Flow.IsBlocked(at)) at = start;
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

    /// <summary>Checks only: moves the stage clock forward (the mode schedules follow it).</summary>
    public void AdvanceClockForTest(float seconds) => Elapsed += seconds;

    /// <summary>Checks: lets survivors go down here (the card lab normally has no downs).</summary>
    public void SetReviveForTest(bool on) => ReviveEnabled = on;

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
    /// By pickup kind, not node name (Godot renames same-named siblings, so a second "Medkit" was not one, I-61) and
    /// not prompt text key (a renamed key broke nothing loudly, I-94 ②).
    /// </summary>
    public static bool IsMedkit(Interactable item) => item.Pickup == PickupKind.Medkit;
    public static bool IsAmmo(Interactable item) => item.Pickup == PickupKind.Ammo;

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
        if (IsAmmo(item)) { RefillAmmo(who); return true; }
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

    private readonly Dictionary<MonsterClimb, FlowField> _climbFlows = new();
    /// <summary>Step costs for each way of climbing, worked out while the stage loads (I-109); a chase field starts from one.</summary>
    private readonly Dictionary<MonsterClimb, FlowField> _climbTemplates = new();

    /// <summary>On a map with tiers: the step costs of every way of climbing, so the first leaper or balloon does not stall a frame.</summary>
    private void PrepareClimbSteps()
    {
        if (!Tiers.Any) return;
        foreach (var climb in new[] { MonsterClimb.Wall, MonsterClimb.Jump, MonsterClimb.Fly })
        {
            var template = Flow.CopyLayout();
            template.Mover = climb;
            template.PrepareSteps();
            _climbTemplates[climb] = template;
        }
    }

    /// <summary>
    /// The chase field for a way of climbing (L-16). On a flat map, or for ladder users, it is <see cref="Flow"/>; on a map
    /// with tiers each other climb gets its own copy toward the same targets, made the first time someone asks.
    /// </summary>
    public FlowField FlowFor(MonsterClimb climb)
    {
        if (!Tiers.Any || climb == MonsterClimb.Ladder) return Flow;
        if (!_climbFlows.TryGetValue(climb, out var field))
        {
            // The field prepared while loading becomes the chase field; walls changed since (doors) are patched on its first build.
            if (_climbTemplates.Remove(climb, out var template))
            {
                field = template;
                field.CopyWallsFrom(Flow);
            }
            else field = Flow.CopyLayout();
            field.Mover = climb;
            if (_flowTargets.Count > 0) field.Update(_flowTargets);
            _climbFlows[climb] = field;
        }
        return field;
    }

    /// <summary>Re-stamp the whole flow grid (doors opening or closing). Layout is null while it is still being built.</summary>
    public void RebuildFlow()
    {
        _map?.ApplyFlow();
        // Climb fields keep their step costs and patch them around the door (I-109); they rebuild toward the same targets.
        foreach (var field in _climbFlows.Values) field.CopyWallsFrom(Flow);
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
        // Survival: the horde also paths to the base.
        if (ModeRules?.Core is { Alive: true } core && _flowTargets.Count > 0) _flowTargets.Add(core.GlobalPosition);
        if (_flowTargets.Count > 0)
        {
            Flow.Update(_flowTargets);
            foreach (var field in _climbFlows.Values) field.Update(_flowTargets);
        }
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
        // A client gets objectives and respawns from the host (multiplayer-sync-design.md 3장).
        if (Authoritative) UpdateObjective();
        UpdateInteraction(dt);
        if (Authoritative) UpdateRespawns();
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
                Map.Shutter?.SetProgress(1 - Director.CrescendoRemaining / Director.CrescendoDuration);
                if (!Director.CrescendoActive) OpenShutter();
                break;
            case Objective.ReachSafehouse:
                if (HumansInSafehouse())
                {
                    if (!_safehouseReached)
                        foreach (var survivor in AlivePlayers) if (!survivor.IsDowned) survivor.ResetDowns();
                    _safehouseReached = true;
                    Objective = Objective.CloseSafehouse;
                    SetEnabled(Map.SafehouseSwitch, true);
                    ShowNotice(Loc.Text("msg_stage_close_to_clear"));
                }
                break;
            case Objective.CloseSafehouse:
                if (!HumansInSafehouse())
                {
                    Objective = Objective.ReachSafehouse;
                    SetEnabled(Map.SafehouseSwitch, false);
                }
                break;
        }
    }

    /// <summary>Every living person (not AI squadmates) is inside; alone it is just you (multiplayer-sync-design.md 5장).</summary>
    private bool HumansInSafehouse()
    {
        bool any = false;
        foreach (var p in AlivePlayers)
        {
            if (p.IsBot) continue;
            if (!Map.InSafehouse(p.GlobalPosition)) return false;
            any = true;
        }
        return any;
    }

    /// <summary>The living person furthest along the route: waves and noise are placed around them.</summary>
    public Player Leader
    {
        get
        {
            Player? best = null;
            float bestLeft = float.MaxValue;
            foreach (var p in AlivePlayers)
            {
                if (p.IsBot) continue;
                float left = Progress(p.GlobalPosition);
                if (left < bestLeft) { bestLeft = left; best = p; }
            }
            return best ?? Player;
        }
    }

    private void OpenShutter()
    {
        _alarm?.QueueFree();
        _alarm = null;
        if (Map.Shutter is { } shutter)
        {
            Sfx.PlayAt("shutter", shutter.GlobalPosition + Vector3.Up * 1.5f);
            shutter.SetOpen(true, 1.2f);
        }
        Objective = Objective.ReachSafehouse;
        ShowNotice(Loc.Text(Map.CrescendoDoneKey));
        // The way back from the mission fills again (L-27); a stage without such groups places nothing.
        Director.FillAfterMission();
    }

    /// <summary>
    /// Client: step the objective to where the host is, playing each step's sounds and notices on the way.
    /// The host decided every step; nothing here can change the game.
    /// </summary>
    public void ApplyNetObjective(Objective to)
    {
        for (int guard = 0; guard < 8 && Objective != to; guard++)
        {
            switch (Objective)
            {
                case Objective.LeaveSafeRoom: LeaveSafeRoom(); break;
                case Objective.ReachShutter:
                    if (to == Objective.HoldOut) StartCrescendo();
                    else { Objective = Objective.HoldOut; }
                    break;
                case Objective.HoldOut: Director.Stop(); OpenShutter(); break;
                case Objective.ReachSafehouse:
                    Objective = Objective.CloseSafehouse;
                    SetEnabled(Map.SafehouseSwitch, true);
                    ShowNotice(Loc.Text("msg_stage_close_to_clear"));
                    break;
                case Objective.CloseSafehouse:
                    if (to == Objective.ReachSafehouse) { Objective = Objective.ReachSafehouse; SetEnabled(Map.SafehouseSwitch, false); }
                    else PresentClear();
                    break;
                default: Objective = to; break;
            }
        }
    }

    /// <summary>Client: the host's stage ended (cleared or wiped).</summary>
    public void ApplyNetState(StageState state)
    {
        if (state == State) return;
        if (ModeRules != null && State == StageState.Playing)
        {
            EndMode(state == StageState.Cleared, state == StageState.Cleared ? "ui_end_clear" : "msg_stage_down");
            return;
        }
        if (state == StageState.Cleared && State == StageState.Playing) PresentClear();
        else if (state == StageState.Dead && State == StageState.Playing) PresentWipe();
    }

    private void UpdateInteraction(float dt)
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
        // Hold-to-use (the stage exit): the key stays down for HoldSeconds; letting go or walking away starts over.
        if (!Input.IsActionPressed(GameInput.Interact)) HoldCancelled = false;
        if (FocusedInteractable is { HoldSeconds: > 0 } hold && RevivePromptTarget == null && !Player.ManualControl && Input.IsActionPressed(GameInput.Interact)
            && !HoldCancelled)
        {
            if (_holding != hold) HoldProgress = 0;
            _holding = hold;
            HoldProgress = Mathf.Min(1, HoldProgress + dt / hold.HoldSeconds);
            if (HoldProgress >= 1)
            {
                HoldProgress = 0;
                _holding = null;
                if (Authoritative) hold.Use(Player);
                else RequestUse?.Invoke(hold);
            }
            return;
        }
        _holding = null;
        HoldProgress = 0;
        // E next to a downed teammate means "pick them up", never also "use the thing on the floor".
        if (FocusedInteractable is { HoldSeconds: <= 0 } && RevivePromptTarget == null && !Player.ManualControl && Input.IsActionJustPressed(GameInput.Interact))
        {
            if (Authoritative) FocusedInteractable.Use(Player);
            else RequestUse?.Invoke(FocusedInteractable);
        }
    }


    // ---------- objective beats (called by layout interactables) ----------

    public void LeaveSafeRoom()
    {
        if (Objective != Objective.LeaveSafeRoom) return;
        if (Map.StartDoor is { } door)
        {
            door.SetOpen(true);
            Sfx.PlayAt("door", door.GlobalPosition + Vector3.Up);
        }
        SetEnabled(Map.StartDoorSwitch, false);
        Objective = Objective.ReachShutter;
        if (DirectorEnabled) Director.Begin();
        ShowNotice(Loc.Text("msg_stage_find_shutter"));
    }

    public void StartCrescendo()
    {
        if (Objective != Objective.ReachShutter) return;
        Objective = Objective.HoldOut;
        SetEnabled(Map.ShutterPanel, false);
        _alarm = Sfx.Bed("alarm_loop", AudioSetup.SfxBus);
        Director.Begin();
        Director.StartCrescendo(Map.CrescendoSeconds);
        Cam.AddShake(.3f);
        ShowNotice(Loc.Text(Map.CrescendoAlarmKey));
    }

    public bool TryCloseSafehouse()
    {
        if (Objective != Objective.CloseSafehouse) return false;
        foreach (var e in AllEnemies)
        {
            if (!e.IsDead && Map.NearSafehouse(e.GlobalPosition))
            {
                ShowNotice(Loc.Text("msg_stage_enemies_inside"));
                return false;
            }
        }
        PresentClear();
        return true;
    }

    private void PresentClear()
    {
        if (Map.SafehouseDoor is { } door)
        {
            door.SetOpen(false);
            Sfx.PlayAt("door", door.GlobalPosition + Vector3.Up);
        }
        SetEnabled(Map.SafehouseSwitch, false);
        Sfx.Play("stage_clear");
        if (NextStageEnabled && Map.NextStage > 0 && Map.StageExit is { } exit)
        {
            // The run goes on: the safehouse's hatch opens instead of the end panel (decisions.md L-14).
            Objective = Objective.TakeExit;
            exit.Enabled = true;
            Director.Stop();
            foreach (var p in AlivePlayers) p.Invulnerable = true;
            ShowNotice(Loc.Text("msg_stage_exit_open"), 5);
            return;
        }
        Objective = Objective.Cleared;
        State = StageState.Cleared;
        Director.Stop();
        Player.Invulnerable = true;
        ShowNotice(Loc.Text("ui_end_clear"));
    }

    /// <summary>Climb down the safehouse hatch: the iris closes on the survivor and the next stage loads (L-14).</summary>
    public void TakeStageExit()
    {
        if (Objective != Objective.TakeExit || StageTransition.Busy) return;
        SetEnabled(Map.StageExit, false);
        Sfx.PlayAt("door", Player.GlobalPosition + Vector3.Up);
        StageTransition.Leave(this, Map.NextStage);
    }

    private static void SetEnabled(Interactable? item, bool on)
    {
        if (item != null) item.Enabled = on;
    }

    /// <summary>
    /// Game over when every human is dead, or when every survivor (squad included) is down or dead —
    /// a downed player can still be picked up by a squadmate (doc/ai-squad-plan.md 6.3).
    /// </summary>
    internal void CheckWipe()
    {
        if (State != StageState.Playing || !Authoritative) return;
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
        SurvivorDowned?.Invoke(who);
        CheckWipe();
    }

    public void OnSurvivorRevived(Player who) => SurvivorRevived?.Invoke(who);

    /// <summary>Someone went down / got up again, or a sidekick fell (cards like the toy train and the jack-in-the-box, M-29).</summary>
    public event System.Action<Player>? SurvivorDowned, SurvivorRevived;
    public event System.Action<Companion>? CompanionDowned;
    internal void RaiseCompanionDowned(Companion who) => CompanionDowned?.Invoke(who);

    private void OnPlayerDied() => PresentWipe();

    /// <summary>Survival / Survivor: the match ends (cleared or lost) with the mode's own notice.</summary>
    public void EndMode(bool cleared, string noticeKey)
    {
        if (State != StageState.Playing) return;
        if (!cleared) { PresentWipe(noticeKey); return; }
        State = StageState.Cleared;
        Director.Stop();
        foreach (var p in AlivePlayers) p.Invulnerable = true;
        Sfx.Play("stage_clear");
        ShowNotice(Loc.Text(noticeKey), 6);
    }

    private void PresentWipe(string noticeKey = "msg_stage_down")
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
        ShowNotice(Loc.Text(noticeKey));
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

    public void PickUp(WeaponPickup pickup) => PickUp(pickup, Player);

    public void PickUp(WeaponPickup pickup, Player who)
    {
        if (ModeRules?.TakePickup(pickup, who) == true) return;
        var at = who.GlobalPosition + Vector3.Up * 2.6f;
        bool local = who.IsLocal;
        if (pickup.Throwable is { } throwable)
        {
            if (!who.Loadout.AddThrowable(throwable))
            {
                if (local) ShowNotice(Loc.Format("fmt_pickup_throwable_max", throwable.DisplayName, throwable.MaxCarry), 2);
                return;
            }
            Effects.FloatingText(at, Loc.Format("fmt_pickup_throwable", throwable.DisplayName), new Color("8dff9c"));
            PickupSound(who);
            RemovePickup(pickup);
            return;
        }
        if (pickup.Weapon is not { } weapon) return;
        if (pickup.Infinite && TakeShared(pickup, weapon, who, at)) return;
        var (result, dropped) = who.Weapons.Pickup(weapon, pickup.State);
        switch (result)
        {
            case PickupResult.Full:
                if (local) ShowNotice(Loc.Text("msg_pickup_full"), 2);
                return;
            case PickupResult.Upgraded:
                Effects.FloatingText(at, Loc.Format("fmt_pickup_upgraded", who.Gun.Def.DisplayName), new Color("6fb6ff"));
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
        PickupSound(who);
        RemovePickup(pickup);
        if (dropped != null)
        {
            var back = -who.AimDirection * .9f;
            var spot = who.GlobalPosition + back;
            if (Flow.IsBlocked(spot)) spot = who.GlobalPosition;
            WeaponPickup.ForWeapon(this, spot, dropped.Def, dropped);
        }
    }

    /// <summary>
    /// A shared table weapon (C-33): the same gun in hand fills its magazine and reserve and nothing is dropped; another
    /// gun is swapped in as usual (the old one drops by the table). The table's weapon always stays.
    /// </summary>
    private bool TakeShared(WeaponPickup pickup, WeaponDefinition weapon, Player who, Vector3 at)
    {
        var held = weapon.Slot switch
        {
            WeaponSlot.Secondary => who.Loadout.Secondary,
            WeaponSlot.Special => who.Loadout.Special,
            _ => who.Loadout.Primary,
        };
        if (held != null && held.Def.Id == weapon.Id)
        {
            if (!held.FillUp())
            {
                if (who.IsLocal) ShowNotice(Loc.Text("msg_pickup_full"), 2);
                return true;
            }
            Effects.FloatingText(at, Loc.Text("msg_pickup_ammo"), new Color("ffd36b"));
            PickupSound(who);
            return true;
        }
        var (result, dropped) = who.Weapons.Pickup(weapon, null);
        if (result == PickupResult.Full) return true;
        Effects.FloatingText(at, weapon.DisplayName, new Color("ffb347"));
        PickupSound(who);
        if (dropped != null)
        {
            var spot = who.GlobalPosition - who.AimDirection * .9f;
            if (Flow.IsBlocked(spot)) spot = who.GlobalPosition;
            WeaponPickup.ForWeapon(this, spot, dropped.Def, dropped);
        }
        return true;
    }

    private void PickupSound(Player who)
    {
        if (who.IsLocal) Sfx.Play("pickup");
        else Sfx.PlayAt("pickup", who.GlobalPosition + Vector3.Up);
    }

    /// <summary>Outpost: loot on the floor goes into the backpack; what does not fit stays (decisions.md M-25).</summary>
    public void TakeLoot(LootPickup loot, Player who)
    {
        if (who.Backpack is not { } bag) return;
        int left = bag.Add(loot.Item.Copy());
        if (left == loot.Item.Count)
        {
            if (who.IsLocal) ShowNotice(Loc.Text("msg_backpack_full"), 2);
            return;
        }
        Effects.FloatingText(who.GlobalPosition + Vector3.Up * 2.6f, $"+{loot.Item.Count - left} {loot.Item.DisplayName}", new Color("ffd36b"));
        PickupSound(who);
        if (left > 0) { loot.SetCount(left); return; }
        Interactables.Remove(loot);
        loot.QueueFree();
    }

    /// <summary>Outpost: a drag in the inventory screen. The deciding PC applies it; a client asks the host.</summary>
    public void InventoryDrag(int from, int to)
    {
        if (Authoritative) InventoryOps.Drag(Player, from, to);
        else Net?.RequestInventory(from, to);
    }

    /// <summary>A mode's own pickups (Onslaught's racks) leave the world the same way a taken weapon does.</summary>
    public void RemovePickupFromWorld(WeaponPickup pickup) => RemovePickup(pickup);

    /// <summary>The deciding PC moves a mode on (Onslaught: arming → fighting). Clients follow the world message.</summary>
    public void SetModeObjective(Objective objective)
    {
        if (ModeRules != null && Authoritative) Objective = objective;
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
        bool local = who.IsLocal;
        var at = who.GlobalPosition + Vector3.Up * 2.6f;
        // Outpost: an ammo pile hands out one stack for each gun carried, into the backpack (decisions.md M-25).
        if (who.Backpack is { } bag)
        {
            int added = 0;
            foreach (var cal in new[] { who.Loadout.Primary, who.Loadout.Special }.Where(w => w is { Def.Refillable: true, Def.Caliber.Length: > 0 }).Select(w => w!.Def.Caliber).Distinct())
            {
                var item = InvItem.Ammo(cal, CaliberDatabase.Get(cal)?.Stack ?? 30);
                added += item.Count - bag.Add(item);
            }
            if (added > 0) Sfx.PlayAt("reload_end", who.GlobalPosition + Vector3.Up);
            Effects.FloatingText(at, added > 0 ? Loc.Format("fmt_refill_got", added) : Loc.Text(bag.FreeCells == 0 ? "msg_backpack_full" : "msg_refill_no_primary"), new Color("ffd36b"));
            return;
        }
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
        Objective.ReachShutter => Loc.Text(Map.CrescendoObjectiveKey),
        Objective.HoldOut => Loc.Format(Map.CrescendoHoldOutKey, Mathf.CeilToInt(Mathf.Max(0, Director.CrescendoRemaining))),
        Objective.ReachSafehouse => Loc.Text("ui_objective_reach_safehouse"),
        Objective.CloseSafehouse => Loc.Text("ui_objective_close_safehouse"),
        Objective.TakeExit => Loc.Text("ui_objective_take_exit"),
        Objective.ModeGoal or Objective.ModeArming when ModeRules != null => ModeRules.ObjectiveText,
        _ => Loc.Text("ui_objective_cleared"),
    };

    /// <summary>
    /// Offline: reload the stage. In a match both PCs have to restart together, so the host takes the whole room back to
    /// the lobby (still connected) and a client waits for that (menu-multiplayer-plan.md 2장).
    /// </summary>
    public void Restart()
    {
        switch (Role)
        {
            case NetRole.Offline: GetTree().ReloadCurrentScene(); break;
            case NetRole.Host when NetLaunch.Lobby is { InMatch: true } lobby: lobby.ReturnToLobby(); break;
            case NetRole.Client: ShowNotice(Loc.Text("msg_net_wait_host_restart"), 3); break;
            default: BackToMenu(); break;
        }
    }

    public void BackToMenu() => GetTree().ChangeSceneToFile("res://scenes/Menu.tscn");

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) QuitHygiene.Collect();
    }
}
