using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>
/// The asset path list (content/data/asset_paths.json, decisions.md T-26): it loads, every file and kit folder it names
/// exists, every pattern matches real files, and no C# source reaches into res://assets directly again.
/// </summary>
public partial class AssetPathsCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }

    public override void _Ready()
    {
        bool failed = false;
        try { Run(); GD.Print("ASSET_PATHS_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("ASSET_PATHS_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private static bool Exists(string path) => ResourceLoader.Exists(path) || FileAccess.FileExists(path);

    private void Run()
    {
        Check(AssetPaths.Errors.Count == 0 && AssetPaths.Files.Count > 0 && AssetPaths.Patterns.Count > 0 && AssetPaths.FolderLists.Count > 0,
            "asset_paths.json loads with files, patterns and folders" + (AssetPaths.Errors.Count > 0 ? ": " + string.Join("; ", AssetPaths.Errors) : ""));
        foreach (var (key, path) in AssetPaths.Files) Check(Exists(path), $"file '{key}' exists ({path})");
        foreach (var (key, folders) in AssetPaths.FolderLists)
            foreach (var folder in folders)
                Check(DirAccess.DirExistsAbsolute(folder) && FileAccess.FileExists(folder + "/manifest.json"), $"folder list '{key}': {folder} has a manifest");
        foreach (var (key, pattern) in AssetPaths.Patterns)
        {
            // At least one real file fits the pattern: the folder exists and has a file with its prefix and extension.
            int at = pattern.IndexOf("{0}", StringComparison.Ordinal);
            string dir = pattern[..pattern.LastIndexOf('/', at)], prefix = pattern[(dir.Length + 1)..at], suffix = pattern[(at + 3)..];
            var names = DirAccess.GetFilesAt(dir).Select(n => n.Replace(".import", "")).Distinct().ToList();
            Check(names.Any(n => n.StartsWith(prefix) && n.EndsWith(suffix)), $"pattern '{key}' matches files in {dir}");
        }
        Check(AssetPaths.Pattern("card_icon", "gather") == "res://assets/icons/cards/card_gather.png", "a pattern fills in the name");
        Check(AssetPaths.File("no.such.key") == "" && AssetPaths.Errors.Any(e => e.Contains("no.such.key")), "a missing key gives an empty path and an error, not a crash");

        // No direct asset paths left in the code (the list is the one place, T-26).
        var offenders = new List<string>();
        void Scan(string dir)
        {
            foreach (var sub in DirAccess.GetDirectoriesAt(dir)) Scan(dir + "/" + sub);
            foreach (var file in DirAccess.GetFilesAt(dir).Where(f => f.EndsWith(".cs")))
                if (FileAccess.GetFileAsString(dir + "/" + file).Contains("\"res://assets")) offenders.Add(file);
        }
        Scan("res://scripts");
        Check(offenders.Count == 0, "no C# source in scripts/ names a res://assets path directly" + (offenders.Count > 0 ? ": " + string.Join(", ", offenders) : ""));
    }
}
