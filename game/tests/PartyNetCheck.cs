using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// A full room on one PC (decisions.md M-30): a host and five friends, each stage in its own SubViewport with its own
/// physics world and multiplayer API, joined over ENet on 127.0.0.1. Everyone must see all six survivors with the right
/// characters, start in their own place, follow each other's walking, tell same-character survivors apart, keep a
/// seventh out, drop one who leaves and seat the next one in the freed place.
/// </summary>
public partial class PartyNetCheck : Node
{
    private int _checks;
    private int _port;
    private Stage _host = null!;
    private readonly List<(SubViewport View, ENetMultiplayerPeer Peer, Stage Stage, int Id)> _friends = new();
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("PARTY_NET_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("PARTY_NET_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private SubViewport View(string name)
    {
        var view = new SubViewport { Name = name, OwnWorld3D = true, Size = new Vector2I(320, 180) };
        AddChild(view);
        return view;
    }

    /// <summary>Windowed runs only: one PC's screen at 1280×720 to output/.</summary>
    private async Task Shot(SubViewport view, string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        var size = view.Size;
        view.Size = new Vector2I(1280, 720);
        view.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        await Frames(20);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        view.GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/" + name + ".png"));
        view.Size = size;
    }

    private IEnumerable<Stage> All => new[] { _host }.Concat(_friends.Select(f => f.Stage));

    private async Task<(SubViewport View, ENetMultiplayerPeer Peer, Stage Stage, int Id)> Join(string name, string character)
    {
        var view = View(name);
        var api = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(api, view.GetPath());
        var peer = new ENetMultiplayerPeer();
        Check(peer.CreateClient("127.0.0.1", _port) == Error.Ok, $"{name} starts connecting");
        api.MultiplayerPeer = peer;
        for (int i = 0; i < 300 && peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected; i++) await Frames(1);
        Check(peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected, $"{name} connects");
        var stage = new Stage { Name = "Stage", CharacterOverride = character, ThemeOverride = "zombie", Role = NetRole.Client, ReviveOverride = true };
        view.AddChild(stage);
        await Frames(3);
        stage.Player.ManualControl = true;
        int id = api.GetUniqueId();
        for (int i = 0; i < 300 && !(stage.Net!.Started && _host.Net!.Peers.ContainsKey(id)); i++) await Frames(1);
        Check(stage.Net!.Started && _host.Net!.Peers.ContainsKey(id), $"{name} ({character}) is in the match");
        return (view, peer, stage, id);
    }

    /// <summary>Waits until every stage has <paramref name="count"/> survivors.</summary>
    private async Task Settle(int count)
    {
        for (int i = 0; i < 300 && !All.All(s => s.Players.Count(IsInstanceValid) == count); i++) await Frames(1);
        await Frames(2);
    }

    private static Dictionary<int, Player> ByPeer(Stage stage) => stage.Players.Where(IsInstanceValid).ToDictionary(p => p.PeerId);

    private async Task Run()
    {
        _port = 26000 + (int)(Time.GetTicksMsec() % 2000);
        var hostView = View("HostView");
        var hostApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(hostApi, hostView.GetPath());
        var hostPeer = new ENetMultiplayerPeer();
        Check(NetLaunch.MaxClients == 5 && NetLaunch.MaxPlayers == 6, "a room holds six: the host and five friends");
        Check(hostPeer.CreateServer(_port, NetLaunch.MaxClients) == Error.Ok, $"the host listens on UDP {_port} for {NetLaunch.MaxClients} friends");
        hostApi.MultiplayerPeer = hostPeer;
        _host = new Stage { Name = "Stage", CharacterOverride = "goose", ThemeOverride = "zombie", Role = NetRole.Host, ReviveOverride = true };
        hostView.AddChild(_host);
        await Frames(3);
        _host.Player.ManualControl = true;

        // ---------- five friends come in; two characters are taken twice ----------
        string[] characters = { "duck", "raccoon", "redpanda", "goose", "duck" };
        for (int i = 0; i < characters.Length; i++) _friends.Add(await Join($"Friend{i + 1}", characters[i]));
        await Settle(6);
        Check(All.All(s => s.Players.Count(IsInstanceValid) == 6 && s.Players.Count(p => IsInstanceValid(p) && !p.IsRemote) == 1),
            "all six PCs have six survivors: their own and five others");
        var truth = ByPeer(_host).ToDictionary(p => p.Key, p => p.Value.Definition.Id);
        Check(_friends.All(f => ByPeer(f.Stage).All(p => truth.TryGetValue(p.Key, out var c) && c == p.Value.Definition.Id) && ByPeer(f.Stage).Count == truth.Count),
            "every PC has the same people on the same characters (friends see each other, not only the host)");
        Check(_friends.All(f => f.Stage.Player.PeerId == f.Id && f.Stage.Net!.Peers.Count == 6), "each friend's own survivor is its own peer");

        // ---------- each in its own place ----------
        var spots = ByPeer(_host).Values.Select(p => p.GlobalPosition).ToList();
        float closest = spots.SelectMany((a, i) => spots.Skip(i + 1).Select(b => new Vector2(a.X - b.X, a.Z - b.Z).Length())).Min();
        Check(closest > 1f, $"six different start places (closest two {closest:0.00} m apart: {string.Join(" ", spots.Select(s => $"({s.X:0.0},{s.Z:0.0})"))}, " +
            $"wanted {string.Join(" ", Enumerable.Range(1, 5).Select(i => StageNet.ClientStart(_host, i)).Select(s => $"({s.X:0.0},{s.Z:0.0})"))})");
        Check(_friends.All(f => f.Stage.Player.GlobalPosition.DistanceTo(ByPeer(_host)[f.Id].GlobalPosition) < .6f),
            "each friend stands where the host put it");

        // ---------- same characters get a number ----------
        var hostLabels = ByPeer(_host).ToDictionary(p => p.Key, p => p.Value.DisplayLabel);
        Check(All.All(s => ByPeer(s).All(p => p.Value.DisplayLabel == hostLabels[p.Key])), "every PC names everyone the same way");
        var geese = ByPeer(_host).Values.Where(p => p.Definition.Id == "goose").Select(p => p.DisplayLabel).ToList();
        var ducks = ByPeer(_host).Values.Where(p => p.Definition.Id == "duck").Select(p => p.DisplayLabel).ToList();
        Check(geese.Count == 2 && geese.Distinct().Count() == 2 && geese.All(l => l.Length > 0) && ducks.Distinct().Count() == 2 && ducks.All(l => l.Length > 0),
            $"two geese and two ducks are told apart ({string.Join(", ", geese.Concat(ducks))})");
        Check(_host.Player.DisplayLabel.EndsWith(" 1") && ByPeer(_host).Values.Single(p => p.Definition.Id == "raccoon").DisplayLabel == "",
            "the host's goose is number 1; a character only one person plays keeps its plain name");

        await Shot(_friends[4].View, "party-six");

        // ---------- a friend walks; every other PC sees it ----------
        var walker = _friends[2];
        var watcher = _friends[4];
        var before = walker.Stage.Player.GlobalPosition;
        for (int i = 0; i < 40; i++)
        {
            walker.Stage.Player.SetControls(new Vector2(1, 0), walker.Stage.Player.GlobalPosition + new Vector3(10, 1, 0), false);
            await Frames(1);
        }
        walker.Stage.Player.SetControls(Vector2.Zero, walker.Stage.Player.GlobalPosition + new Vector3(10, 1, 0), false);
        await Frames(20);
        var walked = walker.Stage.Player.GlobalPosition;
        var onHost = ByPeer(_host)[walker.Id].GlobalPosition;
        var onWatcher = ByPeer(watcher.Stage)[walker.Id].GlobalPosition;
        Check(walked.X - before.X > 1f && onHost.DistanceTo(walked) < .8f && onWatcher.DistanceTo(walked) < .8f,
            $"friend 3 walked {walked.X - before.X:0.0} m; the host and friend 5 see it there ({onHost.DistanceTo(walked):0.00} / {onWatcher.DistanceTo(walked):0.00} m off)");

        // ---------- what it costs the host ----------
        long sentBefore = _host.Net!.BytesSent;
        double clockBefore = _host.Net.Clock;
        await Frames(120);
        double rate = (_host.Net.BytesSent - sentBefore) / (_host.Net.Clock - clockBefore) / 1024;
        Check(rate > 0 && rate < 400, $"the host sends {rate:0.0} KB/s to five friends ({rate / 5:0.0} KB/s each)");
        // Six stages load in one process here, so the earlier friends see the round trip jump while each later stage
        // loads and ENet throttles their unreliable snapshots for a while (I-102). Once everyone is in, every friend must
        // get a steady stream again: measure over a later window, not since joining.
        await Frames(300);
        var counts = _friends.Select(f => f.Stage.Net!.SnapshotsReceived).ToList();
        double t0 = _host.Net.Clock;
        await Frames(180);
        double span = _host.Net.Clock - t0;
        var rates = _friends.Select((f, i) => (f.Stage.Net!.SnapshotsReceived - counts[i]) / span).ToList();
        Check(rates.All(r => r > 10) && _friends.All(f => f.Stage.Net!.ApplyErrors == 0),
            $"once settled every friend gets a steady stream of snapshots and reads every message ({string.Join(", ", rates.Select(r => $"{r:0.0}"))} a second, 20 sent)");

        // ---------- a seventh cannot get in ----------
        var extraView = View("Extra");
        var extraApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(extraApi, extraView.GetPath());
        var extraPeer = new ENetMultiplayerPeer();
        extraPeer.CreateClient("127.0.0.1", _port);
        extraApi.MultiplayerPeer = extraPeer;
        await Frames(120);
        Check(extraPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected && _host.Net.Peers.Count == 6, "a seventh PC is turned away: the room is full");
        extraPeer.Close();
        extraView.QueueFree();
        await Frames(2);

        // ---------- one leaves; the rest drop that survivor ----------
        var leaver = _friends[1];
        var leaverSpot = ByPeer(_host)[leaver.Id].GlobalPosition;
        leaver.Peer.Close();
        leaver.View.QueueFree();
        _friends.RemoveAt(1);
        await Settle(5);
        Check(All.All(s => s.Players.Count(IsInstanceValid) == 5 && !ByPeer(s).ContainsKey(leaver.Id) && !s.Net!.Peers.ContainsKey(leaver.Id)),
            "friend 2 left: every PC drops that survivor");

        // ---------- the next one takes the freed place ----------
        var late = await Join("Friend6", "raccoon");
        _friends.Add(late);
        await Settle(6);
        Check(All.All(s => s.Players.Count(IsInstanceValid) == 6), "a new friend fills the room again, on every PC");
        Check(late.Stage.Player.GlobalPosition.DistanceTo(StageNet.ClientStart(_host, 2)) < .6f && late.Stage.Player.DisplayLabel == "",
            "the newcomer gets the freed second place");
        Check(Mathf.Abs(leaverSpot.DistanceTo(StageNet.ClientStart(_host, 2))) < .6f, "that was the place friend 2 had");
        Check(_friends.All(f => f.Stage.Net!.ApplyErrors == 0), "no unreadable messages after the changes");

        foreach (var f in _friends) f.Peer.Close();
        hostPeer.Close();
        await Frames(2);
    }
}
