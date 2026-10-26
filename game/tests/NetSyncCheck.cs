using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Two-player sync on one PC (doc/multiplayer-sync-design.md 1장, 7장): a host stage and a client stage, each in its own
/// SubViewport with its own physics world and multiplayer API, joined over ENet on 127.0.0.1. Scripted input plays
/// through Stage 1 and both sides are compared: survivors, enemies and their positions, kills, pickups, doors,
/// objectives, damage, and bytes per second.
/// </summary>
public partial class NetSyncCheck : Node
{
    private int _checks;
    private Stage _host = null!, _client = null!;
    private readonly Dictionary<int, List<(double t, Vector3 p)>> _hostHistory = new();
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("NET_SYNC_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("NET_SYNC_FAIL " + e.Message); failed = true; }
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
        int port = 24000 + (int)(Time.GetTicksMsec() % 2000);
        var hostView = View("HostView");
        var clientView = View("ClientView");
        var hostApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(hostApi, hostView.GetPath());
        var hostPeer = new ENetMultiplayerPeer();
        Check(hostPeer.CreateServer(port, 4) == Error.Ok, $"the host listens on UDP {port}");
        hostApi.MultiplayerPeer = hostPeer;
        _host = new Stage { Name = "Stage", CharacterOverride = "goose", ThemeOverride = "zombie", Role = NetRole.Host, ReviveOverride = true };
        hostView.AddChild(_host);
        await Frames(3);
        _host.Player.ManualControl = true;

        var clientApi = MultiplayerApi.CreateDefaultInterface();
        GetTree().SetMultiplayer(clientApi, clientView.GetPath());
        var clientPeer = new ENetMultiplayerPeer();
        Check(clientPeer.CreateClient("127.0.0.1", port) == Error.Ok, "the client starts connecting");
        clientApi.MultiplayerPeer = clientPeer;
        for (int i = 0; i < 300 && clientPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected; i++) await Frames(1);
        Check(clientPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected, "the client connects");
        _client = new Stage { Name = "Stage", CharacterOverride = "duck", ThemeOverride = "zombie", Role = NetRole.Client, ReviveOverride = true };
        clientView.AddChild(_client);
        await Frames(3);
        _client.Player.ManualControl = true;
        for (int i = 0; i < 300 && !(_client.Net!.Started && _host.Net!.Peers.Count == 2); i++) await Frames(1);
        Check(_host.Players.Count == 2 && _client.Players.Count == 2 && _host.Players.Count(p => p.IsRemote) == 1 && _client.Players.Count(p => p.IsRemote) == 1,
            "each side has its own player and the other one's survivor");
        var remoteOnHost = _host.Players.First(p => p.IsRemote);
        var remoteOnClient = _client.Players.First(p => p.IsRemote);
        Check(remoteOnHost.Definition.Id == "duck" && remoteOnClient.Definition.Id == "goose", "each side sees the other's character");

        // ---------- enemies ----------
        await Frames(30);
        int hostCount = _host.Enemies.Count(e => !e.IsDead), clientCount = _client.Enemies.Count(e => !e.IsDead);
        Check(hostCount > 0 && clientCount == hostCount && _client.Enemies.All(e => e.IsPuppet),
            $"the client has the host's waiting monsters as puppets ({clientCount} of {hostCount})");
        Check(_client.Director.FilledThrough == -1 && _client.Director.MobsSpawned == 0, "the client never places or sends monsters itself");

        // The cosmetic critical flag must cross the actual event batch, including a lethal hit.
        var colorTarget = _host.Spawn(MonsterDatabase.Get("humanoid"), _client.Player.GlobalPosition + Vector3.Right * 1.2f, false);
        colorTarget.SetPhysicsProcess(false);
        await Frames(10);
        var colorPuppet = _client.Enemies.Single(e => e.NetId == colorTarget.NetId);
        Color? BurstColor(Node parent, string name)
        {
            var burst = parent.GetChildren().FirstOrDefault(n => n.Name == name && !n.IsQueuedForDeletion());
            if (burst == null) return null;
            return ((ShaderMaterial)burst.GetNode<MeshInstance3D>("DamageRays").MaterialOverride).GetShaderParameter("tint").AsColor();
        }
        colorTarget.TakeHit(new HitInfo(1, Vector3.Right, colorTarget.GlobalPosition + Vector3.Up, true));
        bool sawYellow = false;
        for (int i = 0; i < 12 && !sawYellow; i++)
        {
            await Frames(1);
            sawYellow = BurstColor(colorPuppet.Rig, "HitBurst") is { B: < .5f, R: > .9f };
        }
        Check(sawYellow, "critical enemy hit arrives yellow on the other PC");
        colorTarget.TakeHit(new HitInfo(1, Vector3.Right, colorTarget.GlobalPosition + Vector3.Up));
        bool sawWhite = false;
        for (int i = 0; i < 12 && !sawWhite; i++)
        {
            await Frames(1);
            sawWhite = BurstColor(colorPuppet.Rig, "HitBurst") is { B: > .9f, R: > .9f };
        }
        Check(sawWhite, "a normal hit after critical resets the remote rays to white");
        Check(colorPuppet.Rig.IsVisibleInTree(), "the critical death fixture is visible to the receiving player");
        colorTarget.TakeHit(new HitInfo(99999, Vector3.Right, colorTarget.GlobalPosition + Vector3.Up, true));
        bool sawDeath = false;
        for (int i = 0; i < 12 && !sawDeath; i++)
        {
            await Frames(1);
            sawDeath = BurstColor(_client.Effects, "DeathBurst") is { B: < .5f, R: > .9f };
        }
        Check(sawDeath, "critical death arrives with the same yellow ring on the other PC");

        // ---------- the client opens the safe room door ----------
        var door = _client.Layout.StartDoorSwitch;
        _client.Player.TeleportTo(door.GlobalPosition + Vector3.Left * .6f);
        await Frames(10);
        _client.RequestUse!(door);
        for (int i = 0; i < 60 && _client.Objective != Objective.ReachShutter; i++) await Frames(1);
        Check(_host.Objective == Objective.ReachShutter && _client.Objective == Objective.ReachShutter && _client.Layout.StartDoor.IsOpen,
            "the client's E opens the door on both sides");

        // ---------- movement: own on own PC, the other follows ----------
        _client.Player.TeleportTo(new Vector3(14, 0, 1.5f));
        _host.Player.TeleportTo(new Vector3(14, 0, -1.5f));
        await Frames(30);
        for (int i = 0; i < 60; i++)
        {
            _client.Player.SetControls(new Vector2(1, 0), _client.Player.GlobalPosition + new Vector3(10, 1, 0), false);
            _host.Player.SetControls(new Vector2(1, 0), _host.Player.GlobalPosition + new Vector3(10, 1, 0), false);
            await Frames(1);
        }
        _client.Player.SetControls(Vector2.Zero, _client.Player.GlobalPosition + new Vector3(10, 1, 0), false);
        _host.Player.SetControls(Vector2.Zero, _host.Player.GlobalPosition + new Vector3(10, 1, 0), false);
        await Frames(30);
        float clientGap = Flat(remoteOnHost.GlobalPosition - _client.Player.GlobalPosition);
        float hostGap = Flat(remoteOnClient.GlobalPosition - _host.Player.GlobalPosition);
        Check(_client.Player.GlobalPosition.X > 16 && clientGap < .3f && hostGap < .3f,
            $"walking: each side shows the other where they are ({clientGap:0.00} m / {hostGap:0.00} m)");

        // ---------- puppets follow the host's enemies ----------
        foreach (var e in _host.AllEnemies.ToList()) e.Die(Vector3.Up, 0);
        await Frames(10);
        Check(_client.AllEnemies.Count(e => !e.IsDead) == 0, "kills on the host remove the client's puppets");
        var runners = Enumerable.Range(0, 8).Select(i => _host.SpawnCommon(new Vector3(30 + i % 4, 0, -2 + i / 4 * 2), true)).ToList();
        _host.Player.Invulnerable = true;
        remoteOnHost.Invulnerable = true;
        var errors = new List<float>();
        for (int f = 0; f < 150; f++)
        {
            await Frames(1);
            foreach (var e in runners.Where(e => IsInstanceValid(e) && !e.IsDead))
            {
                if (!_hostHistory.TryGetValue(e.NetId, out var list)) _hostHistory[e.NetId] = list = new();
                list.Add((_host.Net!.Clock, e.GlobalPosition));
            }
            if (f < 60) continue;
            foreach (var puppet in _client.Enemies.Where(p => !p.IsDead && _hostHistory.ContainsKey(p.NetId)))
            {
                var hostAt = AtTime(_hostHistory[puppet.NetId], _client.Net!.RenderTime);
                if (hostAt is { } h) errors.Add(Flat(puppet.GlobalPosition - h));
            }
        }
        Check(errors.Count > 100 && errors.Average() < .3f && errors.Max() < 1f,
            $"running puppets match the host a moment ago (mean {errors.DefaultIfEmpty(0).Average():0.00} m, max {errors.DefaultIfEmpty(0).Max():0.00} m)");

        // ---------- the host bites the client; health arrives on the client ----------
        // The host's own player steps back so the zombies go for the client's survivor.
        _host.Player.TeleportTo(new Vector3(4, 0, 0));
        remoteOnHost.Invulnerable = false;
        int before = remoteOnHost.Health;
        for (int i = 0; i < 240 && remoteOnHost.Health == before; i++) await Frames(1);
        await Frames(10);
        Check(remoteOnHost.Health < before && Math.Abs(_client.Player.Health - remoteOnHost.Health) <= 8,
            $"the host's bites hurt the client, and the client shows the host's number ({_client.Player.Health})");
        remoteOnHost.Invulnerable = true;

        // ---------- the client shoots; the host decides the kill ----------
        foreach (var e in _host.AllEnemies.ToList()) e.Die(Vector3.Up, 0);
        await Frames(10);
        var target = _host.SpawnEnemy<Enemy>(_host.CommonDefinition, _client.Player.GlobalPosition + new Vector3(5, 0, 0), awake: false);
        await Frames(15);
        var gun = ArmoryCatalog.Weapon("m4");
        remoteOnHost.Weapons.Pickup(gun, null);
        _client.Player.Weapons.Pickup(gun, null);
        await Frames(5);
        int kills = _host.Kills;
        var aim = target.GlobalPosition + Vector3.Up;
        for (int i = 0; i < 40 && IsInstanceValid(target) && !target.IsDead; i++)
        {
            // Follow the monster: a sleeping one wanders, and a fixed point loses it (issues.md 2026-10-26).
            aim = target.GlobalPosition + Vector3.Up;
            _client.Player.SetControls(Vector2.Zero, aim, true);
            await Frames(1);
        }
        _client.Player.SetControls(Vector2.Zero, aim, false);
        await Frames(10);
        Check(_host.Kills > kills && _client.AllEnemies.Count(e => !e.IsDead) == 0, "the client's shots kill on the host, and the puppet dies on the client");

        // ---------- pickups: the host gives the item, both floors lose it ----------
        var pickupOnClient = _client.Interactables.OfType<WeaponPickup>().First(p => p.Weapon?.Id == "r870");
        int pickupId = pickupOnClient.NetId;
        _client.Player.TeleportTo(pickupOnClient.GlobalPosition + Vector3.Back * .5f);
        await Frames(10);
        _client.RequestUse!(pickupOnClient);
        for (int i = 0; i < 60 && _client.Player.Loadout.Primary?.Def.Id != "r870"; i++) await Frames(1);
        Check(remoteOnHost.Loadout.Primary?.Def.Id == "r870" && _client.Player.Loadout.Primary?.Def.Id == "r870",
            "picking up on the client: the host hands the shotgun over and the client holds it");
        await Frames(5);
        Check(_host.Interactables.All(i => i.NetId != pickupId) && _client.Interactables.All(i => i.NetId != pickupId),
            "the shotgun is gone from both floors");
        var droppedOnHost = _host.Interactables.OfType<WeaponPickup>().FirstOrDefault(p => p.Weapon?.Id == "m4" && p.State != null);
        Check(droppedOnHost != null && _client.Interactables.Any(i => i.NetId == droppedOnHost.NetId),
            "the rifle it replaced lies on both floors with the same id");

        // ---------- down and revive across the link ----------
        remoteOnHost.Invulnerable = false;
        remoteOnHost.TakeDamage(9999, Vector3.Left, 0);
        for (int i = 0; i < 30 && !_client.Player.IsDowned; i++) await Frames(1);
        Check(remoteOnHost.IsDowned && _client.Player.IsDowned && !_client.Player.IsDead, "the host puts the client down and the client sees itself down");
        _host.Player.TeleportTo(remoteOnHost.GlobalPosition + Vector3.Right * .9f);
        _host.Player.HoldRevive(true);
        for (int i = 0; i < 60 * 6 && _client.Player.IsDowned; i++) await Frames(1);
        _host.Player.HoldRevive(false);
        Check(!remoteOnHost.IsDowned && !_client.Player.IsDowned && _client.Player.Health > 0, "the host picks the client up and the client is back on its feet");
        remoteOnHost.Invulnerable = true;

        // ---------- a full horde: 70 puppets and the bytes it costs ----------
        foreach (var e in _host.AllEnemies.ToList()) e.Die(Vector3.Up, 0);
        await Frames(5);
        for (int i = 0; i < HordeDirector.MaxCommons; i++) _host.SpawnCommon(new Vector3(56 + i % 14 * 1.6f, 0, -8 + i / 14 * 3.4f), true);
        await Frames(30);
        long sentBefore = _host.Net!.BytesSent;
        double clockBefore = _host.Net.Clock;
        await Frames(180);
        double rate = (_host.Net.BytesSent - sentBefore) / (_host.Net.Clock - clockBefore) / 1024;
        int hostAlive = _host.Enemies.Count(e => !e.IsDead), clientAlive = _client.Enemies.Count(e => !e.IsDead);
        Check(hostAlive >= 60 && clientAlive == hostAlive, $"a horde of {hostAlive} reaches the client as {clientAlive} puppets");
        Check(rate < 20, $"the horde costs the host {rate:0.0} KB/s to one client (design estimate about 9)");
        foreach (var e in _host.AllEnemies.ToList()) e.Die(Vector3.Up, 0);
        await Frames(5);

        // ---------- dual pistols: a weapon made from another one still has an id (I-73) ----------
        var pistol = ArmoryCatalog.Weapon("m1911");
        foreach (var who in new[] { _host.Player, remoteOnHost }) { who.Weapons.Pickup(pistol, null); who.Weapons.Pickup(pistol, null); who.Weapons.SelectSlot(1); }
        for (int i = 0; i < 30 && _client.Player.Loadout.Secondary.Def.Id != "m1911_dual"; i++) await Frames(1);
        Check(_client.Player.Loadout.Secondary.Def.Id == "m1911_dual" && remoteOnClient.Loadout.Secondary.Def.Id == "m1911_dual",
            "dual pistols reach the client, in its own hands and in the host's");
        var dualAim = _host.Player.GlobalPosition + new Vector3(8, 1, 0);
        int snapsBefore = _client.Net!.SnapshotsReceived;
        for (int i = 0; i < 20; i++) { _host.Player.SetControls(Vector2.Zero, dualAim, i % 2 == 0); await Frames(1); }
        _host.Player.SetControls(Vector2.Zero, dualAim, false);
        await Frames(10);
        Check(_client.Net.ApplyErrors == 0 && _client.Net.SnapshotsReceived > snapsBefore + 5, "shooting dual pistols breaks nothing on the client");

        // ---------- objectives to the end ----------
        _host.Player.TeleportTo(_host.Layout.ShutterPanel.GlobalPosition + Vector3.Left * .6f);
        await Frames(5);
        _host.Layout.ShutterPanel.Use(_host.Player);
        for (int i = 0; i < 30 && _client.Objective != Objective.HoldOut; i++) await Frames(1);
        Check(_client.Objective == Objective.HoldOut, "the alarm starts on the client too");
        _host.Director.Stop();
        for (int i = 0; i < 90 && _client.Objective != Objective.ReachSafehouse; i++) await Frames(1);
        Check(_host.Objective == Objective.ReachSafehouse && _client.Objective == Objective.ReachSafehouse && _client.Layout.Shutter.IsOpen,
            "when the hold ends the shutter opens on both sides");
        foreach (var e in _host.AllEnemies.ToList()) e.Die(Vector3.Up, 0);
        // Everyone alive has to be inside: the client alone is not enough, the host alone is not enough.
        _client.Player.TeleportTo(new Vector3(120, 0, 1));
        await Frames(40);
        Check(_host.Objective == Objective.ReachSafehouse, "one survivor inside does not end the stage for the other");
        _host.Player.TeleportTo(new Vector3(119, 0, 0));
        for (int i = 0; i < 60 && _client.Objective != Objective.CloseSafehouse; i++) await Frames(1);
        Check(_client.Objective == Objective.CloseSafehouse, "with both inside, closing the door is offered on both sides");
        _client.Player.TeleportTo(_client.Layout.SafehouseSwitch.GlobalPosition + Vector3.Back * .5f);
        await Frames(10);
        _client.RequestUse!(_client.Layout.SafehouseSwitch);
        for (int i = 0; i < 60 && _client.State != StageState.Cleared; i++) await Frames(1);
        Check(_host.State == StageState.Cleared && _client.State == StageState.Cleared, "the client closes the safehouse and both clear the stage");

        // ---------- bandwidth ----------
        double seconds = _host.Net!.Clock;
        var hostNet = _host.Net!;
        var clientNet = _client.Net!;
        Check(hostNet.BytesSent > 0 && clientNet.SnapshotsReceived > seconds * 15 && clientNet.ApplyErrors == 0,
            $"host sent {hostNet.BytesSent / seconds / 1024:0.0} KB/s, client sent {clientNet.BytesSent / seconds / 1024:0.0} KB/s, {clientNet.SnapshotsReceived} snapshots in {seconds:0} s, no unreadable messages");
        clientPeer.Close();
        hostPeer.Close();
    }

    private static float Flat(Vector3 v) => new Vector2(v.X, v.Z).Length();

    private static Vector3? AtTime(List<(double t, Vector3 p)> list, double t)
    {
        for (int i = 1; i < list.Count; i++)
            if (list[i].t >= t)
            {
                var (t0, p0) = list[i - 1];
                var (t1, p1) = list[i];
                return p0.Lerp(p1, (float)((t - t0) / Math.Max(1e-6, t1 - t0)));
            }
        return null;
    }
}
