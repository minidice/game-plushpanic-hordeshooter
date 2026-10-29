using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HordeShooter.Tests;

public partial class FontCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++;
        GD.Print("PASS: " + message);
    }

    public override void _Ready()
    {
        bool failed = false;
        string previous = Loc.Language;
        try
        {
            Loc.Language = "kor";
            Check(UiTheme.Get().DefaultFont.GetFontName() == "Pretendard", "Korean UI uses bundled Pretendard independent of installed fonts");
            var theme = UiTheme.Get();
            var root = new Control { Theme = theme };
            AddChild(root);
            var body = UiTheme.Label("게임 시작 · ESCAPE FUR DEAD");
            var menu = new Button { Text = "게임 시작" };
            var title = UiTheme.Label("ESCAPE FUR DEAD", 52, title: true);
            root.AddChild(body); root.AddChild(menu); root.AddChild(title);
            // Godot reports 700 for this static ExtraBold face; its distinct family identifies the real face.
            Check(body.GetThemeFont("font").GetFontWeight() == 400 && menu.GetThemeFont("font").GetFontWeight() == 700
                  && title.GetThemeFont("font").GetFontName() == "Pretendard ExtraBold", "actual controls resolve regular, bold and real ExtraBold faces without fake bold");
            Font original = body.GetThemeFont("font");
            Check(original.HasChar('한') && original.HasChar('A') && original.HasChar('9'), "bundled body font covers Hangul, Latin and digits");
            var world = new Label3D { Font = UiFonts.Get(), Text = "한글" };
            root.AddChild(world);
            Loc.Language = "eng";
            Check(original == body.GetThemeFont("font") && original.GetFontName() == "Pretendard", "English switch preserves existing controls and uses Pretendard");
            Loc.Language = "unconfigured";
            Check(((FontVariation)original).BaseFont is SystemFont { AllowSystemFallback: true }
                  && ((FontVariation)world.Font).BaseFont is SystemFont,
                  "unconfigured locale switches existing UI and world text to system fonts");
            Check(((FontVariation)menu.GetThemeFont("font")).BaseFont is SystemFont { FontWeight: 700 }, "system fallback preserves menu emphasis");
            Loc.Language = "kor";
            Check(original.GetFontName() == "Pretendard" && original.GetFontWeight() == 400, "switching back restores regular Pretendard on the same handle");
            var bundled = ((FontVariation)original).BaseFont;
            Check(bundled.Fallbacks.Count == 1 && bundled.Fallbacks[0] is SystemFont { AllowSystemFallback: true },
                  "missing bundled glyphs have a system fallback chain");
            var fallback = bundled.Fallbacks[0];
            foreach (char c in new[] { 'ع', 'ก', 'Ж' })
                if (fallback.HasChar(c)) Check(original.HasChar(c), $"system-supported character U+{(int)c:X4} is available through the body font");
            CheckProfiles();
            var standalone = Hud.HudLabel("팀 카드 안내");
            AddChild(standalone);
            Check(standalone.GetThemeFont("font").GetFontName() == "Pretendard",
                  "standalone HUD labels carry the shared font outside themed roots");
            standalone.Free();
            var transition = new StageTransition();
            AddChild(transition);
            var stageName = (Label)transition.FindChild("Name", true, false);
            Check(stageName.GetThemeFont("font").GetFontName() == "Pretendard" && stageName.GetThemeFont("font").GetFontWeight() == 400,
                  "independent stage-transition overlay uses the regular body font");
            transition.Free();
            root.Free();
            GD.Print("FONT_CHECK_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("FONT_CHECK_FAIL " + e.Message); failed = true; }
        finally { Loc.Language = previous; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private void CheckProfiles()
    {
        using var source = JsonDocument.Parse(FileAccess.GetFileAsString(UiFontCatalog.DefaultPath));
        foreach (string language in new[] { "kor", "eng" })
            foreach (string role in new[] { "body", "menu", "title" })
                Check(ResourceLoader.Exists(source.RootElement.GetProperty(language).GetProperty(role).GetString()!, "Font"),
                      $"shipped {language}/{role} references an imported font resource");

        const string fixture = "user://font-check-profiles.json";
        void Write(string json) { using var file = FileAccess.Open(fixture, FileAccess.ModeFlags.Write); file.StoreString(json); }
        try
        {
            var kor = source.RootElement.GetProperty("kor");
            Write(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["kor"] = new { body = kor.GetProperty("body").GetString() },
                ["eng"] = new { body = kor.GetProperty("menu").GetString() },
                ["missing"] = new { body = "res://font-does-not-exist.otf" },
                ["invalid"] = new { body = 42 },
                ["wrong_type"] = new { body = "res://scenes/Menu.tscn" },
            }));
            var catalog = new UiFontCatalog(fixture);
            Check(catalog.GetFont("kor", UiFontRole.Body).GetFontWeight() == 400
                  && catalog.GetFont("eng", UiFontRole.Body).GetFontWeight() == 700,
                  "changing one locale's file leaves the other locale independent");
            Check(catalog.GetFont("kor", UiFontRole.Title) is SystemFont { FontWeight: 800 }, "omitted role uses system font at the role weight");
            Check(catalog.GetFont("missing", UiFontRole.Body) is SystemFont, "missing configured file falls back without crashing (expected warning)");
            Check(catalog.GetFont("invalid", UiFontRole.Body) is SystemFont, "non-string path safely falls back");
            var cachedScene = GD.Load<PackedScene>("res://scenes/Menu.tscn");
            Check(catalog.GetFont("wrong_type", UiFontRole.Body) is SystemFont, "non-font resource safely falls back (expected warning)");
            GC.KeepAlive(cachedScene);
            Write("{");
            Check(new UiFontCatalog(fixture).GetFont("kor", UiFontRole.Body) is SystemFont, "malformed JSON safely falls back (expected warning)");
        }
        finally { DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(fixture)); }
    }
}
