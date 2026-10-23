using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// A gondola across a gorge (decisions.md L-18, doc/elevation-tiers.md 11.2): a cut scene, not a fight. Every living
/// survivor must be on the platform before it leaves. Then the world holds (the same all-stop as the card vote, M-29)
/// while the gondola and its riders stay where they are and a fake backdrop of the gorge scrolls beneath them in depth
/// layers; at the end everyone steps off at the far station. The real gorge is never built (the way the reference game
/// avoids the cost of moving through it). Bundle A supplies the cabin and both station models.
/// </summary>
public partial class Gondola : Node3D
{
    /// <summary>Length of the ride (`[임시]`).</summary>
    public float RideSeconds { get; private set; } = 12f;
    public Rect2 Platform { get; private set; }
    public Vector3 Arrival { get; private set; }
    public bool Riding { get; private set; }
    public bool Completed { get; private set; }
    public float Progress { get; private set; }
    /// <summary>How far the backdrop has scrolled (checks).</summary>
    public float BackdropTravel { get; private set; }

    private Stage _stage = null!;
    private Vector3 _travel;
    private Node3D? _backdrop;
    private Node3D? _cabin, _departureStation, _arrivalStation;
    // A small visual offset prevents the imported floor fighting the terrain's existing walkable surface.
    private const float FloorClearance = .012f;
    private readonly List<Node3D> _hiddenWorld = new();
    private readonly List<Player> _passengers = new();
    private Interactable _use = null!;
    private readonly List<(Node3D layer, float depth)> _layers = new();

    public static Gondola Create(Stage stage, Rect2 platform, float floorY, Vector3 arrival, Vector3 travelDirection, float seconds)
    {
        var center = new Vector3(platform.GetCenter().X, floorY, platform.GetCenter().Y);
        var gondola = new Gondola
        {
            Name = "Gondola",
            _stage = stage,
            Platform = platform,
            Arrival = arrival,
            RideSeconds = seconds,
            _travel = new Vector3(travelDirection.X, 0, travelDirection.Z).Normalized(),
            // It keeps going while the world holds for the ride.
            ProcessMode = ProcessModeEnum.Always,
        };
        stage.AddChild(gondola);
        gondola.GlobalPosition = center;
        gondola.BuildLook();
        var use = new Interactable { Name = "GondolaUse", Position = center, Prompt = "ui_use_gondola", Radius = Mathf.Max(platform.Size.X, platform.Size.Y) * .6f };
        use.OnUse = _ => gondola.TryDepart();
        gondola._use = use;
        stage.AddChild(use);
        stage.AddInteractable(use);
        stage.Gondolas.Add(gondola);
        return gondola;
    }

