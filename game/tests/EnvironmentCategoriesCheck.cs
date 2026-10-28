using Godot;
using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace HordeShooter.Tests;

public partial class EnvironmentCategoriesCheck : Node
{
    private int _checks;
    private void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        _checks++;
    }

    public override void _Ready()
    {
        int exit = 0;
        try { Run(); GD.Print("ENVIRONMENT_CATEGORIES_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError("ENVIRONMENT_CATEGORIES_CHECK_FAIL " + e); exit = 1; }
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    private void Run()
    {
        var catalog = EnvironmentCategories.Load();
        Check(catalog.Errors.Count == 0, string.Join("; ", catalog.Errors));
        Check(catalog.Examples.Count > 0 && catalog.Examples.Values.All(e => catalog.Validate(e.Selection).Count == 0), "authored examples resolve every category");
        Check(catalog.Examples["after_rain"].Selection is { Weather: "clear", Ground: "wet" }, "after rain keeps wet ground without active rain");
        Check(catalog.Examples["after_snow"].Selection is { Weather: "clear", Ground: "snow_covered" }, "after snow keeps snow cover without active snow");
        foreach (string weather in catalog.Weather.Keys)
        foreach (string ground in catalog.Ground.Keys)
        {
            foreach (string light in catalog.IndoorLight.Keys)
            {
                var indoor = new EnvironmentSelection("indoor", light, null, weather, ground);
                Check(catalog.Validate(indoor).Count == 0, "indoor weather is outside context; ground is independent");
                Check(!indoor.HasVisiblePrecipitation, "sheltered indoor context does not display precipitation");
            }
            foreach (string time in catalog.OutdoorTime.Keys)
            {
                var outdoor = new EnvironmentSelection("outdoor", null, time, weather, ground);
                Check(catalog.Validate(outdoor).Count == 0, "outdoor time, weather and ground combine independently");
                Check(outdoor.HasVisiblePrecipitation == (weather is "rain" or "snow"), "only active outdoor rain or snow implies precipitation");
            }
        }
        var valid = new EnvironmentSelection("outdoor", null, "noon", "sunny", "dry");
        Check(catalog.Validate(valid with { Domain = "outside" }).Count > 0, "unknown domain rejected");
        Check(catalog.Validate(valid with { OutdoorTime = null }).Count > 0, "outdoor time required");
        Check(catalog.Validate(valid with { OutdoorTime = "late" }).Count > 0, "unknown time rejected");
        Check(catalog.Validate(valid with { IndoorLight = "dark" }).Count > 0, "outdoor cannot also select indoor light");
        Check(catalog.Validate(valid with { Weather = "after_rain" }).Count > 0, "after rain is a combination, not weather");
        Check(catalog.Validate(valid with { Ground = "muddy" }).Count > 0, "unknown ground rejected");
        Check(catalog.Validate(new("indoor", null, null, "clear", "dry")).Count > 0, "indoor light required");
        Check(catalog.Validate(new("indoor", "dark", "night", "clear", "dry")).Count > 0, "indoor cannot also select outdoor time");
        Check(catalog.Validate(new("indoor", "bright", null, "clear", "dry")).Count > 0, "unknown indoor light rejected");

        string text = FileAccess.GetFileAsString(EnvironmentCategories.DataPath);
        void Bad(Action<JsonObject> alter, string message)
        {
            var json = JsonNode.Parse(text)!.AsObject();
            alter(json);
            Check(EnvironmentCategories.FromText(json.ToJsonString()).Errors.Count > 0, message);
        }
        Bad(j => j["version"] = 2, "unsupported version rejected");
        Bad(j => j.Remove("weather"), "missing category rejected");
        Bad(j => j["outdoor_time"]!.AsArray().RemoveAt(0), "missing stable ID rejected");
        Bad(j => j["ground"]![0]!.AsObject().Remove("id"), "entry without ID rejected");
        Bad(j => j["weather"]![1]!["id"] = "clear", "duplicate IDs rejected");
        Bad(j => j["weather"]![0]!["kor"] = "", "empty localized label rejected");
        Bad(j => j["weather"]![0]!.AsObject().Remove("eng"), "missing localized label rejected");
        Bad(j => j["examples"]![0]!["selection"]!["domain"] = "missing", "invalid example rejected");
        Bad(j => j["examples"]![0]!["selection"]!["outdor_time"] = "night", "misspelled selection field rejected");
        Check(EnvironmentCategories.FromText("{").Errors.Count > 0, "malformed JSON returns diagnostics");
        Check(EnvironmentCategories.FromText(text.Replace("\"version\": 1", "\"version\": 1, \"version\": 1")).Errors.Count > 0, "duplicate JSON keys rejected");
        Check(EnvironmentCategories.Load("res://tests/no_environment_catalog.json").Errors.Count > 0, "missing file returns diagnostics");
    }
}
