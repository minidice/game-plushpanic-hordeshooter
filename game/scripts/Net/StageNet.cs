using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Keeps one stage in sync between a host and its party (doc/multiplayer-sync-design.md). Lives at "Stage/Net" so RPC
/// paths match on every PC. The host decides and sends: snapshots 20 times a second (enemies in every other one),
/// reliable events once per physics frame, and the world state whenever it changes. A client sends its input every
/// physics frame and asks the host to use things; its own player moves on its own PC.
/// </summary>
public partial class StageNet : Node
{
    public const float SnapshotInterval = .05f;
    public const int HostPeer = 1;

    private Stage _stage = null!;
    public NetRole Role { get; private set; }
    /// <summary>Survivors by network peer (the host is 1).</summary>
    public Dictionary<int, Player> Peers { get; } = new();
    /// <summary>Host clock. A client keeps its estimate of it and draws <see cref="NetTrack.Delay"/> behind.</summary>
    public double Clock { get; private set; }
    public double RenderTime => Role == NetRole.Client ? Clock - NetTrack.Delay : Clock;
    /// <summary>Bytes sent and received (measurement for multiplayer-sync-design.md 1장 ④).</summary>
    public long BytesSent { get; private set; }
    public long BytesReceived { get; private set; }
    public int SnapshotsReceived { get; private set; }
    public bool Started { get; private set; }
    /// <summary>The room is going back to the lobby: nothing more is sent, so the other side can leave quietly.</summary>
    public bool Ending { get; set; }
    /// <summary>Host: the line that tells a friend where to join (also shown on the pause screen).</summary>
    public string HostInfo { get; private set; } = "";
    /// <summary>Client: events or survivor records it could not apply (each one is skipped, the rest still apply).</summary>
    public int ApplyErrors { get; private set; }

    private readonly Dictionary<int, Enemy> _enemies = new();
    private readonly Dictionary<int, NetTrack> _playerTracks = new();
    private readonly HashSet<int> _readyPeers = new();
    private readonly List<Enemy> _newEnemies = new();
    private readonly Dictionary<int, (double Time, bool Critical)> _lastHitEvent = new();
    private readonly NetEventBatch _batch = new();
    private int _nextEnemyId, _inputSeq, _snapshotIndex;
    private float _snapshotTimer;
    private byte[] _lastWorld = System.Array.Empty<byte>();
    private bool _clockSet;

    public static StageNet Attach(Stage stage, NetRole role)
    {
        var net = new StageNet { Name = "Net", _stage = stage, Role = role };
        // After every survivor and enemy has moved this frame, so snapshots carry this frame's positions.
        net.ProcessPhysicsPriority = 100;
        stage.AddChild(net);
        return net;
    }

    public override void _Ready()
    {
        if (Role == NetRole.Host)
        {
            Multiplayer.PeerDisconnected += OnPeerLeft;
            Peers[HostPeer] = _stage.Player;
            _stage.Player.PeerId = HostPeer;
            // Tell the host what to give a friend: this PC (127.0.0.1) or its address on the router.
            var lan = NetLaunch.LanAddresses();
            HostInfo = Loc.Format("fmt_net_hosting", lan.Length > 0 ? string.Join(" / ", lan) : "127.0.0.1", NetLaunch.Port);
            _stage.ShowNotice(HostInfo, 12);
            GD.Print("[net] " + HostInfo);
            // The start fill ran before this node existed: number those enemies now, in spawn order.
            foreach (var e in _stage.AllEnemies)
                if (e.NetId == 0) { e.NetId = ++_nextEnemyId; _enemies[e.NetId] = e; }
            Started = true;
            // Friends waiting in the room load the stage now that it can take them.
            NetLaunch.Lobby?.HostLive();
        }
        else
        {
            Multiplayer.ServerDisconnected += () => LeaveMatch("msg_net_host_left");
            int me = Multiplayer.GetUniqueId();
            _stage.Player.PeerId = me;
            Peers[me] = _stage.Player;
            _stage.RequestUse = item => Send(MethodName.ClientUse, item.NetId);
            RpcId(HostPeer, MethodName.ClientReady, _stage.Player.Definition.Id, NetIds.DataHash());
        }
    }

