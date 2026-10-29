using Godot;

namespace HordeShooter;

public partial class Stage
{
    /// <summary>Hats and armor exist in Scenario and Onslaught; Outpost (mode 2) has none (decisions.md C-34).</summary>
    public bool GearEnabled => Mode != GameMode.Survival;

    private readonly RandomNumberGenerator _gearRng = new();

    /// <summary>Host: a new piece on the floor, its option picked from its group now (C-36). Null for an unknown item.</summary>
    public EquipmentPickup? SpawnGear(string equipmentId, Vector3 at)
    {
        if (!GearEnabled || !Authoritative || EquipmentDatabase.Current.Create(equipmentId, _gearRng) is not { } item) return null;
        return EquipmentPickup.Spawn(this, at, item);
    }

    /// <summary>
    /// Host: <paramref name="who"/> puts on the piece. The floor piece goes first, so a repeated request finds nothing; what
    /// they wore in that slot drops at their feet with its option, for anyone to take (C-34). Returns whether it was worn.
    /// </summary>
    public bool TryEquipPickup(EquipmentPickup pickup, Player who)
    {
        if (!Authoritative || !GearEnabled || !IsInstanceValid(pickup) || pickup.IsQueuedForDeletion() || !Interactables.Contains(pickup)) return false;
        if (!pickup.Enabled || who.IsBot || who.IsDead || who.IsDowned || who.IsRestrained) return false;
        if (pickup.GlobalPosition.DistanceTo(who.GlobalPosition) > pickup.Radius + .5f) return false;
        // The very same piece (item and option) changes nothing.
        if (who.Gear.In(pickup.Item.Gear.Slot) is { } worn && worn.Gear == pickup.Item.Gear && worn.Option == pickup.Item.Option) return false;
        Interactables.Remove(pickup);
        pickup.Enabled = false;
        pickup.QueueFree();
        var old = who.Gear.Equip(pickup.Item);
        Effects.FloatingText(who.GlobalPosition + Vector3.Up * 2.6f, Loc.Text(pickup.Item.Gear.NameKey),
            pickup.Item.Gear.Slot == EquipmentSlot.Head ? EquipmentPickup.HeadColor : EquipmentPickup.ArmorColor);
        if (who.IsLocal) Sfx.Play("pickup");
        else Sfx.PlayAt("pickup", who.GlobalPosition + Vector3.Up);
        if (old != null)
        {
            var spot = who.GlobalPosition - who.AimDirection * .9f;
            if (Flow.IsBlocked(spot)) spot = who.GlobalPosition;
            EquipmentPickup.Spawn(this, spot, old);
        }
        return true;
    }
}