    public bool Aboard(Player p) => !p.OnLadder && !p.OnZipline
        && Mathf.Abs(p.GlobalPosition.Y - GlobalPosition.Y) <= .3f
        && Platform.Grow(-p.Definition.ColliderRadius).HasPoint(new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z));

    /// <summary>Leaves when every living survivor is aboard; otherwise says so and waits.</summary>
    public bool TryDepart()
    {
        if (Riding || Completed || _stage.AllStopped) return false;
        var passengers = _stage.AlivePlayers.ToList();
        if (passengers.Count == 0 || !passengers.All(Aboard))
        {
            _stage.ShowNotice(Loc.Text("msg_gondola_wait"), 2.5f);
            return false;
        }
        Riding = true;
        _passengers.AddRange(passengers);
        foreach (var passenger in _passengers) passenger.BeginGondolaRide(this);
        _use.Enabled = false;
        Progress = 0;
        BackdropTravel = 0;
        BuildBackdrop();
        HideWorld();
        _stage.SetAllStop(true);
        return true;
    }

    public override void _Process(double delta)
    {
        if (!Riding) return;
        foreach (var passenger in _passengers)
            if (IsInstanceValid(passenger)) passenger.HoldGondolaPosition();
        float dt = (float)delta;
        Progress += dt / RideSeconds;
        BackdropTravel += dt * 6f;
        // Far layers drift slowly, near ones fast: the depth of a gorge without building it.
        foreach (var (layer, depth) in _layers)
        {
            float span = 120f;
            float shift = Mathf.PosMod(BackdropTravel / depth, span) - span / 2;
            layer.Position = -_travel * shift + Vector3.Down * (8f * depth);
        }
        if (Progress >= 1) Arrive();
    }

    /// <summary>Ends the ride at once (also when the ride time is up): everyone steps off at the far station.</summary>
    public void Arrive()
    {
        if (!Riding) return;
        Riding = false;
        Completed = true;
        Progress = 1;
        _backdrop?.QueueFree();
        _backdrop = null;
        _layers.Clear();
        RestoreWorld();
        if (_cabin != null) _cabin.GlobalPosition = Arrival + Vector3.Up * FloorClearance;
        int i = 0;
        foreach (var p in _passengers)
        {
            if (!IsInstanceValid(p)) continue;
            p.EndGondolaRide(this);
            var side = _travel.Cross(Vector3.Up) * ((i % 3) - 1) * 1.1f - _travel * (i / 3) * 1.1f;
            p.TeleportTo(Arrival + side + Vector3.Up * .05f);
            i++;
        }
        _passengers.Clear();
        _stage.SetAllStop(false);
        _stage.Cam.SnapTo(_stage.Player.GlobalPosition + Vector3.Up * .8f);
    }

    private void BuildLook()
    {
        Node3D? Place(string id, Vector3 at)
        {
            var model = PropLibrary.Instance(id);
            if (model == null) return null;
            AddChild(model);
            model.GlobalPosition = at;
            PropOcclusion.Attach(_stage, model);
            return model;
        }
        _cabin = Place("gondola_cabin", GlobalPosition + Vector3.Up * FloorClearance);
        _departureStation = Place("gondola_station", GlobalPosition);
        _arrivalStation = Place("gondola_station", Arrival);
    }

    private void HideWorld()
    {
        // Collision and flow stay intact. Only rendering is suspended; remember exactly which roots were visible.
        foreach (var node in _stage.GetChildren().OfType<Node3D>())
        {
            // Keep sunlight and rider-attached lights. World lamps carry fixture meshes, so leaving
            // those visible would make streetlamp heads float in the scrolling gorge.
            if (node == this || node is Player or Camera3D or DirectionalLight3D || !node.Visible) continue;
            _hiddenWorld.Add(node);
            node.Visible = false;
        }
        foreach (var station in new[] { _departureStation, _arrivalStation })
            if (station != null && station.Visible) { _hiddenWorld.Add(station); station.Visible = false; }
    }

    private void RestoreWorld()
    {
        foreach (var node in _hiddenWorld)
            if (IsInstanceValid(node)) node.Visible = true;
        _hiddenWorld.Clear();
    }

    public override void _ExitTree()
    {
        if (!Riding) return;
        foreach (var passenger in _passengers)
            if (IsInstanceValid(passenger)) passenger.EndGondolaRide(this);
        _passengers.Clear();
        RestoreWorld();
        if (IsInstanceValid(_stage) && _stage.IsInsideTree()) _stage.SetAllStop(false);
    }

    private void BuildBackdrop()
    {
        _backdrop = new Node3D { Name = "GondolaBackdrop" };
        AddChild(_backdrop);
        // The cabin wheels run along local X. This short visual span disappears with the ride backdrop.
        Mat.Box(_backdrop, Vector3.Up * (3.4f + FloorClearance), new Vector3(100, .04f, .04f), Mat.Solid("8d9298", .4f));
        var rng = new RandomNumberGenerator { Seed = 7 };
        foreach (float depth in new[] { 4f, 2.5f, 1.6f, 1.1f })
        {
            var layer = new Node3D { Name = "Layer" };
            _backdrop.AddChild(layer);
            if(depth==4)
            {
                layer.AddChild(new MeshInstance3D { Name="GorgeFloor",Position=Vector3.Down*25,
                    Mesh=new PlaneMesh{Size=new Vector2(240,240)},MaterialOverride=Mat.Solid("3d5141",.98f),
                    CastShadow=GeometryInstance3D.ShadowCastingSetting.Off });
            }
            var trees=new List<Transform3D>();
            for (int k = 0; k < 14; k++)
            {
                // Tall rock foundations extend down into the gorge; canopies remain below the cabin.
                float height=rng.RandfRange(18,28);
                var size=new Vector3(rng.RandfRange(10,18),height,rng.RandfRange(10,18));
                var at=new Vector3(rng.RandfRange(-65,65),-height,rng.RandfRange(-50,50));
                float yaw=rng.Randf()*Mathf.Tau;
                layer.AddChild(new MeshInstance3D{Name="GorgeRock",Mesh=OutdoorEdgeArt.Rock(k%6),Position=at,
                    Basis=new Basis(Vector3.Up,yaw)*Basis.FromScale(size),CastShadow=GeometryInstance3D.ShadowCastingSetting.Off});
                var treeAt=at+new Basis(Vector3.Up,yaw)*(new Vector3(-.07f,.91f,.065f)*size);
                trees.Add(new Transform3D(new Basis(Vector3.Up,rng.Randf()*Mathf.Tau).Scaled(Vector3.One*rng.RandfRange(1,1.5f)),treeAt));
            }
            FoliageArt.Instances(layer,"GorgePines",FoliageArt.Kind.Pine,1,trees,false,detail:1);
            _layers.Add((layer, depth));
        }
    }
}
