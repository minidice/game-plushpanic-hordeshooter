using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Concrete fence art shared by the game and Studio. Local origin is ground level;
/// X is the span. Collision, damage and explosion effects belong to the caller.</summary>
public static class BreakableWallArt
{
    public const float Width = 4.8f;
    public const float Height = 2.6f;
    public const float Thickness = .45f;
    public const float OpeningWidth = 3.2f;

    public static BreakableWallVisual Create(Node parent, bool cracked = true)
    {
        var visual = new BreakableWallVisual { Name = "ConcretePanelWall" };
        parent.AddChild(visual);
        visual.Build(cracked);
        return visual;
    }
}

/// <summary>Deterministic visual timeline: 0 intact, .35 airborne fragments, 1 settled rubble.
/// SetProgress also stops playback, so Studio snapshots never depend on elapsed frame time.</summary>
public partial class BreakableWallVisual : Node3D
{
    private const float Duration = .8f;
    private const string Concrete = "929388", Cap = "b3b1a1", Fresh = "b5a58b", Steel = "5a5144";
    private static readonly StandardMaterial3D StoneMaterial = new()
    {
        VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = .94f,
    };
    private readonly List<Fragment> _fragments = new();
    private readonly List<Node3D> _dust = new();
    private Node3D _intact = null!, _broken = null!, _rubble = null!;
    private float _progress;
    private bool _playing;

    private sealed record Fragment(Node3D Node, Vector3 Start, Vector3 End, Vector3 Spin, float Delay, float Scale);

    internal void Build(bool cracked)
    {
        SetProcess(false);
        _intact = Group(this, "IntactPanels");
        _broken = Group(this, "FracturedEnds");
        _rubble = Group(this, "GroundGravel");
        var permanent = Group(this, "PermanentPosts");
        foreach (int sign in new[] { -1, 1 })
        {
            float x = sign * 2.13f;
            Box(permanent, new(x, 1.3f, 0), new(.47f, 2.48f, .39f), Concrete);
            Box(permanent, new(x, .105f, 0), new(.54f, .21f, .45f), "777c74");
            Box(permanent, new(x, 2.53f, 0), new(.54f, .14f, .45f), Cap);
            // A narrow casting groove and worn edge give the posts a concrete profile.
            foreach (int face in new[] { -1, 1 })
            {
                Box(permanent, new(x + sign * .15f, 1.3f, face * .198f), new(.025f, 2.25f, .009f), "7b7e74", false);
                Box(permanent, new(x - sign * .16f, 1.3f, face * .199f), new(.025f, 2.2f, .012f), "a4a598", false);
            }
            BuildEnd(sign);
        }
        for (int row = 0; row < 4; row++)
        {
            float bottom = .16f + row * .55f;
            Box(_intact, new(0, bottom + .255f, 0), new(3.84f, .51f, .29f), row % 2 == 0 ? Concrete : "98998d");
            foreach (int face in new[] { -1, 1 })
                Box(_intact, new(0, bottom + .075f, face * .16f), new(3.84f, .10f, .055f), "a0a193");
        }
        Box(_intact, new(0, .095f, 0), new(3.84f, .19f, .34f), "7b8076");
        Box(_intact, new(0, 2.43f, 0), new(3.84f, .14f, .36f), Cap);
        if (cracked) BuildCracks();
        BuildFragments();
        BuildGravel();
        StaticArtBatch.Bake(permanent);
        StaticArtBatch.Bake(_intact);
        StaticArtBatch.Bake(_rubble);
        foreach (var fragment in _fragments) StaticArtBatch.Bake(fragment.Node);
        for (int i = 0; i < 9; i++)
        {
            var puff = Mat.Mesh(this, new SphereMesh { Radius = 1, Height = 2, RadialSegments = 7, Rings = 3 },
                Vector3.Zero, Mat.Solid(i % 2 == 0 ? "a49b87" : "b4a993"), "ConcreteDust");
            puff.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
            _dust.Add(puff);
        }
        SetProgress(0);
    }

