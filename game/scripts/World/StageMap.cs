using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// The ground a <see cref="Stage"/> plays on (decisions.md L-13): its walkable area and walls for the flow field,
/// where survivors start, which doors and loud devices it has, the scenario route if there is one, and where a mode's
/// base and doorways are. A map builds itself from the shared blocks below (floors, walls, crates, lamps, pickups).
/// Stage 1 is <see cref="StageOneLayout"/>; a wide open field or a generated map is another subclass.
/// </summary>
public abstract class StageMap
{
    protected Stage _stage = null!;
    protected readonly RandomNumberGenerator _rng = new();
    private readonly List<(Vector3 center, Vector3 size)> _floors = new();
    private readonly List<(Vector3 center, Vector3 size)> _solids = new();

    /// <summary>
    /// The map for a mode: Onslaught plays its open field, the scenario the stage it has reached (Stage 2 after the
    /// Stage 1 exit, decisions.md L-14), the others Stage 1 (doc/mode-weapon-direction.md 10장).
    /// </summary>
    public static StageMap Create(GameMode mode, int scenarioStage = 1) => mode switch
    {
        GameMode.Survivor => new OpenFieldMap(),
        GameMode.Scenario when scenarioStage == 2 => new StageTwoMap(),
        _ => new StageOneLayout(),
    };

    /// <summary>Random layout seed (maps that scatter things). Set before <see cref="Build"/>; every PC in a match uses the host's.</summary>
    public int Seed { get; set; } = 1;

    public GameMode Mode { get; private set; } = GameMode.Scenario;
    public Vector3 PlayerStart { get; protected set; }
    /// <summary>Language key of the map's name for the HUD title (empty: the title shows the mode alone).</summary>
    public virtual string TitleKey => "";
    /// <summary>Ground covered by the flow field (x, z), with a margin around the walkable floors.</summary>
    public abstract Rect2 Area { get; }
    /// <summary>A scenario route runs to <see cref="RouteGoal"/>: squadmates stay behind the leader along it. Open maps have none.</summary>
    public virtual bool HasRoute => false;
    public virtual Vector3 RouteGoal => PlayerStart;
    /// <summary>Pre-placed groups and wave entries for the scenario director (null: the mode spawns by its own rules).</summary>
    public virtual EncounterSet? Encounters => null;
    /// <summary>Doors whose open state is shared in a match, in a fixed order (bit i of the world message).</summary>
    public virtual IReadOnlyList<Door> Doors => Array.Empty<Door>();
    /// <summary>The loud device (car alarm) whose going off is shared in a match.</summary>
    public virtual AlarmCar? AlarmDevice => null;
    /// <summary>Outpost: where the generator stands.</summary>
    public virtual Vector3 CoreSpot => PlayerStart + Vector3.Right * 2;
    /// <summary>Outpost: groups of spots a wave walks in from (one group per doorway).</summary>
    public virtual IReadOnlyList<Vector3[]> Gates => Array.Empty<Vector3[]>();

    // ---------- scenario beats (I-79): a map with a route gives its own doors, panel and safehouse ----------

    /// <summary>Scenario stage number (0: not a scenario stage).</summary>
    public virtual int StageNumber => 0;
    /// <summary>The scenario stage its exit leads to (0: the last one, clearing it ends the run).</summary>
    public virtual int NextStage => 0;
    /// <summary>First scenario objective: Stage 1 starts shut in the safe room, a later stage starts out on the route.</summary>
    public virtual Objective ScenarioStart => Objective.LeaveSafeRoom;
    public virtual Door? StartDoor => null;
    public virtual Interactable? StartDoorSwitch => null;
    /// <summary>The crescendo's door and panel (none: the stage has no crescendo).</summary>
    public virtual Door? Shutter => null;
    public virtual Interactable? ShutterPanel => null;
    public virtual float CrescendoSeconds { get; set; } = 50f;
    public virtual Door? SafehouseDoor => null;
    public virtual Interactable? SafehouseSwitch => null;
    /// <summary>The hatch that leads to <see cref="NextStage"/>, opened once the safehouse is shut.</summary>
    public virtual Interactable? StageExit => null;
    public virtual bool InSafehouse(Vector3 p) => false;
    /// <summary>Inside, or close enough to the doorway to be shoved in when the door slams.</summary>
    public virtual bool NearSafehouse(Vector3 p) => false;

