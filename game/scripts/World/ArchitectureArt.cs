using Godot;
using System.Collections.Generic;

namespace HordeShooter;

internal enum WallFinish { Shelter, Masonry, Warehouse, Yard }

/// <summary>Static architecture attached to existing wall envelopes. No colliders or gameplay lights.</summary>
internal static class ArchitectureArt
{
    private const string Steel = "465b63", Trim = "859392", Foot = "303e43", Amber = "b29b62";
    private static readonly Dictionary<(WallFinish, bool), ShaderMaterial> WallMaterials = new();
    private static ShaderMaterial? _fabric;
    public static Node3D Group(Node parent, string name, Vector3 at = default, float yaw = 0)
    {
        var root = new Node3D { Name = name, Position = at, RotationDegrees = new Vector3(0,yaw,0) };
        parent.AddChild(root,true); return root;
    }
    private static MeshInstance3D Box(Node parent, Vector3 at, Vector3 size, string color, bool bevel = true)
        => StageArt.Box(parent, at, size, color, bevel);

    public static void Wall(Node3D parent, Vector3 size, WallFinish finish)
    {
        bool along = size.X > size.Z;
        var root = Group(parent, "ArchitectureWall");
        if (size.Y < 1.3f && along && finish is WallFinish.Warehouse or WallFinish.Yard)
        {
            // The low solid envelope remains: cloth/metal panels never suggest a walk-through gap.
            Fence(root, size.X, size.Y, finish == WallFinish.Warehouse, -size.Y/2);
            StaticArtBatch.Bake(root); return;
        }
        if (!WallMaterials.TryGetValue((finish,along),out var material))
        {
            material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/architecture_wall.gdshader") };
            material.SetShaderParameter("along_x",along);
            material.SetShaderParameter("finish",finish == WallFinish.Yard ? 2 : finish == WallFinish.Masonry ? 1 : 0);
            material.SetShaderParameter("upper_color",new Color(finish switch { WallFinish.Shelter=>"9ca9a3",WallFinish.Masonry=>"879392",WallFinish.Warehouse=>"899496",_=>"697d80" }));
            material.SetShaderParameter("lower_color",new Color(finish == WallFinish.Warehouse ? "405c69" : "426e65"));
            WallMaterials.Add((finish,along),material);
        }
        var wall = Box(root, new Vector3(0,-.045f,0), size with { Y = size.Y-.09f }, "ffffff");
        wall.MaterialOverride = material;
        Vector3 strip = along ? new(size.X,.14f,size.Z+.035f) : new(size.X+.035f,.14f,size.Z);
        Box(root,new(0,-size.Y/2+.07f,0),strip,Foot);
        Box(root,new(0,size.Y/2-.045f,0),strip with {Y=.09f},Trim);
        if(size.Y>1.3f && finish!=WallFinish.Yard)
            Box(root,new(0,1.14f-size.Y/2,0),strip with {Y=.035f},Trim);
        float length=along?size.X:size.Z;
        // Pilasters and end caps are within the old wall footprint (no new invisible obstacles).
        for(float t=-length/2+.09f;t<length/2;t+=4.8f)
        {
            var at=along?new Vector3(t,0,0):new Vector3(0,0,t);
            var column=along?new Vector3(.16f,size.Y-.14f,size.Z+.05f):new Vector3(size.X+.05f,size.Y-.14f,.16f);
            Box(root,at-new Vector3(0,.05f,0),column,finish==WallFinish.Warehouse?Steel:Trim);
        }
        var end=along?new Vector3(length/2-.045f,0,0):new Vector3(0,0,length/2-.045f);
        Box(root,end-new Vector3(0,.05f,0),along?new(.09f,size.Y-.14f,size.Z+.035f):new(size.X+.035f,size.Y-.14f,.09f),Trim);
        StaticArtBatch.Bake(root);
    }

