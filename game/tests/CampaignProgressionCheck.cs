using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Two real processes: lobby -> campaign1 -> 2 -> 3 -> lobby, with host-authoritative carry.</summary>
public partial class CampaignProgressionCheck : Node
{
    private string _role = "";
    private async Task Until(Func<bool> predicate, string message, int seconds = 60)
    {
        ulong end = Time.GetTicksMsec() + (ulong)seconds * 1000;
        while (!predicate() && Time.GetTicksMsec() < end) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!predicate())
        {
            string state = GetTree().CurrentScene is Stage stage
                ? $"stage={stage.Map.StageNumber} started={stage.Net?.Started} peers={stage.Net?.Peers.Count} stopped={stage.AllStopped} transition={StageTransition.Busy} paused={GetTree().Paused} received={stage.Net?.BytesReceived} snapshots={stage.Net?.SnapshotsReceived} applyErrors={stage.Net?.ApplyErrors} role={stage.Role}"
                : $"scene={GetTree().CurrentScene?.Name}";
            throw new Exception($"{message}; {state}; fps={Engine.GetFramesPerSecond()}");
        }
        GD.Print("PASS: " + message);
    }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private void Check(bool condition, string message) { if (!condition) throw new Exception(message); GD.Print("PASS: " + message); }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        bool failed = false;
        try { await Run(); GD.Print("CAMPAIGN_PROGRESSION_PASS " + _role); }
        catch (Exception e) { failed = true; GD.PushError(e.ToString()); }
        GetTree().Paused = false;
        if (GetTree().CurrentScene is { } scene) { GetTree().CurrentScene = null; scene.Free(); }
        NetLaunch.Reset(GetTree()); QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        var args = OS.GetCmdlineUserArgs();
        _role = args.First(a => a.StartsWith("--role="))[7..];
        int port = int.Parse(args.First(a => a.StartsWith("--port="))[7..]);
        GameSettings.Path = "user://campaign-progression-" + _role + ".cfg";
        GameSettings.Load(); RoomDirectory.Url = "";
        GameSession.Mode = GameMode.Scenario; GameSession.Campaign = "campaign"; GameSession.LaunchSquad = 0;
        GetTree().CurrentScene = null;
        GetTree().ChangeSceneToFile("res://scenes/Menu.tscn");
        await Until(() => GetTree().CurrentScene is MainMenu, "menu loaded");
        var menu = (MainMenu)GetTree().CurrentScene;
        if (_role == "host")
        {
            var peer = new ENetMultiplayerPeer();
            if (peer.CreateServer(port, 5) != Error.Ok) throw new Exception("test port busy");
            GetTree().GetMultiplayer().MultiplayerPeer = peer;
            var lobby = NetLobby.Create(NetRole.Host, "goose");
            typeof(NetLaunch).GetProperty(nameof(NetLaunch.Lobby))!.SetValue(null, lobby);
            typeof(NetLaunch).GetProperty(nameof(NetLaunch.Pending))!.SetValue(null, NetRole.Host);
            GetTree().Root.AddChild(lobby);
            await Until(() => lobby.Members.Count == 2 && lobby.CanStart, "both peers ready");
            Check(lobby.Start(), "host starts campaign");
        }
        else
        {
            menu.JoinRoom("127.0.0.1", port);
            await Until(() => NetLaunch.Lobby?.Joined == true, "client joined lobby");
            NetLaunch.Lobby!.SetReady(true);
        }
        for (int number = 1; number <= 3; number++)
        {
            await Until(() => GetTree().CurrentScene is Stage s && s.Map.StageNumber == number && s.Net is { Started: true } && s.Net.Peers.Count == 2 && !s.AllStopped && !StageTransition.Busy, $"stage {number}: both loaded and released");
            var stage = (Stage)GetTree().CurrentScene;
            stage.Player.ManualControl = true; stage.Player.Invulnerable = true;
            if (stage.Authoritative)
            {
                stage.Director.Stop();
                foreach (var enemy in stage.AllEnemies.ToArray()) enemy.Die(Vector3.Zero, 0);
                foreach (var player in stage.Players)
                {
                    player.Invulnerable = true;
                    if (number == 1)
                    {
                        player.Loadout.Equip(2, new WeaponState(ArmoryCatalog.Weapon("ak47"), 17, 51));
                        player.Weapons.SelectSlot(2); player.Medkits = 2; player.SetSpawnHealth(67);
                    }
                }
            }
            await Until(() => stage.Player.Loadout.Primary is { Mag: 17, Reserve: 51 } && stage.Player.Medkits == 2 && stage.Player.Health == 67, $"stage {number}: own kit and health preserved");
            Check(stage.Net!.ApplyErrors == 0, $"stage {number}: no stale packet apply errors");
            if (number == 1 && DisplayServer.GetName() != "headless")
            {
                stage.Player.TeleportTo(new Vector3(427, 0, -113)); stage.Cam.SnapTo(stage.Player.GlobalPosition);
                await Frames(5);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/campaign-ladder-approach-{_role}.png"));
            }
            foreach (var method in stage.Authoritative ? new[] { "ClientInput" } : new[] { "HostSnapshot", "HostEvents" })
                typeof(StageNet).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(stage.Net,
                    new object[] { NetLaunch.Lobby!.MatchEpoch - 1, Array.Empty<byte>() });
            Check(stage.Net.ApplyErrors == 0, $"stage {number}: old epoch is rejected before decoding");
            if (number == 2)
            {
                if (_role == "host")
                {
                    await Until(() => stage.Net.Peers.Count == 1, "stage2: guest leaves for late-join check");
                    await Until(() => stage.Net.Peers.Count == 2, "stage2: late guest joins current map");
                    var guest = stage.Players.First(p => p.IsRemote);
                    guest.Invulnerable = true;
                    guest.Loadout.Equip(2, new WeaponState(ArmoryCatalog.Weapon("ak47"), 17, 51));
                    guest.Weapons.SelectSlot(2); guest.Medkits = 2; guest.SetSpawnHealth(67);
                }
                else
                {
                    stage.BackToMenu();
                    await Until(() => GetTree().CurrentScene is MainMenu, "stage2: guest returns to menu");
                    await Frames(60);
                    ((MainMenu)GetTree().CurrentScene).JoinRoom("127.0.0.1", port);
                    await Until(() => GetTree().CurrentScene is Stage joined && joined.Map.StageNumber == 2 && joined.Net is { Started: true } && !joined.AllStopped, "late join loads stage2 instead of stage1");
                    stage = (Stage)GetTree().CurrentScene;
                    stage.Player.ManualControl = true; stage.Player.Invulnerable = true;
                    await Until(() => stage.Player.Loadout.Primary is { Mag: 17, Reserve: 51 } && stage.Player.Health == 67, "late guest state synchronized");
                }
            }
            if (number == 3)
            {
                var ride = stage.Gondolas.Single();
                // Leave enough real time for a new process scene to load during the ride.
                typeof(Gondola).GetProperty(nameof(Gondola.RideSeconds))!.SetValue(ride, 30f);
                stage.Player.TeleportTo(ride.GlobalPosition + (_role == "host" ? Vector3.Left : Vector3.Right) * .7f);
                stage.Cam.SnapTo(stage.Player.GlobalPosition);
                if (_role == "host")
                {
                    await Until(() => stage.AlivePlayers.All(ride.Aboard), "gondola: all survivors aboard across processes");
                    Check(ride.TryDepart(), "gondola departs");
                }
                await Until(() => ride.Riding && stage.AllStopped, "gondola: independent process enters ride pause");
                await Until(() => ride.Progress > .2f, "gondola: host timeline advances while world paused");
                if (DisplayServer.GetName() != "headless")
                {
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/campaign-gondola-{_role}.png"));
                }
                if (_role == "host")
                {
                    await Until(() => stage.Net!.Peers.Count == 1, "gondola: departed guest disconnects safely");
                    await Until(() => stage.Net!.Peers.Count == 2, "gondola: newcomer joins during ride");
                }
                else
                {
                    stage.BackToMenu();
                    await Until(() => GetTree().CurrentScene is MainMenu, "gondola: disconnected guest reaches menu");
                    await Frames(30);
                    ((MainMenu)GetTree().CurrentScene).JoinRoom("127.0.0.1", port);
                    await Until(() => GetTree().CurrentScene is Stage joined && joined.Map.StageNumber == 3 && joined.Net is { Started: true }, "gondola: newcomer loads current stage");
                    stage = (Stage)GetTree().CurrentScene; ride = stage.Gondolas.Single();
                    stage.Player.ManualControl = true; stage.Player.Invulnerable = true;
                    Check(ride.Riding && stage.AllStopped, "gondola: newcomer waits for authoritative completion");
                }
                await Until(() => ride.Completed && !stage.AllStopped && stage.Player.GlobalPosition.DistanceTo(ride.Arrival) < 4, "gondola: both processes arrive and resume");
            }
            if (stage.Authoritative)
            {
                stage.Map.StartDoorSwitch?.Use(stage.Player);
                stage.Map.ShutterPanel?.Use(stage.Player);
                stage.Director.Stop();
            }
            await Until(() => stage.Objective == Objective.ReachSafehouse, $"stage {number}: objective reaches destination");
            stage.Player.TeleportTo(stage.Map.RouteGoal + (_role == "host" ? Vector3.Left : Vector3.Right));
            await Until(() => stage.Objective == Objective.CloseSafehouse, $"stage {number}: both occupants acknowledged");
            if (_role == "client")
            {
                var close = stage.Map.SafehouseSwitch!;
                var near = close.GlobalPosition + Vector3.Back * .6f;
                stage.Player.TeleportTo(near with { Y = stage.Tiers.GroundY(near) });
                await Frames(20); stage.RequestUse!(close);
            }
            if (number < 3)
            {
                await Until(() => stage.Objective == Objective.TakeExit, $"stage {number}: hatch enabled for party");
                if (_role == "client")
                {
                    var hatch = stage.Map.StageExit!;
                    stage.Player.TeleportTo(hatch.GlobalPosition + Vector3.Back * .4f);
                    await Frames(20); stage.RequestUse!(hatch);
                }
                await Until(() => !IsInstanceValid(stage) || GetTree().CurrentScene != stage, $"stage {number}: real scene replaced");
            }
            else await Until(() => stage.State == StageState.Cleared, "last campaign stage ends for both peers");
        }
        if (_role == "host") { await Frames(60); NetLaunch.Lobby!.ReturnToLobby(); }
        await Until(() => GetTree().CurrentScene is MainMenu, "returned to lobby after campaign");
        Check(NetLaunch.Lobby is { InMatch: false }, "connection survives whole campaign");
        await Frames(_role == "host" ? 90 : 30);
    }
}
