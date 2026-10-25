using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Real ENet welcome/live spawn events in isolated worlds; identity is fixed before client _Ready.</summary>
public partial class CommonRosterNetCheck : Node
{
    private sealed class TestMap : StageMap
    {
        public override Rect2 Area => new(-20,-20,40,40);
        protected override void BuildMap() { PlayerStart=new Vector3(-10,0,-10); Floor(-19,19,-19,19,"546852"); }
    }
    private Stage _host=null!, _client=null!;
    private SubViewport? _hostView,_clientView;
    private ENetMultiplayerPeer? _hostPeer,_clientPeer;
    private int _checks;
    private void Check(bool value,string label) { if(!value) throw new Exception(label);_checks++;GD.Print("PASS "+label); }
    private async Task Frames(int n) { for(int i=0;i<n;i++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame); }
    private async Task Until(Func<bool> predicate,string label) { for(int i=0;i<300&&!predicate();i++) await Frames(1);Check(predicate(),label); }
    private SubViewport View(string name) { var view=new SubViewport { Name=name,OwnWorld3D=true,Size=new Vector2I(640,360) };AddChild(view);return view; }
    public override async void _Ready()
    {
        int exit=0;
        try { await Run();GD.Print("COMMON_ROSTER_NET_CHECK_PASS "+_checks); }
        catch(Exception e) { GD.PushError("COMMON_ROSTER_NET_CHECK_FAIL "+e);exit=1; }
        if(IsInstanceValid(_host))_host.Net!.Ending=true;
        if(IsInstanceValid(_client))_client.Net!.Ending=true;
        _clientView?.QueueFree();_hostView?.QueueFree();await Frames(3);
        _clientPeer?.Close();_hostPeer?.Close();_clientPeer?.Dispose();_hostPeer?.Dispose();
        QuitHygiene.Collect();GetTree().Quit(exit);
    }
    private async Task Run()
    {
        int port=30000+(int)(Time.GetTicksMsec()%2000);
        _hostView=View("HostView");_clientView=View("ClientView");
        var hostApi=MultiplayerApi.CreateDefaultInterface();GetTree().SetMultiplayer(hostApi,_hostView.GetPath());
        _hostPeer=new ENetMultiplayerPeer();_hostPeer.SetBindIP("127.0.0.1");
        Check(_hostPeer.CreateServer(port,2)==Error.Ok,"loopback host listens");hostApi.MultiplayerPeer=_hostPeer;
        _host=new Stage {Name="Stage",CharacterOverride="goose",ThemeOverride="armed",Role=NetRole.Host,DirectorEnabled=false,SquadOverride=0,MapOverride=new TestMap()};
        _hostView.AddChild(_host);_host.Player.ManualControl=true;_host.Player.Invulnerable=true;
        var before=new[] {
            new CommonIdentity("zombie","human","ochre","unarmed",9),
            new CommonIdentity("hunter","eagle","red","woodbow",2),
            new CommonIdentity("armed","rhino","green","mp5",5,CommonGear.Vest|CommonGear.Glasses),
            new CommonIdentity("armed","tiger","ochre","ak47",6),
        };
        for(int i=0;i<before.Length;i++) { var e=_host.SpawnCommonIdentity(before[i],new Vector3(i*3,0,4),false);e.SetPhysicsProcess(false); }
        var named=_host.Spawn(MonsterDatabase.Get("armed_veteran"),new Vector3(0,0,10),false);named.SetPhysicsProcess(false);
        await Frames(5);
        var clientApi=MultiplayerApi.CreateDefaultInterface();GetTree().SetMultiplayer(clientApi,_clientView.GetPath());
        _clientPeer=new ENetMultiplayerPeer();Check(_clientPeer.CreateClient("127.0.0.1",port)==Error.Ok,"client connects");clientApi.MultiplayerPeer=_clientPeer;
        await Until(()=>_clientPeer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected,"ENet connection established");
        _client=new Stage {Name="Stage",CharacterOverride="duck",ThemeOverride="armed",Role=NetRole.Client,DirectorEnabled=false,SquadOverride=0,MapOverride=new TestMap()};
        _clientView.AddChild(_client);_client.Player.ManualControl=true;
        await Until(()=>_client.Net!.Started&&_client.AllEnemies.Count()==before.Length+1,"late join receives all birth identities");
        Check(_host.GetWorld3D().Space!=_client.GetWorld3D().Space,"separate physical worlds");
        foreach(var hostEnemy in _host.AllEnemies) Compare(hostEnemy);
        var after=new CommonIdentity("armed","reindeer","red","m4",7,CommonGear.Glasses);
        var live=_host.SpawnCommonIdentity(after,new Vector3(12,0,7),false);live.SetPhysicsProcess(false);
        await Until(()=>_client.AllEnemies.Any(e=>e.NetId==live.NetId),"live spawn arrives after welcome");Compare(live);
        var hunter=_host.SpawnCommonIdentity(new("hunter","parrot","green","knife",4),new Vector3(-3,0,6),false);hunter.SetPhysicsProcess(false);
        await Until(()=>_client.AllEnemies.Any(e=>e.NetId==hunter.NetId),"melee hunter live spawn arrives");Compare(hunter);
        Check(_client.Net!.ApplyErrors==0,"spawn/welcome applied without decode errors");
        Check(_client.AllEnemies.Single(e=>e.NetId==named.NetId).Monster.CommonIdentity==null,"named veteran keeps legacy identity");
        Check(live.Monster.CommonIdentity==after,"birth identity unchanged after peer joins");
        await Frames(20);Check(live.Monster.CommonIdentity==after,"snapshots do not reroll identity");
    }
    private void Compare(Enemy host)
    {
        var remote=_client.AllEnemies.Single(e=>e.NetId==host.NetId);
        Check(remote.IsPuppet&&remote.Monster.CommonIdentity==host.Monster.CommonIdentity,"puppet identity matches #"+host.NetId);
        Check(remote.GetType()==host.GetType()&&remote.Definition.Id==host.Definition.Id,"AI and actual model match #"+host.NetId);
        Check(remote.Monster.Hp==host.Monster.Hp&&remote.Monster.MoveSpeed==host.Monster.MoveSpeed&&remote.Monster.AttackDamage==host.Monster.AttackDamage,"composed combat matches #"+host.NetId);
        if(host.Monster.CommonIdentity is not { } id)return;
        foreach(var module in new[]{"Gear_Vest","Gear_Glasses","Outfit_Shirt","Gear_Eyepatch"})
        {
            var a=host.Rig.Model.FindChild(module,true,false) as Node3D;
            var b=remote.Rig.Model.FindChild(module,true,false) as Node3D;
            Check(a?.Visible==b?.Visible,"module visible state matches "+module);
        }
        Check(id.Weapon=="unarmed"||remote.Rig.Weapon.FindChild("Weapon_"+id.Weapon,true,false)!=null,"selected weapon present before puppet initialization");
    }
}