    /// <summary>Client: the host refused us (other data or a full room).</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void HostRefused() => LeaveMatch("msg_net_peer_refused");

    /// <summary>The match is over for this PC: say why, then go back to the menu (which closes the link).</summary>
    private void LeaveMatch(string reason)
    {
        if (!IsInsideTree()) return;
        _stage.ShowNotice(Loc.Text(reason), 4);
        GetTree().CreateTimer(3).Timeout += () => { if (IsInstanceValid(_stage) && _stage.IsInsideTree()) _stage.BackToMenu(); };
    }

    /// <summary>Nothing goes out once the link is gone (the other side closed or crashed).</summary>
    private bool Connected => Multiplayer.MultiplayerPeer is { } peer && peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;

    private void Send(StringName method, params Variant[] args)
    {
        if (!Connected) return;
        foreach (var a in args) if (a.VariantType == Variant.Type.PackedByteArray) BytesSent += a.AsByteArray().Length;
        RpcId(HostPeer, method, args);
    }

    // =====================================================================
    // Host
    // =====================================================================

    /// <summary>A client finished building its stage: give it a survivor and everything that already exists.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientReady(string characterId, int dataHash)
    {
        if (Role != NetRole.Host) return;
        int peer = Multiplayer.GetRemoteSenderId();
        // Another build or other data would read our ids differently: refuse it (multiplayer-sync-design.md 6장).
        if (dataHash != NetIds.DataHash() || Peers.Count > NetLaunch.MaxClients)
        {
            GD.PushWarning($"[net] refused peer {peer}: {(dataHash != NetIds.DataHash() ? "different data" : "room full")}");
            RpcId(peer, MethodName.HostRefused);
            return;
        }
        if (!Peers.ContainsKey(peer))
        {
            var def = CharacterCatalog.Player(characterId);
            var survivor = _stage.AddRemoteSurvivor(def, ClientStart(_stage), peer);
            Peers[peer] = survivor;
        }
        _readyPeers.Add(peer);
        GD.Print($"[net] peer {peer} joined as {characterId}");
        // Welcome: who the host plays, then every enemy and dropped weapon that exists right now.
        var welcome = new NetEventBatch();
        welcome.Begin(NetEvent.World); welcome.W.Str(_stage.Player.Definition.Id); WriteWorld(welcome.W);
        foreach (var e in _stage.AllEnemies.Where(e => !e.IsDead)) { welcome.Begin(NetEvent.EnemySpawn); WriteSpawn(welcome.W, e); }
        foreach (var p in _stage.Interactables.OfType<WeaponPickup>().Where(p => p.State != null)) { welcome.Begin(NetEvent.PickupSpawn); WritePickup(welcome.W, p); }
        foreach (var l in _stage.Interactables.OfType<LootPickup>()) { welcome.Begin(NetEvent.LootSpawn); WriteLoot(welcome.W, l); }
        SendEvents(peer, welcome.Take());
    }

