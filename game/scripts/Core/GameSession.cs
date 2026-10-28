using Godot;
using System.Linq;

namespace HordeShooter;

public enum GameMode { Scenario, Survival, Survivor }

/// <summary>Game difficulty (decisions.md M-40): for now it sets how often a survivor can go down and come back.</summary>
public enum Difficulty { Easy, Normal, Hard }

/// <summary>State carried from the menu into the stage (single player, so a static holder is enough).</summary>
public static class GameSession
{
    public const string DefaultCharacterId = "goose";
    public static string SelectedCharacterId { get; set; } = DefaultCharacterId;
    /// <summary>
    /// The enemy family of the survival and onslaught maps (zombie · animals = the hunters · armed, C-31), picked as a
    /// card in the menu's game setup (M-39). A scenario takes its family from the map instead (<see cref="StageMap.EnemyTheme"/>).
    /// </summary>
    public static string SelectedTheme { get; set; } = "zombie";
    public static readonly string[] Themes = { "zombie", "animals", "armed" };
    public static GameMode Mode { get; set; } = GameMode.Scenario;

    /// <summary>
    /// The scenario picked in the menu (M-39): "campaign" is the planned campaign on the large stages (L-39), "short" the
    /// short one on today's small Stage 1 and 2 (temporary). The code default stays "short" so test scenes and scenes
    /// opened straight away keep building the small maps; the menu's saved choice (<see cref="GameSettings.Load"/>)
    /// defaults to "campaign".
    /// </summary>
    public static string Campaign { get; set; } = "short";
    public static readonly string[] Campaigns = { "campaign", "short" };
    public static string ValidCampaign(string id) => Campaigns.Contains(id) ? id : Campaigns[0];
    public static Difficulty Difficulty { get; set; } = Difficulty.Normal;
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

    /// <summary>
    /// Launch option <c>--zone=D</c> or <c>--zone=2:D</c> (Start-Game.ps1 -Zone): a level test run on the large campaign that
    /// starts at that zone's entry on the stage's route, safe-room door already open (doc/features/level/guide-map.md 9장).
    /// Every encounter stays as the map sets it. Single player only, like --large-stages.
    /// </summary>
    public static string StartZone { get; set; } = "";
    public static int StartZoneStage { get; set; } = 1;

    /// <summary>
    /// Launch option <c>--squad=0</c> (Start-Game.ps1 -Squad 0): AI squadmates for this run, over the menu's saved choice
    /// (a tutorial test without the strong AI help). Null = the menu's choice.
    /// </summary>
    public static int? LaunchSquad { get; set; }

    static GameSession()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--squad=") && int.TryParse(arg["--squad=".Length..], out int squad)) LaunchSquad = Mathf.Clamp(squad, 0, 3);
            if (!arg.StartsWith("--zone=")) continue;
            var value = arg["--zone=".Length..];
            int colon = value.IndexOf(':');
            if (colon > 0 && int.TryParse(value[..colon], out int stage)) { StartZoneStage = Mathf.Clamp(stage, 1, 3); value = value[(colon + 1)..]; }
            StartZone = value;
            // Also when the scene is opened straight away, without the menu's StartCampaign.
            ScenarioStage = StartZoneStage;
        }
    }

    /// <summary>Whether the scenario builds the large campaign's maps (<see cref="StageMap.Create"/>).</summary>
    public static bool LargeCampaign => LargeStages || Stage3Draft || StartZone != "" || Campaign == "campaign";

    /// <summary>A new run from the menu or the room: back to Stage 1 (Stage 3 with --stage3-draft), nothing carried.</summary>
    public static void StartCampaign()
    {
        ScenarioStage = StartZone != "" ? StartZoneStage : Stage3Draft ? 3 : 1;
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

    /// <summary>
    /// Launch option <c>--debug-escape</c> (Start-Game.ps1 -DebugEscape): F8 frees a restrained survivor, for solo tests.
    /// Off in a normal run (it left the menu with the test options, M-39).
    /// </summary>
    public static bool DebugEscapeEnabled { get; set; } = OS.GetCmdlineUserArgs().Contains("--debug-escape");
}
