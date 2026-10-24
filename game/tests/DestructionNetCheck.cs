using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Two isolated physics worlds joined only over loopback ENet: late join, live destruction, idempotent world state.</summary>
public partial class DestructionNetCheck : Node
{
    private sealed class TestMap : StageMap
    {
        public readonly ObjectBody[] Walls = new ObjectBody[2];
        public readonly ExplosiveBarrel[] Barrels = new ExplosiveBarrel[2];
        public readonly BreakableWallVisual[] Visuals = new BreakableWallVisual[2];
        public Interactable Marker = null!;
        public override Rect2 Area => new(-6, -9, 28, 18);
        protected override void BuildMap()
        {
            PlayerStart = new Vector3(-3, 0, -6);
            Floor(-5, 21, -8, 8, "546852");
            for (int i = 0; i < 2; i++)
            {
                var at = new Vector3(i * 12, 0, 0);
                Walls[i] = BreachWall(at);
                Visuals[i] = Walls[i].GetChildren().OfType<BreakableWallVisual>().Single();
                Barrels[i] = ExplosiveBarrel.Create(_stage, at + Vector3.Back * 1.5f);
            }
            // Changing this existing item's bit requests a fresh real World event without touching production APIs.
            Marker = Use(new Vector3(-3, 0, 6), "ui_use_ammo", () => { });
        }
    }

