using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;
public partial class CharacterRig
{
    private readonly List<(string Channel,StandardMaterial3D Material,Color Original)> _colorMaterials=new();
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
                // Brows are painted in a body tone but exported as plain detail; they follow the tones too.
                bool brow=mesh.Name.ToString().Contains("Brow");
                foreach(var channel in new[]{"body","beak","hat","vest","pack","wing","tail","feet","eyes","hair","brow"})
                    if(brow?channel=="brow":source.ResourceName.StartsWith("custom_"+channel,StringComparison.Ordinal))
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

    /// <summary>Current color of a channel. Body tones ("body", "body2", "body3") report the tone, not a material.</summary>
    public Color ChannelColor(string channel)
    {
        PrepareColors();
        if(BodyTone(channel) is { } tone)return tone;
        foreach(var item in _colorMaterials)if(item.Channel==channel)return item.Material.AlbedoColor;
        return Colors.White;
    }
    public bool HasColorChannel(string channel)
    {
        PrepareColors();
        if(channel is "body" or "body2")return AppearanceCatalog.Palette(_toneSpecies)!=null||_colorMaterials.Exists(m=>m.Channel=="body");
        if(channel=="body3")return AppearanceCatalog.HasMarking(_toneSpecies);
        return _colorMaterials.Exists(m=>m.Channel==channel);
    }

    private Color? BodyTone(string channel)
    {
        if(AppearanceCatalog.Palette(_toneSpecies) is not { } palette)return null;
        var p=_appliedAppearance??new AppearanceProfile();
        return channel switch
        {
            "body"=>Tone(p.Body,palette.Primary),
            "body2"=>Tone(p.BodySecondary,palette.Secondary),
            "body3"=>palette.Marking is { } m?Tone(p.BodyMarking,m):null,
            _=>null,
        };
    }

    /// <summary>
    /// Parts painted with one of the species' tone colors (ears, arms, crest, mammal muzzle, bird tail)
    /// take the new tone, so the whole animal recolors together. Anything else keeps its color.
    /// </summary>
    private Color FollowBody(Color original,AppearanceProfile p)
    {
        if(AppearanceCatalog.Palette(_toneSpecies) is not { } palette)return original;
        static bool Same(Color a,Color b)=>Mathf.Abs(a.R-b.R)<.02f&&Mathf.Abs(a.G-b.G)<.02f&&Mathf.Abs(a.B-b.B)<.02f;
        if(Same(original,palette.Primary))return Tone(p.Body,palette.Primary);
        if(Same(original,palette.Secondary))return Tone(p.BodySecondary,palette.Secondary);
        if(palette.Marking is { } marking&&Same(original,marking))return Tone(p.BodyMarking,marking);
        return original;
    }

    public void ApplyAppearance(AppearanceProfile profile)
    {
        _appliedAppearance=profile;
        profile.Normalize();
        PrepareColors();
        bool bird=AppearanceCatalog.IsBird(_toneSpecies);
        Color? arm=null,armOriginal=null;
        foreach(var item in _colorMaterials)
        {
            string value=profile.ColorFor(item.Channel);
            bool chosen=value.Length>0&&Color.HtmlIsValid(value);
            item.Material.AlbedoColor=item.Channel switch
            {
                // Body-colored attachments follow the palette; the skin uses its own continuous mask shader.
                "body"=>FollowBody(item.Original,profile),
                // Arms always match the body (old saved arm colors are ignored).
                "wing" or "brow"=>FollowBody(item.Original,profile),
                "hair"=>chosen?new Color(value):FollowBody(item.Original,profile),
                "beak"=>chosen?new Color(value):bird?item.Original:FollowBody(item.Original,profile),
                "tail"=>(chosen?new Color(value):Colors.White)*(bird?FollowBody(item.Original,profile):item.Original),
                _=>chosen?new Color(value):item.Original,
            };
            if(item.Channel=="wing"){arm=item.Material.AlbedoColor;armOriginal=item.Original;}
        }
        ApplyBodyTones(profile);
        // Gripping hands move by as much as the arms did, so an unchanged arm leaves the species' hand color.
        HandColor=Definition.HandColor;
        if(arm is { } armNow&&armOriginal is { } armBefore&&!armNow.IsEqualApprox(armBefore))
            HandColor=new Color(Mathf.Clamp(HandColor.R+armNow.R-armBefore.R,0,1),Mathf.Clamp(HandColor.G+armNow.G-armBefore.G,0,1),Mathf.Clamp(HandColor.B+armNow.B-armBefore.B,0,1));
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
        ApplyEars(profile,profile.Hat!=0);
        ApplyTailStyle(profile.TailStyle);
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
                ApplyEars(p,hatOn:false);
                SetPartVisible(Definition.Id+"_crestShort",p.HairStyle==1);
                SetPartVisible(Definition.Id+"_crestSwept",p.HairStyle==2);
            }
        }
        Weapon.Visible=weapon;
        SetPartVisible(Definition.Id+"_leftArm",!weapon||_heldWeapon==null);
        SetPartVisible(Definition.Id+"_rightArm",!weapon||_heldWeapon==null);
        UpdateWeaponGrip();
    }
    /// <summary>Aiming (decisions.md C-26): a low, steady stance. Body and gun sink a little, feet stay planted.</summary>
    public void AimStance(float blend)
    {
        if(blend<=0)return;
        float drop=.14f*blend/Definition.ModelScale;
        Body.Position+=Vector3.Down*drop;
        Deform.Scale=Deform.Scale*new Vector3(1+.04f*blend,1-.08f*blend,1+.04f*blend);
        Weapon.Position+=Vector3.Down*drop;
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
