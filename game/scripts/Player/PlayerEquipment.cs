namespace HordeShooter;

public partial class Player
{
    /// <summary>The hat and armor this survivor wears (decisions.md C-34). Empty at the start of a run.</summary>
    public EquipmentLoadout Gear { get; } = new();
}
