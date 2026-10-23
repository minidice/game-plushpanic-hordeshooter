using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// A graybox stage built from layout data (decisions.md T-24 option B, L-29): the size draft of the new Stage 2
/// (doc/map-expansion-plan.md 7.2 step 3). Regions become raised floors per height tier, merged into rectangles on the
/// 1 m grid; everything outside every region is blocked by tall invisible walls; links become ramps, ladders, drop
/// edges, ziplines and the gondola; climbable edges are painted for wall climbers. Plain boxes only: the look comes
/// later from the existing kit and new models (local Codex work). Not wired into <see cref="StageMap.Create"/> yet:
/// the current Stage 2 stays until this draft is accepted (L-29).
/// </summary>
public sealed class LayoutMap : StageMap
{
    public const string Stage2Path = "res://content/data/stages/stage2_layout.json";
    /// <summary>Ramp length along its slope (`[임시]`): 6 m per tier, about 11°.</summary>
    public const float RampLength = 6f;
    // Strong steps between tiers so a graybox reads at a glance (green ground, then sand, clay, slate, steel blue).
    private static readonly string[] TierColors = { "4f6b45", "9a8f5e", "a8704a", "6f6488", "557d9c", "8fb0c4" };

    public StageLayout Layout { get; }
    /// <summary>Floor and blocker rectangles the grid was merged into (checks).</summary>
    public int FloorRects { get; private set; }
    public int VoidRects { get; private set; }
    public Gondola? Gondola { get; private set; }
    private Vector3 _goal;

    public LayoutMap(string path = Stage2Path) => Layout = StageLayout.Load(path);

    public override Rect2 Area => Layout.Bounds;
    public override string TitleKey => "ui_hud_stage2";
    public override bool HasRoute => true;
    public override Vector3 RouteGoal => _goal;
    public override int StageNumber => 2;
    public override Objective ScenarioStart => Objective.ReachSafehouse;
    public override CameraView View => CameraView.Outdoor;
    public override Rect2? CameraBounds => Layout.Bounds;

    public override void Light(Godot.Environment env, DirectionalLight3D key)
    {
        env.BackgroundColor = new Color("5d6b78");
        env.AmbientLightColor = new Color("b9c6d2");
        env.AmbientLightEnergy = .55f;
        env.TonemapExposure = 1.05f;
        env.FogEnabled = true;
        env.FogMode = Godot.Environment.FogModeEnum.Depth;
        env.FogLightColor = new Color("7d8b97");
        env.FogDensity = .85f;
        env.FogDepthCurve = 1.4f;
        env.FogSkyAffect = 1;
        key.Name = "Sun";
        key.RotationDegrees = new Vector3(-36, -48, 0);
        key.LightColor = new Color("e5e8d5");
        key.LightEnergy = 1.2f;
    }

