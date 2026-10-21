using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Onslaught's card flow over a real host–client link on this PC (decisions.md M-27): the client is sent its three
/// arming offers, its picks go to the host and arm its survivor there, the match starts when both have armed, a team
/// level-up and a middle boss's special card reach the client, and its level-up pick is applied by the host.
/// </summary>
public partial class OnslaughtNetCheck : Node
{
    private int _checks;
    private Stage _host = null!, _client = null!;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    private async Task Until(Func<bool> done, string what, float seconds = 5)
    {
        for (int i = 0; i < seconds * Engine.PhysicsTicksPerSecond; i++)
        {
            if (done()) { Check(true, what); return; }
            await Frames(1);
        }
        Check(done(), what + " (timed out)");
    }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("ONSLAUGHT_NET_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("ONSLAUGHT_NET_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private SubViewport View(string name)
    {
        var view = new SubViewport { Name = name, OwnWorld3D = true, Size = new Vector2I(640, 360) };
        AddChild(view);
        return view;
    }

    private async Task Run()
    {
        int port = 26000 + (int)(Time.GetTicksMsec() % 2000);
        var hostView = View("HostView");
        var clientView = View("ClientView");
        var hostApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(hostApi, hostView.GetPath());
        var hostPeer = new ENetMultiplayerPeer();
        Check(hostPeer.CreateServer(port, 4) == Error.Ok, $"the host listens on UDP {port}");
        hostApi.MultiplayerPeer = hostPeer;
        _host = new Stage { Name = "Stage", CharacterOverride = "goose", ThemeOverride = "zombie", Role = NetRole.Host, ModeOverride = GameMode.Survivor };
        hostView.AddChild(_host);
        await Frames(3);
        _host.Player.ManualControl = true;
        _host.Player.Invulnerable = true;

        var clientApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(clientApi, clientView.GetPath());
        var clientPeer = new ENetMultiplayerPeer();
        Check(clientPeer.CreateClient("127.0.0.1", port) == Error.Ok, "the client starts connecting");
        clientApi.MultiplayerPeer = clientPeer;
        for (int i = 0; i < 300 && clientPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected; i++) await Frames(1);
        _client = new Stage { Name = "Stage", CharacterOverride = "duck", ThemeOverride = "zombie", Role = NetRole.Client, ModeOverride = GameMode.Survivor };
        clientView.AddChild(_client);
        await Frames(3);
        _client.Player.ManualControl = true;
        await Until(() => _client.Net!.Started && _host.Net!.Peers.Count == 2, "the client joins the Onslaught match", 5);

        var hostRules = (SurvivorRules)_host.ModeRules!;
        var clientRules = (SurvivorRules)_client.ModeRules!;
        var remote = _host.Players.First(p => p.IsRemote);
        var hostCopy = hostRules.QueueOf(remote);

        // Arming: the client is sent its own three offers and its picks arm its survivor on the host.
        await Until(() => clientRules.LocalQueue.Count == SurvivorRules.ArmingPicks, "the client is sent its three arming offers");
        Check(clientRules.LocalQueue.Offers.Select(o => o.Choices.Select(c => c.Key)).SequenceEqual(hostCopy.Offers.Select(o => o.Choices.Select(c => c.Key)), new KeysEqual()) &&
              clientRules.Window.IsOpen && clientRules.LocalQueue.Front!.Title == Loc.Format("fmt_card_arming_title", 1),
            "the same cards as the host drew for it, in the card window");
        var picks = new string[3];
        for (int slot = 0; slot < 3; slot++)
        {
            picks[slot] = clientRules.LocalQueue.Front!.Choices[slot].Id;
            clientRules.Window.Pick(slot);
            await Until(() => hostCopy.Count == 2 - slot, $"the client's slot {slot + 1} pick reaches the host");
        }
        Check(remote.Loadout.Secondary.Def.Id == picks[0] && remote.Loadout.Primary?.Def.Id == picks[1] && remote.Loadout.Special?.Def.Id == picks[2],
            $"the host arms the client's survivor with its picks ({string.Join(", ", picks)})");
        Check(hostRules.Arming, "the match waits for the host's own picks");
        for (int i = 0; i < 3; i++) hostRules.LocalQueue.Choose(_host.Player, 0);
        await Until(() => !hostRules.Arming && !clientRules.Arming, "once both have armed the match starts on both PCs");
        await Until(() => _client.Player.Loadout.Primary?.Def.Id == picks[1], "the client's own survivor carries its picks");

        // A team level-up: the client is sent its level-up card and its pick is applied by the host.
        hostRules.AddXp(hostRules.XpNeeded);
        await Until(() => clientRules.Level == 2 && clientRules.LocalQueue.Front?.Kind == CardKind.Normal, "a team level-up reaches the client with its card");
        var card = clientRules.LocalQueue.Front!.Choices[0];
        clientRules.Window.Pick(0);
        await Until(() => hostCopy.Count == 0, $"the client's level-up pick ({card.Key}) is applied by the host");

        // A middle boss down: the next stretch and a special card reach the client.
        var boss = hostRules.SpawnBoss();
        await Frames(10);
        boss.Burn(boss.MaxHealth);
        await Until(() => clientRules.Segment == 1 && clientRules.LocalQueue.Offers.Any(o => o.Kind == CardKind.Special),
            "a middle boss down gives the client its special card and the second stretch");
        Check(!clientRules.BossArrived && clientRules.BarLabel.Contains($"2/{clientRules.Segments}"), "the client's bar counts down to the next boss: " + clientRules.BarLabel);
    }

    private sealed class KeysEqual : System.Collections.Generic.IEqualityComparer<System.Collections.Generic.IEnumerable<string>>
    {
        public bool Equals(System.Collections.Generic.IEnumerable<string>? a, System.Collections.Generic.IEnumerable<string>? b) => a!.SequenceEqual(b!);
        public int GetHashCode(System.Collections.Generic.IEnumerable<string> obj) => 0;
    }
}
