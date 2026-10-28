using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class GraphicsOptionsCheck : Node
{
    private Stage? _stage;
    private int _checks;
    private const string SettingsPath="user://graphics-options-check.cfg";
    private void Check(bool ok,string message){if(!ok)throw new Exception(message);_checks++;GD.Print("PASS: "+message);}
    private async Task Frames(int n){for(int i=0;i<n;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Escape()
    {
        Input.ParseInputEvent(new InputEventKey{Keycode=Key.Escape,PhysicalKeycode=Key.Escape,Pressed=true});await Frames(2);
        Input.ParseInputEvent(new InputEventKey{Keycode=Key.Escape,PhysicalKeycode=Key.Escape,Pressed=false});await Frames(2);
    }
    private async Task Shot(string label)
    {
        if(DisplayServer.GetName()=="headless")return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Check(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/graphics-"+label+".png"))==Error.Ok,"render "+label);
    }
    public override async void _Ready()
    {
        ProcessMode=ProcessModeEnum.Always;int exit=0;
        try{await Run();GD.Print("GRAPHICS_OPTIONS_PASS "+_checks);}
        catch(Exception e){GD.PushError(e.ToString());GD.Print("GRAPHICS_OPTIONS_FAIL "+e.Message);exit=1;}
        GetTree().Paused=false;
        Input.ActionRelease(GameInput.Fire);
        Input.ActionRelease(GameInput.Interact);
        if(_stage!=null&&IsInstanceValid(_stage))_stage.Free();
        GameSettings.Reset();DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SettingsPath));
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private async Task Run()
    {
        GameSettings.Reset();DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SettingsPath));
        Check(GameSettings.ShadowQuality==GraphicsQualityLevel.High && GameSettings.SurfaceQuality==GraphicsQualityLevel.High,"default preserves authored quality");
        GameSettings.Path=SettingsPath;GameSettings.Load();
        GameSettings.ShadowQuality=GameSettings.SurfaceQuality=GraphicsQualityLevel.Low;GameSettings.Save();
        GameSettings.Reset();GameSettings.Path=SettingsPath;GameSettings.Load();
        Check(GameSettings.ShadowQuality==GraphicsQualityLevel.Low && GameSettings.SurfaceQuality==GraphicsQualityLevel.Low,"quality survives save/reload");
        using(var cfg=new ConfigFile()){cfg.SetValue("graphics","shadow_quality",99);cfg.SetValue("graphics","surface_quality",-7);cfg.Save(SettingsPath);}
        GameSettings.Load();
        Check(GameSettings.ShadowQuality==GraphicsQualityLevel.High && GameSettings.SurfaceQuality==GraphicsQualityLevel.High,"invalid quality falls back to high");
        _stage=new Stage{MapOverride=new LayoutMap(),DirectorEnabled=false,SquadOverride=0,CharacterOverride="goose",ModeOverride=GameMode.Scenario};AddChild(_stage);
        await Frames(10);var p=_stage.Player;p.ManualControl=true;p.Invulnerable=true;
        foreach(var e in _stage.AllEnemies.ToArray())e.QueueFree();
        p.TeleportTo(new(94,1.25f,-24));_stage.Cam.SnapTo(p.Position+Vector3.Up);await Frames(45);
        var sun=_stage.GetChildren().OfType<DirectionalLight3D>().Single();
        var env=_stage.GetChildren().OfType<WorldEnvironment>().Single().Environment;
        Check(sun.ShadowEnabled && env.SsaoEnabled,"high preserves map shadows and ambient occlusion");await Shot("high");
        GameSettings.ShadowQuality=GameSettings.SurfaceQuality=GraphicsQualityLevel.Low;await Frames(20);
        Check(!sun.ShadowEnabled && !env.SsaoEnabled,"low disables costly shadow maps and SSAO");
        var blobs=_stage.GetNode<GraphicsQualityController>("GraphicsQuality").GetNode<MultiMeshInstance3D>("SimpleShadows");
        Check(blobs.Visible && blobs.Multimesh.VisibleInstanceCount>0,"low draws batched simple ground shadows");
        var nearbyTrees=((LayoutMap)_stage.Map).KitProps.Where(h=>h.GlobalPosition.DistanceTo(p.GlobalPosition)<14
            && h.GetChildren().OfType<Node3D>().Any(n=>n.SceneFilePath.Contains("forest_pine.glb")||n.SceneFilePath.Contains("forest_broadleaf.glb"))).ToArray();
        Check(nearbyTrees.Length>=2,"forest shadow sample includes nearby trees");
        foreach(var tree in nearbyTrees)
        {
            var meshes=tree.FindChildren("*",nameof(MeshInstance3D),true,false).OfType<MeshInstance3D>().ToArray();
            var box=meshes[0].GlobalTransform*meshes[0].GetAabb();
            foreach(var mesh in meshes.Skip(1))box=box.Merge(mesh.GlobalTransform*mesh.GetAabb());
            var center=box.GetCenter();center.Y=tree.GlobalPosition.Y;
            var shadow=Enumerable.Range(0,blobs.Multimesh.VisibleInstanceCount).Select(blobs.Multimesh.GetInstanceTransform)
                .OrderBy(t=>new Vector2(t.Origin.X-center.X,t.Origin.Z-center.Z).LengthSquared()).First();
            GD.Print($"TREE_SHADOW {tree.Name}: at={tree.GlobalPosition} canopy={box.Size.X:F2}x{box.Size.Z:F2} shadow={shadow.Basis.X.Length():F2}x{shadow.Basis.Z.Length():F2} ground={shadow.Origin}");
            Check(new Vector2(shadow.Origin.X-center.X,shadow.Origin.Z-center.Z).Length()<.2f
                && Mathf.Min(shadow.Basis.X.Length(),shadow.Basis.Z.Length())>=Mathf.Max(box.Size.X,box.Size.Z)*1.25f,
                "tree shadow extends beyond its placed canopy: "+tree.Name);
        }
        Check(Mathf.Abs(blobs.Multimesh.GetInstanceTransform(0).Origin.Y-1.22f)<.05f,"player shadow lands on raised forest floor");
        int before=blobs.Multimesh.VisibleInstanceCount;
        _stage.SpawnEnemy<Enemy>(_stage.CommonDefinition,new(97,1.2f,-24),awake:true);await Frames(20);
        Check(blobs.Multimesh.VisibleInstanceCount>before,"late spawned enemy gets a simple shadow");
        var lamp=new OmniLight3D{ShadowEnabled=true,Position=new(94,3,-24)};_stage.AddChild(lamp);await Frames(3);
        Check(!lamp.ShadowEnabled,"late spawned light obeys low shadows");await Shot("low");
        foreach(var tree in nearbyTrees)tree.Visible=false;
        await Frames(2);await Shot("tree-shadow-ground");
        foreach(var tree in nearbyTrees)tree.Visible=true;
        _stage.Hud.SetPaused(true);_stage.Hud.OpenGraphicsOptions();await Frames(3);
        var high=(Button)_stage.Hud.FindChild("Graphics_Shadow_High",true,false);
        var low=(Button)_stage.Hud.FindChild("Graphics_Shadow_Low",true,false);
        Check(_stage.Hud.GraphicsOpen && low.ButtonPressed && !high.ButtonPressed,"graphics page exposes mutually exclusive current state");await Shot("menu-low");
        var click=high.GetGlobalRect().GetCenter();
        Input.ParseInputEvent(new InputEventMouseMotion{Position=click,GlobalPosition=click});
        Input.ParseInputEvent(new InputEventMouseButton{Position=click,GlobalPosition=click,ButtonIndex=MouseButton.Left,Pressed=true});await Frames(2);
        Input.ParseInputEvent(new InputEventMouseButton{Position=click,GlobalPosition=click,ButtonIndex=MouseButton.Left,Pressed=false});await Frames(3);
        Check(high.ButtonPressed && !low.ButtonPressed && sun.ShadowEnabled && lamp.ShadowEnabled && env.SsaoEnabled && !blobs.Visible,"high restores authored state immediately while paused");
        ((Button)_stage.Hud.FindChild("Graphics_Surface_High",true,false)).EmitSignal(BaseButton.SignalName.Pressed);await Shot("menu-high");
        ((Button)_stage.Hud.FindChild("Graphics_Back",true,false)).EmitSignal(BaseButton.SignalName.Pressed);
        Check(!_stage.Hud.GraphicsOpen && _stage.Hud.Paused,"back returns to pause without resuming");
        Check(GetViewport().GuiGetFocusOwner()==null,"pause Enter shortcut is not intercepted by the graphics button");
        _stage.Hud.OpenGraphicsOptions();await Escape();
        Check(!_stage.Hud.GraphicsOpen && _stage.Hud.Paused,"Escape from graphics returns to pause");await Escape();
        Check(!_stage.Hud.GraphicsOpen && !_stage.Hud.Paused,"second Escape resumes and closes both menus");
        await Shot("restored");
        p.Weapons.Pickup(ArmoryCatalog.Weapon("m4"),null);p.ManualControl=false;
        int uses=0;var item=new Interactable{Position=p.Position,HoldSeconds=.1f,OnUse=_=>uses++};_stage.AddChild(item);_stage.Interactables.Add(item);
        _stage.Hud.SetPaused(true);GetTree().Paused=false; // exercise the local menu with a running world, as online
        int ammo=p.Gun.Mag;Input.ActionPress(GameInput.Fire);Input.ActionPress(GameInput.Interact);await Frames(30);
        Check(p.Gun.Mag==ammo,"menu clicks do not fire while world is running");
        Check(uses==0 && _stage.HoldProgress==0,"menu blocks E interactions and cancels hold progress in a running world");
        _stage.Hud.SetPaused(false);await Frames(20);
        Check(p.Gun.Mag==ammo,"held menu click does not fire on resume");
        Check(uses==0,"held menu E does not activate a hold action on resume");
        Input.ActionRelease(GameInput.Fire);await Frames(3);Input.ActionPress(GameInput.Fire);await Frames(20);
        Check(p.Gun.Mag<ammo,"fresh fire press works after closing menu");Input.ActionRelease(GameInput.Fire);
        Input.ActionRelease(GameInput.Interact);await Frames(3);Input.ActionPress(GameInput.Interact);await Frames(20);
        Check(uses>0,"fresh E hold works after closing menu");Input.ActionRelease(GameInput.Interact);
        p.ManualControl=true;GameSettings.ShadowQuality=GraphicsQualityLevel.Low;
        var ramp=_stage.GetChildren().OfType<StaticBody3D>().First(n=>n.HasMeta("smooth_terrain_ramp"));
        p.TeleportTo(ramp.Position+Vector3.Up*.7f);_stage.Cam.SnapTo(ramp.Position);await Frames(45);
        var rampShadow=blobs.Multimesh.GetInstanceTransform(0);
        Check(Mathf.Abs(rampShadow.Origin.Y-_stage.Tiers.GroundY(p.Position))<.06f && rampShadow.Basis.Y.DistanceTo(Vector3.Up)>.05f,
            "simple shadow projects onto sloping ramp support");await Shot("ramp-low");
        _stage.Free();await Frames(2);GameSettings.ShadowQuality=GameSettings.SurfaceQuality=GraphicsQualityLevel.High;
        _stage=new Stage{MapOverride=LayoutMap.ForStage(1),DirectorEnabled=false,SquadOverride=0,CharacterOverride="goose",ModeOverride=GameMode.Scenario};AddChild(_stage);await Frames(5);
        _stage.Player.ManualControl=true;_stage.Player.Invulnerable=true;
        foreach(var e in _stage.AllEnemies.ToArray())e.QueueFree();
        _stage.Player.TeleportTo(new(70,1.25f,-14));_stage.Cam.SnapTo(_stage.Player.Position+Vector3.Up);await Frames(25);await Shot("urban-high");
        GameSettings.ShadowQuality=GameSettings.SurfaceQuality=GraphicsQualityLevel.Low;await Frames(15);await Shot("urban-low");
        Check(_stage.GetChildren().OfType<DirectionalLight3D>().All(l=>!l.ShadowEnabled),"quality applies on a different stage after transition");
        _stage.Free();await Frames(2);
        GameSettings.Save();GameSettings.Reset();GameSettings.Path=SettingsPath;
        _stage=new GraphicsDirectLaunchStage{MapOverride=LayoutMap.ForStage(1),DirectorEnabled=false,SquadOverride=0,CharacterOverride="goose",ModeOverride=GameMode.Scenario};
        GetTree().Root.AddChild(_stage);await Frames(3);
        Check(GameSettings.Loaded && GameSettings.ShadowQuality==GraphicsQualityLevel.Low && GameSettings.SurfaceQuality==GraphicsQualityLevel.Low,
            "direct stage launch loads saved graphics without MainMenu");
        Check(_stage.GetChildren().OfType<DirectionalLight3D>().All(l=>!l.ShadowEnabled),"direct stage launch applies saved shadows during startup");
        GetTree().CurrentScene=this;
    }
}

public partial class GraphicsDirectLaunchStage : Stage
{
    public override void _Ready()
    {
        GetTree().CurrentScene=this;
        base._Ready();
    }
}