    protected override void BuildMap()
    {
        foreach (var error in Layout.Errors) GD.PushWarning("LayoutMap: " + error);
        var tiers = _stage.Tiers;
        int width = tiers.Width, depth = tiers.Depth;
        var grid = new int[width * depth];
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
            grid[z * width + x] = Layout.TierAt(new Vector2(tiers.MinX + (x + .5f) * tiers.Cell, tiers.MinZ + (z + .5f) * tiers.Cell));

        foreach (var (x0, z0, x1, z1, value) in MergeRects(grid, width, depth))
        {
            float wx0 = tiers.MinX + x0 * tiers.Cell, wx1 = tiers.MinX + x1 * tiers.Cell;
            float wz0 = tiers.MinZ + z0 * tiers.Cell, wz1 = tiers.MinZ + z1 * tiers.Cell;
            if (value >= 0)
            {
                Raised(wx0, wx1, wz0, wz1, value, "", draw: false);
                float top = value * TierMap.Height;
                var size = new Vector3(wx1 - wx0, top + .4f, wz1 - wz0);
                Mat.Box(_stage, new Vector3((wx0 + wx1) / 2, (top - .4f) / 2, (wz0 + wz1) / 2), size,
                    Mat.Solid(TierColors[Math.Clamp(value, 0, TierColors.Length - 1)], .95f));
                FloorRects++;
            }
            else
            {
                // Off the map: a tall invisible wall, and a dark floor so the edge reads as "nowhere to go".
                Solid(new Vector3((wx0 + wx1) / 2, 4, (wz0 + wz1) / 2), new Vector3(wx1 - wx0, 8, wz1 - wz0), "", drawCargo: false);
                Mat.Box(_stage, new Vector3((wx0 + wx1) / 2, -.05f, (wz0 + wz1) / 2), new Vector3(wx1 - wx0, .1f, wz1 - wz0), Mat.Solid("27332b", 1f));
                VoidRects++;
            }
        }

        foreach (var edge in Layout.Edges)
        {
            if (!edge.Climbable) continue;
            for (int i = 0; i + 1 < edge.Line.Length; i++)
            {
                var a = edge.Line[i];
                var b = edge.Line[i + 1];
                ClimbableWall(Mathf.Min(a.X, b.X) - 1, Mathf.Max(a.X, b.X) + 1, Mathf.Min(a.Y, b.Y) - 1, Mathf.Max(a.Y, b.Y) + 1);
            }
        }

        foreach (var link in Layout.Links) BuildLink(link);

        var start = Layout.FindMarker("start")?.Pos ?? Layout.Bounds.GetCenter();
        PlayerStart = Ground(start) + Vector3.Up * .05f;
        var goal = Layout.FindMarker("safehouse")?.Pos ?? start;
        _goal = Ground(goal);
        foreach (var marker in Layout.Markers) Post(marker);
        Lamp(PlayerStart + Vector3.Up * 4, "ffe6b0", 3, 24, false);
    }

    private Vector3 Ground(Vector2 p) => new(p.X, _stage.Tiers.GroundY(new Vector3(p.X, 0, p.Y)), p.Y);

    private void BuildLink(StageLayout.Link link)
    {
        switch (link.Type)
        {
            case "ramp":
            {
                if (Crossing(link.From, link.To) is not { } c) return;
                bool alongX = Mathf.Abs(c.Up.X) > .5f;
                // Whole cells across too: the cells painted as ramp and the wedge's body must be the same strip,
                // or a walker on the ramp's side cell is led into the cliff beside it.
                float side0 = Mathf.Round((alongX ? c.Edge.Y : c.Edge.X) - link.Width / 2), side1 = side0 + Mathf.Round(link.Width);
                if (alongX)
                {
                    float x0 = c.Up.X > 0 ? c.Edge.X - RampLength : c.Edge.X, x1 = c.Up.X > 0 ? c.Edge.X : c.Edge.X + RampLength;
                    Ramp(x0, x1, side0, side1, c.Low, c.Up, TierColors[Math.Clamp(c.Low, 0, TierColors.Length - 1)]);
                }
                else
                {
                    float z0 = c.Up.Z > 0 ? c.Edge.Y - RampLength : c.Edge.Y, z1 = c.Up.Z > 0 ? c.Edge.Y : c.Edge.Y + RampLength;
                    Ramp(side0, side1, z0, z1, c.Low, c.Up, TierColors[Math.Clamp(c.Low, 0, TierColors.Length - 1)]);
                }
                break;
            }
            case "ladder":
            {
                if (Crossing(link.From, link.To) is not { } c) return;
                var edge = new Vector3(c.Edge.X, 0, c.Edge.Y);
                Ladder(edge - c.Up * .5f, edge + c.Up * .5f);
                break;
            }
            case "drop":
            {
                if (Crossing(link.From, link.To) is not { } c) return;
                // The drop cells are the rim of the high side, facing down toward the low side.
                var edge = new Vector3(c.Edge.X, 0, c.Edge.Y);
                var rim = edge + c.Up * .9f;
                var across = c.Up.Cross(Vector3.Up).Abs() * 1.6f;
                DropEdge(Mathf.Min(rim.X - across.X, edge.X), Mathf.Max(rim.X + across.X, edge.X), Mathf.Min(rim.Z - across.Z, edge.Z), Mathf.Max(rim.Z + across.Z, edge.Z));
                break;
            }
            case "zipline":
                Zipline(Ground(link.From), Ground(link.To));
                break;
            case "gondola":
            {
                var from = Ground(link.From);
                var to = Ground(link.To);
                Gondola = Gondola(new Rect2(link.From.X - 2, link.From.Y - 2, 4, 4), from.Y, to, to - from);
                // The route crosses the gorge by the gondola (survivors only, like a zipline).
                _stage.Tiers.AddRide(from, to, Gondola.RideSeconds);
                break;
            }
        }
    }

