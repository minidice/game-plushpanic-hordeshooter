namespace HordeShooter;

/// <summary>Encounter pools in the existing campaign map.</summary>
public static class StageTheme
{
    public static string Normalize(string? theme) => theme is "animals" or "armed" ? theme : "zombie";
    public static string[] Commons(string theme) => Normalize(theme) switch
    {
        "animals" => new[] { "wild_animal" },
        "armed" => new[] { "armed_animal" },
        _ => new[] { "humanoid" },
    };
    public static string[] Specials(string theme) => Normalize(theme) switch
    {
        "animals" => new[] { "bull", "balloon" },
        "armed" => new[] { "armed_veteran", "bull" },
        _ => new[] { "tether", "leaper", "spitter", "balloon", "bull" },
    };
}
