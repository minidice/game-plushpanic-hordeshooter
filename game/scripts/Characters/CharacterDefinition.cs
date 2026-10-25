using Godot;

namespace HordeShooter;

public enum CharacterRole
{
    Player,
    Enemy,
}

/// <summary>Primitive look used when <see cref="CharacterDefinition.Model"/> is empty.</summary>
public enum PlaceholderShape
{
    Capsule,
    Round,
    Balloon,
    Bull,
}

/// <summary>
/// Data-only description of a character look. Gameplay code never references a model file directly:
/// it asks <see cref="CharacterRig"/> to build whatever a definition points at. Adding or swapping a
/// species means adding or editing a .tres in content/characters (players) or content/enemies.
/// See doc/character-model-spec.md for the GLB conventions.
/// </summary>
[GlobalClass]
public partial class CharacterDefinition : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public CharacterRole Role { get; set; } = CharacterRole.Player;
    [Export] public int SortOrder { get; set; }

    [ExportGroup("Model")]
    /// <summary>GLB/scene to instantiate. Leave empty to build a primitive placeholder.</summary>
    [Export] public PackedScene? Model { get; set; }
    /// <summary>True if the model's nose points at +Z (glTF export default). False for Godot-style -Z.</summary>
    [Export] public bool ModelFacesPositiveZ { get; set; } = true;
    [Export] public float ModelScale { get; set; } = 1f;

    [ExportGroup("Model Parts")]
    // Node names inside the model. Empty or missing names get generated pivots, so a model
    // with no named parts still works (it just bobs as one piece).
    [Export] public string BodyNode { get; set; } = "";
    [Export] public string DeformNode { get; set; } = "";
    [Export] public string HeadNode { get; set; } = "";
    [Export] public string LeftFootNode { get; set; } = "";
    [Export] public string RightFootNode { get; set; } = "";
    [Export] public string WeaponNode { get; set; } = "";
    [Export] public string MuzzleNode { get; set; } = "";
    /// <summary>Equipment parts (helmet, vest, pack). Hidden on the zombie version of this character.</summary>
    [Export] public string[] GearNodes { get; set; } = System.Array.Empty<string>();
    /// <summary>The gun that is part of the model. Hidden when the game attaches its own weapon, and on zombies.</summary>
    [Export] public string BuiltInWeaponNode { get; set; } = "";
    /// <summary>Color of the generated hands that hold game weapons (and zombie arms).</summary>
    [Export] public Color HandColor { get; set; } = new("f2ede2");

    [ExportGroup("Placeholder")]
    [Export] public PlaceholderShape Shape { get; set; } = PlaceholderShape.Capsule;
    [Export] public Color PrimaryColor { get; set; } = new("8a9a6a");
    [Export] public Color SecondaryColor { get; set; } = new("2a2a2a");

    [ExportGroup("Zombie")]
    /// <summary>Enemy only: build commons from the player species (gear stripped, tinted) instead of this look.</summary>
    [Export] public bool MirrorPlayerSpecies { get; set; }
    /// <summary>Overlay color for zombie versions; alpha controls how strong the tint is.</summary>
    [Export] public Color ZombieTint { get; set; } = new(.42f, .6f, .32f, .5f);

    [ExportGroup("Body")]
    [Export] public float ColliderRadius { get; set; } = .45f;
    [Export] public float ColliderHeight { get; set; } = 1.8f;
    /// <summary>Survivors only; a monster's speed and health come from monsters.csv (<see cref="MonsterDefinition"/>).</summary>
    [Export] public float MoveSpeed { get; set; } = 4.2f;
    /// <summary>Survivors only, like <see cref="MoveSpeed"/>.</summary>
    [Export] public int MaxHealth { get; set; } = 100;

    [ExportGroup("Motion")]
    [Export] public float StepRate { get; set; } = 13f;
    [Export] public float WaddleDegrees { get; set; } = 7f;
    /// <summary>Visual head spring amplitude; continuous neck models can keep the joint nearly fixed.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float HeadSpringScale { get; set; } = 1f;
    /// <summary>Color of the stuffing that bursts out on hits ("cute gore").</summary>
    [Export] public Color StuffingColor { get; set; } = new("fff4e0");

    /// <summary>Runtime-only: this is a zombie variant built by <see cref="MakeZombie"/>.</summary>
    public bool IsZombie { get; private set; }

    private StandardMaterial3D? _zombieOverlay;

    /// <summary>Shared tint overlay for every zombie built from this definition.</summary>
    public StandardMaterial3D ZombieOverlay => _zombieOverlay ??= new StandardMaterial3D
    {
        AlbedoColor = ZombieTint,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        Roughness = 1,
    };

    /// <summary>
    /// Looks like us, minus the gear: the species model and size, with the enemy definition stats,
    /// gear hidden and a sickly tint (see CharacterRig.Zombify).
    /// </summary>
    public static CharacterDefinition MakeZombie(CharacterDefinition species, CharacterDefinition stats)
    {
        var zombie = (CharacterDefinition)species.Duplicate();
        zombie.Id = $"{stats.Id}_{species.Id}";
        zombie.DisplayName = Loc.Format("fmt_zombie", Loc.Text(species.DisplayName));
        zombie.Role = CharacterRole.Enemy;
        zombie.StepRate = stats.StepRate;
        zombie.WaddleDegrees = stats.WaddleDegrees;
        zombie.StuffingColor = stats.StuffingColor;
        zombie.ZombieTint = stats.ZombieTint;
        zombie.IsZombie = true;
        return zombie;
    }
}
