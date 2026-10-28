using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class CampaignPacingCheck : Node
{
    private int _checks;
    private Stage? _stage;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("CAMPAIGN_PACING_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); failed = true; }
        if (_stage != null && IsInstanceValid(_stage)) _stage.Free();
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        string[] paths = { LayoutMap.Stage1SuppliesPath, LayoutMap.Stage2SuppliesPath, LayoutMap.Stage3SuppliesPath };
        string[][] kept = { new[] { "p_base", "p6_base", "c1_base", "guard_post", "arrival" },
            new[] { "drain_kit", "ranger_post", "arrival" }, new[] { "hatch_kit", "watch_post", "safehouse" } };
        StageCarry? carry = null;
        for (int i = 0; i < paths.Length; i++)
        {
            var supplies = StageSupplies.Load(paths[i]);
            Check(supplies.Errors.Count == 0, $"stage {i + 1} supply data remains valid");
            var ammoPoints = supplies.Points.Where(p => p.Items.Any(it => it.Kind == SupplyItemKind.Ammo)).Select(p => p.Id).ToHashSet();
            Check(ammoPoints.SetEquals(kept[i]), $"stage {i + 1} ammo exists only at safehouses and transition bases");
            _stage = new Stage { ModeOverride = GameMode.Scenario, MapOverride = LayoutMap.ForStage(i + 1), CharacterOverride = "goose", DirectorEnabled = false, SquadOverride = 0 };
            AddChild(_stage); _stage.Player.Invulnerable = true; await Frames(4);
            var expected = supplies.Points.SelectMany(p => p.Items).Where(it => it.Kind == SupplyItemKind.Ammo).ToArray();
            var actual = _stage.Interactables.Where(it => it.Pickup == PickupKind.Ammo).ToArray();
            Check(actual.Length == expected.Length && actual.All(it => expected.Any(e => new Vector2(e.At.X - it.GlobalPosition.X, e.At.Z - it.GlobalPosition.Z).Length() < .1f)),
                $"stage {i + 1} runtime ammo matches the retained bases");
            if (i == 0)
            {
                var p = _stage.Player;
                Check(p.Gun.Def.Id == "wooden_club" && p.Loadout.Primary == null && p.Loadout.Special == null, "campaign one starts with only the club");
                Check(Mathf.IsEqualApprox(p.Gun.Def.Damage, ArmoryCatalog.Weapon("frying_pan").Damage * .5f), "starting club deals half the previous starting melee damage");
                var remote = _stage.AddRemoteSurvivor(CharacterCatalog.Player("duck"), p.Position + Vector3.Right, 2);
                Check(remote.Gun.Def.Id == "wooden_club", "new campaign-one remote survivor receives the same start weapon");
                p.Weapons.Pickup(ArmoryCatalog.Weapon("m1911"), null);
                Check(p.Loadout.Secondary.Def.Id == "m1911", "first pistol replaces the club through normal pickup");
                carry = StageCarry.From(p);
                p.Weapons.PlaceInSlot(1, ArmoryCatalog.Weapon("wooden_club"));
                remote.Position += Vector3.Right * 5;
                if (DisplayServer.GetName() != "headless")
                {
                    await Frames(12); await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/campaign-start-club.png"));
                }
            }
            else
            {
                Check(_stage.Player.Gun.Def.Id == "frying_pan", $"direct stage {i + 1} launch does not force the stage-one club");
                carry!.ApplyTo(_stage.Player);
                Check(_stage.Player.Gun.Def.Id == "m1911", $"stage {i + 1} preserves the weapon carried from the previous stage");
            }
            _stage.Free(); _stage = null; await Frames(2);
        }
        Check(ArmoryCatalog.NewLoadout().Secondary.Def.Id == "frying_pan", "non-campaign default loadout is unchanged");
        Check(StageSupplies.Stage1.Points.Any(p => p.Id == "warehouse_stack" && p.Items.Any(i => i.Kind == SupplyItemKind.Ammo)),
            "separate short-scenario supply data is unchanged");
    }
}
