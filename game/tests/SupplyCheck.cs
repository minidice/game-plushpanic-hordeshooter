using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Stage 1 supply points (scenario-level-design.md 5장, decisions.md L-12): data loads, the trial budget holds,
/// items are grouped on open floor away from spawn spots, and no endless ammo sits at the crescendo's hold spot.
/// The budget numbers are a trial, not a balance verdict — play measurements decide them (I-26).
/// </summary>
public partial class SupplyCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("SUPPLY_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("SUPPLY_CHECK_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        var supplies = StageSupplies.Stage1;
        Check(supplies.Errors.Count == 0, "stage 1 supplies load without errors" + (supplies.Errors.Count > 0 ? ": " + string.Join("; ", supplies.Errors) : ""));
        var bad = StageSupplies.FromText("{\"points\": [{\"id\": \"a\", \"kind\": \"shop\", \"items\": []}, {\"id\": \"b\", \"kind\": \"main\", \"items\": ["
            + "{\"item\": \"weapon\", \"id\": \"laser\", \"at\": [0, 0]}, {\"item\": \"ammo\", \"at\": [0, 0, 1]}, {\"item\": \"ammo\", \"at\": [0, 0]}, {\"item\": \"medkit\", \"at\": [0.5, 0]}, {\"item\": \"medkit\", \"at\": [9, 0]}]}]}", "test").Errors;
        Check(bad.Count == 5, "unknown kinds and weapons, x,y,z coordinates, items on top of each other and a straggler are reported: " + string.Join(" / ", bad));

        // ---------- the trial budget (4-player team, start kit and safehouse excluded) ----------
        var route = new[] { SupplyKind.Main, SupplyKind.Optional, SupplyKind.Event };
        var items = supplies.ItemsOf(route).ToList();
        int main = supplies.Points.Count(p => p.Kind == SupplyKind.Main), optional = supplies.Points.Count(p => p.Kind == SupplyKind.Optional);
        Check(main == 2 && optional is >= 1 and <= 2, $"two main supply points and one or two optional ones ({main}, {optional})");
        int medkits = items.Count(i => i.Kind == SupplyItemKind.Medkit), throwables = items.Count(i => i.Kind == SupplyItemKind.Throwable);
        Check(medkits is >= 2 and <= 4 && throwables is >= 2 and <= 4, $"2–4 medkits and 2–4 throwables along the way ({medkits}, {throwables})");
        int ammo = items.Count(i => i.Kind == SupplyItemKind.Ammo);
        Check(ammo is >= 1 and <= 2 && supplies.ItemsOf(SupplyKind.Optional).All(i => i.Kind != SupplyItemKind.Ammo),
            $"1–2 ammo piles along the way, all at main points ({ammo})");
        var weaponPoints = supplies.Points.Where(p => p.Kind is SupplyKind.Main or SupplyKind.Optional && p.Items.Any(i => i.Kind == SupplyItemKind.Weapon)).ToList();
        Check(weaponPoints.Count is >= 1 and <= 2 && weaponPoints.All(p => p.Items.Where(i => i.Kind == SupplyItemKind.Weapon).Select(i => i.Id).Distinct().Count() <= 2),
            "new weapons turn up at one or two points, at most two kinds each");
        var heavy = supplies.Points.SelectMany(p => p.Items.Select(i => (p, i)))
            .Where(t => t.i.Kind == SupplyItemKind.Weapon && ArmoryCatalog.Weapon(t.i.Id).Category == WeaponCategory.Heavy).ToList();
        Check(heavy.Count <= 1 && heavy.All(t => t.p.Kind == SupplyKind.Event), "at most one heavy weapon, and only as an event reward");
        var start = supplies.ItemsOf(SupplyKind.Start).ToList();
        Check(start.Count(i => i.Kind == SupplyItemKind.Weapon) is >= 2 and <= 3, "the start kit offers two or three weapons, not the whole armory");

        // ---------- in the built stage ----------
        var stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
        AddChild(stage);
        await Frames(3);
        var all = supplies.Points.SelectMany(p => p.Items).ToList();
        Check(stage.Interactables.OfType<WeaponPickup>().Count() == all.Count(i => i.Kind is SupplyItemKind.Weapon or SupplyItemKind.Throwable)
              && stage.Interactables.Count(Stage.IsMedkit) == all.Count(i => i.Kind == SupplyItemKind.Medkit),
            $"the stage places exactly the listed weapons, throwables and medkits ({stage.Interactables.OfType<WeaponPickup>().Count()} pickups, {stage.Interactables.Count(Stage.IsMedkit)} medkits)");
        // Walkable from the start with doors open; an item may stand against furniture as long as you can reach it.
        var reachable = new FlowField(-2, -12, 128, 12);
        stage.Layout.ApplyFlow(reachable, doors: false);
        reachable.Update(stage.Layout.PlayerStart);
        bool InReach(Vector3 at)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                    if (reachable.Reachable(new Vector3(at.X + dx, 0, at.Z + dz))) return true;
            return false;
        }
        var stranded = all.Where(i => !InReach(i.At)).ToList();
        Check(stranded.Count == 0, "every item can be reached on foot from the start" + string.Join(",", stranded.Select(i => $" {i.Kind} {i.Id} ({i.At.X}, {i.At.Z})")));
        var entries = stage.Director.Entries;
        var crowded = supplies.Points.Where(p => p.Kind != SupplyKind.Start && entries.Any(e => e.Ground.DistanceTo(p.Center) < 3f)).ToList();
        Check(crowded.Count == 0, "no wave comes in on top of a supply point" + string.Join(", ", crowded.Select(p => " " + p.Id)));
        var panel = stage.Layout.ShutterPanel.GlobalPosition;
        Check(all.Where(i => i.Kind == SupplyItemKind.Ammo).All(i => i.At.DistanceTo(panel) > 10f),
            "no endless ammo pile at the crescendo's hold spot (scenario-level-design.md 5.2-5)");
        stage.QueueFree();
        await Frames(2);
    }
}
