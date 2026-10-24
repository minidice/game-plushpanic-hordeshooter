using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class StudioPreview
{
    private object StatusCatalog() => new {
        source = StatusVisualCatalog.DataPath,
        statuses = StatusVisualCatalog.Current.All.Values.Select(d => new {
            id=d.Id, name=d.Name, color=d.Color.ToHtml(false), shape=d.Shape, priority=d.Priority,
            surface=d.Surface, surface_alpha=d.SurfaceAlpha, binding=d.Binding, sources=d.Sources, svg=StatusVisualIcons.Svg(d) }),
        targets = _characters.Select(d => new { id="player:"+d.Id, name="플레이어 · "+Loc.Text(d.DisplayName) })
            .Concat(MonsterDatabase.Current.Monsters.Values.Select(d => new { id="monster:"+d.Id, name="몬스터 · "+d.DisplayName })),
        limits = new { surfaces=1, decorations=StatusVisualController.MaxDecorations, world_icons=StatusVisualController.MaxIcons },
        loadedAt=DateTimeOffset.Now.ToString("O")
    };

    private async Task<object> RenderStatuses(JsonElement data)
    {
        string target=data.GetProperty("target").GetString()??"";
        CharacterDefinition definition;
        if(target.StartsWith("player:",StringComparison.Ordinal))
            definition=_characters.SingleOrDefault(d=>d.Id==target[7..])??throw new ArgumentException("알 수 없는 캐릭터입니다.");
        else if(target.StartsWith("monster:",StringComparison.Ordinal) && MonsterDatabase.Current.Monsters.TryGetValue(target[8..],out var monster))
            definition=MonsterLook(monster,"goose");
        else throw new ArgumentException("알 수 없는 대상입니다.");
        var states=data.GetProperty("states").EnumerateArray().Select(s=>new StatusVisualState(s.GetProperty("id").GetString()??"",0,s.GetProperty("stacks").GetInt32())).ToArray();
        if(states.Length>12)throw new ArgumentException("최대 12개 상태입니다.");
        foreach(var state in states) {StatusVisualCatalog.Current.Get(state.Id);if(state.Stacks<1||state.Stacks>5)throw new ArgumentException("중첩은 1–5입니다.");}
        bool hit=data.GetProperty("hit").GetBoolean(),release=data.GetProperty("release").GetBoolean();
        const int fps=15,steps=42,pixels=512;
        const double dt=1d/fps;
        var size=_viewport.Size;var update=_viewport.RenderTargetUpdateMode;var transform=_camera.Transform;var projection=_camera.Projection;
        float cameraSize=_camera.Size;bool platformVisible=_platform.Visible,rigVisible=_rig?.Visible??false,subjectVisible=_librarySubject?.Visible??false;
        var root=new Node3D{Name="StudioStatusSample",ProcessMode=ProcessModeEnum.Disabled};
        try
        {
            if(_rig!=null)_rig.Visible=false;if(_librarySubject!=null)_librarySubject.Visible=false;_platform.Visible=false;
            _viewport.Size=new(pixels,pixels);_viewport.AddChild(root);_viewport.RenderTargetUpdateMode=SubViewport.UpdateMode.Always;
            float h=definition.ColliderHeight;
            var focus=Vector3.Up*h*.6f;_camera.Projection=Camera3D.ProjectionType.Orthogonal;_camera.Size=h*1.8f;
            _camera.Position=focus+new Vector3(3,4,7);_camera.LookAt(focus);
            Mat.Mesh(root,new PlaneMesh{Size=Vector2.One*h*5},new(0,-.04f,0),Mat.Solid("303c39"));
            CharacterRig rig;string runtimeType="CharacterRig";
            if(target.StartsWith("monster:",StringComparison.Ordinal))
            {
                var enemy=CreateMonsterSubject(root,MonsterDatabase.Get(target[8..]),definition);
                rig=enemy.Rig;runtimeType=enemy.GetType().Name;
            }
            else {rig=CharacterRig.Create(definition);root.AddChild(rig);rig.HideBuiltInWeapon();}
            int weaponMeshes=Descendants(rig.Weapon).OfType<MeshInstance3D>().Count(m=>m.IsVisibleInTree());
            var controller=StatusVisualController.Attach(rig);controller.LiveSource=null;controller.SetStates(states);
            foreach(var n in Descendants(root)){n.SetProcess(false);n.SetPhysicsProcess(false);}
            var frames=new List<EffectFrame>();
            for(int frame=0;frame<=steps;frame++)
            {
                if(hit&&frame==12)rig.FlashHit();
                if(release&&frame==30)controller.Clear();
                // Newly created hit nodes also use fixed-step time, independent of readback speed.
                foreach(var n in Descendants(root).ToArray())
                {
                    n.SetProcess(false);n.SetPhysicsProcess(false);
                    if(frame>0&&frame!=12&&n is CharacterHitFlashEffect or DamageBurstEffect)n._Process(dt);
                }
                controller._Process(frame==0?0:dt);
                await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                using var image=_viewport.GetTexture().GetImage();
                frames.Add(new(frame/(double)fps,Convert.ToBase64String(image.SavePngToBuffer())));
            }
            return new {target,width=pixels,height=pixels,fps,duration=steps*dt,frames,subject=new {runtime_type=runtimeType,weapon_meshes=weaponMeshes},
                renderer="Godot Forward+ · 공통 상태 렌더러",timing="0.8초 피격 / 2.0초 해제 (선택 시)",preview_only=states.Where(s=>StatusVisualCatalog.Current.Get(s.Id).Binding=="preview").Select(s=>s.Id)};
        }
        finally
        {
            if(IsInstanceValid(root))root.Free();_viewport.Size=size;_viewport.RenderTargetUpdateMode=update;
            _camera.Transform=transform;_camera.Projection=projection;_camera.Size=cameraSize;_camera.MakeCurrent();_platform.Visible=platformVisible;
            if(_rig!=null&&IsInstanceValid(_rig))_rig.Visible=rigVisible;
            if(_librarySubject!=null&&IsInstanceValid(_librarySubject))_librarySubject.Visible=subjectVisible;
        }
    }
}
