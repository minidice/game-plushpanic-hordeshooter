using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>
/// Network side of a survivor (doc/multiplayer-sync-design.md). Each PC moves its own player; the host decides health,
/// downs, grabs and inventory. A survivor on another PC follows positions it is sent (<see cref="NetMotion"/>).
/// </summary>
public partial class Player
{
    /// <summary>A position this body walks to instead of using its own move input (remote players, host overrides).</summary>
    public Vector3? NetMotion { get; set; }
    /// <summary>Client: the host has this survivor grabbed; blocks actions like a real grab.</summary>
    public bool NetRestrained { get; set; }
    /// <summary>Host: the host is moving this survivor right now (grabbed, stunned or launched, down), not its owner.</summary>
    public bool HostMovesMe => OnLadder || OnZipline || OnGondola || IsRestrained || StunTime > 0 || IsDowned || IsDead;
    public int MotionRevision { get; private set; }
    public byte NetTravelKind { get; set; }
    public byte TravelKind => OnLadder ? (byte)1 : OnZipline ? (byte)2 : OnGondola ? (byte)3 : NetTravelKind;

    /// <summary>Repeated snapshots carry this revision until the owner acknowledges it in its input.</summary>
    internal void CommitNetPosition()
    {
        if (!Stage.Authoritative || Stage.Net == null) return;
        MotionRevision++;
        NetMotion = null;
    }

    public bool ApplyNetPosition(NetPlayerState state)
    {
        if (state.MotionRevision < MotionRevision) return false;
        if (state.MotionRevision > MotionRevision)
        {
            MotionRevision = state.MotionRevision;
            if (_gondola != null && state.TravelKind != 3) EndGondolaRide(_gondola);
            TeleportTo(state.Position);
            NetMotion = null;
        }
        if (NetTravelKind == 2 && state.TravelKind != 2) Rig.EndZiplinePose();
        NetTravelKind = state.TravelKind;
        if (NetTravelKind != 0) SetAim(state.Aim);
        return true;
    }

    private readonly byte[] _pressCounts = new byte[(int)NetPress.Count];
    private readonly byte[] _pressSeen = new byte[(int)NetPress.Count];
    private int _slotAsked, _slotPresses, _slotPressesSeen, _slotMismatch;

    private void CountPress(NetPress press) => _pressCounts[(int)press]++;

    /// <summary>Called each physics frame for a client's own player: counts this frame's presses.</summary>
    private void RecordNetEdges()
    {
        if (_firePressed) CountPress(NetPress.Fire);
        if (_reloadPressed) CountPress(NetPress.Reload);
        if (_healPressed) CountPress(NetPress.Heal);
        if (_throwPressed) CountPress(NetPress.Throw);
        if (_cancelPressed) CountPress(NetPress.Cancel);
        if (_rollPressed) CountPress(NetPress.Roll);
        if (_cycle > 0) CountPress(NetPress.CycleUp);
        if (_cycle < 0) CountPress(NetPress.CycleDown);
        if (_slotPressed != 0) { _slotAsked = _slotPressed; _slotPresses = (_slotPresses + 1) & 0xFF; }
    }

    /// <summary>Client: this frame's input for the host, with the running press counts.</summary>
    public NetInput TakeNetInput(int seq)
    {
        var held = NetHeld.None;
        if (_fire) held |= NetHeld.Fire;
        if (_reviveHeld) held |= NetHeld.Revive;
        if (_aimHeld) held |= NetHeld.Aim;
        return new NetInput
        {
            Seq = seq, MotionRevision = MotionRevision, Aim = AimPoint, Held = held, Presses = (byte[])_pressCounts.Clone(),
            Slot = _slotAsked, SlotPresses = _slotPresses, Position = GlobalPosition,
        };
    }

    private bool Pressed(NetInput input, NetPress press)
    {
        int i = (int)press;
        bool any = (byte)(input.Presses[i] - _pressSeen[i]) != 0;
        _pressSeen[i] = input.Presses[i];
        return any;
    }

