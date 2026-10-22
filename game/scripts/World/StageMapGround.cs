using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Painted outdoor ground (decisions.md A-24): instead of 4 m kit tiles whose edges cut paths square, the map lays a
/// few flat pieces with <c>shaders/ground_blend.gdshader</c> over a <see cref="GroundPainter"/>'s blend map, so dirt
/// and asphalt fray into the grass; then grass tufts and loose stones from the ground-bits set are instanced over all
/// natural ground (in patches, a little thicker along dirt edges) and gravel along road shoulders. Decoration only: no
/// colliders, no shadows, seeded — every PC sees the same, though nothing would break if they did not.
/// </summary>
public abstract partial class StageMap
{
    public const string GroundShader = "res://shaders/ground_blend.gdshader";
    /// <summary>The painter this map's ground was baked from (checks), null on maps that do not paint their ground.</summary>
    public GroundPainter? Ground { get; private set; }
    /// <summary>Ground bits placed by <see cref="GroundDressing"/>, by model id (checks, budgets).</summary>
    public Dictionary<string, int> GroundBits { get; } = new();
    /// <summary>Where those bits stand (x, z); kept here because a headless MultiMesh does not keep its transforms.</summary>
    public List<Vector2> GroundBitSpots { get; } = new();
    private readonly RandomNumberGenerator _bitRng = new() { Seed = 31 };

    /// <summary>Lays <paramref name="pieces"/> (x, z rectangles; leave holes for water and indoor floors) with the painted ground.</summary>
    protected void PaintGround(GroundPainter painter, IEnumerable<Rect2> pieces)
    {
        Ground = painter;
        var material = new ShaderMaterial { Shader = GD.Load<Shader>(GroundShader) };
        material.SetShaderParameter("blend", painter.Bake());
        material.SetShaderParameter("area", new Vector4(painter.Area.Position.X, painter.Area.Position.Y, painter.Area.Size.X, painter.Area.Size.Y));
        material.SetShaderParameter("lane", painter.Lane);
        int n = 0;
        foreach (var piece in pieces)
        {
            if (piece.Size.X <= 0 || piece.Size.Y <= 0) continue;
            _stage.AddChild(new MeshInstance3D
            {
                Name = $"PaintedGround{n++}",
                Mesh = new PlaneMesh { Size = piece.Size },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(piece.GetCenter().X, 0, piece.GetCenter().Y),
            });
        }
    }

    /// <summary>
    /// Ground bits over <paramref name="area"/> (x, z), skipping where <paramref name="skip"/> says and any artificial
    /// floor: grass tufts in patches over the grass and a few on dirt, loose single stones anywhere natural, now and
    /// then a small spread of pebbles; along dirt edges a few more tufts, along road shoulders gravel.
    /// </summary>
    protected void GroundDressing(GroundPainter painter, Rect2 area, Func<Vector2, bool>? skip = null)
    {
        var bits = new Dictionary<string, List<Transform3D>>();
        void Put(string id, Vector2 at, float yaw)
        {
            if (skip?.Invoke(at) == true || painter.Artificial(at) > .3f) return;
            if (!bits.TryGetValue(id, out var list)) bits[id] = list = new List<Transform3D>();
            list.Add(new Transform3D(new Basis(Vector3.Up, yaw).Scaled(Vector3.One * _bitRng.RandfRange(.8f, 1.25f)), new Vector3(at.X, 0, at.Y)));
            GroundBitSpots.Add(at);
        }
        float AnyYaw() => _bitRng.RandfRange(0, Mathf.Tau);

        // Field: one try per square metre, jittered inside its cell.
        for (float x = area.Position.X; x < area.End.X; x++)
            for (float z = area.Position.Y; z < area.End.Y; z++)
            {
                var at = new Vector2(x + _bitRng.Randf(), z + _bitRng.Randf());
                float bare = Mathf.Max(painter.Weight(GroundKind.Dirt, at), painter.Weight(GroundKind.Gravel, at));
                float patch = painter.Patchiness(at);
                float roll = _bitRng.Randf();
                float tuft = Mathf.Lerp(.03f + .26f * patch * patch, .012f, bare), clump = (1 - bare) * .035f * patch * patch;
                if ((roll -= tuft) < 0) Put("bit_grass_tuft", at, AnyYaw());
                else if ((roll -= clump) < 0) Put("bit_grass_clump", at, AnyYaw());
                else if ((roll -= .018f) < 0) Put("bit_stone", at, AnyYaw());
                else if ((roll -= .004f) < 0) Put("bit_pebbles", at, AnyYaw());
            }

        // Edges: a little more along dirt, gravel along shoulders (lying along the road).
        foreach (var (at, kind, along) in painter.EdgePoints(1.5f))
        {
            float roll = _bitRng.Randf();
            if (kind == GroundKind.Gravel)
            {
                if (roll < .5f) Put("bit_gravel", at, along + Mathf.Pi / 2 + _bitRng.RandfRange(-.2f, .2f));
                else if (roll < .6f) Put("bit_grass_tuft", at, AnyYaw());
            }
            else if (roll < .45f) Put("bit_grass_tuft", at, AnyYaw());
            else if (roll < .55f) Put("bit_grass_clump", at, AnyYaw());
            else if (roll < .62f) Put("bit_stone", at, AnyYaw());
        }

        foreach (var (id, list) in bits)
        {
            Scatter(id, list, shadows: false);
            GroundBits[id] = GroundBits.GetValueOrDefault(id) + list.Count;
        }
    }
}
