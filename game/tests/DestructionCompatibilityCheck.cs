using Godot;
using System;
using System.Text.Json.Nodes;

namespace HordeShooter.Tests;

/// <summary>Handshake compatibility includes actual destruction rules and deterministic placements, without mutating source data.</summary>
public partial class DestructionCompatibilityCheck : Node
{
    private int _checks;
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); _checks++; GD.Print("PASS " + label); }
    public override void _Ready()
    {
        int exit = 0;
        try { Run(); GD.Print("DESTRUCTION_COMPATIBILITY_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("DESTRUCTION_COMPATIBILITY_CHECK_FAIL " + e.Message); exit = 1; }
        QuitHygiene.Collect();
        GetTree().Quit(exit);
    }

    private void Run()
    {
        string rules = FileAccess.GetFileAsString(ObjectInteractions.DataPath);
        string placements = FileAccess.GetFileAsString(DestructionPlacements.Path);
        int actual = NetIds.DataHash();
        Check(actual == NetIds.DataHash(rules, placements), "handshake reads both actual JSON files");
        string lfRules = rules.Replace("\r\n", "\n").Replace('\r', '\n');
        string lfPlacements = placements.Replace("\r\n", "\n").Replace('\r', '\n');
        Check(NetIds.DataHash(lfRules, lfPlacements) == NetIds.DataHash(lfRules.Replace("\n", "\r\n"), lfPlacements.Replace("\n", "\r\n")),
            "CRLF and LF checkouts remain network compatible");
        string canonicalRules = JsonNode.Parse(rules)!.ToJsonString();
        string canonicalPlacements = JsonNode.Parse(placements)!.ToJsonString();
        int canonical = NetIds.DataHash(canonicalRules, canonicalPlacements);
        foreach (var (edit, label) in new (Action<JsonNode>, string)[]
        {
            (j => j["profiles"]!["breach_wall"]!["destruction"]!["max_health"] = 61, "wall health"),
            (j => j["profiles"]!["breach_wall"]!["destruction"]!["breakable_by"] = "gun", "wall strength class"),
            (j => j["profiles"]!["breach_wall"]!.AsObject().Remove("destruction"), "indestructible wall policy"),
            (j => j["profiles"]!["explosive_barrel"]!["explosion"]!["damage"] = 126, "barrel explosion damage"),
            (j => j["profiles"]!["explosive_barrel"]!["explosion"]!["radius_m"] = 5, "barrel explosion radius"),
            (j => j["profiles"]!["explosive_barrel"]!["explosion"]!["impulse"] = 141, "barrel explosion impulse"),
            (j => j["profiles"]!["explosive_barrel"]!["explosion"]!["delay_s"] = .2, "barrel fuse"),
        })
        {
            var changed = JsonNode.Parse(canonicalRules)!;
            edit(changed);
            Check(NetIds.DataHash(changed.ToJsonString(), canonicalPlacements) != canonical, label + " change alters compatibility hash");
        }
        foreach (var (edit, label) in new (Action<JsonNode>, string)[]
        {
            (j => j["placements"]![0]!["at"]![0] = 30, "wall position"),
            (j => j["placements"]![0]!["barrel"]![0] = 30, "barrel position"),
            (j => j["placements"]![0]!["yaw"] = 90, "wall orientation"),
            (j => j["placements"]!.AsArray().RemoveAt(0), "placement count and network build order"),
        })
        {
            var changed = JsonNode.Parse(canonicalPlacements)!;
            edit(changed);
            Check(NetIds.DataHash(canonicalRules, changed.ToJsonString()) != canonical, label + " change alters compatibility hash");
        }
        Check(FileAccess.GetFileAsString(ObjectInteractions.DataPath) == rules && FileAccess.GetFileAsString(DestructionPlacements.Path) == placements,
            "compatibility checks leave both source files unchanged");
    }
}
