using Godot;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>Finds every CharacterDefinition .tres in a content folder. New files appear automatically.</summary>
public static class CharacterCatalog
{
    public const string PlayerDir = "res://content/characters";
    public const string EnemyDir = "res://content/enemies";

    public static List<CharacterDefinition> LoadAll(string dir)
    {
        var found = new Dictionary<string, CharacterDefinition>();
        foreach (var file in DirAccess.GetFilesAt(dir))
        {
            // Exported builds list "x.tres.remap"; loading the original path still works.
            var name = file.EndsWith(".remap") ? file[..^".remap".Length] : file;
            if (!name.EndsWith(".tres") && !name.EndsWith(".res")) continue;
            if (ContentCache.Load($"{dir}/{name}") is not CharacterDefinition def) continue;
            if (string.IsNullOrEmpty(def.Id)) def.Id = name.GetBaseName();
            found[def.Id] = def;
        }
        return found.Values.OrderBy(d => d.SortOrder).ThenBy(d => d.Id).ToList();
    }

    public static CharacterDefinition Get(string dir, string id)
    {
        var all = LoadAll(dir);
        return all.FirstOrDefault(d => d.Id == id)
            ?? all.FirstOrDefault()
            ?? throw new System.InvalidOperationException($"No character definitions in {dir}");
    }

    public static CharacterDefinition Player(string id) => Get(PlayerDir, id);
    public static CharacterDefinition Enemy(string id) => Get(EnemyDir, id);
}
