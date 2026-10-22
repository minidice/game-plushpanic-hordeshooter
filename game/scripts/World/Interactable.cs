using Godot;
using System;

namespace HordeShooter;

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
    /// <summary>The prompt's text key, for code that needs to know what kind of thing this is.</summary>
    public string PromptKey => _prompt;
    public float Radius { get; set; } = 1.9f;
    public bool Enabled { get; set; } = true;
    /// <summary>Seconds the interact key must be held (0: a press uses it).</summary>
    public float HoldSeconds { get; set; }
    /// <summary>Called with the survivor who used it; null means this PC's player.</summary>
    public Action<Player?>? OnUse { get; set; }
    /// <summary>Same on every PC: level items get it in build order, dropped weapons from the host (multiplayer-sync-design.md 4.4).</summary>
    public int NetId { get; set; }

    public void Use(Player? who = null)
    {
        if (Enabled) OnUse?.Invoke(who);
    }
}
