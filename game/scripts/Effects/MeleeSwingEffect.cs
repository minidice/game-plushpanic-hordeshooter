using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>A tapered, hollow arc at the impact marker. It never performs damage or changes the melee pose.</summary>
public partial class MeleeSwingEffect : Node3D
{
    private static readonly Dictionary<float, ArrayMesh> Meshes = new();
    private static readonly Shader SweepShader = GD.Load<Shader>("res://shaders/melee_sweep.gdshader");
    private ShaderMaterial _material = null!;
    private float _age;

    public static void Play(Node3D parent, Vector3 at, Vector3 direction, float range, float arcDegrees)
    {
        var effect = new MeleeSwingEffect { Name = "MeleeSwing" };
        parent.AddChild(effect);
        effect.GlobalPosition = at;
        effect.Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
        effect.Scale = Vector3.One * range;
        effect._material = new ShaderMaterial { Shader = SweepShader };
        effect.AddChild(new MeshInstance3D
        {
            Name = "MeleeSweep", Mesh = Arc(arcDegrees), MaterialOverride = effect._material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    private static ArrayMesh Arc(float degrees)
    {
        if (Meshes.TryGetValue(degrees, out var cached)) return cached;
        const int steps = 48;
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        float half = Mathf.DegToRad(degrees) * .5f;
        void Vertex(float t, bool outer)
        {
            float angle = Mathf.Lerp(half, -half, t);
            float width = .17f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.Pi * t)), .7f);
            float radius = outer ? 1 : 1 - width;
            vertices.Add(new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius);
            uvs.Add(new Vector2(t, outer ? 1 : 0));
        }
        for (int i = 0; i < steps; i++)
        {
            float a = (float)i / steps, b = (float)(i + 1) / steps;
            Vertex(a, false); Vertex(a, true); Vertex(b, true);
            Vertex(a, false); Vertex(b, true); Vertex(b, false);
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uvs.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Meshes[degrees] = mesh;
        return mesh;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= .16f) { QueueFree(); return; }
        _material.SetShaderParameter("age", _age);
    }
}
