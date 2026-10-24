using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Small fixed-count decorations; all positions are rig-local and all randomness is analytic.</summary>
public partial class StatusVisualParts : Node3D
{
    private StatusVisualDefinition _def = null!;
    private readonly List<MeshInstance3D> _pieces = new();
    private float _height, _radius, _strength;
    private ShaderMaterial? _volume;
    public static StatusVisualParts Create(Node3D parent, CharacterDefinition character, StatusVisualDefinition def, int stacks)
    {
        var p = new StatusVisualParts { Name = "Status_"+def.Id, _def = def, _height = character.ColliderHeight,
            _radius = character.ColliderRadius, _strength = 1 + .08f*(stacks-1) };
        parent.AddChild(p); p.Build(); p.Step(0); return p;
    }
    private static ArrayMesh? _star;
    private static ArrayMesh Star()
    {
        if (_star != null) return _star;
        using var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i=0;i<10;i++)
        {
            Vector3 Point(int j) { float a=j*Mathf.Tau/10; return new(Mathf.Cos(a)*(j%2==0?.5f:.23f),0,Mathf.Sin(a)*(j%2==0?.5f:.23f)); }
            foreach(var v in new[]{Vector3.Zero,Point(i+1),Point(i)}) {surface.SetNormal(Vector3.Up);surface.AddVertex(v);}
        }
        return _star=surface.Commit();
    }
    private void Build()
    {
        string shape = _def.Shape;
        var material = Mat.Glow(_def.Color, .25f);
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        Mesh mesh = CombatBurstEffect.Blob;
        int count = shape is "flame" or "bubble" or "drop" or "ice" or "frost" ? 5 : 3;
        if (shape is "flame" or "bubble")
        {
            _volume = CombatBurstEffect.Volume(_def.Color, _def.Color.Lightened(.35f), shape=="flame"?1.1f:.1f);
            _volume.SetShaderParameter("flame",shape=="flame");
        }
        if (shape is "ice" or "frost") mesh = new CylinderMesh { TopRadius=0,BottomRadius=.5f,Height=1,RadialSegments=5,Rings=1 };
        if (shape=="star") mesh = Star();
        if (shape is "arc" or "fear" or "weak") mesh = CombatBurstEffect.Shard;
        if (shape is "chain" or "air" or "eye") mesh = new TorusMesh { InnerRadius=.34f,OuterRadius=.5f,Rings=16,RingSegments=6 };
        for(int i=0;i<count;i++) _pieces.Add(CombatBurstEffect.Mesh(this,mesh,(Material?)_volume??material));
    }
    public void Step(float age)
    {
        _volume?.SetShaderParameter("clock",age);
        string shape=_def.Shape;
        for(int i=0;i<_pieces.Count;i++)
        {
            var m=_pieces[i];float a=i*2.39996f,t=Mathf.PosMod(age*(shape=="drop"?1.4f:.7f)+i*.213f,1);
            // Collider radius is smaller than the clothed torso; keep the decorations on its outside.
            float r=_radius*(1.25f+.1f*(i%2)),h=_height;
            m.Visible=true;m.Rotation=Vector3.Zero;
            m.Position=new(Mathf.Cos(a)*r,h*(.35f+(i%3)*.13f),Mathf.Sin(a)*r);
            float size=h*.075f*_strength;
            m.Scale=Vector3.One*size;
            switch(shape)
            {
                case "flame":
                    m.Position+=Vector3.Up*h*.015f*Mathf.Sin(age*6+i);
                    m.Scale=new(size,h*(.16f+.035f*Mathf.Sin(age*9+i))*_strength,size*.85f);
                    m.SetInstanceShaderParameter("visibility_alpha",.83f);break;
                case "bubble":
                    m.Position+=Vector3.Up*t*h*.23f;m.Scale*=.5f+t*.65f;
                    m.SetInstanceShaderParameter("visibility_alpha",Mathf.Sin(t*Mathf.Pi)*.75f);break;
                case "drop":
                    m.Position=new(Mathf.Cos(a)*r,h*(.5f-t*.36f),Mathf.Sin(a)*r);
                    m.Scale=new(size*.45f,size*1.4f,size*.45f);m.Transparency=1-Mathf.Sin(t*Mathf.Pi);break;
                case "ice": case "frost":
                    m.Position=new(Mathf.Cos(a)*r,h*(.12f+(i%3)*.16f),Mathf.Sin(a)*r);
                    m.Scale=new(size*.8f,h*(shape=="ice"?.22f:.1f),size*.8f);m.Rotation=new(.25f*Mathf.Sin(a),a,.3f*Mathf.Cos(a));break;
                case "star":
                    a+=age*1.6f;m.Position=new(Mathf.Cos(a)*r,h*.97f,Mathf.Sin(a)*r);
                    m.Scale=Vector3.One*h*.13f;m.Rotation=new(.3f,age+i,0);break;
                case "arc":
                    m.Position+=new Vector3(Mathf.Sin(age*24+i)*h*.025f,0,0);
                    m.Scale=new(h*.018f,h*.17f,h*.018f);m.Rotation=new(0,a,i%2==0?.5f:-.5f);m.Visible=Mathf.Sin(age*15+i)>-.2f;break;
                case "eye":
                    m.Position=new((i-1)*h*.08f,h*.75f,_radius*1.4f);m.Scale=new(h*.1f,h*.03f,h*.06f);m.Rotation=new(Mathf.Pi/2,0,0);break;
                case "fear":
                    m.Position=new((i-1)*h*.13f+Mathf.Sin(age*18+i)*h*.014f,h*.9f,-_radius*.2f);
                    m.Scale=new(h*.022f,h*.11f,h*.022f);m.Rotation=new(0,0,(i-1)*.2f);break;
                case "weak":
                    m.Position=new((i-1)*h*.12f,h*(.56f-t*.15f),_radius*1.35f);
                    m.Scale=new(h*.025f,h*.08f,h*.025f);m.Rotation=new(0,0,(i-1)*.35f);break;
                case "chain":
                    m.Position=new((i-1)*r*.55f,h*.45f,_radius*1.3f);m.Scale=new(h*.17f,h*.12f,h*.17f);m.Rotation=new(Mathf.Pi/2,0,i%2*.45f);break;
                case "air":
                    m.Position=new(0,h*(.05f+t*.18f),0);m.Scale=new(r*2*(.7f+t*.3f),h*.04f,r*2*(.7f+t*.3f));m.Transparency=t;break;
            }
        }
    }
}
