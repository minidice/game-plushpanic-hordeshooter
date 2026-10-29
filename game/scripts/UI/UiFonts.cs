using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HordeShooter;

public enum UiFontRole { Body, Menu, Title }

/// <summary>Shared font handles survive language switches, including on existing Label3D nodes.</summary>
public static class UiFonts
{
    private static readonly UiFontCatalog Catalog = new();
    private static readonly Dictionary<UiFontRole, FontVariation> Fonts = new();

    static UiFonts() => Loc.LanguageChanged += Refresh;

    public static Font Get(UiFontRole role = UiFontRole.Body)
    {
        if (!Fonts.TryGetValue(role, out var font))
        {
            font = new FontVariation { BaseFont = Catalog.GetFont(Loc.Language, role) };
            Fonts[role] = font;
        }
        return font;
    }

    private static void Refresh()
    {
        foreach (var (role, font) in Fonts) font.BaseFont = Catalog.GetFont(Loc.Language, role);
    }
}

/// <summary>Locale/role mapping is data, so each language can replace its font independently.</summary>
public sealed class UiFontCatalog
{
    public const string DefaultPath = "res://content/data/ui_fonts.json";
    private readonly JsonElement _profiles;
    private readonly Dictionary<(string, UiFontRole), Font> _fonts = new();

    public UiFontCatalog(string path = DefaultPath)
    {
        if (!FileAccess.FileExists(path)) { GD.PushWarning($"Font profiles missing: {path}; using system fonts."); return; }
        try
        {
            using var doc = JsonDocument.Parse(FileAccess.GetFileAsString(path));
            _profiles = doc.RootElement.Clone();
            if (_profiles.ValueKind != JsonValueKind.Object) GD.PushWarning("Font profiles must be an object; using system fonts.");
        }
        catch (JsonException e) { GD.PushWarning($"Invalid font profiles: {e.Message}; using system fonts."); }
    }

    public Font GetFont(string language, UiFontRole role)
    {
        if (_fonts.TryGetValue((language, role), out var cached)) return cached;
        int weight = role == UiFontRole.Title ? 800 : role == UiFontRole.Menu ? 700 : 400;
        var system = new SystemFont
        {
            FontNames = new[] { "sans-serif" }, FontWeight = weight, AllowSystemFallback = true,
        };
        Font result = system;
        string key = role.ToString().ToLowerInvariant();
        if (_profiles.ValueKind == JsonValueKind.Object && _profiles.TryGetProperty(language, out var profile)
            && profile.ValueKind == JsonValueKind.Object && profile.TryGetProperty(key, out var entry)
            && entry.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(entry.GetString()))
        {
            string path = entry.GetString()!;
            // Paths stay project-local; absent or incompatible resources safely use the OS font.
            if (path.StartsWith("res://", StringComparison.Ordinal) && ResourceLoader.Exists(path, "Font"))
            {
                if (GD.Load(path) is Font source)
                {
                    var font = (Font)source.Duplicate();
                    if (font is FontFile file) file.AllowSystemFallback = true;
                    font.Fallbacks = new Godot.Collections.Array<Font> { system };
                    result = font;
                }
            }
            if (result == system) GD.PushWarning($"Font {language}/{key} unavailable: {path}; using system font.");
        }
        _fonts[(language, role)] = result;
        return result;
    }
}
