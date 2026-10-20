using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Onslaught's open field (decisions.md L-13, M-26): a wide night meadow with almost no walls. Only where things
/// stand is random: trees and stone pillars spread with a minimum spacing (dart throwing, a simple Poisson-disc),
/// a clear circle around the start, a few barrels and lamps. Hedges close the edge; the camera-side one is low.
/// The same <see cref="StageMap.Seed"/> gives the same field on every PC.
/// </summary>
public sealed class OpenFieldMap : StageMap
{
    /// <summary>Half the side of the walkable square (metres).</summary>
    public const float Half = 44f;
    public const float StartClear = 9f;
    public const float Spacing = 6.5f;

    public override Rect2 Area => new(-Half - 2, -Half - 2, Half * 2 + 4, Half * 2 + 4);
    public override string TitleKey => "ui_map_open_field";
    /// <summary>Where each tree and pillar stands (checks).</summary>
    public IReadOnlyList<Vector3> Obstacles => _obstacles;
    public int Trees { get; private set; }
    public int Pillars { get; private set; }

    private readonly List<Vector3> _obstacles = new();

    protected override void BuildMap()
    {
        PlayerStart = Vector3.Zero;
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        Floor(-Half, Half, -Half, Half, "2f3a2c");
        Edges();

        // Trees and pillars, never closer than Spacing to each other, none in the start circle.
        for (int attempt = 0; attempt < 4000 && _obstacles.Count < 70; attempt++)
        {
            var p = new Vector3(rng.RandfRange(-Half + 3, Half - 3), 0, rng.RandfRange(-Half + 3, Half - 3));
            if (p.Length() < StartClear || TooClose(p, Spacing)) continue;
            _obstacles.Add(p);
            if (rng.Randf() < .72f) Tree(p, rng);
            else Pillar(p, rng);
        }
        // Barrels away from the start and from the trees' trunks.
        var barrels = new List<Vector3>();
        for (int attempt = 0; attempt < 600 && barrels.Count < 10; attempt++)
        {
            var p = new Vector3(rng.RandfRange(-Half + 4, Half - 4), 0, rng.RandfRange(-Half + 4, Half - 4));
            if (p.Length() < StartClear + 3 || TooClose(p, 2.5f) || barrels.Exists(b => b.DistanceTo(p) < 8)) continue;
            barrels.Add(p);
            Barrel(p);
        }
        // A light over the start and a loose grid of lamps so the swarm shows as silhouettes (C-23).
        Lamp(new Vector3(0, 4.2f, 0), "ffe2b0", 4.2f, 16, false);
        for (float x = -30; x <= 30; x += 20)
            for (float z = -30; z <= 30; z += 20)
            {
                if (Mathf.Abs(x) < 1 && Mathf.Abs(z) < 1) continue;
                var at = new Vector3(x + rng.RandfRange(-4, 4), 0, z + rng.RandfRange(-4, 4));
                if (TooClose(at, 2f)) continue;
                LampPost(at, rng.Randf() < .25f);
            }
        // Medkits only: reserves never run out in Onslaught.
        for (int i = 0; i < 4; i++)
        {
            float a = Mathf.Tau * (i + .5f) / 4;
            var at = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 18f;
            Medkit(TooClose(at, 2f) ? at * .8f : at);
        }
    }

    private bool TooClose(Vector3 p, float distance)
    {
        foreach (var o in _obstacles)
            if (o.DistanceTo(p) < distance) return true;
        return false;
    }

    private void Edges()
    {
        // Far side and flanks: tall hedges. Camera side: a low hedge that never hides the action.
        Hedge(-Half - .8f, Half + .8f, -Half - .8f, -Half, 2.4f);
        Hedge(-Half - .8f, Half + .8f, Half, Half + .8f, 1f);
        Hedge(-Half - .8f, -Half, -Half, Half, 2.4f);
        Hedge(Half, Half + .8f, -Half, Half, 2.4f);
    }

    private void Hedge(float x0, float x1, float z0, float z1, float height)
    {
        var body = Wall(x0, x1, z0, z1, height, "3b5a32", drawWall: false);
        StageArt.Box(body, Vector3.Zero, new Vector3(x1 - x0, height, z1 - z0), "35512e", true);
    }

    /// <summary>A round plush tree: a short trunk you collide with and a soft two-ball crown.</summary>
    private void Tree(Vector3 at, RandomNumberGenerator rng)
    {
        Trees++;
        float s = rng.RandfRange(.85f, 1.2f);
        var body = Solid(at + Vector3.Up * 1f, new Vector3(.9f, 2f, .9f), "6b4a2f", drawCargo: false);
        Mat.Mesh(body, new CylinderMesh { TopRadius = .32f, BottomRadius = .42f, Height = 2f, RadialSegments = 10 }, Vector3.Zero, Mat.Solid("6b4a2f"));
        var leaf = Mat.Solid(rng.Randf() < .5f ? "3f6b3a" : "4b7a3e");
        Mat.Mesh(body, new SphereMesh { Radius = 1.25f * s, Height = 2.1f * s, RadialSegments = 14, Rings = 8 }, new Vector3(0, 1.55f, 0), leaf);
        Mat.Mesh(body, new SphereMesh { Radius = .8f * s, Height = 1.4f * s, RadialSegments = 12, Rings = 6 }, new Vector3(.45f, 2.35f, -.2f), leaf);
    }

    /// <summary>An old stone pillar, sometimes broken off short.</summary>
    private void Pillar(Vector3 at, RandomNumberGenerator rng)
    {
        Pillars++;
        float h = rng.Randf() < .35f ? 1.3f : 2.8f;
        var body = Solid(at + Vector3.Up * (h / 2), new Vector3(1.1f, h, 1.1f), "8a8f88", drawCargo: false);
        Mat.Mesh(body, new CylinderMesh { TopRadius = .5f, BottomRadius = .58f, Height = h, RadialSegments = 12 }, Vector3.Zero, Mat.Solid("8f948c"));
        Mat.Mesh(body, new CylinderMesh { TopRadius = .66f, BottomRadius = .66f, Height = .22f, RadialSegments = 12 }, new Vector3(0, h / 2 - .11f, 0), Mat.Solid("a3a89f"));
    }

    private void LampPost(Vector3 at, bool flicker)
    {
        var post = Solid(at + Vector3.Up * 1.6f, new Vector3(.25f, 3.2f, .25f), "3a3f44", drawCargo: false);
        Mat.Mesh(post, new CylinderMesh { TopRadius = .07f, BottomRadius = .1f, Height = 3.2f, RadialSegments = 8 }, Vector3.Zero, Mat.Solid("3a3f44"));
        Lamp(at + Vector3.Up * 3.3f, "ffcf8a", 3.2f, 14, flicker);
    }
}