    private void BuildEnd(int sign)
    {
        // The inner jagged edge never crosses the 3.2 m clear corridor, including steel.
        Vector2[] outline = {
            new(1.60f, .10f), new(1.91f, .10f), new(1.91f, 2.40f),
            new(1.76f, 2.40f), new(1.81f, 2.16f), new(1.64f, 1.94f),
            new(1.78f, 1.68f), new(1.62f, 1.44f), new(1.72f, 1.17f),
            new(1.60f, .91f), new(1.72f, .60f), new(1.60f, .35f),
        };
        for (int i = 0; i < outline.Length; i++) outline[i].X *= sign;
        Prism(_broken, outline, .29f, Concrete, Fresh, "JaggedPanelEnd");
        for (int row = 0; row < 4; row++)
        {
            float y = .235f + row * .55f;
            foreach (int face in new[] { -1, 1 })
                Box(_broken, new(sign * 1.84f, y, face * .16f), new(.14f, .10f, .055f), "a0a193");
        }
        Rod(_broken, new(sign * 1.68f, .75f, .005f), new(sign * 1.67f, 1.27f, .015f));
        Rod(_broken, new(sign * 1.76f, 1.82f, -.015f), new(sign * 1.65f, 2.24f, -.03f));
        Rod(_broken, new(sign * 1.92f, 1.43f, .015f), new(sign * 1.63f, 1.40f, .045f));
        // Ribbing makes the rods read as reinforcing steel, rather than wooden splinters.
        for (int i = 0; i < 6; i++)
            Box(_broken, new(sign * 1.675f, .84f + i * .067f, .01f), new(.048f, .014f, .048f), "796853", false);
    }

    private void BuildCracks()
    {
        Vector2[][] paths = {
            new[] { new Vector2(-.46f, 2.46f), new(-.29f, 2.12f), new(-.48f, 1.86f), new(-.17f, 1.54f), new(-.28f, 1.29f), new(.03f, .98f), new(-.10f, .65f), new(.19f, .33f), new(.11f, .04f) },
            new[] { new Vector2(-.17f, 1.54f), new(.23f, 1.67f), new(.48f, 1.55f), new(.77f, 1.82f), new(1.10f, 1.90f), new(1.40f, 2.13f) },
            new[] { new Vector2(-.28f, 1.29f), new(-.64f, 1.14f), new(-.78f, .88f), new(-1.14f, .73f), new(-1.40f, .40f) },
            new[] { new Vector2(.03f, .98f), new(.37f, .88f), new(.60f, .57f), new(.95f, .47f) },
            new[] { new Vector2(-.48f, 1.86f), new(-.91f, 1.97f), new(-1.10f, 2.22f) },
        };
        foreach (int face in new[] { -1, 1 })
        {
            // Angular exposed chips sit under tapered dark fissures. No flat warning decal.
            foreach (var chip in new[] { new Vector2(-.20f, 1.50f), new(-.43f, 1.88f), new(.06f, .96f) })
            {
                Vector2[] polygon = { chip + new Vector2(-.10f, -.06f), chip + new Vector2(.035f, -.15f), chip + new Vector2(.12f, .03f), chip + new Vector2(.02f, .13f) };
                var patch = Prism(_intact, polygon, .008f, "b3a38a", "9c8e76", "ExposedAggregate");
                patch.Position = new(0, 0, face * .192f);
            }
            for (int p = 0; p < paths.Length; p++)
                for (int i = 0; i < paths[p].Length - 1; i++)
                {
                    float width = p == 0 ? .035f : Mathf.Lerp(.026f, .009f, i / (float)(paths[p].Length - 1));
                    CrackSegment(_intact, paths[p][i], paths[p][i + 1], width, face * .199f);
                }
        }
    }

