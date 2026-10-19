using Godot;
using System;

public partial class Field : Node3D
{
    public Goose Player {get;private set;}=null!;
    public TargetDummy OpenTarget {get;private set;}=null!;
    public TargetDummy CoverTarget {get;private set;}=null!;
    private Camera3D _camera=null!;
    private Label _status=null!;
    private Label _hint=null!;
    private int _shots;
    private bool _close;
    private float _hazard;
    public static StandardMaterial3D Material(string color)=>new(){AlbedoColor=new Color(color),Roughness=.85f};
    public static MeshInstance3D Box(Node parent,Vector3 position,Vector3 size,Material material)
    {
        var mesh=new MeshInstance3D{Mesh=new BoxMesh{Size=size},Position=position,MaterialOverride=material};parent.AddChild(mesh);return mesh;
    }
    private void Solid(string name,Vector3 position,Vector3 size,string color)
    {
        var body=new StaticBody3D{Name=name,Position=position,CollisionLayer=1};AddChild(body);
        body.AddChild(new CollisionShape3D{Shape=new BoxShape3D{Size=size}});Box(body,Vector3.Zero,size,Material(color));
    }
    public override void _Ready()
    {
        var env=new WorldEnvironment{Environment=new Godot.Environment{BackgroundMode=Godot.Environment.BGMode.Color,BackgroundColor=new Color("192528"),AmbientLightSource=Godot.Environment.AmbientSource.Color,AmbientLightColor=new Color("c6dddf"),AmbientLightEnergy=.35f,ReflectedLightSource=Godot.Environment.ReflectionSource.Disabled,TonemapMode=Godot.Environment.ToneMapper.Agx,SsaoEnabled=true,TonemapExposure=1}};AddChild(env);
        AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-55,-35,0),LightColor=new Color("ffdfae"),LightEnergy=1.0f,LightAngularDistance=3,ShadowEnabled=true,DirectionalShadowMaxDistance=45});
        AddChild(new DirectionalLight3D{RotationDegrees=new Vector3(-30,140,0),LightColor=new Color("adcdd9"),LightEnergy=.25f});
        Solid("Floor",new Vector3(0,-.2f,0),new Vector3(22,.4f,20),"60675e");
        var stripe=Material("71776b");
        for(int x=-10;x<=10;x+=2)Box(this,new Vector3(x,.003f,0),new Vector3(.018f,.005f,20),stripe);
        for(int z=-10;z<=10;z+=2)Box(this,new Vector3(0,.003f,z),new Vector3(22,.005f,.018f),stripe);
        Solid("North",new Vector3(0,1,-10),new Vector3(22,2,.35f),"354744");
        Solid("South",new Vector3(0,.5f,10),new Vector3(22,1,.35f),"354744");
        Solid("East",new Vector3(11,.75f,0),new Vector3(.35f,1.5f,20),"354744");
        Solid("West",new Vector3(-11,.75f,0),new Vector3(.35f,1.5f,20),"354744");
        Solid("Cover",new Vector3(3,.8f,0),new Vector3(1,1.6f,3.8f),"3e514b");
        Solid("Crate",new Vector3(-4,.5f,3.5f),new Vector3(1.6f,1,1.6f),"8a7755");
        Solid("CrateStack",new Vector3(-5.6f,.8f,3.5f),new Vector3(1.4f,1.6f,1.4f),"736749");
        for(int i=0;i<3;i++)Box(this,new Vector3(3,1.61f,-1.25f+i*1.2f),new Vector3(1.04f,.03f,.17f),Material("d6bc70"));
        OpenTarget=new TargetDummy{Name="OpenTarget",Position=new Vector3(-5,0,-5)};AddChild(OpenTarget);
        CoverTarget=new TargetDummy{Name="CoverTarget",Position=new Vector3(6,0,0),RotationDegrees=new Vector3(0,-90,0)};AddChild(CoverTarget);
        AddChild(new TargetDummy{Name="FarTarget",Position=new Vector3(1,0,-7)});
        // An obvious test pad provides world-driven damage, in addition to the H key.
        Box(this,new Vector3(-7,.015f,6),new Vector3(2.5f,.025f,2.5f),Material("a4583f"));
        var sign=new Label3D{Text="HIT ZONE",Position=new Vector3(-7,.06f,6),RotationDegrees=new Vector3(-90,0,0),FontSize=48,PixelSize=.012f,Modulate=new Color("ffdf9d")};AddChild(sign);
        Player=GD.Load<PackedScene>("res://scenes/Goose.tscn").Instantiate<Goose>();AddChild(Player);Player.Shot+=Trace;
        _camera=new Camera3D{Projection=Camera3D.ProjectionType.Orthogonal,Size=18,Current=true,Far=100};AddChild(_camera);PlaceCamera(Vector3.Zero);
        BuildHud();
        if(OS.GetCmdlineUserArgs().Length>0&&Array.Exists(OS.GetCmdlineUserArgs(),a=>a=="--capture"))Capture();
    }
    private void BuildHud()
    {
        var layer=new CanvasLayer();AddChild(layer);
        var top=new ColorRect{Color=new Color(.045f,.075f,.08f,.91f),Position=new Vector2(24,20),Size=new Vector2(390,97),MouseFilter=Control.MouseFilterEnum.Ignore};layer.AddChild(top);
        var title=new Label{Text="GOOSE / FIELD TEST 01",Position=new Vector2(42,30)};title.AddThemeFontSizeOverride("font_size",25);title.AddThemeColorOverride("font_color",new Color("e5d9ad"));layer.AddChild(title);
        _status=new Label{Position=new Vector2(43,70)};layer.AddChild(_status);
        var bottom=new ColorRect{Color=new Color(.045f,.075f,.08f,.92f),Position=new Vector2(24,710),Size=new Vector2(1232,70),MouseFilter=Control.MouseFilterEnum.Ignore};layer.AddChild(bottom);
        _hint=new Label{Position=new Vector2(42,720),Text="WASD  Move     MOUSE  Aim     LMB  Fire     H  Hit     K  Knockout     R  Respawn     TAB  Zoom\nShoot the orange targets. Cover blocks bullets. Orange pad deals damage."};_hint.AddThemeFontSizeOverride("font_size",17);layer.AddChild(_hint);
        var badge=new Label{Text="LOCAL PLAYTEST\n55° / QUARTER VIEW",Position=new Vector2(1030,34),HorizontalAlignment=HorizontalAlignment.Right};badge.AddThemeColorOverride("font_color",new Color("d6dabc"));layer.AddChild(badge);
    }
    private void PlaceCamera(Vector3 focus)
    {
        var angle=Mathf.DegToRad(55);_camera.Position=focus+new Vector3(0,Mathf.Sin(angle)*24,Mathf.Cos(angle)*24);_camera.LookAt(focus);_camera.Size=_close?7.5f:18;
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(input is InputEventKey key&&key.Pressed&&!key.Echo&&key.PhysicalKeycode==Key.Tab)_close=!_close;
    }
    public override void _Process(double delta)
    {
        var focus=Player.Corpse?.GlobalPosition??Player.GlobalPosition+Vector3.Up*.8f;
        var currentFocus=_camera.Position-new Vector3(0,Mathf.Sin(Mathf.DegToRad(55))*24,Mathf.Cos(Mathf.DegToRad(55))*24);
        PlaceCamera(currentFocus.Lerp(focus,1-Mathf.Exp(-(float)delta*5)));
        _status.Text=$"HP {Player.Health:000} / 100    •    SHOTS {_shots:000}    •    {(Player.IsDead?"DOWN — R TO RESPAWN":"READY")}";
    }
    public override void _PhysicsProcess(double delta)
    {
        _hazard-=(float)delta;
        var p=Player.GlobalPosition;
        if(!Player.IsDead&&Math.Abs(p.X+7)<1.25&&Math.Abs(p.Z-6)<1.25&&_hazard<=0){Player.TakeDamage(25,Vector3.Right);_hazard=1;}
    }
    private void Trace(Vector3 from,Vector3 to)
    {
        _shots++;var length=from.DistanceTo(to);if(length<.01)return;
        var mat=new StandardMaterial3D{ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded,AlbedoColor=new Color("ffe5a0")};
        var beam=Box(this,(from+to)*.5f,new Vector3(.025f,.025f,length),mat);beam.LookAt(to);
        GetTree().CreateTimer(.055).Timeout+=()=>{if(IsInstanceValid(beam))beam.QueueFree();};
    }
    private async void Capture()
    {
        Player.ManualControl=true;Player.SetControls(Vector2.Zero,new Vector3(4,0,5),false);
        for(int i=0;i<30;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        System.IO.Directory.CreateDirectory(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/gameplay.png"));
        _close=true;
        for(int i=0;i<10;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/close.png"));
        GD.Print("GOOSE_CAPTURE_SAVED");GetTree().Quit();
    }
}