    public static void Fence(Node3D root,float length,float height,bool fabric,float bottom=0)
    {
        int count=Mathf.CeilToInt(length/2.7f); float width=length/count;
        Box(root,new(0,bottom+.1f,0),new(length,.2f,.25f),Foot);
        for(int i=0;i<=count;i++)
        {
            float x=-length/2+i*width;
            Box(root,new(x,bottom+height/2,0),new(.09f,height,.11f),Trim);
            Box(root,new(x,bottom+.045f,0),new(.22f,.09f,.32f),Steel);
            Box(root,new(x,bottom+height,0),new(.13f,.055f,.14f),Trim);
        }
        for(int i=0;i<count;i++)
        {
            float x=-length/2+(i+.5f)*width;
            Box(root,new(x,bottom+height-.06f,0),new(width,.045f,.055f),Trim);
            if(!fabric)
            {
                Box(root,new(x,bottom+height*.52f,0),new(width-.1f,height*.7f,.045f),"556c70",false);
                for(float y=bottom+.3f;y<bottom+height-.12f;y+=.16f)
                    Box(root,new(x,y,.035f),new(width-.15f,.025f,.035f),Steel,false);
                continue;
            }
            _fabric ??= new ShaderMaterial { Shader=GD.Load<Shader>("res://shaders/architecture_fabric.gdshader") };
            var surface=new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
            Vector3 Point(float u,float v)=>new(x+(u-.5f)*(width-.13f),bottom+.2f+v*(height-.27f)-Mathf.Sin(u*Mathf.Pi)*.09f*v,
                .03f+Mathf.Sin(u*Mathf.Pi*3)*.025f+Mathf.Sin(u*Mathf.Pi)*.05f);
            void Vertex(float u,float v) { surface.SetUV(new Vector2(u,v));surface.SetNormal(Vector3.Back);surface.AddVertex(Point(u,v)); }
            for(int j=0;j<12;j++) {float a=j/12f,b=(j+1)/12f;Vertex(a,0);Vertex(a,1);Vertex(b,1);Vertex(a,0);Vertex(b,1);Vertex(b,0);}
            Mat.Mesh(root,surface.Commit(),Vector3.Zero,_fabric); surface.Dispose();
        }
    }

    /// <summary>Frame along Z, leaving the exact existing opening width clear. x is the wall's centre.</summary>
    public static void Portal(Node parent,float x,float width,float height,bool shutter=false)
    {
        var root=Group(parent,"PortalFrame",new Vector3(x,0,0));
        foreach(int side in new[]{-1,1})
        {
            Box(root,new(0,height/2,side*(width/2+.13f)),new(.56f,height,.26f),Steel);
            Box(root,new(-.29f,.48f,side*(width/2+.14f)),new(.06f,.96f,.22f),Amber);
            Box(root,new(-.305f,height*.66f,side*(width/2+.14f)),new(.04f,.22f,.18f),Trim);
        }
        // The shutter slides up through x +/-0.2: its lintel and housing sit on the yard side.
        float headerX=shutter?.75f:0;
        Box(root,new(headerX,height+.12f,0),new(.62f,.24f,width+.52f),Trim);
        if(shutter)
        {
            Box(root,new(headerX,height+.37f,0),new(.85f,.32f,width+.6f),Steel);
            foreach(int side in new[]{-1,1})
                Box(root,new(.35f,height+.12f,side*(width/2+.13f)),new(1.3f,.24f,.26f),Steel);
        }
        // Walkable threshold is below the collision plane apart from its 8 mm visual top.
        Box(root,new(0,-.012f,0),new(.8f,.04f,width),Foot,false);
        for(float z=-width/2+.12f;z<width/2;z+=.17f)
            Box(root,new(0,.011f,z),new(.48f,.007f,.025f),Trim,false);
        StaticArtBatch.Bake(root);
    }

    /// <summary>Wall-mounted vent, conduit and junction box. Its back is local -Z.</summary>
    public static void Services(Node parent,Vector3 at,float yaw=0)
    {
        var root=Group(parent,"WallServices",at,yaw);
        Box(root,new(0,1.95f,.07f),new(1.1f,.6f,.14f),Foot);
        for(int i=0;i<6;i++) Box(root,new(0,1.73f+i*.085f,.17f),new(.96f,.045f,.12f),Trim,false);
        Box(root,new(1.1f,1.4f,.1f),new(.48f,.68f,.22f),Steel);
        Box(root,new(1.1f,1.43f,.225f),new(.37f,.51f,.03f),"74857f");
        Box(root,new(1.25f,1.43f,.25f),new(.035f,.13f,.04f),Foot);
        Box(root,new(.78f,2.6f,.08f),new(2.5f,.055f,.055f),Steel,false);
        Box(root,new(1.1f,2.2f,.08f),new(.055f,.8f,.055f),Steel,false);
        foreach(float x in new[]{-.35f,.65f,1.65f}) Box(root,new(x,2.6f,.08f),new(.05f,.12f,.09f),Trim,false);
        StaticArtBatch.Bake(root);
    }

    public static void FloorApron(Node parent,float x,float width,bool industrial)
    {
        var root=Group(parent,"ThresholdApron",new Vector3(x,0,0));
        var plate=Box(root,new(0,.006f,0),new(1.8f,.012f,width),industrial?"52646a":"616e65",false);
        plate.CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        foreach(float side in new[]{-1f,1f})
        {
            Box(root,new(side*.84f,.015f,0),new(.06f,.01f,width),industrial?Amber:Trim,false).CastShadow=GeometryInstance3D.ShadowCastingSetting.Off;
        }
        StaticArtBatch.Bake(root);
    }
}
