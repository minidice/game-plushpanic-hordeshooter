using Godot;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Real loopback shots in separate worlds: host contact, owner prediction and misses.</summary>
public partial class SurfaceAudioNetCheck : Node
{
    private sealed class TestMap : StageMap
    {
        public StaticBody3D MetalWall = null!;
        public override Rect2 Area => new(-30, -20, 80, 50);
        protected override void BuildMap()
        {
            PlayerStart = new Vector3(0, 0, -6);
            Floor(-29, 49, -19, 29, "546852");
            MetalWall = Solid(new Vector3(0, 2, 3), new Vector3(14, 4, 1), "546852", drawCargo:false);
            SurfaceAudio.Tag(MetalWall, "corrugated_fence");
            Prop("sandbags", new Vector3(24, 0, 3));
        }
    }
    private Stage _host = null!, _client = null!;
    private SubViewport? _hostView, _clientView;
    private ENetMultiplayerPeer? _hostPeer, _clientPeer;
    private int _checks;
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); _checks++; GD.Print("PASS " + label); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Until(Func<bool> predicate, string label)
    {
        for (int i = 0; i < 300 && !predicate(); i++) await Frames(1);
        Check(predicate(), label);
    }
    private SubViewport View(string name)
    {
        var view = new SubViewport { Name = name, OwnWorld3D = true, Size = new Vector2I(640, 360) };
        AddChild(view); return view;
    }
    public override async void _Ready()
    {
        int exit = 0;
        try { await Run(); GD.Print("SURFACE_AUDIO_NET_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("SURFACE_AUDIO_NET_FAIL " + e.Message); exit = 1; }
        if (IsInstanceValid(_host)) _host.Net!.Ending = true;
        if (IsInstanceValid(_client)) _client.Net!.Ending = true;
        _clientView?.QueueFree(); _hostView?.QueueFree(); await Frames(3);
        _clientPeer?.Close(); _hostPeer?.Close(); _clientPeer?.Dispose(); _hostPeer?.Dispose();
        QuitHygiene.Collect(); GetTree().Quit(exit);
    }
    private static int Hits(Stage stage) => stage.Sfx.Played.Where(p => p.Key.StartsWith("surface_hit_")).Sum(p => p.Value);
    private static int Played(Stage stage, string id) => stage.Sfx.Played.GetValueOrDefault(id);
    private async Task FireOnce(Player player, Vector3 aim)
    {
        int shots = player.Weapons.ShotsFired;
        player.SetControls(Vector2.Zero, aim, true); await Frames(2);
        player.SetControls(Vector2.Zero, aim, false); await Frames(65);
        Check(player.Weapons.ShotsFired == shots + 1, "one trigger fires exactly one round");
    }
    private async Task Run()
    {
        CodecAndCompatibility();
        int port = 31000 + (int)(Time.GetTicksMsec() % 1000);
        _hostView = View("HostView"); _clientView = View("ClientView");
        var hostApi = MultiplayerApi.CreateDefaultInterface(); GetTree().SetMultiplayer(hostApi, _hostView.GetPath());
        _hostPeer = new ENetMultiplayerPeer(); _hostPeer.SetBindIP("127.0.0.1");
        Check(_hostPeer.CreateServer(port, 2) == Error.Ok, "loopback host listens"); hostApi.MultiplayerPeer = _hostPeer;
        var hostMap = new TestMap();
        _host = new Stage { Name="Stage", CharacterOverride="goose", ThemeOverride="zombie", Role=NetRole.Host,
            DirectorEnabled=false, SquadOverride=0, ModeOverride=GameMode.Scenario, MapOverride=hostMap };
        _hostView.AddChild(_host); await Frames(5); _host.Player.ManualControl=true; _host.Player.Invulnerable=true;
        var clientApi = MultiplayerApi.CreateDefaultInterface(); GetTree().SetMultiplayer(clientApi, _clientView.GetPath());
        _clientPeer = new ENetMultiplayerPeer(); Check(_clientPeer.CreateClient("127.0.0.1",port)==Error.Ok,"client connects"); clientApi.MultiplayerPeer=_clientPeer;
        await Until(()=>_clientPeer.GetConnectionStatus()==MultiplayerPeer.ConnectionStatus.Connected,"ENet connected");
        var clientMap = new TestMap();
        _client = new Stage { Name="Stage", CharacterOverride="duck", ThemeOverride="zombie", Role=NetRole.Client,
            DirectorEnabled=false, SquadOverride=0, ModeOverride=GameMode.Scenario, MapOverride=clientMap };
        _clientView.AddChild(_client); _client.Player.ManualControl=true; _client.Player.Invulnerable=true;
        await Until(()=>_client.Net!.Started && _host.Net!.Peers.Count==2,"both peers ready");
        Check(_host.GetWorld3D().Space != _client.GetWorld3D().Space,"isolated physics worlds");
        _client.Player.TeleportTo(new Vector3(40,0,-6)); await Frames(10);
        var rifle=ArmoryCatalog.Weapon("m4"); _host.Player.Weapons.Pickup(rifle,null);
        await Frames(12);
        await FireOnce(_host.Player,new Vector3(0,Player.AimHeight,3));
        Check(Played(_host,"surface_hit_metal")==1,"host rifle resolves metal once");
        Check(Played(_client,"surface_hit_metal")==1,"remote rifle contact reaches client once");
        Check(Played(_client,rifle.ShotSound)==1,"remote rifle shot sound remains once");

        // Deliberately divergent prediction: only the authoritative material should be audible.
        clientMap.MetalWall.SetMeta(SurfaceAudio.ProfileMeta,"wood_solid");
        _host.Player.TeleportTo(new Vector3(40,0,-6)); _client.Player.TeleportTo(new Vector3(0,0,-6));
        _host.Players.Single(p=>p.IsRemote).Weapons.Pickup(rifle,null); _client.Player.Weapons.Pickup(rifle,null);
        await Frames(15);
        int ownShots=Played(_client,rifle.ShotSound), ownHits=Played(_client,"surface_hit_metal");
        await FireOnce(_client.Player,new Vector3(0,Player.AimHeight,3));
        Check(Played(_client,rifle.ShotSound)==ownShots+1,"own predicted shot sound is not replayed");
        Check(Played(_client,"surface_hit_metal")==ownHits+1 && Played(_client,"surface_hit_wood")==0,
            "own contact uses host material exactly once despite divergent prediction");

        int hostHits=Hits(_host), clientHits=Hits(_client);
        await FireOnce(_client.Player,new Vector3(0,Player.AimHeight,-15));
        Check(Hits(_host)==hostHits && Hits(_client)==clientHits,"miss endpoints create no surface sound");

        _client.Player.TeleportTo(new Vector3(40,0,-6)); _host.Player.TeleportTo(new Vector3(-15,0,-6));
        var enemy = _host.SpawnEnemy<Enemy>(_host.CommonDefinition,new Vector3(-15,0,0),awake:false);
        enemy.SetPhysicsProcess(false); await Frames(15);
        float health = enemy.Health;
        await FireOnce(_host.Player,enemy.GlobalPosition + Vector3.Up * Player.AimHeight);
        Check(enemy.Health < health,"enemy test round applies real damage");
        Check(Hits(_host)==hostHits && Hits(_client)==clientHits,"enemy hit does not invent a surface contact");
        enemy.QueueFree(); await Frames(3);

        _host.Player.TeleportTo(new Vector3(24,0,-6)); await Frames(12);
        var sandbag = _host.Map.KitProps.OfType<ObjectBody>().Single();
        float sandHealth=sandbag.Health;
        await FireOnce(_host.Player,new Vector3(24,Player.AimHeight,3));
        Check(sandbag.Health < sandHealth && sandbag.Interaction.Cover,"cover keeps real damage and blocking contract");
        Check(Played(_host,"surface_hit_soft")==1 && Played(_client,"surface_hit_soft")==1,"cover screen transmits owner's soft material");

        _host.Player.TeleportTo(new Vector3(0,0,-6)); _host.Player.Weapons.Pickup(ArmoryCatalog.Weapon("r870"),null);
        await Frames(15); hostHits=Hits(_host); clientHits=Hits(_client);
        Check(_host.Player.Weapons.Current.Def.Pellets>3,"shotgun fixture has more than three pellets");
        await FireOnce(_host.Player,new Vector3(0,Player.AimHeight,3));
        Check(Hits(_host)==hostHits+3 && Hits(_client)==clientHits+3,"shotgun shares maximum three contact sounds locally and remotely");
        Check(_client.Net!.ApplyErrors==0,"shot events apply without errors");
    }

    private void CodecAndCompatibility()
    {
        var profile=SurfaceProfiles.Current.Profiles["metal_hollow"];
        var hits=new[] {new NetSurfaceHit(profile,new Vector3(12.34f,1.25f,-4.56f)),
            new NetSurfaceHit(SurfaceProfiles.Current.Profiles["sandbag"],new Vector3(-2,3,4)),
            new NetSurfaceHit(SurfaceProfiles.Current.Profiles["grass"],new Vector3(1,0,2))};
        var writer=new NetWriter(); NetSurfaceHit.Write(writer,hits);
        Check(writer.Length==25,"three contacts need only 25 bytes");
        var reader=new NetReader(writer.ToArray()); var decoded=NetSurfaceHit.Read(reader);
        Check(decoded.Length==3 && decoded[0].Profile.Id=="metal_hollow" && decoded[0].Position.DistanceTo(new Vector3(12.34f,1.25f,-4.56f))<.001f
            && decoded[1].Profile.Id=="sandbag" && decoded[2].Profile.Id=="grass" && !reader.More,"wire roundtrip retains host material and centimeter position");
        Check(decoded[0].Profile.HitDb==-2,"preset gain survives network lookup");
        writer=new NetWriter(); NetSurfaceHit.Write(writer,null);
        Check(writer.Length==1 && NetSurfaceHit.Read(new NetReader(writer.ToArray())).Length==0,"non-contact shots send only zero count");
        foreach (var (bytes,label) in new[] {
            (new byte[]{4},"over-budget contact count"),
            (new byte[]{1,0,0},"truncated contact"),
            (new byte[]{1,255,255,0,0,0,0,0,0},"unknown profile") })
        {
            bool rejected=false;
            try { NetSurfaceHit.Read(new NetReader(bytes)); } catch(FormatException) { rejected=true; }
            Check(rejected,label+" rejected before replay");
        }
        bool oversize=false;
        try { NetSurfaceHit.Write(new NetWriter(),new[]{hits[0],hits[0],hits[0],hits[0]}); }
        catch(ArgumentOutOfRangeException) { oversize=true; }
        Check(oversize,"sender cannot serialize over-budget shot");
        string rules=FileAccess.GetFileAsString(ObjectInteractions.DataPath), placements=FileAccess.GetFileAsString(DestructionPlacements.Path);
        string surfaces=FileAccess.GetFileAsString(SurfaceProfiles.DataPath);
        string lf=surfaces.Replace("\r\n","\n").Replace('\r','\n');
        int hash=NetIds.DataHash(rules,placements,surfacesJson:lf);
        Check(hash==NetIds.DataHash() && hash==NetIds.DataHash(rules,placements,surfacesJson:lf.Replace("\n","\r\n")),"surface compatibility accepts cross-platform line endings");
        var changed=System.Text.Json.Nodes.JsonNode.Parse(lf)!;
        string canonical=changed.ToJsonString();
        int canonicalHash=NetIds.DataHash(rules,placements,surfacesJson:canonical);
        changed["profiles"]!["metal_hollow"]!["hit_db"]=-1;
        Check(NetIds.DataHash(rules,placements,surfacesJson:changed.ToJsonString())!=canonicalHash,"surface gain change rejects incompatible peer data");
        changed=System.Text.Json.Nodes.JsonNode.Parse(canonical)!;
        changed["objects"]!["sandbags"]="concrete";
        Check(NetIds.DataHash(rules,placements,surfacesJson:changed.ToJsonString())!=canonicalHash,"object mapping change rejects incompatible peer data");
        Check(FileAccess.GetFileAsString(SurfaceProfiles.DataPath)==surfaces,"compatibility tests preserve source data");
    }
}

