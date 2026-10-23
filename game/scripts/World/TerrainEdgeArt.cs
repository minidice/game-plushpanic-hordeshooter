using Godot;

namespace HordeShooter;

/// <summary>Small terrain surfaces authored from the map, sharing its ground material and height profile.</summary>
public static class TerrainEdgeArt
{
    public static void Triangle(SurfaceTool s,Vector3 a,Vector3 b,Vector3 c,Vector3 outward)
    {
        var normal=(c-a).Cross(b-a);
        if(normal.LengthSquared()<.00000001f)return;
        if(normal.Dot(outward)<0){(b,c)=(c,b);normal=-normal;}
        normal=normal.Normalized();s.SetNormal(normal);s.AddVertex(a);s.AddVertex(b);s.AddVertex(c);
    }
    public static void Quad(SurfaceTool s,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
    {Triangle(s,a,b,c,outward);Triangle(s,a,c,d,outward);}

    public static ArrayMesh Ramp(float run,float width)
    {
        using var s=new SurfaceTool();s.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 Point(float t,float side)=>new((t-.5f)*run,(TierMap.TerrainRampProfile(t)-.5f)*TierMap.Height,side*width*.5f);
        for(int i=0;i<24;i++)
        {
            float t0=i/24f,t1=(i+1)/24f;var a=Point(t0,-1);var b=Point(t1,-1);var c=Point(t1,1);var d=Point(t0,1);
            // Smooth top normals also meet the horizontal floors without a lighting seam.
            foreach(var (t,side) in new[]{(t0,-1f),(t1,-1f),(t1,1f),(t0,-1f),(t1,1f),(t0,1f)})
            {s.SetNormal(new Vector3(-TierMap.Height/run*6*t*(1-t),1,0).Normalized());s.AddVertex(Point(t,side));}
            Quad(s,a,b,b with{Y=-TierMap.Height*.5f},a with{Y=-TierMap.Height*.5f},Vector3.Forward);
            Quad(s,c,d,d with{Y=-TierMap.Height*.5f},c with{Y=-TierMap.Height*.5f},Vector3.Back);
        }
        Quad(s,Point(1,-1),Point(1,1),Point(1,1) with{Y=-TierMap.Height*.5f},Point(1,-1) with{Y=-TierMap.Height*.5f},Vector3.Right);
        Quad(s,Point(0,-1),Point(0,1),Point(1,1) with{Y=-TierMap.Height*.5f},Point(1,-1) with{Y=-TierMap.Height*.5f},Vector3.Down);
        s.Index();return s.Commit();
    }

    private static float Relief(Vector3 p)=>.5f+.28f*Mathf.Sin(p.X*2.3f+p.Z*1.7f)+.22f*Mathf.Sin(p.X*5.1f-p.Z*3.2f);

    /// <summary>Visual-only wall skin, at most 18cm outside the existing solid. A narrow lip joins the flat top.</summary>
    public static void Rock(SurfaceTool s,Vector3 a,Vector3 b,float lowA,float lowB,float highA,float highB,Vector3 outward)
    {
        if(Mathf.Max(highA-lowA,highB-lowB)<.035f)return;
        Vector3 Point(Vector3 p,float low,float high,int row)
        {
            float height=Mathf.Max(0,high-low);float t=new[]{0f,.28f,.65f,.94f,1f}[row];
            float grain=Relief(p+Vector3.One*row*.39f);
            float offset=row==4?.025f+.055f*grain:.035f+.145f*grain;
            offset*=Mathf.Min(1,height/.35f);
            float y=Mathf.Lerp(low-.025f,high+.006f,t);
            if(row==4)y+=.025f+.035f*Relief(p);
            if(row>0&&row<3)y+=Mathf.Sin(p.X*3+p.Z*2+row)*Mathf.Min(.07f,height*.06f);
            return new Vector3(p.X,y,p.Z)+outward*offset;
        }
        for(int row=0;row<4;row++)Quad(s,Point(a,lowA,highA,row),Point(b,lowB,highB,row),Point(b,lowB,highB,row+1),Point(a,lowA,highA,row+1),outward);
        Quad(s,Point(a,lowA,highA,4),Point(b,lowB,highB,4),(b with{Y=highB+.006f})-outward*.18f,(a with{Y=highA+.006f})-outward*.18f,Vector3.Up);
    }
}
