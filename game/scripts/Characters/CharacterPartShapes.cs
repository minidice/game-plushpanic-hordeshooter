using Godot;
using System.Collections.Generic;

namespace HordeShooter;

public partial class CharacterRig
{
    private readonly Dictionary<Node3D,Transform3D> _featureHomes=new();

    // Authored transforms are cached once, so repeated edits and presets never compound scale.
    private Node3D? RestoreFeature(string suffix)
    {
        if(Model.FindChild(Definition.Id+"_"+suffix,true,false) is not Node3D node)return null;
        if(!_featureHomes.ContainsKey(node))_featureHomes[node]=node.Transform;
        node.Transform=_featureHomes[node];return node;
    }
    private void ScaleFeature(string suffix,Vector3 scale)
    {
        if(RestoreFeature(suffix) is { } node)node.Scale*=scale;
    }
    private void ApplyPartShapes(AppearanceProfile p)
    {
        // Keep the equipment, grips and continuous skin in the same visual space.
        // The CharacterBody collider and gameplay stats deliberately retain the species baseline.
        Model.Scale=new Vector3(p.BodyWidth,p.BodyHeight,p.BodyWidth)*Definition.ModelScale;
        _wingSize=p.WingSize;
        foreach(var (side,eyeName,browName,armName,footName) in new[]
        {
            (-1f,"leftEye","leftBrow","leftArm","leftFoot_mesh"),
            (1f,"rightEye","rightBrow","rightArm","rightFoot_mesh")
        })
        {
            if(RestoreFeature(eyeName) is { } eye)
            {
                MoveAcrossFace(eye,p.EyeSpacing);
                float eyeHeight=p.FaceStyle==2?.52f:p.FaceStyle==1?1.10f:1;
                eye.Scale*=new Vector3(p.EyeSize,p.EyeSize*eyeHeight,1);
            }
            if(RestoreFeature(browName) is { } brow)
            {
                MoveAcrossFace(brow,p.EyeSpacing);
                if(p.FaceStyle==1)brow.Rotation+=new Vector3(0,0,side*.28f);
                if(p.FaceStyle==2){brow.Position+=Vector3.Down*.025f;brow.Rotation+=new Vector3(0,0,-side*.12f);}
            }
            ScaleFeature(armName,Vector3.One*p.WingSize);
            // Width/depth only; ankle height and the ground contact stay stable during walking.
            ScaleFeature(footName,new Vector3(p.FeetSize,1,p.FeetSize));
        }
        Vector3 snout=p.BeakStyle==1?new Vector3(1.18f,.9f,.85f):p.BeakStyle==2?new Vector3(.95f,1.10f,.70f):Vector3.One;
        ScaleFeature(AppearanceCatalog.IsBird(Definition.Id)?"beak":"snout",snout*p.BeakSize);
        ScaleFeature("tail",Vector3.One*p.TailSize);
        SetPartVisible(Definition.Id+"_crestShort",p.Hat==0&&p.HairStyle==1);
        SetPartVisible(Definition.Id+"_crestSwept",p.Hat==0&&p.HairStyle==2);
        UpdateWeaponGrip();
    }
    private static void MoveAcrossFace(Node3D part,float spacing)
    {
        var at=part.Position;float oldX=at.X;at.X*=spacing;
        // Follow the rounded face surface as the eyes move apart, rather than hovering in front.
        const float radius=.47f;
        at.Z+=(Mathf.Sqrt(Mathf.Max(.001f,radius*radius-at.X*at.X))-Mathf.Sqrt(Mathf.Max(.001f,radius*radius-oldX*oldX)))*.86f;
        part.Position=at;
    }
}