    private void BuildFragments()
    {
        // Four courses, split along irregular diagonals; each piece retains its cast panel rib.
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
            {
                int index = row * 4 + col;
                float x = -1.2f + col * .8f, y = .415f + row * .55f;
                float skew = ((row + col) % 2 == 0 ? 1 : -1) * .09f;
                var node = Group(this, "FallingConcrete");
                Vector2[] polygon = { new(-.40f, -.255f), new(.40f, -.255f), new(.40f + skew, .255f), new(-.40f + skew, .255f) };
                Prism(node, polygon, .29f, row % 2 == 0 ? Concrete : "98998d", Fresh, "ConcreteShard");
                foreach (int face in new[] { -1, 1 })
                    Box(node, new(0, -.18f, face * .16f), new(.74f, .10f, .055f), "a0a193");
                int side = col < 2 ? -1 : 1;
                // End piles are tucked next to the posts; X extent remains outside +/-1.6.
                var end = new Vector3(side * (2.02f + .10f * (index % 2)), .08f + .045f * (row % 2),
                    (index % 2 == 0 ? -1 : 1) * (.34f + row * .20f));
                var spin = new Vector3(1.40f + .14f * (index % 3), .11f * ((index % 3) - 1), side * .12f);
                _fragments.Add(new(node, new(x, y, 0), end, spin, (index % 4) * .025f, .44f));
            }
        // Coping breaks into separate angular pieces instead of leaving a floating beam.
        for (int i = 0; i < 4; i++)
        {
            int side = i < 2 ? -1 : 1;
            var node = Group(this, "BrokenCoping");
            Prism(node, new[] { new Vector2(-.38f, -.07f), new(.38f, -.07f), new(.43f, .07f), new(-.36f, .07f) }, .36f, Cap, Fresh, "CopingShard");
            _fragments.Add(new(node, new(-1.2f + i * .8f, 2.43f, 0),
                new(side * 2.04f, .10f, i % 2 == 0 ? -.62f : .72f), new(1.12f, .24f, side * .21f), .015f * i, .48f));
        }
    }

    private void BuildGravel()
    {
        for (int i = 0; i < 24; i++)
        {
            // Only these low stones survive inside the walkable central gap (top <= 6 cm).
            float x = -1.48f + (i * .417f % 2.96f), z = -.48f + (i * .263f % .96f);
            float radius = .035f + (i % 3) * .012f;
            var stone = Prism(_rubble, new[] { new Vector2(-radius, -.018f), new(radius, -.014f), new(radius * .7f, .023f), new(-radius * .5f, .033f) },
                radius * 1.3f, i % 2 == 0 ? "8c8e81" : "aeaa95", Fresh, "SmallGravel");
            stone.Position = new(x, .022f, z);
            stone.Rotation = new(0, i * .73f, 0);
        }
    }

    public void SetProgress(float progress)
    {
        _playing = false;
        SetProcess(false);
        ApplyProgress(Mathf.Clamp(progress, 0, 1));
    }

    public void PlayBreak()
    {
        if (_playing || _progress >= 1) return;
        _playing = true;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        if (!_playing) return;
        ApplyProgress(Mathf.Min(1, _progress + (float)delta / Duration));
        if (_progress >= 1) { _playing = false; SetProcess(false); }
    }

    private void ApplyProgress(float progress)
    {
        _progress = progress;
        _intact.Visible = progress <= 0;
        _broken.Visible = progress > 0;
        _rubble.Visible = progress > .12f;
        for (int index = 0; index < _fragments.Count; index++)
        {
            var fragment = _fragments[index];
            fragment.Node.Visible = progress > 0;
            float t = Mathf.Clamp((progress - fragment.Delay) / (1 - fragment.Delay), 0, 1);
            float settle = t * t * (3 - 2 * t);
            var position = fragment.Start.Lerp(fragment.End, settle);
            position.Y += Mathf.Sin(t * Mathf.Pi) * .28f;
            float burst = Mathf.Sin(t * Mathf.Pi);
            position.Z += burst * (fragment.End.Z > 0 ? .38f : -.38f);
            fragment.Node.Position = position;
            // Alternating tumble avoids rigid columns of identical floating panel slats.
            var tumble = new Vector3(.35f * (index % 3 - 1), .45f * (index % 2 == 0 ? 1 : -1), .38f * (index % 5 - 2));
            fragment.Node.Rotation = fragment.Spin * settle + tumble * burst;
            fragment.Node.Scale = Vector3.One * Mathf.Lerp(1, fragment.Scale, settle);
        }
        for (int i = 0; i < _dust.Count; i++)
        {
            float t = Mathf.Clamp((progress - .08f - i * .012f) / .75f, 0, 1);
            float size = Mathf.Sin(t * Mathf.Pi) * (.18f + (i % 3) * .055f);
            _dust[i].Visible = size > .005f;
            _dust[i].Position = new(-1.3f + i * .325f, .24f + t * .42f, (i % 2 == 0 ? -1 : 1) * (.24f + t * .24f));
            _dust[i].Scale = new Vector3(1.5f, .7f, 1) * Mathf.Max(.001f, size);
        }
    }

