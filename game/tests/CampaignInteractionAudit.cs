using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Audits shipped campaign interactions over ENet; collects all failures instead of stopping at the first.</summary>
public partial class CampaignInteractionAudit : Node
{
    private int _pass, _fail, _stageNumber, _port;
    private bool _short;
    private Stage _host = null!, _client = null!;
    private SubViewport _hv = null!, _cv = null!;
    private Player Remote => _host.Net!.Peers[_client.Player.PeerId];
    private bool Finished(Stage stage) => stage.Map.NextStage > 0
        ? stage.Objective == Objective.TakeExit && stage.Map.StageExit!.Enabled && stage.State == StageState.Playing
        : stage.State == StageState.Cleared;
    private StageMap Map() => _short ? (_stageNumber == 1 ? new StageOneLayout() : new StageTwoMap()) : LayoutMap.ForStage(_stageNumber);
    private void Check(bool ok, string text) { if (ok) _pass++; else _fail++; GD.Print($"{(ok ? "PASS" : "FAIL")}: {text}"); }
    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private SubViewport View(string name)
    {
        var v = new SubViewport { Name = name, OwnWorld3D = true, Size = new Vector2I(800, 450), ProcessMode = ProcessModeEnum.Pausable };
        AddChild(v); GetTree().SetMultiplayer(MultiplayerApi.CreateDefaultInterface(), v.GetPath()); return v;
    }
    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _stageNumber = int.Parse(OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--audit-stage="))?.Split('=')[1] ?? "1");
        _short = OS.GetCmdlineUserArgs().Contains("--audit-short");
        try { await Run(); }
        catch (Exception e) { _fail++; GD.PushError(e.ToString()); }
        GetTree().Paused = false;
        Close(_cv); Close(_hv);
        GD.Print($"CAMPAIGN_AUDIT stage={_stageNumber} short={_short} pass={_pass} fail={_fail}");
        QuitHygiene.Collect(); GetTree().Quit(_fail == 0 ? 0 : 1);
    }
    private void Close(SubViewport? view)
    {
        if (view == null || !IsInstanceValid(view)) return;
        if (view.GetMultiplayer().MultiplayerPeer is ENetMultiplayerPeer p) p.Close();
        GetTree().SetMultiplayer(null, view.GetPath()); view.Free();
    }
    private async Task Join()
    {
        _cv = View("ClientView");
        var peer = new ENetMultiplayerPeer();
        if (peer.CreateClient("127.0.0.1", _port) != Error.Ok) throw new Exception("client socket");
        _cv.GetMultiplayer().MultiplayerPeer = peer;
        for (int n = 0; n < 300 && peer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Connected; n++) await Frames(1);
        _client = new Stage { Name = "Stage", Role = NetRole.Client, MapOverride = Map(), ModeOverride = GameMode.Scenario, CharacterOverride = "duck", DirectorEnabled = false, SquadOverride = 0, NextStageOverride = true };
        _cv.AddChild(_client); _client.Player.ManualControl = true;
        for (int n = 0; n < 300 && !(_client.Net!.Started && _host.Net!.Peers.Count == 2); n++) await Frames(1);
        if (!_client.Net!.Started || _host.Net!.Peers.Count != 2) throw new Exception("join timeout");
        Remote.Invulnerable = true;
        await Frames(15);
    }
    private Interactable ClientItem(Interactable item) => _client.Interactables.Single(c => c.NetId == item.NetId);
    private async Task Request(Interactable item, Vector3? at = null)
    {
        var near = item.GlobalPosition + Vector3.Back * .35f;
        var p = at ?? (near with { Y = _client.Tiers.GroundY(near) });
        _client.Player.TeleportTo(p + Vector3.Up * .05f);
        await Frames(35);
        GD.Print($"USE id={item.NetId} name={item.Name} pos={item.GlobalPosition} local={_client.Player.GlobalPosition} host={Remote.GlobalPosition}");
        _client.RequestUse!(ClientItem(item));
        await Frames(25);
    }
    private void WorldMatches(string label)
    {
        Check(_host.Map.Doors.Count == _client.Map.Doors.Count && _host.Map.Doors.Select((d, i) => d.IsOpen == _client.Map.Doors[i].IsOpen).All(b => b), label + " doors");
        Check(_client.Map.Doors.All(d => d.GetChildren().OfType<StaticBody3D>().All(b => (b.CollisionLayer == 0) == d.IsOpen && b.GetChildren().OfType<CollisionShape3D>().All(s => s.Disabled == d.IsOpen))), label + " actual door collision");
        Check(_host.Interactables.Where(i => i is not WeaponPickup && i is not LootPickup).All(i => _client.Interactables.Any(c => c.NetId == i.NetId && c.Enabled == i.Enabled && c.Visible == i.Visible)), label + " interaction enabled/visible");
        Check(_host.Objective == _client.Objective && _host.State == _client.State, label + " objective/state");
    }
    private async Task Run()
    {
        _port = 29000 + (int)(Time.GetTicksMsec() % 1000);
        _hv = View("HostView"); var peer = new ENetMultiplayerPeer();
        if (peer.CreateServer(_port, 4) != Error.Ok) throw new Exception("host socket");
        _hv.GetMultiplayer().MultiplayerPeer = peer;
        _host = new Stage { Name = "Stage", Role = NetRole.Host, MapOverride = Map(), ModeOverride = GameMode.Scenario, CharacterOverride = "goose", DirectorEnabled = false, SquadOverride = 0, NextStageOverride = true };
        _hv.AddChild(_host); _host.Player.ManualControl = true; _host.Player.Invulnerable = true;
        foreach (var e in _host.AllEnemies.ToArray()) e.Die(Vector3.Zero, 0);
        await Join();
        GD.Print($"INVENTORY doors={_host.Map.Doors.Count} interactions={_host.Interactables.Count} ladders={_host.Ladders.Count} ziplines={_host.Ziplines.Count} gondolas={_host.Gondolas.Count}");
        Check(_host.Interactables.Count == _client.Interactables.Count && _host.Interactables.All(i => _client.Interactables.Any(c => c.NetId == i.NetId && c.GetType() == i.GetType() && c.GlobalPosition.DistanceTo(i.GlobalPosition) < .01f)), "initial interaction ids/types/positions");
        WorldMatches("initial");
        var critical = _host.Interactables.Where(i => i.GetType() == typeof(Interactable) && i.Pickup == PickupKind.None).ToArray();
        foreach (var item in critical)
        {
            var w = new NetWriter(); w.Pos(item.GlobalPosition); var decoded = new NetReader(w.ToArray()).Pos();
            Check(decoded.DistanceTo(item.GlobalPosition) < .02f, $"position roundtrip id={item.NetId} at={item.GlobalPosition} decoded={decoded}");
        }
        if (_host.Map.StartDoorSwitch is { } start)
        {
            await Request(start);
            Check(_host.Map.StartDoor!.IsOpen, "client opens start room");
            if (!_host.Map.StartDoor.IsOpen) start.Use(_host.Player);
            await Frames(15); WorldMatches("start room");
        }
        // Base entry holds, inside exits, side-lane switches, and the destination entrance.
        // Requests exercise the post-hold network route; local hold duration/cancellation is covered separately.
        foreach (var item in critical.Where(i => i.Enabled && new[] { "ui_use_open_base", "ui_use_open_door", "ui_use_open_shutter" }.Any(k => i.Prompt == Loc.Text(k)) && i != _host.Map.ShutterPanel && i != _host.Map.StartDoorSwitch))
        {
            await Request(item);
            Check(!item.Enabled, $"client operates door switch id={item.NetId} at={item.GlobalPosition}");
            if (item.Enabled) item.Use(_host.Player); // Isolate world replication even when request/position failed.
            await Frames(15); WorldMatches($"switch {item.NetId}");
        }
        if (_host.Map.ShutterPanel is { } panel)
        {
            await Request(panel);
            Check(_host.Objective == Objective.HoldOut, "client starts crescendo");
            if (_host.Objective != Objective.HoldOut) panel.Use(_host.Player);
            await Frames(12); WorldMatches("crescendo start");
            _host.Director.Stop(); await Frames(25);
            Check(_client.Map.Shutter!.IsOpen, "crescendo completion removes client gate");
            WorldMatches("crescendo complete");
        }
        foreach (var alarm in _host.Map.AlarmDevices) alarm.Sound();
        await Frames(15);
        Check(_host.Map.AlarmDevices.Select((a, i) => a.Triggered == _client.Map.AlarmDevices[i].Triggered).All(b => b), "all alarms");
        foreach (var ladder in _host.Ladders)
        {
            Check(ladder.Top.Y > ladder.Bottom.Y && !_host.Flow.IsBlocked(ladder.Bottom), "ladder connects a walkable foot to a higher landing");
            if (ladder.Top.Y - ladder.Bottom.Y > .5f)
            {
                Remote.TeleportTo(ladder.Bottom); _client.Player.TeleportTo(ladder.Top + Vector3.Up * .05f);
                await Frames(35);
                Check(Mathf.Abs(Remote.GlobalPosition.Y - _client.Player.GlobalPosition.Y) < .3f, $"owner height follows onto elevated floor {ladder.Top}, host={Remote.GlobalPosition}, client={_client.Player.GlobalPosition}");
            }
            foreach (bool up in new[] { true, false })
            {
                var from = up ? ladder.Bottom : ladder.Top; var to = up ? ladder.Top : ladder.Bottom;
                var item = critical.First(i => i.Prompt == Loc.Text(up ? "ui_use_ladder_up" : "ui_use_ladder_down") && i.GlobalPosition.DistanceTo(from) < .1f);
                await Request(item, from);
                await Frames((int)(ladder.Tiers * (up ? Player.LadderUpSeconds : Player.LadderDownSeconds) * 60) + 30);
                Check(_client.Player.GlobalPosition.DistanceTo(to) < .8f, $"client ladder {(up ? "up" : "down")} {from} -> {to}, actual={_client.Player.GlobalPosition}");
                Check(Remote.GlobalPosition.DistanceTo(_client.Player.GlobalPosition) < .8f, "ladder host/client final position agrees");
                int revision = Remote.MotionRevision;
                Remote.ApplyNetInput(new NetInput { MotionRevision = revision - 1, Position = from, Presses = new byte[(int)NetPress.Count] });
                Check(Remote.NetMotion == null, "pre-landing input cannot overwrite authoritative arrival");
                var before = _client.Player.GlobalPosition;
                Check(!_client.Player.ApplyNetPosition(new NetPlayerState { MotionRevision = revision - 1, Position = from }) && _client.Player.GlobalPosition == before, "old snapshot cannot undo arrival");
                await Frames(5);
                Check(_client.Player.TravelKind == 0 && Remote.NetMotion != null, "owner acknowledges arrival and normal movement resumes");
            }
        }
        foreach (var line in _host.Ziplines)
        {
            var item = critical.First(i => i.Prompt == Loc.Text("ui_use_zipline") && i.GlobalPosition.DistanceTo(line.Start) < 4);
            await Request(item, item.GlobalPosition);
            await Frames((int)(line.Seconds * 60) + 30);
            Check(_client.Player.GlobalPosition.DistanceTo(line.Landing) < 1, $"client zipline reaches {line.Landing}, actual={_client.Player.GlobalPosition}");
        }
        if (_host.Ladders.FirstOrDefault() is { } cancelLadder)
        {
            _client.Player.TeleportTo(cancelLadder.Bottom); await Frames(20);
            Remote.ClimbLadder(cancelLadder, true); await Frames(8);
            Remote.TeleportTo(cancelLadder.Bottom); await Frames(20);
            Check(!Remote.OnLadder && _client.Player.TravelKind == 0 && _client.Player.GlobalPosition.DistanceTo(cancelLadder.Bottom) < .3f,
                "cancelled ladder releases owner at authoritative position");
        }
        if (_host.Ziplines.FirstOrDefault() is { } cancelLine)
        {
            _client.Player.TeleportTo(cancelLine.Start); await Frames(20);
            Remote.RideZipline(cancelLine); await Frames(8);
            Remote.TeleportTo(cancelLine.Start); await Frames(20);
            Check(!Remote.OnZipline && _client.Player.TravelKind == 0 && _client.Player.GlobalPosition.DistanceTo(cancelLine.Start) < .3f,
                "cancelled zipline releases owner at authoritative position");
        }
        foreach (var ride in _host.Gondolas)
        {
            // Both peers share a SceneTree only in this fixture. Keep the client link polling during the host pause.
            _client.Net!.ProcessMode = ProcessModeEnum.Always;
            _host.Net!.ProcessMode = ProcessModeEnum.Always;
            var center = ride.GlobalPosition;
            _host.Player.TeleportTo(center + Vector3.Left * .7f);
            _client.Player.TeleportTo(center + Vector3.Right * .7f);
            await Frames(20);
            bool began = ride.TryDepart();
            Check(began, "host can depart gondola with all survivors aboard");
            var departure = new NetWriter(); ride.WriteNet(departure);
            ride.Arrive();
            _host.Net!.ProcessMode = ProcessModeEnum.Always;
            // Reliable world and unreliable snapshots have different channels: arrival may be seen first.
            var arrived = Remote.CaptureNetState();
            _client.Player.ApplyNetPosition(arrived);
            _client.Gondolas.Single().ApplyNet(new NetReader(departure.ToArray()));
            Check(!_client.Player.OnGondola && _client.Player.GlobalPosition.DistanceTo(arrived.Position) < .02f, "delayed departure cannot undo an already acknowledged arrival");
            await Frames(35);
            Check(_client.Player.GlobalPosition.DistanceTo(ride.Arrival) < 4, $"client arrives at gondola destination {ride.Arrival}, actual={_client.Player.GlobalPosition}");
            Check(_client.Gondolas.Any(g => g.Completed), "client receives completed gondola");
        }
        // A fresh join must receive completed doors/alarms, not only live change events.
        Check(_client.Net!.ApplyErrors == 0, "original client has no decode errors");
        Close(_cv); await Frames(30); await Join(); WorldMatches("late join");
        foreach (var ride in _host.Gondolas.Where(g => g.Completed))
            Check(_client.Gondolas.Any(g => g.Completed) && _client.Player.GlobalPosition.DistanceTo(ride.Arrival) < 5, "late join starts beyond completed gondola");
        Check(_host.Map.AlarmDevices.Select((a, i) => a.Triggered == _client.Map.AlarmDevices[i].Triggered).All(b => b), "late join alarms");
        foreach (var e in _host.AllEnemies.ToArray()) e.Die(Vector3.Zero, 0);
        var goal = _host.Map.RouteGoal;
        _host.Player.TeleportTo(goal + Vector3.Left); _client.Player.TeleportTo(goal + Vector3.Right);
        await Frames(60);
        Check(_host.Objective == Objective.CloseSafehouse, $"all living clients at destination enable closure; host remote={Remote.GlobalPosition}, target={goal}");
        if (_host.Objective != Objective.CloseSafehouse) { Remote.TeleportTo(goal + Vector3.Right); await Frames(1); }
        if (_host.Map.SafehouseSwitch is { } close)
        {
            await Request(close);
            Check(Finished(_host) && Finished(_client), "client can close destination and enable next hatch or final clear");
            // Fallback separates world replication from a rejected far-coordinate request.
            if (!Finished(_host)) { _host.ApplyNetObjective(Objective.CloseSafehouse); close.Use(_host.Player); }
            await Frames(20);
            Check(Finished(_host) && Finished(_client), "destination completion reaches client");
            WorldMatches("destination clear");
        }
        if (_host.Map.NextStage > 0) Check(_client.Map.StageExit!.Enabled, "next-stage hatch state reaches client (scene reload checked by CampaignProgressionCheck)");
        Check(_client.Net!.ApplyErrors == 0, "no decode errors");
        var departing = _client.Net;
        _cv.RemoveChild(_client);
        int received = departing.SnapshotsReceived;
        typeof(StageNet).GetMethod("HostSnapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(departing, new object[] { 0, Array.Empty<byte>() });
        Check(departing.Ending && departing.SnapshotsReceived == received, "detached scene rejects packets before touching player transforms");
        _client.Free();
    }
}
