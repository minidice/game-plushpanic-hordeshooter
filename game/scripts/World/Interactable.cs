using Godot;
using System;

namespace HordeShooter;

/// <summary>Floor supplies the squad AI and pickups tell apart (everything else is <see cref="None"/>).</summary>
public enum PickupKind { None, Medkit, Ammo }

/// <summary>Anything the player can use with the interact key. The stage shows the nearest one's prompt.</summary>
public partial class Interactable : Node3D
{
    private string _prompt = "";

    /// <summary>Set a text key (or ready-made text); reads back in the current language.</summary>
    public string Prompt
    {
        get => Loc.Text(_prompt);
        set => _prompt = value;
    }
    /// <summary>
    /// Which floor supply this is. Code asks this, never the prompt's text key, so renaming a text key cannot quietly stop
    /// squadmates and pickups from finding medkits and ammo (I-94 ②).
    /// </summary>
    public PickupKind Pickup { get; set; }
    public float Radius { get; set; } = 1.9f;
    public bool Enabled { get; set; } = true;
    /// <summary>Seconds the interact key must be held (0: a press uses it).</summary>
    public float HoldSeconds { get; set; }
    /// <summary>A hit on the one holding breaks the hold; they must let go and start over (base doors, decisions.md L-35).</summary>
    public bool CancelOnHit { get; set; }
    /// <summary>Called with the survivor who used it; null means this PC's player.</summary>
    public Action<Player?>? OnUse { get; set; }
    /// <summary>Same on every PC: level items get it in build order, dropped weapons from the host (multiplayer-sync-design.md 4.4).</summary>
    public int NetId { get; set; }

    public void Use(Player? who = null)
    {
        if (Enabled) OnUse?.Invoke(who);
    }
}
