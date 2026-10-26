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
    /// Launch option <c>--large-stages</c> (Start-Game.ps1 -LargeStages): the scenario plays the large campaign drafts
    /// (decisions.md L-30) — the urban Stage 1, the forest Stage 2 and the size draft as Stage 3 (<see cref="LayoutMap"/>),
    /// joined by the safehouse hatches, before they replace today's Stage 1 and 2. Single player only: a room's other PCs
    /// would build today's stages.
    /// </summary>
    public static bool LargeStages { get; set; } = OS.GetCmdlineUserArgs().Contains("--large-stages");

    /// <summary>
    /// Launch option <c>--stage3-draft</c> (Start-Game.ps1 -Stage3Draft; the old <c>--stage2-draft</c> still works): a new
    /// run starts on the size draft, Stage 3 of the large campaign (L-29 → L-30).
    /// </summary>
    public static bool Stage3Draft { get; set; } = OS.GetCmdlineUserArgs().Any(a => a is "--stage3-draft" or "--stage2-draft");

    /// <summary>Whether the scenario builds the large campaign's maps (<see cref="StageMap.Create"/>).</summary>
    public static bool LargeCampaign => LargeStages || Stage3Draft;

    /// <summary>A new run from the menu or the room: back to Stage 1 (Stage 3 with --stage3-draft), nothing carried.</summary>
    public static void StartCampaign()
    {
        ScenarioStage = Stage3Draft ? 3 : 1;
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
