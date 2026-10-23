using Godot;

namespace HordeShooter;

/// <summary>Original maintenance/utility props. Geometry only; StageMap owns collision and flow.</summary>
internal static class WorkshopArt
{
    public enum Kind { Workbench, ToolChest, PartsShelf, Generator, CableReel, PipeRack, SafetyBoard }
    private const string Steel="4c6470", Edge="a4b0aa", Dark="263740", Teal="527e75", Wood="a28a63", Amber="c99b4e";
    public static Vector3 Size(Kind kind)=>kind switch
    {
        Kind.Workbench=>new(3,1.25f,1.2f), Kind.ToolChest=>new(1.1f,1.12f,1),
        Kind.Generator=>new(2.4f,1.5f,1.45f), Kind.CableReel=>new(1.05f,1.05f,.95f),
        Kind.PipeRack=>new(2.8f,1.5f,1.35f), Kind.PartsShelf=>new(2.4f,.7f,.4f),
        _=>new(.9f,1.1f,.12f)
    };
    private static MeshInstance3D Box(Node p,Vector3 at,Vector3 size,string color,bool bevel=true)
        =>StageArt.Box(p,at,size,color,bevel);
    private static MeshInstance3D Cylinder(Node p,Vector3 at,float radius,float length,string color,Vector3 rotation=default)
    {
        var mesh=new CylinderMesh { TopRadius=radius,BottomRadius=radius,Height=length,RadialSegments=16,Rings=0 };
        var node=Mat.Mesh(p,mesh,at,Mat.Solid(color,.67f));node.RotationDegrees=rotation;return node;
    }
    private static void Bar(Node p,Vector3 a,Vector3 b,float width,string color)
    {
        var bar=Box(p,(a+b)/2,new Vector3(width,a.DistanceTo(b),width),color,false);
        bar.Quaternion=new Quaternion(Vector3.Up,(b-a).Normalized());
    }
    public static Node3D Create(Node parent,Kind kind,Vector3 at=default,float yaw=0)
    {
        var root=ArchitectureArt.Group(parent,"WorkshopModel_"+kind,at,yaw);
        switch(kind)
        {
            case Kind.Workbench: Workbench(root);break;
            case Kind.ToolChest: ToolChest(root);break;
            case Kind.PartsShelf: PartsShelf(root);break;
            case Kind.Generator: Generator(root);break;
            case Kind.CableReel: CableReel(root);break;
            case Kind.PipeRack: PipeRack(root);break;
            case Kind.SafetyBoard: SafetyBoard(root);break;
        }
        StaticArtBatch.Bake(root);return root;
    }
    private static void Workbench(Node3D p)
    {
        foreach(float x in new[]{-1.30f,1.30f})foreach(float z in new[]{-.45f,.45f})
        {
            Box(p,new(x,.43f,z),new(.11f,.82f,.11f),Steel);
            Box(p,new(x,.035f,z),new(.20f,.07f,.19f),Dark);
        }
        Box(p,new(0,.26f,0),new(2.72f,.08f,.90f),Steel);
        Box(p,new(0,.79f,.48f),new(2.78f,.16f,.08f),Teal);
        Box(p,new(0,.88f,0),new(2.94f,.14f,1.12f),Wood);
        foreach(float z in new[]{-.18f,.18f})Box(p,new(0,.952f,z),new(2.78f,.004f,.013f),"79674e",false);
        // A bench vise, not another carton: open jaws and its screw read from the game camera.
        Box(p,new(.94f,1.0f,.12f),new(.43f,.10f,.40f),Steel);
        Box(p,new(.94f,1.10f,.12f),new(.29f,.16f,.28f),Teal);
        foreach(float z in new[]{-.04f,.28f})Box(p,new(.94f,1.19f,z),new(.43f,.10f,.09f),Edge);
        Cylinder(p,new(.94f,1.07f,.37f),.035f,.28f,Edge,new(90,0,0));
        Bar(p,new(.80f,1.07f,.49f),new(1.08f,1.07f,.49f),.035f,Steel);
        Box(p,new(-.75f,1.015f,-.14f),new(.62f,.12f,.40f),Dark);
        foreach(float x in new[]{-.93f,-.73f,-.53f})Box(p,new(x,1.086f,-.14f),new(.05f,.022f,.28f),Edge,false);
        Box(p,new(-.65f,.47f,0),new(.75f,.34f,.65f),Wood);
        Box(p,new(-.65f,.47f,.33f),new(.35f,.055f,.025f),Edge);
    }
    private static void ToolChest(Node3D p)
    {
        foreach(float x in new[]{-.42f,.42f})foreach(float z in new[]{-.34f,.34f})
            Cylinder(p,new(x,.08f,z),.075f,.09f,Dark,new(0,0,90));
        Box(p,new(0,.59f,0),new(1.02f,.91f,.88f),Teal);
        Box(p,new(0,1.065f,0),new(1.08f,.09f,.96f),Dark);
        foreach(float y in new[]{.30f,.51f,.70f,.87f})
        {
            Box(p,new(0,y,.447f),new(.89f,.14f,.035f),Steel);
            Box(p,new(0,y+.035f,.477f),new(.56f,.025f,.035f),Edge);
        }
        Box(p,new(-.37f,.98f,.455f),new(.11f,.065f,.025f),Amber);
    }
    private static void PartsShelf(Node3D p)
    {
        foreach(float x in new[]{-1.02f,1.02f})
        {
            Box(p,new(x,.32f,0),new(.065f,.64f,.055f),Steel);
            Bar(p,new(x,.05f,.02f),new(x,.25f,.31f),.045f,Steel);
        }
        Box(p,new(0,.25f,.16f),new(2.34f,.06f,.37f),Edge);
        for(int i=0;i<5;i++)
        {
            float x=-.91f+i*.45f;
            Box(p,new(x,.43f,.14f),new(.38f,.29f,.28f),i==3?Amber:Teal);
            Box(p,new(x,.525f,.285f),new(.31f,.09f,.025f),Dark,false);
            Box(p,new(x,.38f,.294f),new(.16f,.045f,.012f),Edge,false);
        }
    }
    private static void Generator(Node3D p)
    {
        foreach(float z in new[]{-.55f,.55f})Box(p,new(0,.10f,z),new(2.25f,.20f,.22f),Dark);
        Box(p,new(0,.27f,0),new(2.32f,.13f,1.35f),Steel);
        Box(p,new(-.18f,.86f,0),new(1.84f,1.10f,1.18f),"b7b394");
        Box(p,new(-.18f,1.42f,0),new(1.91f,.10f,1.23f),Teal);
        // Recessed fan and guard give the large casing a clear purpose.
        Cylinder(p,new(-.47f,.89f,.605f),.36f,.035f,Dark,new(90,0,0));
        Cylinder(p,new(-.47f,.89f,.633f),.10f,.035f,Edge,new(90,0,0));
        for(int i=0;i<6;i++)
        {
            float a=i*Mathf.Tau/6;
            Bar(p,new(-.47f,.89f,.65f),new(-.47f+Mathf.Cos(a)*.29f,.89f+Mathf.Sin(a)*.29f,.65f),.045f,Steel);
        }
        foreach(float y in new[]{.72f,.89f,1.06f})Box(p,new(-.47f,y,.68f),new(.63f,.025f,.025f),Edge,false);
        Box(p,new(.40f,1.03f,.61f),new(.39f,.43f,.055f),Dark);
        Cylinder(p,new(.40f,1.11f,.649f),.095f,.022f,Edge,new(90,0,0));
        Bar(p,new(.40f,1.11f,.665f),new(.45f,1.15f,.665f),.016f,Dark);
        foreach(float x in new[]{.29f,.49f})Cylinder(p,new(x,.91f,.65f),.04f,.03f,Amber,new(90,0,0));
        foreach(float y in new[]{.55f,.67f,.79f,.91f,1.03f})Box(p,new(.95f,y,0),new(.045f,.045f,.79f),Dark,false);
        Box(p,new(.93f,1.23f,0),new(.35f,.11f,.91f),Steel);
        Box(p,new(.43f,.52f,.612f),new(.38f,.13f,.025f),Amber);
    }
    private static void CableReel(Node3D p)
    {
        foreach(float z in new[]{-.36f,.36f})
        {
            Cylinder(p,new(0,.54f,z),.48f,.095f,Wood,new(90,0,0));
            Cylinder(p,new(0,.54f,z*1.15f),.12f,.035f,Steel,new(90,0,0));
            foreach(float x in new[]{-.27f,.27f})Cylinder(p,new(x,.54f,z*1.15f),.035f,.03f,Dark,new(90,0,0));
        }
        Cylinder(p,new(0,.54f,0),.35f,.64f,Dark,new(90,0,0));
        for(int i=0;i<7;i++)Cylinder(p,new(0,.54f,-.27f+i*.09f),.365f,.035f,Steel,new(90,0,0));
        foreach(float x in new[]{-.32f,.32f})Box(p,new(x,.045f,0),new(.16f,.09f,.83f),Wood);
    }
    private static void PipeRack(Node3D p)
    {
        foreach(float x in new[]{-1.31f,1.31f})foreach(float z in new[]{-.56f,.56f})
        {
            Box(p,new(x,.73f,z),new(.09f,1.46f,.09f),Steel);
            Box(p,new(x,.04f,z),new(.16f,.08f,.16f),Dark);
        }
        foreach(float y in new[]{.24f,.84f})
        {
            foreach(float z in new[]{-.56f,.56f})Box(p,new(0,y,z),new(2.7f,.12f,.09f),Amber);
            foreach(float x in new[]{-1.17f,1.17f})Box(p,new(x,y,0),new(.09f,.10f,1.2f),Steel);
            foreach(float z in new[]{-.36f,0,.36f})
            {
                Cylinder(p,new(0,y+.23f,z),.16f,2.55f,Edge,new(0,0,90));
                foreach(float x in new[]{-1.285f,1.285f})
                {
                    Cylinder(p,new(x,y+.23f,z),.17f,.04f,Steel,new(0,0,90));
                    Cylinder(p,new(x*1.02f,y+.23f,z),.125f,.007f,Dark,new(0,0,90));
                }
            }
        }
        Bar(p,new(-1.25f,.30f,-.58f),new(1.25f,1.39f,-.58f),.045f,Steel);
    }
    private static void SafetyBoard(Node3D p)
    {
        Box(p,new(0,.55f,0),new(.86f,1.08f,.075f),Steel);
        Box(p,new(0,.55f,.045f),new(.76f,.96f,.025f),"d4d1b8");
        Box(p,new(0,.91f,.065f),new(.68f,.12f,.018f),Teal,false);
        var a=new Vector3(0,.80f,.085f);var b=new Vector3(-.25f,.36f,.085f);var c=new Vector3(.25f,.36f,.085f);
        Bar(p,a,b,.045f,Amber);Bar(p,b,c,.045f,Amber);Bar(p,c,a,.045f,Amber);
        Box(p,new(0,.58f,.085f),new(.045f,.18f,.018f),Dark,false);
        Box(p,new(0,.43f,.085f),new(.045f,.045f,.018f),Dark,false);
        foreach(float y in new[]{.17f,.24f})Box(p,new(0,y,.071f),new(.51f,.025f,.012f),Steel,false);
    }
}