    private Stage _host = null!, _client = null!;
    private SubViewport? _hostView, _clientView;
    private ENetMultiplayerPeer? _hostPeer, _clientPeer;
    private int _checks;
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); _checks++; GD.Print("PASS " + label); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Until(Func<bool> predicate, string label, int frames = 300)
    {
        for (int i = 0; i < frames && !predicate(); i++) await Frames(1);
        Check(predicate(), label);
    }
    private SubViewport View(string name)
    {
        var view = new SubViewport { Name = name, OwnWorld3D = true, Size = new Vector2I(640, 360) };
        AddChild(view);
        return view;
    }

    public override async void _Ready()
    {
        int exit = 0;
        try { await Run(); GD.Print("DESTRUCTION_NET_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("DESTRUCTION_NET_CHECK_FAIL " + e.Message); exit = 1; }
        if (IsInstanceValid(_host)) _host.Net!.Ending = true;
        if (IsInstanceValid(_client)) _client.Net!.Ending = true;
        _clientView?.QueueFree();
        _hostView?.QueueFree();
        await Frames(3);
        _clientPeer?.Close();
        _hostPeer?.Close();
        _clientPeer?.Dispose();
        _hostPeer?.Dispose();
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    private async Task Run()
    {
        int port = 28000 + (int)(Time.GetTicksMsec() % 2000);
        _hostView = View("HostView");
        _clientView = View("ClientView");
        var hostApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(hostApi, _hostView.GetPath());
        _hostPeer = new ENetMultiplayerPeer();
        _hostPeer.SetBindIP("127.0.0.1");
        Check(_hostPeer.CreateServer(port, 2) == Error.Ok, "host listens only on loopback UDP " + port);
        hostApi.MultiplayerPeer = _hostPeer;
        var hostMap = new TestMap();
        _host = new Stage { Name = "Stage", CharacterOverride = "goose", ThemeOverride = "zombie", Role = NetRole.Host,
            DirectorEnabled = false, SquadOverride = 0, ModeOverride = GameMode.Scenario, MapOverride = hostMap };
        _hostView.AddChild(_host);
        await Frames(5);
        _host.Player.ManualControl = true;
        hostMap.Barrels[0].TakeDamage(100, ObjectDamage.Gun, _host.Player);
        await Until(() => !IsInstanceValid(hostMap.Walls[0]) && !IsInstanceValid(hostMap.Barrels[0]), "host destroys first pair before client joins");
        await Frames(60);

        var clientApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(clientApi, _clientView.GetPath());
        _clientPeer = new ENetMultiplayerPeer();
        Check(_clientPeer.CreateClient("127.0.0.1", port) == Error.Ok, "client connects to loopback host");
        clientApi.MultiplayerPeer = _clientPeer;
        await Until(() => _clientPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected, "ENet connection established");
        var clientMap = new TestMap();
        _client = new Stage { Name = "Stage", CharacterOverride = "duck", ThemeOverride = "zombie", Role = NetRole.Client,
            DirectorEnabled = false, SquadOverride = 0, ModeOverride = GameMode.Scenario, MapOverride = clientMap };
        _clientView.AddChild(_client);
        _client.Player.ManualControl = true;
        await Until(() => _client.Net!.Started && _host.Net!.Peers.Count == 2 && !IsInstanceValid(clientMap.Walls[0]), "late join receives authoritative destroyed state");
        Check(_host.GetWorld3D().Space != _client.GetWorld3D().Space, "host and client use separate physics worlds");
        Check(!IsInstanceValid(clientMap.Barrels[0]) && !clientMap.Visuals[0].IsProcessing()
            && clientMap.Visuals[0].GetNode<Node3D>("GroundGravel").Visible, "late join removes barrel and restores settled rubble without replay");
        Check(_client.Effects.SplatCount == 0, "late-join snapshot does not replay historical explosion");
        CheckOpening(_client, 0, "late join");

        float wallHealth = clientMap.Walls[1].Health, barrelHealth = clientMap.Barrels[1].Health;
        clientMap.Walls[1].TakeDamage(999, ObjectDamage.Explosion, _client.Player);
        clientMap.Barrels[1].TakeDamage(999, ObjectDamage.Gun, _client.Player);
        Check(clientMap.Walls[1].Health == wallHealth && clientMap.Barrels[1].Health == barrelHealth && !clientMap.Barrels[1].Ignited,
            "client-local damage cannot change wall or barrel authority");
        Check(clientMap.Walls[1].NetId == hostMap.Walls[1].NetId && clientMap.Barrels[1].NetId == hostMap.Barrels[1].NetId,
            "remaining pair keeps deterministic IDs despite late join");

        // This client-only prop is within the visual explosion. A replay that applies physics would move it.
        var witness = new RigidBody3D { Name = "ClientBlastWitness", Position = new Vector3(13, .5f, 1.5f),
            GravityScale = 0, CollisionLayer = 0, CollisionMask = 0 };
        witness.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = .1f } });
        _client.AddChild(witness);
        _client.Props.Add(witness);
        var witnessAt = witness.Position;
        int clientHealth = _client.Player.Health, splats = _client.Effects.SplatCount;
        hostMap.Barrels[1].TakeDamage(hostMap.Barrels[1].Health, ObjectDamage.Gun, _host.Player);
        Check(hostMap.Barrels[1].Ignited, "host shot starts second barrel fuse with host owner");
        await Until(() => !IsInstanceValid(clientMap.Walls[1]) && !IsInstanceValid(clientMap.Barrels[1]), "active destruction events reach client");
        Check(clientMap.Visuals[1].IsProcessing(), "active client receives collapse animation rather than snapshot-only settlement");
        await Frames(70);
        CheckOpening(_host, 12, "host live blast");
        CheckOpening(_client, 12, "client live blast");
        Check(!clientMap.Visuals[1].IsProcessing() && clientMap.Visuals[1].GetNode<Node3D>("GroundGravel").Visible,
            "client collapse settles into retained rubble");
        Check(_client.Effects.SplatCount == splats + 1 && _client.Player.Health == clientHealth
            && witness.Position.DistanceTo(witnessAt) < .01f && witness.LinearVelocity.Length() < .01f,
            "client replays one explosion visual without extra damage or prop impulse");

        int finalSplats = _client.Effects.SplatCount, flowVersion = _client.FlowVersion;
        for (int i = 0; i < 3; i++)
        {
            hostMap.Marker.Enabled = !hostMap.Marker.Enabled;
            bool expected = hostMap.Marker.Enabled;
            await Until(() => clientMap.Marker.Enabled == expected, "fresh world snapshot delivered " + (i + 1));
        }
        await Frames(8);
        Check(_client.GetChildren().OfType<BreakableWallVisual>().Count() == 2 && _client.Breakables.Count == 0
            && !_client.Props.OfType<ExplosiveBarrel>().Any() && _client.FlowVersion == flowVersion,
            "repeated world snapshots do not duplicate rubble, bodies, or flow rebuilds");
        Check(_client.Effects.SplatCount == finalSplats && _client.Player.Health == clientHealth
            && witness.Position.DistanceTo(witnessAt) < .01f && _client.Net!.ApplyErrors == 0,
            "repeated state does not replay explosion or damage and produces no apply errors");
    }

    private void CheckOpening(Stage stage, float x, string label)
    {
        var at = new Vector3(x, 0, 0);
        Check(!stage.Flow.IsBlocked(at) && Rays.Clear(stage, at + new Vector3(0, 1, -1), at + new Vector3(0, 1, 1), Layers.MovementWorld),
            label + ": center opens in physics and flow");
        foreach (float side in new[] { -2f, 2f })
            Check(stage.Flow.IsBlocked(at + Vector3.Right * side)
                && !Rays.Clear(stage, at + new Vector3(side, 1, -1), at + new Vector3(side, 1, 1), Layers.MovementWorld),
                label + ": permanent side remains " + side);
    }
}
