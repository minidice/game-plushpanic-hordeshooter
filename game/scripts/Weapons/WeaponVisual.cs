using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Builds a weapon look along +Z from the grip: <see cref="WeaponDefinition.Model"/> if set, otherwise
/// primitives shaped by category. Exposes muzzle points (two for dual pistols).
/// </summary>
public static class WeaponVisual
{
    public enum PoseFamily { Dedicated, Shouldered, Pistol, Dual }
    public sealed record Built(Node3D Root, List<Node3D> Muzzles, Node3D RightGrip, Node3D? LeftGrip, WeaponDefinition Definition)
    {
        public Node3D? StockContact { get; init; }
        public PoseFamily Pose { get; init; }
    }

    public static Built Build(WeaponDefinition def, Color? handColor)
    {
        var root = new Node3D { Name = "Weapon_" + def.Id };
        var muzzles = new List<Node3D>();
        Node3D? rightGrip = null, leftGrip = null;
        if (def.Dual)
        {
            var dualScene = def.LoadModel();
            foreach (var side in new[] { -1f, 1f })
            {
                var gun = new Node3D { Position = new Vector3(side * .2f, 0, -.05f) };
                root.AddChild(gun);
                if (dualScene is { } modelScene)
                {
                    var model = modelScene.Instantiate<Node3D>();
                    gun.AddChild(model);
                    muzzles.Add(model.FindChild("Muzzle", true, false) as Node3D ?? Point(gun, new Vector3(0, 0, def.Length)));
                }
                else muzzles.Add(Gun(gun, def));
                var grip = Grip(gun, side > 0 ? "GripRight" : "GripLeft", new Vector3(side * .035f, -.30f * def.Length, .04f * def.Length), handColor, side);
                if(side > 0) rightGrip = grip; else leftGrip = grip;
            }
            return new Built(root, muzzles, rightGrip!, leftGrip, def){Pose=PoseFamily.Dual};
        }

        if (def.LoadModel() is { } scene)
        {
            var model = scene.Instantiate<Node3D>();
            root.AddChild(model);
            muzzles.Add(model.FindChild("Muzzle", true, false) as Node3D ?? Point(root, new Vector3(0, 0, def.Length)));
        }
        else muzzles.Add(Gun(root, def));

        // Match the authored handle/forend, in metres. A GLB may supply its own GripRight/GripLeft markers.
        float l=def.Length;
        var right=new Vector3(.035f,-.09f*l,0);
        Vector3? left=new Vector3(-.035f,-.07f*l,.52f*l);
        // Support the rear of the forend, leaving a bent support elbow with a shouldered stock.
        if(def.Category is WeaponCategory.Rifle or WeaponCategory.Smg)left=new Vector3(-.035f,-.07f*l,.42f*l);
        if(def.Category==WeaponCategory.Pistol){right=new Vector3(.035f,-.30f*l,.04f*l);left=new Vector3(-.065f,-.31f*l,.075f*l);}
        else if(def.Category==WeaponCategory.Melee){right=new Vector3(.025f,0,.08f);left=null;}
        else if(def.Mode==FireMode.Lob){right=new Vector3(.10f*l,.17f*l,.05f*l);left=new Vector3(-.10f*l,.30f*l,.28f*l);}
        else if(def.Category==WeaponCategory.Heavy){right=new Vector3(.035f,-.09f*l,.16f*l);left=new Vector3(-.055f,-.02f*l,.46f*l);}
        rightGrip=Grip(root,"GripRight",right,handColor,1);
        if(left is {} support)leftGrip=Grip(root,"GripLeft",support,handColor,-1);
        bool stocked=def.Category is WeaponCategory.Smg or WeaponCategory.Rifle or WeaponCategory.Shotgun or WeaponCategory.Sniper or WeaponCategory.Lmg;
        var contact=root.FindChild("StockContact",true,false) as Node3D;
        if(stocked&&contact==null) {
            // Procedural stock rear face. Imported models should author their own marker.
            contact=new Node3D{Name="StockContact",Position=new Vector3(0,-.03f,-.25f)};root.AddChild(contact);
        }
        return new Built(root, muzzles, rightGrip, leftGrip, def){StockContact=contact,
            Pose=stocked?PoseFamily.Shouldered:def.Category==WeaponCategory.Pistol?PoseFamily.Pistol:PoseFamily.Dedicated};
    }

