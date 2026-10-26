using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HordeShooter;

/// <summary>Explicit trial placements, shared by map construction and placement checks. Heights are floor heights.</summary>
public sealed record DestructionPlacement(string Map, string Id, Vector3 At, float Yaw, Vector3 Barrel, string Label, bool PlainPanel);

public static class DestructionPlacements
{
    public const string Path = "res://content/data/stages/destruction_placements.json";

    public static IReadOnlyList<DestructionPlacement> Load(string map) =>
        FromText(FileAccess.GetFileAsString(Path)).Where(p => p.Map == map).ToArray();

    public static IReadOnlyList<DestructionPlacement> FromText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<DestructionPlacement>();
        var ids = new HashSet<string>();
        foreach (var row in doc.RootElement.GetProperty("placements").EnumerateArray())
        {
            string map = row.GetProperty("map").GetString()!, id = row.GetProperty("id").GetString()!;
            float yaw = row.GetProperty("yaw").GetSingle();
            if (map is not ("stage2" or "stage3") || string.IsNullOrWhiteSpace(id) || !ids.Add(map + ":" + id)
                || yaw is not (0 or 90)) throw new FormatException("Invalid or duplicate destruction placement: " + map + ":" + id);
            Vector3 Point(string key)
            {
                var values = row.GetProperty(key).EnumerateArray().Select(v => v.GetSingle()).ToArray();
                if (values.Length != 3 || values.Any(v => !float.IsFinite(v))) throw new FormatException(id + ": invalid " + key);
                return new Vector3(values[0], values[1], values[2]);
            }
            result.Add(new DestructionPlacement(map, id, Point("at"), yaw, Point("barrel"),
                row.GetProperty("label").GetString()!, row.GetProperty("plain_panel").GetBoolean()));
        }
        return result;
    }
}

public abstract partial class StageMap
{
    /// <summary>
    /// A removable 3.2 m middle with permanent 0.8 m shoulders. Only the middle's footprint is removed on break;
    /// no full-width invisible wall remains behind the opening. The art is based at floor height, as are kit props.
    /// </summary>
    protected ObjectBody BreachWall(Vector3 at, float yaw = 0)
    {
        if (yaw is not (0 or 90)) throw new ArgumentOutOfRangeException(nameof(yaw), "Breach walls support 0 or 90 degrees.");
        var rule = ObjectInteractions.Current.ForId("runtime:breach_wall")
            ?? throw new InvalidOperationException("Missing runtime:breach_wall interaction.");
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(yaw));
        var size = new Vector3(BreakableWallArt.OpeningWidth, BreakableWallArt.Height, BreakableWallArt.Thickness);
        var center = at + Vector3.Up * size.Y / 2;
        var footprint = (center, WallBounds(size, yaw));
        // The runtime kind stays interactive even when data removes destruction (an indestructible wall has NetId 0).
        var body = ObjectBody.Create(_stage, "BreachWall", rule, () => { _solids.Remove(footprint); _stage.RebuildFlow(); });
        body.Position = at;
        body.Basis = basis;
        _stage.AddChild(body);
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = Vector3.Up * size.Y / 2 });
        if (rule.BlocksMovement) _solids.Add(footprint);
        BreakableWallArt.Create(body);
        KitProps.Add(body);

        float shoulder = (BreakableWallArt.Width - BreakableWallArt.OpeningWidth) / 2;
        foreach (float side in new[] { -1f, 1f })
        {
            var local = new Vector3(side * (BreakableWallArt.OpeningWidth + shoulder) / 2, size.Y / 2, 0);
            var pillar = Solid(at + basis * local, WallBounds(new Vector3(shoulder, size.Y, size.Z), yaw), "", drawCargo: false);
            pillar.Name = "BreachWallShoulder";
        }
        return body;
    }

    protected void PlaceDestructionExamples(string map)
    {
        foreach (var placement in DestructionPlacements.Load(map))
        {
            var wall = BreachWall(placement.At, placement.Yaw);
            wall.Name = "BreachWall_" + placement.Id;
            wall.SetMeta("destruction_placement", placement.Id);
            var barrel = ExplosiveBarrel.Create(_stage, placement.Barrel);
            barrel.SetMeta("destruction_placement", placement.Id);
            if (!placement.PlainPanel) continue;
            var basis = new Basis(Vector3.Up, Mathf.DegToRad(placement.Yaw));
            var at = placement.At + basis * Vector3.Right * BreakableWallArt.Width;
            var size = new Vector3(BreakableWallArt.Width, BreakableWallArt.Height, BreakableWallArt.Thickness);
            var solid = Solid(at + Vector3.Up * size.Y / 2, WallBounds(size, placement.Yaw), "", drawCargo: false);
            solid.Name = "ConcretePanel_" + placement.Id;
            var visual = BreakableWallArt.Create(solid, cracked: false);
            visual.Position = Vector3.Down * size.Y / 2;
            visual.Basis = basis;
        }
    }

    private static Vector3 WallBounds(Vector3 size, float yaw) => yaw == 90 ? new Vector3(size.Z, size.Y, size.X) : size;
}