    /// <summary>Host: a remote player's input drives this survivor like the AI drives a squadmate.</summary>
    public void ApplyNetInput(NetInput input)
    {
        if (input.MotionRevision != MotionRevision || TravelKind != 0)
        {
            System.Array.Copy(input.Presses, _pressSeen, _pressSeen.Length);
            _slotPressesSeen = input.SlotPresses;
            ClearGondolaControls(); NetMotion = null;
            return;
        }
        bool held = input.Held.HasFlag(NetHeld.Fire);
        bool fired = Pressed(input, NetPress.Fire);
        SetControls(Vector2.Zero, input.Aim, held || fired);
        if (fired) _firePressed = true;
        if (!held && fired) _fire = false;
        if (Pressed(input, NetPress.Reload)) PressReload();
        if (Pressed(input, NetPress.Heal)) PressHeal();
        if (Pressed(input, NetPress.Throw)) PressThrow();
        if (Pressed(input, NetPress.Cancel)) PressCancel();
        if (Pressed(input, NetPress.Roll)) PressRoll();
        if (Pressed(input, NetPress.CycleUp)) PressCycle(1);
        if (Pressed(input, NetPress.CycleDown)) PressCycle(-1);
        if (input.SlotPresses != _slotPressesSeen) { _slotPressesSeen = input.SlotPresses; if (input.Slot != 0) PressSlot(input.Slot); }
        HoldRevive(input.Held.HasFlag(NetHeld.Revive));
        // Aiming changes range and focus, which the host decides with the hit (decisions.md C-26).
        _aimHeld = input.Held.HasFlag(NetHeld.Aim);
        // Own movement on the owner's PC (decided 2026-10-20); the host only takes over while it moves them itself.
        NetMotion = HostMovesMe ? null : input.Position;
    }

    /// <summary>Velocity that walks the body to <paramref name="target"/>; jumps there when far behind.</summary>
    private Vector3 FollowVelocity(Vector3 target, float dt)
    {
        // The owner already resolved ground/cliffs. Re-simulating gravity at an old height strands its remote body.
        GlobalPosition = GlobalPosition with { Y = target.Y };
        var to = target - GlobalPosition;
        to.Y = 0;
        if (to.Length() > 4f)
        {
            GlobalPosition = new Vector3(target.X, GlobalPosition.Y, target.Z);
            return Vector3.Zero;
        }
        return (to / Mathf.Max(dt, .001f)).LimitLength(14f);
    }

    /// <summary>Host: what every PC needs to know about this survivor.</summary>
    public NetPlayerState CaptureNetState()
    {
        var flags = NetPlayerFlags.None;
        if (IsRolling) flags |= NetPlayerFlags.Rolling;
        if (Gun.Reloading) flags |= NetPlayerFlags.Reloading;
        if (IsHealing) flags |= NetPlayerFlags.Healing;
        if (IsDowned) flags |= NetPlayerFlags.Downed;
        if (IsDead) flags |= NetPlayerFlags.Dead;
        if (IsRestrained) flags |= NetPlayerFlags.Restrained;
        if (HostMovesMe) flags |= NetPlayerFlags.HostMoves;
        if (StunTime > 0) flags |= NetPlayerFlags.Stunned;
        var l = Loadout;
        return new NetPlayerState
        {
            Peer = PeerId, MotionRevision = MotionRevision, TravelKind = TravelKind, Position = GlobalPosition, Aim = TravelKind != 0 ? GlobalPosition + Rig.Forward * 8 : AimPoint, Health = Health, Medkits = Medkits, BleedLeft = BleedLeft, Flags = flags,
            RollCharges = RollCharges, RollRechargeLeft = RollRechargeLeft, RollRechargeActive = _rollRechargeActive,
            RollPressSeen = _pressSeen[(int)NetPress.Roll],
            ActiveSlot = l.ActiveSlot, TapStacks = TapStacks, Mad = (MadSize > 0 ? 1 : MadSize < 0 ? 2 : 0) + MadLevel * 4,
            Daze = Mathf.Clamp(Mathf.CeilToInt(DazeTime * 10), 0, 255), Weak = Mathf.Clamp(Mathf.CeilToInt(WeakTime * 10), 0, 255),
            Pole = Pole > 0 ? 1 : Pole < 0 ? 2 : 0,
            Secondary = NetIds.Weapon(l.Secondary.Def.Id), SecondaryMag = l.Secondary.Mag, SecondaryReserve = l.Secondary.Reserve,
            Primary = l.Primary == null ? -1 : NetIds.Weapon(l.Primary.Def.Id), PrimaryMag = l.Primary?.Mag ?? 0, PrimaryReserve = l.Primary?.Reserve ?? 0,
            Special = l.Special == null ? -1 : NetIds.Weapon(l.Special.Def.Id), SpecialMag = l.Special?.Mag ?? 0, SpecialReserve = l.Special?.Reserve ?? 0,
            Backpack = Backpack == null ? null : Enumerable.Range(0, Backpack.Size).Where(i => Backpack[i] != null)
                .Select(i => (i, (int)Backpack[i]!.Kind, NetIds.Item(Backpack[i]!.Kind, Backpack[i]!.Id), Backpack[i]!.Count, Backpack[i]!.State?.Mag ?? 0)).ToList(),
            Throwables = l.Throwables.Select(t => (NetIds.Throwable(t.def.Id), t.count)).ToList(),
        };
    }

