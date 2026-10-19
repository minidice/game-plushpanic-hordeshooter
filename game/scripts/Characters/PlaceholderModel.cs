using Godot;

namespace HordeShooter;

/// <summary>
/// Builds a primitive stand-in model exposing the same part names a real model would
/// (Body, Deform, Head, LeftFoot, RightFoot, Weapon, Muzzle). Everything faces +Z.
/// </summary>
public static class PlaceholderModel
{
    public const string PlaceholderWeaponNode = "WeaponMesh";

    public static Node3D Build(CharacterDefinition def)
    {
        var root = new Node3D { Name = "Placeholder" };
        var body = new Node3D { Name = "Body" };
        var deform = new Node3D { Name = "Deform" };
        root.AddChild(body);
        body.AddChild(deform);

        var main = Mat.Solid(def.PrimaryColor);
        var detail = Mat.Solid(def.SecondaryColor, .6f);
        float r = def.ColliderRadius, h = def.ColliderHeight;
        bool armed = def.Role == CharacterRole.Player;

        Node3D head;
        switch (def.Shape)
        {
            case PlaceholderShape.Round:
                Mat.Mesh(deform, new SphereMesh { Radius = r * 1.05f, Height = h * .9f }, new Vector3(0, h * .45f, 0), main);
                head = Part(deform, "Head", new Vector3(0, h * .62f, r * .25f));
                Ears(head, r, main, detail);
                Face(head, r * .9f, detail);
                break;
            case PlaceholderShape.Balloon:
                Mat.Mesh(deform, new CapsuleMesh { Radius = r * .72f, Height = h * .63f }, new Vector3(0, h * .35f, 0), main);
                head = Part(deform, "Head", new Vector3(0, h * .68f, 0));
                Mat.Mesh(head, new SphereMesh { Radius = r * 1.12f, Height = h * .62f }, Vector3.Zero, Mat.Solid(def.PrimaryColor.Lightened(.2f), .45f));
                ComicEyes(head, r * 1.12f, r * 1.03f);
                Mat.Mesh(head, new SphereMesh { Radius = r * .20f, Height = r * .32f }, new Vector3(0, -r * .40f, r * 1.06f), detail);
                Mat.Mesh(head, new SphereMesh { Radius = r * .12f, Height = r * .21f }, new Vector3(-r * .58f, r * .55f, r * .81f), Mat.Solid("fff2e4"));
                Arms(deform, r, h * .8f, main);
                break;
            case PlaceholderShape.Bull:
                Mat.Mesh(deform, new SphereMesh { Radius = r, Height = h * .72f }, new Vector3(0, h * .43f, -r * .1f), main);
                head = Part(deform, "Head", new Vector3(0, h * .63f, r * .60f));
                var bullFace = Mat.Mesh(head, new SphereMesh { Radius = r * .90f, Height = r * 1.5f }, Vector3.Zero, main);
                bullFace.Scale = new Vector3(1, 1, .75f);
                foreach (var side in new[] { -1f, 1f })
                {
                    var horn = Mat.Mesh(head, new CylinderMesh { TopRadius = .02f, BottomRadius = r * .14f, Height = r * .9f },
                        new Vector3(side * r * .8f, r * .45f, 0), Mat.Solid("f1e6c8", .4f));
                    horn.RotationDegrees = new Vector3(0, 0, -side * 65f);
                }
                var nose = Mat.Mesh(head, new SphereMesh { Radius = r * .58f, Height = r * .49f }, new Vector3(0, -r * .28f, r * .63f), Mat.Solid("dcb1a1"));
                nose.Scale = new Vector3(1, 1, .4f);
                ComicEyes(head, r * .85f, r * .64f);
                foreach (var side in new[] {-1f, 1f})
                    Mat.Mesh(head, new SphereMesh { Radius = r * .06f, Height = r * .09f }, new Vector3(side * r * .23f, -r * .25f, r * .86f), detail);
                break;
            default:
                Mat.Mesh(deform, new CapsuleMesh { Radius = r * .95f, Height = h * .78f }, new Vector3(0, h * .39f + .02f, 0), main);
                head = Part(deform, "Head", new Vector3(0, h * .8f, 0));
                Mat.Mesh(head, new SphereMesh { Radius = r * .82f, Height = r * 1.55f }, Vector3.Zero, main);
                Face(head, r * .78f, detail);
                if (!armed) Arms(deform, r, h, main);
                break;
        }

        foreach (var side in new[] { -1f, 1f })
        {
            var foot = Part(root, side < 0 ? "LeftFoot" : "RightFoot", new Vector3(side * r * .45f, .07f, .05f));
            Mat.Mesh(foot, new SphereMesh { Radius = r * .3f, Height = r * .28f }, Vector3.Zero, detail);
        }

        if (armed)
        {
            var weapon = Part(body, "Weapon", new Vector3(r * .55f, h * .42f, r * .55f));
            var gun = Part(weapon, PlaceholderWeaponNode, Vector3.Zero);
            Mat.Box(gun, new Vector3(0, 0, .25f), new Vector3(.12f, .16f, .75f), Mat.Solid("2f3330", .5f));
            Mat.Box(gun, new Vector3(0, -.12f, .1f), new Vector3(.08f, .2f, .12f), Mat.Solid("4a3f30"));
            Part(weapon, "Muzzle", new Vector3(0, 0, .65f));
        }
        return root;
    }

