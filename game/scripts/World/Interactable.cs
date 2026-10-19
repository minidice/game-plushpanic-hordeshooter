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
    public float Radius { get; set; } = 1.9f;
    public bool Enabled { get; set; } = true;
    public Action? OnUse { get; set; }

    public void Use()
    {
        if (Enabled) OnUse?.Invoke();
    }
}