    // ---------- look ----------

    /// <summary>How the camera sees this map (Stage 1's indoor quarter view unless a map says otherwise, L-2 · L-14).</summary>
    public virtual CameraView View => CameraView.Indoor;
    /// <summary>Sky, ambient light, fog and the key light. The default is Stage 1's night.</summary>
    public virtual void Light(Godot.Environment env, DirectionalLight3D key) { }

    /// <summary>Builds the map into <paramref name="stage"/> and stamps the flow field.</summary>
    public void Build(Stage stage, GameMode mode)
    {
        _stage = stage;
        Mode = mode;
        _rng.Seed = 1;
        BuildMap();
        ApplyFlow();
    }

    protected abstract void BuildMap();

    // ---------- flow field ----------

    /// <summary>Everything starts blocked; floors are carved out, then walls and doors are stamped back in.</summary>
    public void ApplyFlow() => ApplyFlow(_stage.Flow, doors: true);

    /// <summary>Stamps walls into <paramref name="flow"/>; without doors it is the stage's route, not what is walkable now.</summary>
    public void ApplyFlow(FlowField flow, bool doors)
    {
        var area = Area;
        var center = area.GetCenter();
        flow.SetBlocked(new Vector3(center.X, 0, center.Y), new Vector3(area.Size.X + 10, 0, area.Size.Y + 10), 0, true);
        foreach (var (c, size) in _floors) flow.SetBlocked(c, size, -.05f, false);
        foreach (var (c, size) in _solids) flow.SetBlocked(c, size, Stage.WallPad, true);
        if (doors) foreach (var door in Doors) door.StampFlow();
    }

    /// <summary>Something solid that was not made by <see cref="Solid"/> (a car, a generator) still blocks the flow field.</summary>
    protected void BlockFlow(Vector3 center, Vector3 size) => _solids.Add((center, size));

    // ---------- building blocks ----------

    /// <summary>Tiled indoor look or plain outdoor ground for a floor starting at <paramref name="x0"/>.</summary>
    protected virtual bool IndoorFloor(float x0) => false;

    /// <summary>A walkable floor; <paramref name="draw"/> off when the map lays its own ground models over it.</summary>
    protected void Floor(float x0, float x1, float z0, float z1, string color, bool draw = true)
    {
        var center = new Vector3((x0 + x1) / 2, -.2f, (z0 + z1) / 2);
        var size = new Vector3(x1 - x0, .4f, z1 - z0);
        var body = new StaticBody3D { Name = "Floor", Position = center, CollisionLayer = Layers.World, CollisionMask = 0 };
        _stage.AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (draw) StageArt.Floor(body, size, IndoorFloor(x0));
        _floors.Add((center, size));
    }

