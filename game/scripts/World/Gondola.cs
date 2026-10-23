using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// A gondola across a gorge (decisions.md L-18, doc/elevation-tiers.md 11.2): a cut scene, not a fight. Every living
/// survivor must be on the platform before it leaves. Then the world holds (the same all-stop as the card vote, M-29)
/// while the gondola and its riders stay where they are and a fake backdrop of the gorge scrolls beneath them in depth
/// layers; at the end everyone steps off at the far station. The real gorge is never built (the way the reference game
/// avoids the cost of moving through it). Placeholder boxes until the final model and backdrop (local Codex work).
/// </summary>
public partial class Gondola : Node3D
{
    /// <summary>Length of the ride (`[임시]`).</summary>
    public float RideSeconds { get; private set; } = 12f;
    public Rect2 Platform { get; private set; }
    public Vector3 Arrival { get; private set; }
    public bool Riding { get; private set; }
    public float Progress { get; private set; }
    /// <summary>How far the backdrop has scrolled (checks).</summary>
    public float BackdropTravel { get; private set; }

    private Stage _stage = null!;
    private Vector3 _travel;
    private Node3D? _backdrop;
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
        Mat.Box(gondola, Vector3.Up * .05f, new Vector3(platform.Size.X, .1f, platform.Size.Y), Mat.Solid("8a6a46", .8f));
        var use = new Interactable { Name = "GondolaUse", Position = center, Prompt = "ui_use_gondola", Radius = Mathf.Max(platform.Size.X, platform.Size.Y) * .6f };
        use.OnUse = _ => gondola.TryDepart();
        stage.AddChild(use);
        stage.AddInteractable(use);
        stage.Gondolas.Add(gondola);
        return gondola;
    }

    public bool Aboard(Player p) => Platform.Grow(.3f).HasPoint(new Vector2(p.GlobalPosition.X, p.GlobalPosition.Z));

    /// <summary>Leaves when every living survivor is aboard; otherwise says so and waits.</summary>
    public bool TryDepart()
    {
        if (Riding) return false;
        if (!_stage.AlivePlayers.All(Aboard))
        {
            _stage.ShowNotice(Loc.Text("msg_gondola_wait"), 2.5f);
            return false;
        }
        Riding = true;
        Progress = 0;
        BackdropTravel = 0;
        BuildBackdrop();
        _stage.SetAllStop(true);
        return true;
    }

    public override void _Process(double delta)
    {
        if (!Riding) return;
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
        Progress = 1;
        _backdrop?.QueueFree();
        _backdrop = null;
        _layers.Clear();
        int i = 0;
        foreach (var p in _stage.AlivePlayers.ToList())
        {
            var side = _travel.Cross(Vector3.Up) * ((i % 3) - 1) * 1.1f - _travel * (i / 3) * 1.1f;
            p.TeleportTo(Arrival + side + Vector3.Up * .05f);
            i++;
        }
        _stage.SetAllStop(false);
    }

    private void BuildBackdrop()
    {
        _backdrop = new Node3D { Name = "GondolaBackdrop" };
        AddChild(_backdrop);
        var colors = new[] { ("2f4a3a", 4f), ("3d5c46", 2.5f), ("51704f", 1.6f), ("6d8a69", 1.1f) };
        var rng = new RandomNumberGenerator { Seed = 7 };
        foreach (var (color, depth) in colors)
        {
            var layer = new Node3D { Name = "Layer" };
            _backdrop.AddChild(layer);
            var mat = Mat.Solid(color, .95f);
            for (int k = 0; k < 14; k++)
            {
                var size = new Vector3(rng.RandfRange(4, 12), rng.RandfRange(3, 14) / depth, rng.RandfRange(4, 12));
                var at = new Vector3(rng.RandfRange(-60, 60), 0, rng.RandfRange(-60, 60));
                Mat.Box(layer, at, size, mat);
            }
            _layers.Add((layer, depth));
        }
    }
}
