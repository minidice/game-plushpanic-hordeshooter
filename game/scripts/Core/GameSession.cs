using System.Linq;

namespace HordeShooter;

public enum GameMode { Scenario, Survival, Survivor }

/// <summary>State carried from the menu into the stage (single player, so a static holder is enough).</summary>
public static class GameSession
{
    public const string DefaultCharacterId = "goose";
    public static string SelectedCharacterId { get; set; } = DefaultCharacterId;
    public static string SelectedTheme { get; set; } = "zombie";
    public static GameMode Mode { get; set; } = GameMode.Scenario;

    /// <summary>
    /// Modes the current build can actually start; any other would be listed in the menu as "coming soon".
    /// Survival and Survivor are first versions on the Stage 1 map (doc/menu-multiplayer-plan.md 3.1).
    /// </summary>
    public static readonly GameMode[] PlayableModes = { GameMode.Scenario, GameMode.Survival, GameMode.Survivor };

    /// <summary>
    /// AI squadmates in single player (decisions.md M-11): none, one (default) or three for 4-player video shots.
    /// Multiplayer has none. Stored only for now: squadmates are not spawned yet.
    /// </summary>
    public static readonly int[] SquadChoices = { 0, 1, 3 };
    public const int DefaultSquadSize = 1;
    public static int SquadSize { get; set; } = DefaultSquadSize;
    public static int ValidSquad(int size) => SquadChoices.Contains(size) ? size : DefaultSquadSize;

    // Temporary solo-test escape. Disable when teammate rescue is available.
    public static bool DebugEscapeEnabled { get; set; } = true;
}
