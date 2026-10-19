namespace HordeShooter;

/// <summary>State carried from the menu into the stage (single player, so a static holder is enough).</summary>
public static class GameSession
{
    public const string DefaultCharacterId = "goose";
    public static string SelectedCharacterId { get; set; } = DefaultCharacterId;
    public static string SelectedTheme { get; set; } = "zombie";
    // Temporary solo-test escape. Disable when teammate rescue is available.
    public static bool DebugEscapeEnabled { get; set; } = true;
}