    /// <summary>Where a client's survivor starts: beside the host's.</summary>
    public static Vector3 ClientStart(Stage stage) => stage.Map.PlayerStart + new Vector3(1.2f, 0, 1.2f);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 1)]
    private void ClientInput(byte[] data)
    {
        if (Role != NetRole.Host) return;
        BytesReceived += data.Length;
        if (!Peers.TryGetValue(Multiplayer.GetRemoteSenderId(), out var survivor)) return;
        survivor.ApplyNetInput(NetInput.Read(new NetReader(data)));
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientUse(int interactableId)
    {
        if (Role != NetRole.Host || !Peers.TryGetValue(Multiplayer.GetRemoteSenderId(), out var who)) return;
        var item = _stage.Interactables.FirstOrDefault(i => i.NetId == interactableId);
        // The host checks reach and state itself, like the local E key does, rather than trusting the request.
        if (item == null || !item.Enabled || !item.Visible || item.GlobalPosition.DistanceTo(who.GlobalPosition) > item.Radius + .5f) return;
        if (who.IsDead || who.IsDowned || who.IsStunned || who.IsRestrained || who.Reviving != null) return;
        item.Use(who);
    }

    private void OnPeerLeft(long id)
    {
        int peer = (int)id;
        _readyPeers.Remove(peer);
        if (peer == HostPeer || !Peers.Remove(peer, out var survivor)) return;
        _stage.Players.Remove(survivor);
        survivor.QueueFree();
        GD.Print($"[net] peer {peer} left");
        _stage.CheckWipe();
    }

    // ----- hooks the game calls on the host -----

    public void OnEnemySpawned(Enemy enemy)
    {
        if (Role != NetRole.Host) return;
        enemy.NetId = ++_nextEnemyId;
        _enemies[enemy.NetId] = enemy;
        _newEnemies.Add(enemy); // written at the end of the frame, after the spawner set its reaction
    }

    public void OnEnemyKilled(Enemy enemy, Vector3 direction, float force)
    {
        if (Role != NetRole.Host || enemy.NetId == 0) return;
        FlushNewEnemies();
        _batch.Begin(NetEvent.EnemyDeath); _batch.W.U16(enemy.NetId); _batch.W.Pos(direction); _batch.W.F32(force);
        _batch.W.U8(enemy.CriticalDeath ? 1 : 0);
        _enemies.Remove(enemy.NetId);
        _lastHitEvent.Remove(enemy.NetId);
    }

    public void OnEnemyHit(Enemy enemy, Vector3 point, Vector3 direction, bool critical)
    {
        if (Role != NetRole.Host || enemy.NetId == 0) return;
        // A shotgun hits a crowd many times a frame: one picture per enemy per 80 ms is plenty.
        if (_lastHitEvent.TryGetValue(enemy.NetId, out var last) && Clock - last.Time < .08 && last.Critical == critical) return;
        _lastHitEvent[enemy.NetId] = (Clock, critical);
        _batch.Begin(NetEvent.EnemyHit); _batch.W.U16(enemy.NetId); _batch.W.Pos(point); _batch.W.Yaw(direction);
        _batch.W.U8(critical ? 1 : 0);
    }

    public void OnEnemyAttack(Enemy enemy, NetAttack kind, Player? victim, Vector3 point)
    {
        if (Role != NetRole.Host || enemy.NetId == 0) return;
        FlushNewEnemies();
        _batch.Begin(NetEvent.EnemyAttack); _batch.W.U16(enemy.NetId); _batch.W.U8((int)kind); _batch.W.I32(victim?.PeerId ?? 0); _batch.W.Pos(point);
    }

    public void OnEnemyAttackEnd(Enemy enemy)
    {
        if (Role != NetRole.Host || enemy.NetId == 0 || enemy.IsDead) return;
        _batch.Begin(NetEvent.EnemyAttackEnd); _batch.W.U16(enemy.NetId);
    }

    public void OnEnemyProjectile(Enemy enemy, Vector3 from, Vector3 target)
    {
        if (Role != NetRole.Host || enemy.NetId == 0) return;
        _batch.Begin(NetEvent.EnemyProjectile); _batch.W.U16(enemy.NetId); _batch.W.Pos(from); _batch.W.Pos(target);
    }

    public void OnPlayerHit(Player who, Vector3 direction)
    {
        if (Role != NetRole.Host || who.PeerId == 0) return;
        _batch.Begin(NetEvent.PlayerHit); _batch.W.I32(who.PeerId); _batch.W.Pos(direction);
    }

    /// <summary>Someone fired: hitscan ends, or the rocket/mortar/melee start. Owners already drew their own.</summary>
    public void OnShot(Player shooter, WeaponDefinition def, Vector3 from, IReadOnlyList<Vector3> ends, Vector3 aim)
    {
        if (Role != NetRole.Host || shooter.PeerId == 0) return;
        _batch.Begin(NetEvent.Shot); _batch.W.I32(shooter.PeerId); _batch.W.U16(NetIds.Weapon(def.Id)); _batch.W.Pos(from); _batch.W.Pos(aim);
        _batch.W.U8(ends.Count);
        foreach (var end in ends) _batch.W.Pos(end);
    }

    public void OnThrow(Player thrower, ThrowableDefinition def, Vector3 from, Vector3 target)
    {
        if (Role != NetRole.Host || thrower.PeerId == 0) return;
        _batch.Begin(NetEvent.Throw); _batch.W.I32(thrower.PeerId); _batch.W.U8(NetIds.Throwable(def.Id)); _batch.W.Pos(from); _batch.W.Pos(target);
    }

    public void OnBarrelExplode(ExplosiveBarrel barrel)
    {
        if (Role != NetRole.Host) return;
        _batch.Begin(NetEvent.BarrelExplode); _batch.W.U16(barrel.NetId);
    }

    /// <summary>Host: one client's card queue and the mode's card state (Onslaught, decisions.md M-27). False = not sent yet.</summary>
    public bool SendCards(Player who, byte[] data)
    {
        if (Role != NetRole.Host || !Started) return false;
        int peer = Peers.FirstOrDefault(p => p.Value == who).Key;
        if (peer == 0 || peer == HostPeer || !_readyPeers.Contains(peer)) return false;
        BytesSent += data.Length;
        RpcId(peer, MethodName.HostCards, data);
        return true;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void HostCards(byte[] data) => _stage.ModeRules?.ApplyCardSync(data);

    /// <summary>Client: which card we picked; the host applies it to our survivor.</summary>
    public void RequestCardPick(int serial, int index) => Send(MethodName.ClientPickCard, serial, index);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientPickCard(int serial, int index)
    {
        if (Role != NetRole.Host || !Peers.TryGetValue(Multiplayer.GetRemoteSenderId(), out var who)) return;
        _stage.ModeRules?.PickCard(who, serial, index);
    }

    /// <summary>Client: an Outpost inventory drag goes to the host, which applies it to our survivor (decisions.md M-25).</summary>
    public void RequestInventory(int from, int to) => Send(MethodName.ClientInventory, from, to);

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ClientInventory(int from, int to)
    {
        if (Role != NetRole.Host || !Peers.TryGetValue(Multiplayer.GetRemoteSenderId(), out var who)) return;
        if (who.IsDead || who.IsDowned || who.IsRestrained) return;
        InventoryOps.Drag(who, from, to);
    }

    public void OnLootSpawn(LootPickup loot)
    {
        if (Role != NetRole.Host || !Started) return;
        _batch.Begin(NetEvent.LootSpawn); WriteLoot(_batch.W, loot);
    }

    private static void WriteLoot(NetWriter w, LootPickup l)
    {
        w.U16(l.NetId); w.U8((int)l.Item.Kind); w.U16(NetIds.Item(l.Item.Kind, l.Item.Id)); w.U16(l.Item.Count); w.Pos(l.GlobalPosition);
    }

    public void OnPickupSpawn(WeaponPickup pickup)
    {
        if (Role != NetRole.Host || !Started) return;
        _batch.Begin(NetEvent.PickupSpawn); WritePickup(_batch.W, pickup);
    }

    // ----- host frame -----

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Clock += dt;
        if (!Connected || Ending) return;
        if (Role == NetRole.Client) { ClientFrame(dt); return; }
        if (_readyPeers.Count == 0) { _batch.Take(); _newEnemies.Clear(); return; }
        FlushNewEnemies();
        var world = World();
        if (!world.AsSpan().SequenceEqual(_lastWorld))
        {
            _lastWorld = world;
            _batch.Begin(NetEvent.World); _batch.W.Str(_stage.Player.Definition.Id);
            foreach (var b in world) _batch.W.U8(b);
        }
        if (_batch.Count > 0)
        {
            var packet = _batch.Take();
            foreach (var peer in _readyPeers) SendEvents(peer, packet);
        }
        _snapshotTimer -= dt;
        if (_snapshotTimer > 0) return;
        _snapshotTimer += SnapshotInterval;
        // The shutter's progress in Scenario; the HUD bar (base health, boss health) in the other modes.
        float bar = _stage.ModeRules?.BarFraction
            ?? (_stage.Objective == Objective.HoldOut ? 1 - _stage.Director.CrescendoRemaining / Mathf.Max(.01f, _stage.Director.CrescendoDuration) : 0);
        var snap = new NetSnapshot { Time = Clock, Shutter = bar };
        foreach (var p in Peers.Values) if (IsInstanceValid(p)) snap.Players.Add(p.CaptureNetState());
        if (_snapshotIndex++ % 2 == 0)
        {
            snap.Enemies = new List<NetEnemyState>();
            foreach (var e in _stage.Enemies)
            {
                if (e.IsDead || e.NetId == 0) continue;
                var flags = NetEnemyFlags.None;
                if (e.Awake) flags |= NetEnemyFlags.Awake;
                if (e.StunTime > 0) flags |= NetEnemyFlags.Stunned;
                if (e is BullEnemy bull && bull.State == BullEnemy.Mode.Charge) flags |= NetEnemyFlags.Charging;
                if (e is BullEnemy b2 && b2.State == BullEnemy.Mode.Windup) flags |= NetEnemyFlags.Winding;
                snap.Enemies.Add(new NetEnemyState { Id = e.NetId, Position = e.GlobalPosition, Facing = e.Rig.Forward, Flags = flags });
            }
        }
        var bytes = snap.Encode();
        foreach (var peer in _readyPeers)
        {
            RpcId(peer, MethodName.HostSnapshot, bytes);
            BytesSent += bytes.Length;
        }
    }

    private void FlushNewEnemies()
    {
        if (_newEnemies.Count == 0) return;
        var batch = _newEnemies.ToList();
        _newEnemies.Clear();
        foreach (var e in batch)
        {
            if (!IsInstanceValid(e) || e.IsDead) continue;
            _batch.Begin(NetEvent.EnemySpawn);
            WriteSpawn(_batch.W, e);
        }
    }

    private static void WriteSpawn(NetWriter w, Enemy e)
    {
        w.U16(e.NetId); w.U8(NetIds.Monster(e.Monster.Id)); w.Pos(e.GlobalPosition); w.U8((int)e.Reaction); w.U8(e.Awake ? 1 : 0);
    }

    private static void WritePickup(NetWriter w, WeaponPickup p)
    {
        w.U16(p.NetId); w.U16(NetIds.Weapon(p.Weapon!.Id)); w.Pos(p.GlobalPosition); w.U16(p.State?.Mag ?? -1 & 0xFFFF); w.U16(p.State?.Reserve ?? 0);
    }

    /// <summary>Objective, doors (bit i = the map's door i), the alarm (the bit after the doors) and every usable thing's on/off state.</summary>
    private byte[] World()
    {
        var w = new NetWriter();
        WriteWorld(w);
        return w.ToArray();
    }

    private void WriteWorld(NetWriter w)
    {
        var map = _stage.Map;
        w.U8((int)_stage.Objective);
        w.U8((int)_stage.State);
        int bits = 0;
        for (int i = 0; i < map.Doors.Count; i++) if (map.Doors[i].IsOpen) bits |= 1 << i;
        if (map.AlarmDevice is { Triggered: true }) bits |= 1 << map.Doors.Count;
        w.U8(bits);
        w.U16(_stage.Interactables.Count);
        foreach (var i in _stage.Interactables) { w.U16(i.NetId); w.U8((i.Enabled ? 1 : 0) | (i.Visible ? 2 : 0)); }
        var barrels = _stage.Props.OfType<ExplosiveBarrel>().ToList();
        w.U16(barrels.Count);
        foreach (var b in barrels) w.U16(b.NetId);
    }

    private void SendEvents(int peer, byte[] packet)
    {
        RpcId(peer, MethodName.HostEvents, packet);
        BytesSent += packet.Length;
    }

    // =====================================================================
    // Client
    // =====================================================================

    private void ClientFrame(float dt)
    {
        // Own input every physics frame (presses since the last one ride along).
        var local = _stage.Player;
        var w = new NetWriter();
        local.TakeNetInput(++_inputSeq).Write(w);
        var bytes = w.ToArray();
        RpcId(HostPeer, MethodName.ClientInput, bytes);
        BytesSent += bytes.Length;
        // Other survivors walk to where the host had them a moment ago.
        foreach (var (peer, survivor) in Peers)
        {
            if (survivor == local || !IsInstanceValid(survivor) || !_playerTracks.TryGetValue(peer, out var track)) continue;
            if (!track.Sample(RenderTime, out var pos, out var aim)) continue;
            survivor.NetMotion = pos;
            survivor.SetControls(Vector2.Zero, aim, false);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered, TransferChannel = 2)]
    private void HostSnapshot(byte[] data)
    {
        if (Role != NetRole.Client) return;
        BytesReceived += data.Length;
        SnapshotsReceived++;
        var snap = NetSnapshot.Decode(data);
        // Follow the host's clock: jump on a big gap, otherwise drift toward it.
        if (!_clockSet || Mathf.Abs((float)(snap.Time - Clock)) > .5f) { Clock = snap.Time; _clockSet = true; }
        else Clock += (snap.Time - Clock) * .1;
        foreach (var s in snap.Players)
        {
            if (!Peers.TryGetValue(s.Peer, out var survivor) || !IsInstanceValid(survivor)) continue;
            try
            {
                survivor.ApplyNetVitals(s);
                survivor.ApplyNetInventory(s); // the model in hand, for everyone; ammo and slot for us
            }
            catch (Exception e) { ApplyErrors++; GD.PushError($"[net] could not apply survivor {s.Peer}: {e.Message}"); }
            if (survivor == _stage.Player)
            {
                // While the host moves us (grabbed, stunned, down) we follow its position instead of our input.
                survivor.NetMotion = s.Flags.HasFlag(NetPlayerFlags.HostMoves) ? s.Position : null;
                continue;
            }
            if (!_playerTracks.TryGetValue(s.Peer, out var track)) _playerTracks[s.Peer] = track = new NetTrack();
            track.Push(snap.Time, s.Position, s.Aim, (int)s.Flags);
        }
        if (snap.Enemies != null)
            foreach (var e in snap.Enemies)
                if (_enemies.TryGetValue(e.Id, out var enemy) && IsInstanceValid(enemy) && !enemy.IsDead)
                    enemy.Track!.Push(snap.Time, e.Position, e.Facing, (int)e.Flags);
        if (_stage.Objective == Objective.HoldOut) _stage.Map.Shutter?.SetProgress(snap.Shutter);
        _stage.ModeRules?.ApplyNetBar(snap.Shutter);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void HostEvents(byte[] data)
    {
        if (Role != NetRole.Client) return;
        BytesReceived += data.Length;
        var r = new NetReader(data);
        int count = r.U8() | r.U8() << 8;
        for (int i = 0; i < count && r.More; i++)
        {
            var type = (NetEvent)r.U8();
            var body = r.Slice(r.U16());
            // One event we cannot read must not take the rest of the frame with it (I-73).
            try { ApplyEvent(type, body); }
            catch (Exception e) { ApplyErrors++; GD.PushError($"[net] could not apply {type}: {e.Message}"); }
        }
    }

    private Player? PeerPlayer(int peer) => Peers.TryGetValue(peer, out var p) && IsInstanceValid(p) ? p : null;

    private void ApplyEvent(NetEvent type, NetReader r)
    {
        switch (type)
        {
            case NetEvent.World:
            {
                string hostCharacter = r.Str();
                if (!Peers.ContainsKey(HostPeer))
                {
                    Peers[HostPeer] = _stage.AddRemoteSurvivor(CharacterCatalog.Player(hostCharacter), _stage.Map.PlayerStart, HostPeer);
                    Started = true;
                    GD.Print($"[net] synced with the host ({hostCharacter})");
                }
                ApplyWorld(r);
                break;
            }
            case NetEvent.EnemySpawn:
            {
                int id = r.U16();
                var monster = MonsterDatabase.Get(NetIds.MonsterAt(r.U8()));
                var at = r.Pos();
                var reaction = (Reaction)r.U8();
                bool awake = r.U8() != 0;
                if (_enemies.ContainsKey(id)) break;
                var enemy = _stage.Spawn(monster, at, awake);
                enemy.MakePuppet();
                enemy.NetId = id;
                enemy.Reaction = reaction;
                enemy.Awake = awake;
                enemy.Track!.Push(Clock, at, enemy.Rig.Forward, awake ? 1 : 0);
                _enemies[id] = enemy;
                break;
            }
            case NetEvent.EnemyDeath:
            {
                int id = r.U16();
                var direction = r.Pos();
                float force = r.F32();
                bool critical = r.U8() != 0;
                if (_enemies.Remove(id, out var enemy) && IsInstanceValid(enemy) && !enemy.IsDead) enemy.DieFromHit(direction, force, critical);
                break;
            }
            case NetEvent.EnemyHit:
            {
                int id = r.U16();
                var point = r.Pos();
                var dir = r.Yaw();
                bool critical = r.U8() != 0;
                if (_enemies.TryGetValue(id, out var enemy) && IsInstanceValid(enemy)) enemy.ShowNetHit(point, dir, critical);
                break;
            }
            case NetEvent.EnemyAttack:
            {
                int id = r.U16();
                var kind = (NetAttack)r.U8();
                var victim = PeerPlayer(r.I32());
                var point = r.Pos();
                if (!_enemies.TryGetValue(id, out var enemy) || !IsInstanceValid(enemy) || enemy.IsDead) break;
                switch (kind)
                {
                    case NetAttack.Melee:
                        var to = (victim?.GlobalPosition ?? point) - enemy.GlobalPosition;
                        to.Y = 0;
                        enemy.PuppetMelee(victim, to);
                        break;
                    case NetAttack.Ranged when enemy is RangedEnemy ranged: ranged.StartWindup(victim); break;
                    case NetAttack.Capture when enemy is CaptureEnemy captor: captor.StartWindup(victim); break;
                    case NetAttack.Charge when enemy is BullEnemy bull: bull.StartWindup(point - bull.GlobalPosition); break;
                }
                break;
            }
            case NetEvent.EnemyAttackEnd:
            {
                if (_enemies.TryGetValue(r.U16(), out var enemy) && IsInstanceValid(enemy) && !enemy.IsDead) enemy.PuppetEndAttack();
                break;
            }
            case NetEvent.EnemyProjectile:
            {
                int id = r.U16();
                var from = r.Pos();
                var target = r.Pos();
                if (!_enemies.TryGetValue(id, out var enemy) || !IsInstanceValid(enemy) || enemy.Monster.Ranged is not { } shot) break;
                var projectile = new EnemyProjectile();
                projectile.Setup(_stage, from, target, shot, 0); // the host already decided the damage
                _stage.AddChild(projectile);
                _stage.Sfx.PlayAt(enemy.Monster.Behavior == MonsterBehavior.Shooter ? "shot_rifle" : "zombie_hit", from);
                enemy.PuppetEndAttack();
                break;
            }
            case NetEvent.PlayerHit:
            {
                var who = PeerPlayer(r.I32());
                var direction = r.Pos();
                who?.ShowNetHit(direction);
                break;
            }
            case NetEvent.Shot:
            {
                var shooter = PeerPlayer(r.I32());
                var def = NetIds.WeaponDef(r.U16());
                var from = r.Pos();
                var aim = r.Pos();
                int n = r.U8();
                var ends = new List<Vector3>(n);
                for (int i = 0; i < n; i++) ends.Add(r.Pos());
                if (shooter == null || shooter == _stage.Player) break; // our own shots were drawn when we fired
                shooter.Weapons.ShowNetShot(def, from, aim, ends);
                break;
            }
            case NetEvent.Throw:
            {
                var thrower = PeerPlayer(r.I32());
                var def = ArmoryCatalog.Throwable(NetIds.ThrowableAt(r.U8()));
                var from = r.Pos();
                var target = r.Pos();
                if (thrower == null || thrower == _stage.Player) break;
                ThrownItem.Throw(_stage, def, from, target, thrower);
                break;
            }
            case NetEvent.BarrelExplode:
            {
                int id = r.U16();
                _stage.Props.OfType<ExplosiveBarrel>().FirstOrDefault(b => b.NetId == id)?.Explode();
                break;
            }
            case NetEvent.PickupSpawn:
            {
                int id = r.U16();
                var def = NetIds.WeaponDef(r.U16());
                var at = r.Pos();
                int mag = r.U16(), reserve = r.U16();
                if (_stage.Interactables.Any(i => i.NetId == id)) break;
                var state = mag == 0xFFFF ? null : new WeaponState(def, mag, reserve);
                var pickup = WeaponPickup.ForWeapon(_stage, at, def, state, id);
                break;
            }
            case NetEvent.LootSpawn:
            {
                int id = r.U16();
                var kind = (ItemKind)r.U8();
                var itemId = NetIds.ItemAt(kind, r.U16());
                int count = r.U16();
                var at = r.Pos();
                if (_stage.Interactables.Any(i => i.NetId == id)) break;
                LootPickup.Spawn(_stage, at, new InvItem { Kind = kind, Id = itemId, Count = count }, id);
                break;
            }
        }
    }

    private void ApplyWorld(NetReader r)
    {
        var objective = (Objective)r.U8();
        var state = (StageState)r.U8();
        int bits = r.U8();
        var map = _stage.Map;
        _stage.ApplyNetObjective(objective);
        for (int i = 0; i < map.Doors.Count; i++)
        {
            var door = map.Doors[i];
            bool open = (bits & (1 << i)) != 0;
            if (open != door.IsOpen) door.SetOpen(open, door.OpenSeconds);
        }
        if ((bits & (1 << map.Doors.Count)) != 0) map.AlarmDevice?.Sound();
        int n = r.U16();
        var listed = new HashSet<int>();
        for (int i = 0; i < n; i++)
        {
            int id = r.U16();
            int on = r.U8();
            listed.Add(id);
            var item = _stage.Interactables.FirstOrDefault(x => x.NetId == id);
            if (item == null) continue;
            item.Enabled = (on & 1) != 0;
            item.Visible = (on & 2) != 0;
        }
        foreach (var gone in _stage.Interactables.Where(i => i is WeaponPickup or LootPickup && !listed.Contains(i.NetId)).ToList())
        {
            _stage.Interactables.Remove(gone);
            gone.QueueFree();
        }
        int barrels = r.U16();
        var alive = new HashSet<int>();
        for (int i = 0; i < barrels; i++) alive.Add(r.U16());
        foreach (var b in _stage.Props.OfType<ExplosiveBarrel>().Where(b => !alive.Contains(b.NetId)).ToList()) b.Explode();
        _stage.ApplyNetState(state);
    }
}
