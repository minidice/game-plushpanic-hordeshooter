using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// What a painted ground shape is made of (a channel of the blend map). Grass (no shape), dirt and the gravel shoulder
/// are natural ground; asphalt is artificial (<see cref="GroundPainter.IsArtificial"/>), and any later made floor —
/// concrete, paving — joins it there, so loose stones and grass never land on it.
/// </summary>
public enum GroundKind { Dirt, Asphalt, Gravel }

/// <summary>
/// The outdoor ground as data (decisions.md A-24): straight strips and discs of dirt, asphalt and gravel on grass. It
/// bakes them into a small blend map (4 px per metre, R dirt · G asphalt · B gravel) with noisy, soft edges, which
/// <c>shaders/ground_blend.gdshader</c> turns into colour on one flat ground; and it lists points along each shape's
/// edge for the little tufts and pebbles that break the line. Seeded, so every PC paints the same ground.
/// </summary>
public sealed class GroundPainter
{
    public const int PixelsPerMetre = 4;

    private abstract record Shape(GroundKind Kind, float Soft, float Fray)
    {
        /// <summary>Signed distance from the shape's edge (negative inside).</summary>
        public abstract float Distance(Vector2 p);
    }

    private sealed record StripShape(GroundKind Kind, Vector2 A, Vector2 B, float HalfWidth, float Soft, float Fray) : Shape(Kind, Soft, Fray)
    {
        public override float Distance(Vector2 p)
        {
            var ab = B - A;
            float t = Mathf.Clamp((p - A).Dot(ab) / Mathf.Max(ab.LengthSquared(), .0001f), 0, 1);
            return (p - (A + ab * t)).Length() - HalfWidth;
        }
    }

    private sealed record DiscShape(GroundKind Kind, Vector2 Center, float Radius, float Soft, float Fray) : Shape(Kind, Soft, Fray)
    {
        public override float Distance(Vector2 p) => (p - Center).Length() - Radius;
    }

    public Rect2 Area { get; }
    private readonly List<Shape> _shapes = new();
    private readonly FastNoiseLite _noise;
    private readonly FastNoiseLite _patches;
    private readonly RandomNumberGenerator _rng;
    /// <summary>The straight dashed centre line the shader paints (x0, x1, z, half width), if any.</summary>
    public Vector4 Lane { get; private set; }