    private static Node3D Group(Node parent, string name)
    {
        var node = new Node3D { Name = name }; parent.AddChild(node); return node;
    }

    private static void Box(Node parent, Vector3 at, Vector3 size, string color, bool bevel = true)
        => StageArt.Box(parent, at, size, color, bevel);

    private static void Rod(Node parent, Vector3 a, Vector3 b)
    {
        var rod = Mat.Mesh(parent, new CylinderMesh { TopRadius = .017f, BottomRadius = .017f, Height = a.DistanceTo(b), RadialSegments = 6 },
            (a + b) / 2, Mat.Solid(Steel, .8f), "ExposedRebar");
        rod.Quaternion = new Quaternion(Vector3.Up, (b - a).Normalized());
    }

    private static void CrackSegment(Node parent, Vector2 a, Vector2 b, float width, float z)
    {
        Vector2 n = (b - a).Orthogonal().Normalized() * width / 2;
        var segment = Prism(parent, new[] { a - n, b - n * .7f, b + n * .7f, a + n }, .005f, "484c43", "484c43", "BranchingCrack");
        segment.Position = new(0, 0, z);
        segment.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }

    /// <summary>Flat shaded polygon with bevel rings and warm, freshly fractured side faces.</summary>
    private static MeshInstance3D Prism(Node parent, Vector2[] polygon, float depth, string faceColor, string edgeColor, string name)
    {
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var center = Vector2.Zero;
        foreach (var p in polygon) center += p;
        center /= polygon.Length;
        var face = new Color(faceColor); var edge = new Color(edgeColor);
        Vector3 Point(int i, float z, bool inset) { var p = inset ? polygon[i].Lerp(center, .035f) : polygon[i]; return new(p.X, p.Y, z); }
        void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal, Color color)
        {
            if ((b - a).Cross(c - a).Dot(normal) > 0) (b, c) = (c, b);
            foreach (var p in new[] { a, b, c }) { surface.SetNormal(normal); surface.SetColor(color); surface.AddVertex(p); }
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Color color)
        { Triangle(a, b, c, normal, color); Triangle(a, c, d, normal, color); }
        var indices = Geometry2D.TriangulatePolygon(polygon);
        foreach (int sign in new[] { -1, 1 })
            for (int i = 0; i < indices.Length; i += 3)
                Triangle(Point(indices[i], sign * depth / 2, true), Point(indices[i + 1], sign * depth / 2, true),
                    Point(indices[i + 2], sign * depth / 2, true), new Vector3(0, 0, sign), face);
        bool clockwise = Geometry2D.IsPolygonClockwise(polygon);
        for (int i = 0; i < polygon.Length; i++)
        {
            int j = (i + 1) % polygon.Length;
            var direction = polygon[j] - polygon[i];
            var outward = new Vector3(direction.Y, -direction.X, 0).Normalized() * (clockwise ? -1 : 1);
            Quad(Point(i, -depth * .35f, false), Point(j, -depth * .35f, false), Point(j, depth * .35f, false), Point(i, depth * .35f, false), outward, edge);
            foreach (int sign in new[] { -1, 1 })
                Quad(Point(i, sign * depth * .35f, false), Point(j, sign * depth * .35f, false), Point(j, sign * depth / 2, true), Point(i, sign * depth / 2, true),
                    (outward + new Vector3(0, 0, sign)).Normalized(), face.Lerp(edge, .4f));
        }
        surface.SetMaterial(StoneMaterial);
        var result = new MeshInstance3D { Name = name, Mesh = surface.Commit() };
        parent.AddChild(result);
        return result;
    }
}