    private static Node3D Part(Node parent, string name, Vector3 position)
    {
        var node = new Node3D { Name = name, Position = position };
        parent.AddChild(node);
        return node;
    }

    private static void ComicEyes(Node3D head, float r, float z)
    {
        foreach(var side in new[]{-1f,1f})
        {
            var at=new Vector3(side*r*.38f,r*.20f,z);
            var eye=Mat.Mesh(head,new SphereMesh{Radius=r*.235f,Height=r*.52f},at,Mat.Solid("fff1cc"));
            eye.Scale=new Vector3(1,1,.35f);
            var pupil=Mat.Mesh(head,new SphereMesh{Radius=r*.085f,Height=r*.21f},at+new Vector3(-side*r*.03f,side*r*.035f,r*.08f),Mat.Solid("30383d"));
            pupil.Scale=new Vector3(1,1,.4f);
        }
    }

    private static void Face(Node3D head, float radius, Material detail)
    {
        // Button eyes and a stitched mouth on the +Z side, so facing reads from above.
        foreach (var side in new[] { -1f, 1f })
        {
            var eye = Mat.Mesh(head, new CylinderMesh { TopRadius = radius * .2f, BottomRadius = radius * .2f, Height = .04f },
                new Vector3(side * radius * .38f, radius * .2f, radius * .88f), detail);
            eye.RotationDegrees = new Vector3(90, 0, 0);
        }
        for (int i = -1; i <= 1; i++)
            Mat.Box(head, new Vector3(i * radius * .16f, -radius * .25f, radius * .92f), new Vector3(.025f, radius * .22f, .03f), detail);
    }

    private static void Ears(Node3D head, float r, Material main, Material detail)
    {
        foreach (var side in new[] { -1f, 1f })
        {
            Mat.Mesh(head, new SphereMesh { Radius = r * .28f, Height = r * .3f }, new Vector3(side * r * .6f, r * .55f, -r * .1f), main);
            Mat.Mesh(head, new SphereMesh { Radius = r * .16f, Height = r * .2f }, new Vector3(side * r * .6f, r * .56f, -r * .02f), detail);
        }
    }

    private static void Arms(Node3D deform, float r, float h, Material main)
    {
        foreach (var side in new[] { -1f, 1f })
        {
            var arm = Mat.Mesh(deform, new CapsuleMesh { Radius = r * .2f, Height = r * 1.5f }, new Vector3(side * r * .75f, h * .55f, r * .7f), main);
            arm.RotationDegrees = new Vector3(80, 0, 0);
        }
    }
}