    public GroundPainter(Rect2 area, int seed)
    {
        Area = area;
        _noise = new FastNoiseLite { Seed = seed, Frequency = .35f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        _patches = new FastNoiseLite { Seed = seed + 1, Frequency = .11f, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex };
        _rng = new RandomNumberGenerator { Seed = (ulong)seed };
    }

    /// <summary>
    /// A straight band from <paramref name="a"/> to <paramref name="b"/> (x, z). <paramref name="soft"/> is how wide
    /// the edge blends, <paramref name="fray"/> how far it wanders (metres).
    /// </summary>
    public void Strip(GroundKind kind, Vector2 a, Vector2 b, float halfWidth, float soft = .6f, float fray = .7f) =>
        _shapes.Add(new StripShape(kind, a, b, halfWidth, soft, fray));

    public void Disc(GroundKind kind, Vector2 center, float radius, float soft = .8f, float fray = 1.1f) =>
        _shapes.Add(new DiscShape(kind, center, radius, soft, fray));

    /// <summary>A straight road with a gravel shoulder and a dashed centre line.</summary>
    public void Road(Vector2 a, Vector2 b, float halfWidth)
    {
        Strip(GroundKind.Gravel, a, b, halfWidth + .7f, .35f, .7f);
        Strip(GroundKind.Asphalt, a, b, halfWidth, .08f, .06f);
        if (Mathf.IsEqualApprox(a.Y, b.Y)) Lane = new Vector4(Mathf.Min(a.X, b.X), Mathf.Max(a.X, b.X), a.Y, .07f);
    }

    /// <summary>How much of <paramref name="kind"/> lies at <paramref name="p"/> (0–1), edges frayed by noise.</summary>
    public float Weight(GroundKind kind, Vector2 p) => Weight(kind, p, null);

    private float Weight(GroundKind kind, Vector2 p, Shape? except)
    {
        float best = 0;
        foreach (var s in _shapes)
        {
            if (s.Kind != kind || s == except) continue;
            float d = s.Distance(p) + _noise.GetNoise2D(p.X, p.Y) * s.Fray;
            best = Mathf.Max(best, 1 - Mathf.SmoothStep(-s.Soft, s.Soft, d));
            if (best >= 1) break;
        }
        return best;
    }

    public static bool IsArtificial(GroundKind kind) => kind == GroundKind.Asphalt;

    /// <summary>How much made floor (asphalt, later concrete) lies at <paramref name="p"/> (0–1).</summary>
    public float Artificial(Vector2 p)
    {
        float best = 0;
        foreach (var kind in System.Enum.GetValues<GroundKind>())
            if (IsArtificial(kind)) best = Mathf.Max(best, Weight(kind, p));
        return best;
    }

    /// <summary>Slow 0–1 noise (about 9 m across) so scattered grass gathers in patches instead of an even dusting.</summary>
    public float Patchiness(Vector2 p) => _patches.GetNoise2D(p.X, p.Y) * .5f + .5f;

    /// <summary>The blend map over <see cref="Area"/> (R dirt, G asphalt, B gravel).</summary>
    public ImageTexture Bake()
    {
        int w = Mathf.CeilToInt(Area.Size.X * PixelsPerMetre), h = Mathf.CeilToInt(Area.Size.Y * PixelsPerMetre);
        var image = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = Area.Position + new Vector2((x + .5f) / PixelsPerMetre, (y + .5f) / PixelsPerMetre);
                image.SetPixel(x, y, new Color(Weight(GroundKind.Dirt, p), Weight(GroundKind.Asphalt, p), Weight(GroundKind.Gravel, p), 1));
            }
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Points along every dirt and gravel edge (x, z, which kind of edge), about <paramref name="step"/> metres apart,
    /// for the border tufts and pebbles. Points that fall inside another shape of the same kind are dropped.
    /// </summary>
    public IEnumerable<(Vector2 At, GroundKind Kind, float Along)> EdgePoints(float step = .9f)
    {
        foreach (var s in _shapes)
        {
            if (s.Kind == GroundKind.Asphalt) continue;
            foreach (var (p, along) in Outline(s, step))
            {
                if (Weight(s.Kind, p, s) > .75f) continue;
                yield return (p, s.Kind, along);
            }
        }
    }

    // Tufts and pebbles straddle a dirt edge; gravel bits sit on the shoulder's outer half, not out on the grass.
    private float Jitter(GroundKind kind) => kind == GroundKind.Gravel ? _rng.RandfRange(-.55f, .05f) : _rng.RandfRange(-.25f, .45f);

    private IEnumerable<(Vector2 Point, float Along)> Outline(Shape s, float step)
    {
        switch (s)
        {
            case StripShape st:
            {
                var dir = (st.B - st.A).Normalized();
                var side = new Vector2(-dir.Y, dir.X);
                float length = (st.B - st.A).Length();
                float angle = Mathf.Atan2(dir.X, dir.Y);
                for (float t = _rng.RandfRange(0, step); t < length; t += step * _rng.RandfRange(.7f, 1.3f))
                    foreach (int sign in new[] { -1, 1 })
                    {
                        float off = st.HalfWidth + _noise.GetNoise2D(st.A.X + dir.X * t, st.A.Y + dir.Y * t) * st.Fray + Jitter(st.Kind);
                        yield return (st.A + dir * t + side * sign * off, angle);
                    }
                break;
            }
            case DiscShape d:
            {
                float around = Mathf.Tau * d.Radius;
                for (float t = _rng.RandfRange(0, step); t < around; t += step * _rng.RandfRange(.7f, 1.3f))
                {
                    float a = t / d.Radius;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float r = d.Radius + _noise.GetNoise2D(d.Center.X + dir.X * d.Radius, d.Center.Y + dir.Y * d.Radius) * d.Fray + Jitter(d.Kind);
                    yield return (d.Center + dir * r, a);
                }
                break;
            }
        }
    }
}
