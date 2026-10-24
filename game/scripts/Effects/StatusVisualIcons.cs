using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Same shape symbols in the world, HUD and Studio. No font glyph dependency.</summary>
public static class StatusVisualIcons
{
    private static readonly Dictionary<string, Texture2D> Cache = new();
    public static string Svg(StatusVisualDefinition def)
    {
        string shape = def.Shape switch
        {
            "bubble" => "<circle cx='23' cy='25' r='7'/><circle cx='40' cy='20' r='4'/><path d='M16 42q16-16 32 0M24 43v6m16-6v6'/>",
            "drop" => "<path d='M32 12C27 24 18 29 18 39a14 14 0 0028 0C46 29 37 24 32 12Z'/><path d='M25 38v5'/>",
            "flame" => "<path d='M34 10c5 15-8 14-3 26l9-10c14 21 0 28-9 28S10 44 20 30c0 12 8 9 7 1-2-8 2-15 7-21Z'/>",
            "frost" => "<path d='M32 10v44M13 21l38 22M13 43l38-22M25 15l7 6 7-6M25 49l7-6 7 6M14 29l8-2-1-8M43 45l-1-8 8-2'/>",
            "ice" => "<path d='m32 10 19 11v22L32 54 13 43V21ZM13 21l19 11 19-11M32 32v22M25 18v9m-5-5 10 1M40 34v9'/>",
            "eye" => "<path d='M10 32q22-27 44 0-22 27-44 0Z'/><circle cx='32' cy='32' r='7'/><path d='M13 51l38-38'/>",
            "star" => "<path d='m32 11 6 14 15 2-11 11 3 15-13-7-13 7 3-15-11-11 15-2Z'/>",
            "arc" => "<path d='M37 9 16 36h14l-3 19 23-29H36Z'/>",
            "fear" => "<path d='M18 45V28a14 14 0 0128 0v17l-7-4-7 8-7-8Z'/><path d='M26 27v6m12-6v6M10 19l-4-5m48 5 4-5'/>",
            "weak" => "<path d='m32 10 20 8v14q-2 14-20 22Q14 46 12 32V18Z'/><path d='m35 14-9 15 12 6-9 16'/>",
            "chain" => "<rect x='10' y='22' width='27' height='20' rx='10'/><rect x='27' y='22' width='27' height='20' rx='10'/>",
            "air" => "<path d='M11 36h33q12 0 8-10t-12 0M13 46h23q9 0 7 8M16 25h14M22 16l7-7 7 7m-7-6v17'/>",
            _ => throw new ArgumentException("Unknown status icon")
        };
        return $"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'><rect x='2' y='2' width='60' height='60' rx='16' fill='#182427' stroke='#{def.Color.ToHtml(false)}' stroke-width='3'/><g fill='none' stroke='#{def.Color.Lightened(.25f).ToHtml(false)}' stroke-width='3.3' stroke-linecap='round' stroke-linejoin='round'>{shape}</g></svg>";
    }
    public static Texture2D Texture(string id)
    {
        if (Cache.TryGetValue(id, out var cached)) return cached;
        using var image = new Image();
        if (image.LoadSvgFromString(Svg(StatusVisualCatalog.Current.Get(id)), 2) != Error.Ok) throw new InvalidOperationException("Status icon SVG failed");
        return Cache[id] = ImageTexture.CreateFromImage(image);
    }
}
