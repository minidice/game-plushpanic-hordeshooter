using Godot;

namespace HordeShooter;

// One immutable authored surface per hand. Only shader parameters change during animation.
// The vertex frame is the same model space used by Weapon and the approved offline candidates.
internal static class CharacterGripSurface
{
    public static MeshInstance3D Create(Node3D parent,MeshInstance3D source,Transform3D toGripSpace,string name,float side,bool bird)
    {
        var mesh=new ArrayMesh();
        for(int i=0;i<source.Mesh.GetSurfaceCount();i++)
        {
            var arrays=source.Mesh.SurfaceGetArrays(i);
            var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            var normals=arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
            for(int j=0;j<vertices.Length;j++){vertices[j]=toGripSpace*vertices[j];normals[j]=(toGripSpace.Basis.Inverse().Transposed()*normals[j]).Normalized();}
            arrays[(int)Mesh.ArrayType.Vertex]=vertices;arrays[(int)Mesh.ArrayType.Normal]=normals;
            // Tangents were authored for the unbent mesh and are not used by this material.
            arrays[(int)Mesh.ArrayType.Tangent]=default;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
        }
        var material=new ShaderMaterial{Shader=GD.Load<Shader>("res://shaders/character_grip.gdshader")};
        var arm=new MeshInstance3D{Name=name,Mesh=mesh,MaterialOverride=material,CustomAabb=new Aabb(new Vector3(-2,-1,-2),new Vector3(4,4,4))};
        parent.AddChild(arm);arm.SetMeta("authored_grip",true);
        arm.SetInstanceShaderParameter("side",side);arm.SetInstanceShaderParameter("bird",bird);arm.SetInstanceShaderParameter("grip_surface",true);return arm;
    }

    public static void Pose(MeshInstance3D arm,Vector3 hand,bool holding,float size,float running,Color color)
    {
        arm.SetInstanceShaderParameter("grip",hand);arm.SetInstanceShaderParameter("holding",holding);
        arm.SetInstanceShaderParameter("wing_size",size);arm.SetInstanceShaderParameter("running",running);arm.SetInstanceShaderParameter("fur_color",color);
        // The deformation curve ends at this actual shader parameter in arm-local space.
    }
    public static bool IsSurface(MeshInstance3D arm)=>arm.HasMeta("authored_grip");
    public static Vector3 Wrist(MeshInstance3D arm)=>arm.GlobalTransform*arm.GetInstanceShaderParameter("grip").AsVector3();
}