    private static Node3D Gun(Node3D root, WeaponDefinition def)
    {
        var body = Mat.Solid(def.BodyColor, .5f);
        var accent = Mat.Solid(def.AccentColor, .6f);
        float l = def.Length, t = def.Thickness;
        switch (def.Category)
        {
            case WeaponCategory.Melee:
                // Frying pan: handle + pan. The classic.
                Mat.Box(root, new Vector3(0, 0, l * .3f), new Vector3(t * .6f, t * .6f, l * .6f), accent);
                var pan = Mat.Mesh(root, new CylinderMesh { TopRadius = .2f, BottomRadius = .17f, Height = .06f }, new Vector3(0, 0, l * .6f + .18f), body);
                pan.RotationDegrees = new Vector3(90, 0, 0);
                return Point(root, new Vector3(0, 0, l + .3f));
            case WeaponCategory.Pistol:
                Mat.Box(root, new Vector3(0, .02f, l * .45f), new Vector3(t * .8f, t, l), body);
                Mat.Box(root, new Vector3(0, -.08f, .02f), new Vector3(t * .7f, .16f, t), accent);
                return Point(root, new Vector3(0, .03f, l * .95f));
            case WeaponCategory.Heavy when def.Mode == FireMode.Lob:
                var tube = Mat.Mesh(root, new CylinderMesh { TopRadius = t * .5f, BottomRadius = t * .55f, Height = l }, new Vector3(0, l * .35f, l * .25f), body);
                tube.RotationDegrees = new Vector3(40, 0, 0);
                Mat.Box(root, new Vector3(0, -.1f, 0), new Vector3(.35f, .04f, .35f), accent);
                return Point(root, new Vector3(0, l * .7f, l * .55f));
            case WeaponCategory.Heavy:
                var launcher = Mat.Mesh(root, new CylinderMesh { TopRadius = t * .5f, BottomRadius = t * .5f, Height = l }, new Vector3(0, .04f, l * .4f), body);
                launcher.RotationDegrees = new Vector3(90, 0, 0);
                var warhead = Mat.Mesh(root, new CylinderMesh { TopRadius = 0, BottomRadius = t * .45f, Height = .22f }, new Vector3(0, .04f, l * .9f + .1f), accent);
                warhead.RotationDegrees = new Vector3(90, 0, 0);
                Mat.Box(root, new Vector3(0, -.1f, l * .25f), new Vector3(.06f, .14f, .08f), accent);
                return Point(root, new Vector3(0, .04f, l * .95f));
        }

        // Long guns: receiver, barrel, stock, magazine, plus category details.
        Mat.Box(root, new Vector3(0, 0, l * .3f), new Vector3(t, t * 1.3f, l * .55f), body);
        var barrel = Mat.Mesh(root, new CylinderMesh { TopRadius = t * .22f, BottomRadius = t * .22f, Height = l * .5f }, new Vector3(0, .02f, l * .78f), body);
        barrel.RotationDegrees = new Vector3(90, 0, 0);
        Mat.Box(root, new Vector3(0, -.03f, -.12f), new Vector3(t * .8f, t * 1.2f, .26f), accent);
        switch (def.Category)
        {
            case WeaponCategory.Sniper:
                var scope = Mat.Mesh(root, new CylinderMesh { TopRadius = .045f, BottomRadius = .045f, Height = .32f }, new Vector3(0, t * .95f, l * .3f), Mat.Solid("15181a", .3f));
                scope.RotationDegrees = new Vector3(90, 0, 0);
                break;
            case WeaponCategory.Shotgun:
                Mat.Box(root, new Vector3(0, -t * .55f, l * .62f), new Vector3(t * .9f, t * .5f, l * .22f), accent);
                if (def.MagSize <= 2)
                {
                    var second = Mat.Mesh(root, new CylinderMesh { TopRadius = t * .22f, BottomRadius = t * .22f, Height = l * .5f }, new Vector3(t * .45f, .02f, l * .78f), body);
                    second.RotationDegrees = new Vector3(90, 0, 0);
                }
                break;
            default:
                bool drum = def.Category == WeaponCategory.Lmg;
                Mat.Box(root, new Vector3(0, -t * (drum ? 1.1f : .9f), l * .32f), drum ? new Vector3(t * 1.6f, t * 1.2f, t * 1.6f) : new Vector3(t * .6f, t * 1.4f, t * .9f), accent);
                break;
        }
        return Point(root, new Vector3(0, .02f, l * 1.03f));
    }

    private static Node3D Grip(Node3D parent,string name,Vector3 at,Color? color,float side)
    {
        var point=parent.FindChild(name,true,false) as Node3D;
        if(point==null){point=new Node3D{Name=name,Position=at};parent.AddChild(point);}
        if(color is {} c)
        {
            var palm=Mat.Mesh(point,new SphereMesh{Radius=.087f,Height=.16f},Vector3.Zero,Mat.Solid(c),"HandPalm");
            palm.Scale=new Vector3(.85f,1,.88f);
            Mat.Mesh(point,new SphereMesh{Radius=.042f,Height=.09f},new Vector3(-side*.06f,.04f,.025f),Mat.Solid(c),"HandThumb");
        }
        return point;
    }

    private static Node3D Point(Node3D parent, Vector3 at)
    {
        var node = new Node3D { Name = "Muzzle", Position = at };
        parent.AddChild(node);
        return node;
    }
}