    /// <summary>
    /// Where the segment <paramref name="a"/> → <paramref name="b"/> crosses from one tier to another, snapped to the grid
    /// line along its main axis: the edge point, the lower tier, and the unit axis pointing from the low side to the high.
    /// </summary>
    private (Vector2 Edge, int Low, Vector3 Up)? Crossing(Vector2 a, Vector2 b)
    {
        int ta = Layout.TierAt(a), tb = Layout.TierAt(b);
        if (ta < 0 || tb < 0 || ta == tb)
        {
            GD.PushWarning($"LayoutMap: link {a} → {b} does not cross between two tiers ({ta}, {tb})");
            return null;
        }
        var d = b - a;
        bool alongX = Mathf.Abs(d.X) >= Mathf.Abs(d.Y);
        float steps = Mathf.Max(1, d.Length() / .05f);
        var edge = b;
        for (int i = 1; i <= steps; i++)
        {
            var p = a + d * (i / steps);
            if (Layout.TierAt(p) == ta) continue;
            edge = p;
            break;
        }
        edge = alongX ? new Vector2(Mathf.Round(edge.X), edge.Y) : new Vector2(edge.X, Mathf.Round(edge.Y));
        var toHigh = (tb > ta ? d : -d);
        var up = alongX ? new Vector3(Mathf.Sign(toHigh.X), 0, 0) : new Vector3(0, 0, Mathf.Sign(toHigh.Y));
        return (edge, Math.Min(ta, tb), up);
    }

    /// <summary>A tall coloured post with a label where the layout marks something (graybox orientation only).</summary>
    private void Post(StageLayout.Marker marker)
    {
        string color = marker.Type switch
        {
            "start" => "e04a3a", "safehouse" => "e04a3a", "safe_zone" => "3fbf6a", "mission" => "f08a24",
            "blocked" => "e04a3a", "noise" => "c63dbf", _ => "dddddd",
        };
        var at = Ground(marker.Pos);
        Mat.Box(_stage, at + Vector3.Up * 1.5f, new Vector3(.3f, 3, .3f), Mat.Glow(new Color(color), 1.2f));
        _stage.AddChild(new Label3D
        {
            Text = marker.Label,
            Position = at + Vector3.Up * 3.4f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 48,
            PixelSize = .01f,
            Modulate = new Color(color),
        });
    }

    /// <summary>Greedy rectangles of equal values on the grid: (x0, z0, x1, z1) in cells, end exclusive.</summary>
    public static List<(int x0, int z0, int x1, int z1, int value)> MergeRects(int[] grid, int width, int depth)
    {
        var done = new bool[grid.Length];
        var rects = new List<(int, int, int, int, int)>();
        for (int z = 0; z < depth; z++)
        for (int x = 0; x < width; x++)
        {
            int i = z * width + x;
            if (done[i]) continue;
            int v = grid[i];
            int x1 = x + 1;
            while (x1 < width && !done[z * width + x1] && grid[z * width + x1] == v) x1++;
            int z1 = z + 1;
            while (z1 < depth)
            {
                bool full = true;
                for (int k = x; k < x1 && full; k++) full = !done[z1 * width + k] && grid[z1 * width + k] == v;
                if (!full) break;
                z1++;
            }
            for (int zz = z; zz < z1; zz++)
            for (int xx = x; xx < x1; xx++) done[zz * width + xx] = true;
            rects.Add((x, z, x1, z1, v));
        }
        return rects;
    }
}
