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
            new CommonIdentity("armed","rhino","green","mp5",5,CommonGear.Vest),
            new CommonIdentity("armed","tiger","ochre","ak47",6),
            new CommonIdentity("hunter","lion","ochre","club",9),
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
        var after=new CommonIdentity("armed","reindeer","red","m4",7,CommonGear.None);
        var live=_host.SpawnCommonIdentity(after,new Vector3(12,0,7),false);live.SetPhysicsProcess(false);
        await Until(()=>_client.AllEnemies.Any(e=>e.NetId==live.NetId),"live spawn arrives after welcome");Compare(live);
        var hunter=_host.SpawnCommonIdentity(new("hunter","parrot","green","knife",4),new Vector3(-3,0,6),false);hunter.SetPhysicsProcess(false);
        await Until(()=>_client.AllEnemies.Any(e=>e.NetId==hunter.NetId),"melee hunter live spawn arrives");Compare(hunter);
        Check(_client.Net!.ApplyErrors==0,"spawn/welcome applied without decode errors");
        Check(_client.AllEnemies.Single(e=>e.NetId==named.NetId).Monster.CommonIdentity==null,"named veteran keeps legacy identity");
        Check(live.Monster.CommonIdentity==after,"birth identity unchanged after peer joins");
        await Frames(20);Check(live.Monster.CommonIdentity==after,"snapshots do not reroll identity");
        await HunterPresentation(hunter);
    }

    private int Heard(string id) => _client.Sfx.Played.TryGetValue(id,out int count)?count:0;
    private static bool AttachedSoundPlaying(Enemy enemy) => enemy.GetChildren().OfType<AudioStreamPlayer3D>().Any(p=>p.Playing);
    private static int DurationFrames(float seconds) => Mathf.CeilToInt(seconds*Engine.PhysicsTicksPerSecond)+3;

    private async Task HunterPresentation(Enemy knife)
    {
        knife.GlobalPosition=new Vector3(-7,.05f,-7);
        var club=_host.SpawnCommonIdentity(new("hunter","eagle","red","club"),new Vector3(-5,.05f,-7),false);
        club.SetPhysicsProcess(false);
        await Until(()=>_client.AllEnemies.Any(e=>e.NetId==club.NetId),"club hunter live spawn arrives for presentation checks");
        await Frames(20);
        foreach(var hunter in new[]{club,knife}) await HunterMeleeEvents(hunter);
        await HunterBowEvents((RangedEnemy)_host.AllEnemies.Single(e=>e.Monster.CommonIdentity?.Weapon=="woodbow"));
        Check(_client.Net!.ApplyErrors==0&&_host.Net!.ApplyErrors==0,"hunter attack events apply without decode errors on either peer");
    }

    private async Task HunterMeleeEvents(Enemy host)
    {
        var remote=_client.AllEnemies.Single(e=>e.NetId==host.NetId);
        string weapon=host.Monster.CommonIdentity!.Weapon;
        string prefix="hunter_"+weapon;
        string readyId=prefix+(weapon=="club"?"_raise":"_ready");
        int ready=Heard(readyId),swing=Heard(prefix+"_swing"),hit=Heard(prefix+"_hit");
        var idle=remote.Rig.Weapon.Transform;
        // Simulated authoritative decisions, transported over real ENet. Damage authority is covered
        // by HunterCombatCheck; these assertions exercise the actual event decoder and puppet timeline.
        _host.Net!.OnEnemyAttack(host,NetAttack.Melee,null,host.GlobalPosition+Vector3.Back);
        await Until(()=>remote.IsMeleeAttacking,weapon+" simulated server start reaches puppet");
        Check(Heard(readyId)==ready+1,weapon+" puppet plays ready cue exactly once");
        await Frames(3);
        Check(!remote.Rig.Weapon.Transform.IsEqualApprox(idle),weapon+" puppet applies shared anticipation pose");
        await Until(()=>Heard(prefix+"_swing")==swing+1,weapon+" puppet timeline plays swing cue");
        Check(Heard(prefix+"_hit")==hit,weapon+" timeline alone does not invent contact");
        _host.Net.OnHunterMeleeImpact(host,host.GlobalPosition+Vector3.Back+Vector3.Up*.8f);
        await Until(()=>Heard(prefix+"_hit")==hit+1,weapon+" simulated confirmed-contact event plays one impact cue");
        await Until(()=>!remote.IsMeleeAttacking,weapon+" puppet finishes recovery");
        Check(Heard(readyId)==ready+1&&Heard(prefix+"_swing")==swing+1&&Heard(prefix+"_hit")==hit+1,
            weapon+" recovery does not repeat ready swing or impact");

        _host.Net.OnEnemyAttack(host,NetAttack.Melee,null,host.GlobalPosition+Vector3.Back);
        await Until(()=>remote.IsMeleeAttacking,weapon+" second simulated start reaches puppet");
        Check(AttachedSoundPlaying(remote),weapon+" cancellation starts with a live attached ready emitter");
        _host.Net.OnEnemyAttackEnd(host);
        await Until(()=>!remote.IsMeleeAttacking,weapon+" server end cancels puppet attack");
        Check(!AttachedSoundPlaying(remote),weapon+" cancellation stops attached ready emitter");
        Check(remote.Rig.Weapon.Transform.IsEqualApprox(idle),weapon+" cancellation restores idle weapon immediately");
        await Frames(DurationFrames(host.Monster.MeleeWindup+Enemy.MeleeRecovery));
        Check(Heard(prefix+"_swing")==swing+1&&Heard(prefix+"_hit")==hit+1,
            weapon+" cancelled timeline never produces a later swing or hit");
    }

    private async Task HunterBowEvents(RangedEnemy host)
    {
        var remote=(RangedEnemy)_client.AllEnemies.Single(e=>e.NetId==host.NetId);
        host.GlobalPosition=new Vector3(-4,.05f,-10);
        host.Rig.Face(_host.Player.GlobalPosition-host.GlobalPosition);
        await Frames(20);
        var arrow=(Node3D)remote.Rig.Weapon.FindChild("BowNockedArrow",true,false);
        var nock=(Node3D)remote.Rig.Weapon.FindChild("BowNock",true,false);
        int draw=Heard("hunter_bow_draw"),release=Heard("hunter_bow_release");
        host.StartWindup(_host.Player);
        await Until(()=>remote.WindingUp,"actual host bow windup reaches puppet over ENet");
        Check(Heard("hunter_bow_draw")==draw+1,"puppet bow draws with one dedicated cue");
        await Frames(DurationFrames(host.Monster.Ranged!.Windup+.10f));
        Check(arrow.Visible&&nock.Position.Z<-.30f,"puppet keeps arrow nocked and string drawn while host timeline is frozen");
        Check(Heard("hunter_bow_release")==release&&host.ProjectilesFired==0,"puppet timer alone cannot release a projectile or sound");

        // Resume real authoritative AI: the projectile and following end event use production order.
        host.SetPhysicsProcess(true);
        await Until(()=>host.ProjectilesFired==1,"actual host bow fires its projectile");
        host.SetPhysicsProcess(false);
        await Until(()=>!arrow.Visible,"actual projectile event hides puppet nocked arrow");
        Check(Heard("hunter_bow_release")==release+1,"actual projectile event plays exactly one puppet release cue");
        Check(!remote.WindingUp&&!AttachedSoundPlaying(remote),"projectile/end ordering clears telegraph and draw emitter");
        await Frames(3);Check(!arrow.Visible,"following attack-end event preserves visible release recovery");
        await Until(()=>arrow.Visible,"puppet bow renocks after release recovery");
        Check(nock.Position.IsEqualApprox(new Vector3(0,0,.08f)),"recovered puppet string returns to idle");

        host.StartWindup(_host.Player);
        await Until(()=>remote.WindingUp,"second actual host bow windup reaches puppet");
        Check(AttachedSoundPlaying(remote),"bow cancellation starts with a live draw emitter");
        host.Stun(2);
        await Until(()=>!remote.WindingUp,"actual host stun sends bow cancellation over ENet");
        Check(!AttachedSoundPlaying(remote)&&arrow.Visible&&nock.Position.IsEqualApprox(new Vector3(0,0,.08f)),
            "bow cancellation stops draw emitter and immediately restores nocked idle pose");
        await Frames(DurationFrames(host.Monster.Ranged.Windup+RangedEnemy.HunterBowRecovery));
        Check(Heard("hunter_bow_release")==release+1&&host.ProjectilesFired==1,"cancelled network bow never invents another release");
    }

    private void Compare(Enemy host)
    {
        var remote=_client.AllEnemies.Single(e=>e.NetId==host.NetId);
        Check(remote.IsPuppet&&remote.Monster.CommonIdentity==host.Monster.CommonIdentity,"puppet identity matches #"+host.NetId);
        Check(remote.GetType()==host.GetType()&&remote.Definition.Id==host.Definition.Id,"AI and actual model match #"+host.NetId);
        Check(remote.Monster.Hp==host.Monster.Hp&&remote.Monster.MoveSpeed==host.Monster.MoveSpeed&&remote.Monster.AttackDamage==host.Monster.AttackDamage,"composed combat matches #"+host.NetId);
        if(host.Monster.CommonIdentity is not { } id)return;
        foreach (string brow in new[] { "common_brow_left", "common_brow_right" })
        {
            var a = host.Rig.Head?.GetNodeOrNull<MeshInstance3D>(brow);
            var b = remote.Rig.Head?.GetNodeOrNull<MeshInstance3D>(brow);
            bool supported = id.Species is "parrot" or "eagle" or "lion" or "reindeer";
            Check(supported ? a != null && b != null && a.Transform.IsEqualApprox(b.Transform)
                && a.Mesh.GetFaces().SequenceEqual(b.Mesh.GetFaces()) : a == null && b == null,
                "birth eyebrow geometry and transform match " + id.Species + " " + brow);
        }
        foreach(var module in new[]{"Gear_Vest","Gear_Glasses","Outfit_Shirt","Gear_Eyepatch"})
        {
            var a=host.Rig.Model.FindChild(module,true,false) as Node3D;
            var b=remote.Rig.Model.FindChild(module,true,false) as Node3D;
            Check(a?.Visible==b?.Visible,"module visible state matches "+module);
        }
        Check(id.Weapon=="unarmed"||remote.Rig.Weapon.FindChild("Weapon_"+id.Weapon,true,false)!=null,"selected weapon present before puppet initialization");
    }
}
