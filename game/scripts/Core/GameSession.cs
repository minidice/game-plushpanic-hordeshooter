using Godot;
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
    /// <summary>The host's map seed for this match, sent with the start (0 = none: offline picks its own).</summary>
    public static int MapSeed { get; set; }
    /// <summary>Scenario stage being played (1 from the menu; the Stage 1 exit hatch moves on to 2, decisions.md L-14).</summary>
    public static int ScenarioStage { get; set; } = 1;
    /// <summary>What the local survivor carries out of the last stage (null: start with the stage's own kit).</summary>
    public static StageCarry? Carry { get; set; }

    /// <summary>
    /// Launch option <c>--stage2-draft</c> (Start-Game.ps1 -Stage2Draft): scenario Stage 2 is the size draft
    /// (<see cref="LayoutMap"/>, decisions.md L-29) and a new run starts there, for playing it before it replaces today's
    /// Stage 2. Single player only: a room's other PCs would build today's Stage 2.
    /// </summary>
    public static bool Stage2Draft { get; set; } = OS.GetCmdlineUserArgs().Contains("--stage2-draft");

    /// <summary>A new run from the menu or the room: back to Stage 1 (the draft's Stage 2 with --stage2-draft), nothing carried.</summary>
    public static void StartCampaign()
    {
        ScenarioStage = Stage2Draft ? 2 : 1;
        Carry = null;
    }

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