    protected StaticBody3D Wall(float x0, float x1, float z0, float z1, float height, string color, bool drawWall = true)
    {
        var wallSize = new Vector3(x1 - x0, height, z1 - z0);
        var body = Solid(new Vector3((x0 + x1) / 2, height / 2, (z0 + z1) / 2), wallSize, color, false);
        if (drawWall) StageArt.Wall(body, wallSize);
        // Low camera-side fences get an invisible tall collider so nobody gets launched out of the map.
        if (height < 2f)
        {
            var size = new Vector3(x1 - x0, 4f, z1 - z0);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, 2f - height / 2, 0) });
        }
        return body;
    }

    protected StaticBody3D Solid(Vector3 center, Vector3 size, string color, bool drawCargo = true)
    {
        var body = new StaticBody3D { Name = "Solid", Position = center, CollisionLayer = Layers.World, CollisionMask = 0 };
        _stage.AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (drawCargo) StageArt.Cargo(body, size, color);
        _solids.Add((center, size));
        return body;
    }

    protected void Barrel(Vector3 at) => ExplosiveBarrel.Create(_stage, at);

    /// <summary>
    /// A kit model (<see cref="PropLibrary"/>) turned <paramref name="yaw"/> degrees about Y. With <paramref name="solid"/>
    /// its manifest collider (box or cylinder, as a box for the flow field) blocks bullets, survivors and the flow field;
    /// "none" and "compound" colliders stay decoration, the map adds what it needs.
    /// </summary>
    protected Node3D? Prop(string id, Vector3 at, float yaw = 0, float scale = 1, bool solid = true)
    {
        if (PropLibrary.Instance(id) is not { } model) return null;
        var info = PropLibrary.Find(id)!;
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(yaw));
        var c = info.Collision;
        Node3D holder;
        if (solid && c.Kind is "box" or "cylinder")
        {
            var size = (c.Kind == "box" ? c.Size : new Vector3(c.Radius * 2, c.Height, c.Radius * 2)) * scale;
            var center = c.Kind == "box" ? c.Center * scale : new Vector3(0, c.Height / 2 * scale, 0);
            var body = new StaticBody3D { Name = id, Position = at, Basis = basis, CollisionLayer = Layers.World, CollisionMask = 0 };
            _stage.AddChild(body);
            Shape3D shape = c.Kind == "box" ? new BoxShape3D { Size = size } : new CylinderShape3D { Radius = c.Radius * scale, Height = c.Height * scale };
            body.AddChild(new CollisionShape3D { Shape = shape, Position = center });
            // The flow field is axis aligned: a turned box blocks its turned footprint's bounds.
            var world = basis * center + at;
            var foot = (basis * new Vector3(size.X, 0, 0)).Abs() + (basis * new Vector3(0, 0, size.Z)).Abs();
            _solids.Add((world, new Vector3(foot.X, size.Y, foot.Z)));
            holder = body;
        }
        else
        {
            holder = new Node3D { Name = id, Position = at, Basis = basis };
            _stage.AddChild(holder);
        }
        model.Scale = Vector3.One * scale;
        holder.AddChild(model);
        KitProps.Add(holder);
        return holder;
    }

    /// <summary>Every kit model this map placed (checks, captures).</summary>
    public List<Node3D> KitProps { get; } = new();
    /// <summary>How many copies of each kit model <see cref="Scatter"/> drew (checks).</summary>
    public Dictionary<string, int> Scattered { get; } = new();

    /// <summary>
    /// Many copies of a decoration-only kit model (ground tiles, backdrop trees) drawn as one MultiMesh per mesh part:
    /// no colliders, no per-copy nodes.
    /// </summary>
    protected void Scatter(string id, IReadOnlyList<Transform3D> where)
    {
        if (where.Count == 0 || PropLibrary.Instance(id) is not { } model) return;
        foreach (var mesh in model.FindChildren("*", nameof(MeshInstance3D), true, false))
        {
            var part = (MeshInstance3D)mesh;
            var local = Transform3D.Identity;
            for (Node? n = part; n != null && n != model; n = n.GetParent()) if (n is Node3D n3) local = n3.Transform * local;
            var multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = part.Mesh, InstanceCount = where.Count };
            for (int i = 0; i < where.Count; i++) multi.SetInstanceTransform(i, where[i] * local);
            _stage.AddChild(new MultiMeshInstance3D { Name = id + "_" + part.Name, Multimesh = multi });
        }
        model.Free();
        Scattered[id] = Scattered.GetValueOrDefault(id) + where.Count;
    }

    protected void Weapon(Vector3 at, string id) => WeaponPickup.ForWeapon(_stage, at, ArmoryCatalog.Weapon(id));

    protected void Throwable(Vector3 at, string id) => WeaponPickup.ForThrowable(_stage, at, ArmoryCatalog.Throwable(id));

    protected void Boxes(Vector3 at, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var box = new RigidBody3D
            {
                Name = "Box",
                Mass = .6f,
                Position = at + new Vector3(i * .75f, .3f + (i == 2 ? .62f : 0), i == 2 ? 0 : _rng.RandfRange(-.2f, .2f)),
                CollisionLayer = Layers.Props,
                CollisionMask = Layers.World | Layers.Props | Layers.Player | Layers.Enemy,
            };
            box.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Vector3.One * .6f } });
            StageArt.Carton(box, Vector3.Zero, Vector3.One * .6f);
            _stage.AddChild(box);
            _stage.Props.Add(box);
        }
    }

    protected OmniLight3D Lamp(Vector3 at, string color, float energy, float range, bool flicker)
    {
        var light = new OmniLight3D { Position = at, LightColor = new Color(color), LightEnergy = energy, OmniRange = range, ShadowEnabled = false };
        light.SetMeta("base_energy", energy);
        // Enemies in a lamp's glow show as silhouettes (decisions.md C-23).
        light.AddToGroup(VisionSystem.LightGroup);
        _stage.AddChild(light);
        StageArt.Fixture(light, color);
        if (flicker) _stage.FlickerLights.Add(light);
        return light;
    }

    protected void Decal(Vector3 at, Vector3 size, string color) => Mat.Box(_stage, at + Vector3.Up * .01f, size, Mat.Solid(color, .9f));

    protected void FloorText(Vector3 at, string textKey, string color)
    {
        _stage.AddChild(new Label3D
        {
            Text = Loc.Text(textKey),
            Position = at + Vector3.Up * .03f,
            RotationDegrees = new Vector3(-90, 0, 0),
            FontSize = 72,
            PixelSize = .012f,
            Modulate = new Color(color, .8f),
            OutlineSize = 0,
        });
    }

    protected Interactable Use(Vector3 at, string promptKey, Action action)
    {
        var item = new Interactable { Name = "Use", Position = at, Prompt = promptKey, OnUse = _ => action() };
        _stage.AddChild(item);
        _stage.AddInteractable(item);
        return item;
    }

    protected void AmmoPile(Vector3 at)
    {
        var node = new Node3D { Name = "Ammo", Position = at + Vector3.Up * .2f };
        _stage.AddChild(node);
        StageArt.Ammo(node);
        var use = Use(at, "ui_use_ammo", () => { });
        use.OnUse = who => _stage.RefillAmmo(who ?? _stage.Player);
        use.Radius = 1.6f;
    }

    protected void Medkit(Vector3 at)
    {
        var node = new Interactable { Name = "Medkit", Position = at, Prompt = "ui_use_medkit", Radius = 1.6f };
        _stage.AddChild(node);
        _stage.AddInteractable(node);
        StageArt.Box(node, new Vector3(0, .15f, 0), new Vector3(.6f, .3f, .45f), "d3d8cc", true);
        StageArt.Box(node, new Vector3(0, .15f, .229f), new Vector3(.5f, .025f, .025f), "557a79");
        Mat.Box(node, new Vector3(0, .31f, 0), new Vector3(.36f, .02f, .1f), Mat.Glow(new Color("ff4040"), 1.5f));
        Mat.Box(node, new Vector3(0, .31f, 0), new Vector3(.1f, .02f, .3f), Mat.Glow(new Color("ff4040"), 1.5f));
        node.OnUse = who =>
        {
            var taker = who ?? _stage.Player;
            // Outpost: a second medkit goes into the backpack.
            if (taker.Medkits > 0 && taker.Backpack is { } bag && bag.Add(InvItem.Medkit()) == 0)
            {
                node.Enabled = false;
                node.Visible = false;
                _stage.Sfx.PlayAt("pickup", taker.GlobalPosition + Vector3.Up);
                return;
            }
            if (taker.Medkits > 0)
            {
                if (taker.IsLocal) _stage.ShowNotice(Loc.Text("msg_pickup_medkit_full"), 2);
                return;
            }
            taker.Medkits = 1;
            node.Enabled = false;
            node.Visible = false;
            if (!taker.IsLocal) { _stage.Sfx.PlayAt("pickup", taker.GlobalPosition + Vector3.Up); return; }
            _stage.Sfx.Play("pickup");
            _stage.ShowNotice(Loc.Text("msg_pickup_medkit"), 3);
        };
    }

    /// <summary>Weapons, throwables, ammo and medkits from supply-point data (decisions.md L-12).</summary>
    protected void PlaceSupplies(StageSupplies supplies)
    {
        foreach (var item in supplies.Points.SelectMany(p => p.Items))
            switch (item.Kind)
            {
                case SupplyItemKind.Weapon: Weapon(item.At, item.Id); break;
                case SupplyItemKind.Throwable: Throwable(item.At, item.Id); break;
                case SupplyItemKind.Ammo: AmmoPile(item.At); break;
                case SupplyItemKind.Medkit: Medkit(item.At); break;
            }
    }
}
