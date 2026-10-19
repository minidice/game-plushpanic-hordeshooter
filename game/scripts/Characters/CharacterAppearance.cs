using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;
public partial class CharacterRig
{
    private readonly List<(string Channel,StandardMaterial3D Material,Color Original)> _colorMaterials=new();
    private readonly Dictionary<Node3D,Transform3D> _earHomes=new();
    private AppearanceProfile? _appliedAppearance;
    public Color HandColor {get;private set;}=Colors.White;
    private void PrepareColors()
    {
        if(_colorMaterials.Count>0)return;
        foreach(var child in Model.FindChildren("*",nameof(MeshInstance3D),true,false))
        {
            var mesh=(MeshInstance3D)child;if(mesh.Mesh==null)continue;
            for(int i=0;i<mesh.Mesh.GetSurfaceCount();i++)
            {
                if(mesh.GetActiveMaterial(i) is not StandardMaterial3D source)continue;
                foreach(var channel in new[]{"body","beak","hat","vest","pack","wing","tail","feet","eyes","hair"})
                    if(source.ResourceName.StartsWith("custom_"+channel,StringComparison.Ordinal))
                    {
                        var owned=(StandardMaterial3D)source.Duplicate();
                        // Imported skin has COLOR_0 markings, but the glTF material can omit the vertex-color flag.
                        var arrays=mesh.Mesh.SurfaceGetArrays(i);
                        if(arrays.Count>(int)Mesh.ArrayType.Color && arrays[(int)Mesh.ArrayType.Color].VariantType==Variant.Type.PackedColorArray)
                            owned.VertexColorUseAsAlbedo=arrays[(int)Mesh.ArrayType.Color].AsColorArray().Length>0;
                        mesh.SetSurfaceOverrideMaterial(i,owned);
                        _colorMaterials.Add((channel,owned,source.AlbedoColor));break;
                    }
            }
        }
    }
    public Color ChannelColor(string channel)
    {
        PrepareColors();foreach(var item in _colorMaterials)if(item.Channel==channel)return item.Material.AlbedoColor;
        return Colors.White;
    }
    public bool HasColorChannel(string channel){PrepareColors();return _colorMaterials.Exists(m=>m.Channel==channel);}
    public void ApplyAppearance(AppearanceProfile profile)
    {
        _appliedAppearance=profile;
        profile.Normalize();
        PrepareColors();
        foreach(var item in _colorMaterials)
        {
            string value=profile.ColorFor(item.Channel);
            if(value.Length==0&&(item.Channel=="hair"||(item.Channel=="wing"&&AppearanceCatalog.IsBird(Definition.Id))))
            {
                item.Material.AlbedoColor=profile.Body.Length>0&&Color.HtmlIsValid(profile.Body)?item.Original*new Color(profile.Body):item.Original;
                continue;
            }
            item.Material.AlbedoColor=value.Length>0&&Color.HtmlIsValid(value)
                ? new Color(value)*(item.Channel is "body" or "tail"?item.Original:Colors.White) : item.Original;
        }
        HandColor=Definition.HandColor;
        if((Definition.Id=="goose"||Definition.Id=="duck")&&profile.Body.Length>0&&Color.HtmlIsValid(profile.Body))HandColor*=new Color(profile.Body);
        if(profile.Wing.Length>0&&Color.HtmlIsValid(profile.Wing))HandColor=new Color(profile.Wing);
        foreach(var child in Weapon.FindChildren("Hand*",nameof(MeshInstance3D),true,false))
            ((MeshInstance3D)child).MaterialOverride=Mat.Solid(HandColor);
        UpdateWeaponGrip();
        SetPartVisible(Definition.Id+"_helmet",profile.Hat==1);
        SetPartVisible(Definition.Id+"_beret",profile.Hat==2);
        SetPartVisible(Definition.Id+"_vest",profile.Vest&&profile.VestStyle==0);
        SetPartVisible(Definition.Id+"_vestPadded",profile.Vest&&profile.VestStyle==1);
        SetPartVisible(Definition.Id+"_goggles",profile.Goggles);
        SetPartVisible(Definition.Id+"_headset",profile.Headset);
        SetPartVisible(Definition.Id+"_pack",profile.Pack);
        foreach(var (suffix,side) in new[]{("leftEar",-1f),("rightEar",1f)})
            if(Model.FindChild(Definition.Id+"_"+suffix,true,false) is Node3D ear)
            {
                if(!_earHomes.ContainsKey(ear))_earHomes[ear]=ear.Transform;
                ear.Transform=_earHomes[ear];
                if(profile.Hat!=0){ear.Position=new Vector3(side*.43f,.16f,-.06f);ear.Rotation=new Vector3(0,0,-side*.55f);ear.Scale=new Vector3(.72f,.60f,.80f);}
                ear.Scale*=profile.EarSize;
            }
        ApplyPartShapes(profile);
    }
    private void SetPartVisible(string name,bool visible){if(Model.FindChild(name,true,false) is Node3D part)part.Visible=visible;}
    /// <summary>Workshop-only inspection. Restore saved choices first, then temporarily uncover the body.</summary>
    public void SetPreviewVisibility(bool equipment,bool weapon)
    {
        if(_appliedAppearance is { } p)
        {
            ApplyAppearance(p);
            if(!equipment)
            {
                foreach(var name in Definition.GearNodes)HidePart(name);
                foreach(var (ear,home) in _earHomes){ear.Transform=home;ear.Scale*=p.EarSize;}
                SetPartVisible(Definition.Id+"_crestShort",p.HairStyle==1);
                SetPartVisible(Definition.Id+"_crestSwept",p.HairStyle==2);
            }
        }
        Weapon.Visible=weapon;
        SetPartVisible(Definition.Id+"_leftArm",!weapon||_heldWeapon==null);
        SetPartVisible(Definition.Id+"_rightArm",!weapon||_heldWeapon==null);
        UpdateWeaponGrip();
    }
    /// <summary>Short forward bound. The collider follows the ground; only the visual arcs upwards.</summary>
    public void DodgePose(float phase)
    {
        phase=Math.Clamp(phase,0,1);
        if(phase<=0||phase>=1){Pose.Transform=Transform3D.Identity;return;}
        float arc=Mathf.Sin(phase*Mathf.Pi);
        Pose.Position=Vector3.Up*(arc*.48f);
        Pose.Rotation=new Vector3(arc*.65f,0,0);
        Deform.Scale=_deformHome*new Vector3(1-arc*.05f,1-arc*.12f,1+arc*.14f);
        LeftFoot.Position=_leftHome.Origin+new Vector3(-.045f,arc*.04f,-arc*.17f);
        RightFoot.Position=_rightHome.Origin+new Vector3(.045f,arc*.04f,-arc*.17f);
        ApplyCarryPose(0);
        Weapon.Position+=new Vector3(0,-arc*.08f,arc*.10f);
        Weapon.Rotation+=new Vector3(-arc*.18f,0,0);
        UpdateWeaponGrip();
    }
}