    /// <summary>Client: health, downs, death and grabs exactly as the host has them.</summary>
    public void ApplyNetVitals(NetPlayerState s)
    {
        NetApplying = true;
        try
        {
            // A snapshot preceding our latest press must not give back the locally predicted charge.
            if (Stage.Role == NetRole.Client && (!IsLocal || s.RollPressSeen == _pressCounts[(int)NetPress.Roll]))
            {
                RollCharges = Mathf.Clamp(s.RollCharges, 0, MaxRollCharges);
                RollRechargeLeft = RollCharges == MaxRollCharges ? 0 : Mathf.Clamp(s.RollRechargeLeft, 0, RollCooldown);
                _rollRechargeActive = RollCharges < MaxRollCharges && s.RollRechargeActive;
            }
            bool dead = s.Flags.HasFlag(NetPlayerFlags.Dead), downed = s.Flags.HasFlag(NetPlayerFlags.Downed);
            if (dead && !IsDead) { Health = 0; Die(Vector3.Zero); return; }
            if (downed && !IsDowned && !IsDead) GoDown();
            else if (!downed && IsDowned) Revive();
            if (s.Health < Health && !IsDowned)
            {
                InterruptAutoRun();
                Rig.FlashHit();
                _hurt = 1;
                DamageFlash = Mathf.Min(1, DamageFlash + .5f + (Health - s.Health) / 40f);
            }
            Health = s.Health;
            if (IsDowned) BleedLeft = s.BleedLeft;
            Medkits = s.Medkits;
            TapStacks = s.TapStacks;
            MadLevel = Mathf.Max(1, s.Mad / 4);
            MadSize = (s.Mad & 3) switch { 1 => 1, 2 => -1, _ => 0 };
            DazeTime = s.Daze / 10f;
            WeakTime = s.Weak / 10f;
            Pole = s.Pole switch { 1 => 1, 2 => -1, _ => 0 };
            NetRestrained = s.Flags.HasFlag(NetPlayerFlags.Restrained);
            // Stunned on the host (bull, flashbang): no shooting or switching here either (I-73).
            if (s.Flags.HasFlag(NetPlayerFlags.Stunned)) StunTime = Mathf.Max(StunTime, .12f);
        }
        finally { NetApplying = false; }
    }

    /// <summary>
    /// Client: weapons, ammo and throwables as the host counts them (the model in hand follows for every survivor).
    /// The slot follows the host when it still differs after a few snapshots: a switch the host refused does not stick.
    /// </summary>
    public void ApplyNetInventory(NetPlayerState s)
    {
        var secondary = NetIds.WeaponDef(s.Secondary);
        var primary = s.Primary < 0 ? null : NetIds.WeaponDef(s.Primary);
        var special = s.Special < 0 ? null : NetIds.WeaponDef(s.Special);
        var throws = s.Throwables.Select(t => (ArmoryCatalog.Throwable(NetIds.ThrowableAt(t.Kind)), t.Count)).ToList();
        bool changed = Loadout.ApplyNet(secondary, s.SecondaryMag, s.SecondaryReserve, primary, s.PrimaryMag, s.PrimaryReserve, special, s.SpecialMag, s.SpecialReserve, s.ActiveSlot, throws);
        _slotMismatch = Loadout.ActiveSlot != s.ActiveSlot ? _slotMismatch + 1 : 0;
        if (_slotMismatch >= 4 && Weapons.SelectSlot(s.ActiveSlot)) changed = true;
        if (_slotMismatch >= 4) _slotMismatch = 0;
        if (s.Backpack != null)
        {
            // Outpost: the host's backpack is the truth (decisions.md M-25).
            Backpack ??= new Backpack();
            Backpack.Clear();
            foreach (var (cell, kind, id, count, mag) in s.Backpack)
            {
                var itemKind = (ItemKind)kind;
                var itemId = NetIds.ItemAt(itemKind, id);
                Backpack.Set(cell, itemKind == ItemKind.Weapon
                    ? InvItem.Weapon(new WeaponState(ArmoryCatalog.Weapon(itemId), mag, 0))
                    : new InvItem { Kind = itemKind, Id = itemId, Count = count });
            }
        }
        if (changed) Weapons.RefreshVisual();
    }
}
