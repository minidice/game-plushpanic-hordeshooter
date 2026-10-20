using Godot;

namespace HordeShooter;

/// <summary>Drives a rig's appear animation (<see cref="CharacterRig.ShowAs"/>) while it runs, then sleeps.</summary>
public partial class RevealFader : Node
{
    public CharacterRig Rig { get; set; } = null!;

    public override void _Process(double delta)
    {
        if (!Rig.StepReveal((float)delta)) SetProcess(false);
    }
}
