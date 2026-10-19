using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Original warehouse art. Visuals only: collision and navigation belong to StageOneLayout.</summary>
internal static class StageArt
{
    private static readonly Dictionary<(Vector3, bool), Mesh> Meshes = new();
    private static readonly Dictionary<string, ShaderMaterial> Floors = new();
    private static readonly Dictionary<bool, ShaderMaterial> Walls = new();
    private const string Steel = "3b5360";
    private const string Edge = "79939b";
    private const string Dark = "172733";
    private const string Amber = "dbad59";

    public static MeshInstance3D Box(Node parent, Vector3 at, Vector3 size, string color, bool bevel = false)
        => Shape(parent, at, size, Mat.Solid(color, .67f), bevel);

    private static MeshInstance3D Shape(Node parent, Vector3 at, Vector3 size, Material material, bool bevel = false)
    {
        var key = (size, bevel);
        if (!Meshes.TryGetValue(key, out var mesh))
        {
            mesh = bevel ? BeveledBox(size) : new BoxMesh { Size = size };
            Meshes.Add(key, mesh);
        }
        return Mat.Mesh(parent, mesh, at, material);
    }

    // Four eight-sided rings give every exposed edge a highlight, with only 60 triangles.
    private static ArrayMesh BeveledBox(Vector3 size)
    {
        float b = Mathf.Min(.085f, Mathf.Min(size.X, Mathf.Min(size.Y, size.Z)) * .12f);
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3[] Ring(float x, float z, float y, float cut) => new[]
        {
            new Vector3(-x + cut, y, -z), new Vector3(x - cut, y, -z),
            new Vector3(x, y, -z + cut), new Vector3(x, y, z - cut),
            new Vector3(x - cut, y, z), new Vector3(-x + cut, y, z),
            new Vector3(-x, y, z - cut), new Vector3(-x, y, -z + cut),
        };
        var half = size * .5f;
        var rings = new[]
        {
            Ring(half.X - b, half.Z - b, -half.Y, b * .4f),
            Ring(half.X, half.Z, -half.Y + b, b),
            Ring(half.X, half.Z, half.Y - b, b),
            Ring(half.X - b, half.Z - b, half.Y, b * .4f),
        };
        void Triangle(Vector3 a, Vector3 c, Vector3 d)
        {
            var normal = (c - a).Cross(d - a).Normalized();
            if (normal.Dot((a + c + d) / 3) < 0) normal = -normal;
            // Godot front faces use clockwise winding, independent of the vertex normal.
            if ((c - a).Cross(d - a).Dot(normal) > 0) (c, d) = (d, c);
            surface.SetNormal(normal); surface.AddVertex(a);
            surface.SetNormal(normal); surface.AddVertex(c);
            surface.SetNormal(normal); surface.AddVertex(d);
        }
        for (int r = 0; r < 3; r++)
            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;
                Triangle(rings[r][i], rings[r][next], rings[r + 1][next]);
                Triangle(rings[r][i], rings[r + 1][next], rings[r + 1][i]);
            }
        for (int i = 1; i < 7; i++)
        {
            Triangle(rings[0][0], rings[0][i], rings[0][i + 1]);
            Triangle(rings[3][0], rings[3][i], rings[3][i + 1]);
        }
        return surface.Commit();
    }

    public static void Floor(Node3D parent, Vector3 size, bool interior)
    {
        string color = interior ? "344655" : "34414a";
        if (!Floors.TryGetValue(color, out var material))
        {
            material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/environment/factory_floor.gdshader") };
            material.SetShaderParameter("tile_color", new Color(color));
            material.SetShaderParameter("tile_size", interior ? 1f : 1.5f);
            Floors.Add(color, material);
        }
        Shape(parent, Vector3.Zero, size, material);
    }

    public static void Wall(Node3D parent, Vector3 size)
    {
        bool alongX = size.X > size.Z;
        if (!Walls.TryGetValue(alongX, out var material))
        {
            material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/environment/factory_wall.gdshader") };
            material.SetShaderParameter("along_x", alongX);
            Walls.Add(alongX, material);
        }
        // The separate cap owns the upper surface; the wall never shares its top plane.
        Shape(parent, new(0, -.035f, 0), size with { Y = size.Y - .07f }, material);
        Vector3 strip = alongX ? new(size.X, .12f, size.Z + .035f) : new(size.X + .035f, .12f, size.Z);
        Box(parent, new(0, -size.Y / 2 + .07f, 0), strip, Dark);
        Box(parent, new(0, size.Y / 2 - .045f, 0), strip with { Y = .09f }, Edge);
        if (size.Y > 1.3f)
            Box(parent, new(0, 1.18f - size.Y / 2, 0), strip with { Y = .045f }, "7d9996");
    }

    public static void Cargo(Node3D parent, Vector3 size, string color)
    {
        if (size.Y > 2f) { Container(parent, size, color); return; }
        if (size.X > 2 && size.Z < 1.2f) { Workbench(parent, size); return; }
        Box(parent, new(0, -.055f, 0), size with { Y = size.Y - .11f }, color, true);
        float z = size.Z / 2;
        Box(parent, new(0, size.Y / 2 - .055f, 0), new(size.X * .94f, .11f, size.Z * .94f), "8b9b98", true);
        foreach (float x in new[] { -.32f, .32f })
        {
            Box(parent, new(size.X * x, 0, 0), new(.065f, size.Y * .94f, size.Z + .012f), Dark);
            Box(parent, new(size.X * x, size.Y / 2 + .008f, 0), new(.065f, .02f, size.Z * .94f), Dark);
        }
        Box(parent, new(0, .04f, z + .008f), new(Mathf.Min(.48f, size.X * .38f), .18f, .018f), Amber);
        Box(parent, new(0, -.08f, z + .02f), new(.18f, .06f, .025f), Dark);
        foreach (float x in new[] { -.4f, .4f })
            Box(parent, new(size.X * x, -size.Y / 2 + .05f, 0), new(.12f, .1f, size.Z * .9f), Dark);
    }

    private static void Container(Node3D parent, Vector3 size, string color)
    {
        Box(parent, new(0, -.04f, 0), size with { Y = size.Y - .08f }, color, true);
        for (float x = -size.X / 2 + .25f; x < size.X / 2; x += .32f)
            foreach (int side in new[] { -1, 1 })
                Box(parent, new(x, 0, side * (size.Z / 2 - .012f)), new(.075f, size.Y - .2f, .055f), color);
        foreach (float y in new[] { -size.Y / 2 + .08f, size.Y / 2 - .08f })
            Box(parent, new(0, y, 0), new(size.X, .13f, size.Z + .025f), Steel);
        foreach (float x in new[] { -size.X / 2 + .08f, size.X / 2 - .08f })
            Box(parent, new(x, 0, 0), new(.13f, size.Y, size.Z + .025f), Steel);
        Box(parent, new(size.X * .28f, .1f, size.Z / 2 + .025f), new(.72f, .32f, .02f), "d4c79e");
        Box(parent, new(size.X * .28f, .1f, size.Z / 2 + .04f), new(.48f, .055f, .02f), Dark);
    }

    private static void Workbench(Node3D parent, Vector3 size)
    {
        Box(parent, new(0, size.Y / 2 - .07f, 0), new(size.X, .14f, size.Z), Edge, true);
        Box(parent, new(0, -.05f, 0), new(size.X - .12f, size.Y - .22f, size.Z - .1f), Steel, true);
        for (float x = -size.X / 2 + .4f; x < size.X / 2 - .1f; x += .72f)
        {
            Box(parent, new(x, -.04f, size.Z / 2 - .04f), new(.65f, size.Y - .31f, .045f), "536e78", true);
            Box(parent, new(x, .12f, size.Z / 2), new(.2f, .035f, .06f), "bbc5bd");
        }
        Box(parent, new(0, -size.Y / 2 + .05f, 0), new(size.X - .2f, .1f, size.Z - .12f), Dark);
    }

    public static void Carton(Node3D parent, Vector3 at, Vector3 size, string color = "af885b")
    {
        Box(parent, at, size, color, true);
        Box(parent, at + new Vector3(0, size.Y / 2 + .003f, 0), new(.12f, .012f, size.Z * .96f), "c7b08a");
        Box(parent, at + new Vector3(size.X * .16f, .03f, size.Z / 2 + .003f), new(size.X * .34f, size.Y * .26f, .012f), "ddcfac");
        Box(parent, at + new Vector3(size.X * .16f, .03f, size.Z / 2 + .011f), new(size.X * .2f, .025f, .012f), "665a4b");
    }

    public static void Shelf(Node3D parent, float length, string[] cartonColors)
    {
        // Entire rack stays inside the original 2.2 m high collision envelope.
        foreach (float y in new[] { -.92f, -.07f })
        {
            Box(parent, new(0, y, 0), new(length, .09f, 1.14f), Steel);
            foreach (float z in new[] { -.55f, .55f })
                Box(parent, new(0, y - .07f, z), new(length, .12f, .055f), Amber);
        }
        // Open upper bracing exposes the varied stock instead of a continuous empty roof.
        foreach (float z in new[] { -.55f, .55f })
            Box(parent, new(0, .9f, z), new(length, .085f, .055f), Steel);
        for (float x = -length / 2 + .08f; x < length / 2; x += 2f)
            foreach (float z in new[] { -.51f, .51f })
                Box(parent, new(x, 0, z), new(.105f, 2.2f, .105f), Edge);
        foreach (float z in new[] { -.51f, .51f })
            Box(parent, new(length / 2 - .08f, 0, z), new(.105f, 2.2f, .105f), Edge);
        int i = 0;
        for (float x = -length / 2 + .8f; x < length / 2 - .4f; x += 1.6f)
        {
            string color = cartonColors[i++];
            Carton(parent, new(x, -.58f, .03f), new(1.08f, .58f, .85f), color);
            float height = i % 3 == 0 ? .78f : .51f;
            Carton(parent, new(x - .13f, -.025f + height / 2, -.05f), new(.78f, height, .76f), color);
            if (i % 3 == 1)
                Carton(parent, new(x + .03f, .67f, -.03f), new(.58f, .36f, .58f), "b99b72");
            else if (i % 3 == 2)
                Carton(parent, new(x + .52f, .16f, .11f), new(.33f, .37f, .48f), "727f73");
        }
    }

    public static void Ammo(Node3D parent)
    {
        Cargo(parent, new(1f, .38f, .6f), "44645c");
        foreach (float x in new[] { -.32f, -.12f, .08f, .28f })
            Box(parent, new(x, .24f, -.02f), new(.12f, .08f, .3f), Amber, true);
        Shape(parent, new(0, .21f, .27f), new(.38f, .025f, .025f), Mat.Glow(new Color("d8bb76"), .75f));
    }

    public static void Fixture(Node3D parent, string color)
    {
        // Local -Z is the wall. The short stand-off and mounting plate visibly anchor the fitting.
        Box(parent, new(0, -.025f, -.27f), new(.36f, .42f, .055f), Steel, true);
        Box(parent, new(0, .025f, -.16f), new(.14f, .12f, .22f), Dark);
        Box(parent, new(0, .025f, 0), new(.85f, .16f, .32f), Dark, true);
        Shape(parent, new(0, -.055f, .055f), new(.68f, .075f, .22f), Mat.Glow(new Color(color), 2f), true);
        Shape(parent, new(0, .11f, .035f), new(.65f, .035f, .19f), Mat.Glow(new Color(color), 1.8f), true);
        foreach (float x in new[] { -.3f, .3f })
            Box(parent, new(x, -.09f, .04f), new(.035f, .02f, .25f), Steel);
    }

    public static void SafeRoomDetails(Node3D stage)
    {
        // Shallow wall-mounted equipment stays clear of the original pickup and travel lanes.
        for (int i = 0; i < 3; i++)
        {
            var at = new Vector3(5.6f + i * .72f, 1.45f, -3.9f);
            Box(stage, at, new(.66f, 1.9f, .18f), "4c6670", true);
            Box(stage, at + new Vector3(0, 0, .105f), new(.55f, 1.73f, .035f), "6a8188", true);
            Box(stage, at + new Vector3(.18f, -.1f, .135f), new(.045f, .19f, .04f), Amber);
            for (int slit = 0; slit < 3; slit++)
                Box(stage, at + new Vector3(0, .52f - slit * .085f, .13f), new(.32f, .025f, .016f), Dark);
            Box(stage, at + new Vector3(0, .18f, .13f), new(.22f, .105f, .018f), "d4c79e");
        }
        // Pegboard above the existing workbench, with tool silhouettes and warm task lighting.
        Box(stage, new(2.9f, 1.8f, -3.93f), new(2.65f, 1.3f, .12f), Dark, true);
        Box(stage, new(2.9f, 1.8f, -3.85f), new(2.48f, 1.15f, .035f), "596966");
        for (int i = 0; i < 6; i++)
        {
            Box(stage, new(1.95f + i * .36f, 1.76f, -3.8f), new(.055f, .44f - (i % 2) * .13f, .055f), Edge);
            Box(stage, new(1.95f + i * .36f, 1.98f, -3.79f), new(.14f, .08f, .05f), Dark);
        }
        Shape(stage, new(2.9f, 2.45f, -3.72f), new(2.3f, .045f, .1f), Mat.Glow(new Color("ffe2a1"), 1.2f));
        // Compact terminal is mounted into the left wall rather than occupying walkable floor.
        var terminal = new Node3D { Position = new(.09f, 1.45f, -1.5f), RotationDegrees = new(0, 90, 0) };
        stage.AddChild(terminal);
        Box(terminal, Vector3.Zero, new(.85f, 1.35f, .2f), Steel, true);
        Box(terminal, new(0, .18f, .115f), new(.66f, .65f, .045f), Dark, true);
        Shape(terminal, new(0, .18f, .14f), new(.52f, .48f, .02f), Mat.Glow(new Color("529b9c"), .4f));
        for (int i = 0; i < 3; i++)
            Shape(terminal, new(-.11f, .31f - i * .12f, .155f), new(.23f + i * .045f, .028f, .012f), Mat.Glow(new Color("b5dfc7"), .6f));
        Box(terminal, new(0, -.36f, .15f), new(.55f, .18f, .12f), Dark, true);
        Box(terminal, new(.23f, -.35f, .22f), new(.08f, .08f, .02f), Amber);
        Vent(stage, new(8.7f, 2.5f, -3.94f));
        // Door threshold chevrons stop before the interactable and do not add collision.
        for (int i = 0; i < 6; i++)
        {
            var stripe = Box(stage, new(8.95f, .013f, -.95f + i * .38f), new(.55f, .016f, .12f), Amber);
            stripe.RotationDegrees = new(0, -35, 0);
        }
        Box(stage, new(8.55f, .013f, 0), new(.035f, .018f, 2.5f), "869b9b");
    }

    private static void Vent(Node3D stage, Vector3 at)
    {
        Box(stage, at, new(.8f, .65f, .14f), Edge, true);
        Box(stage, at + new Vector3(0, 0, .08f), new(.65f, .5f, .025f), Dark);
        for (int i = 0; i < 5; i++)
            Box(stage, at + new Vector3(0, -.19f + i * .09f, .11f), new(.62f, .035f, .08f), Steel);
    }
}
