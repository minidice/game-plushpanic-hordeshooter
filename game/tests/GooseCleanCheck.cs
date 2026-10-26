using Godot;
using System;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Goose GLB, customization, weapon grip, and rendered Stage 2 smoke check.</summary>
public partial class GooseCleanCheck : Node
{
    private int _checks;
    private Stage? _stage;
    private void Check(bool ok, string label) { if (!ok) throw new Exception(label); _checks++; }
    private async Task Frames(int count) { for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private async Task Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Check(image.SavePng(ProjectSettings.GlobalizePath("res://output/goose-clean/" + name + ".png")) == Error.Ok, "captured " + name);
    }
    public override async void _Ready()
    {
        int exit = 0;
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output/goose-clean"));
            var def = CharacterCatalog.Player("goose");
            Check(def.Model?.ResourcePath.EndsWith("goose-clean.glb") == true, "clean model wired to player catalog");
            var rig = CharacterRig.Create(def); AddChild(rig);
            try
            {
                foreach (var part in new[] { "leftEye", "rightEye", "beak", "leftArm", "rightArm", "leftFoot", "rightFoot", "torso", "helmet", "vest", "goggles" })
                    Check(rig.Model.FindChild("goose_" + part, true, false) != null, "part " + part);
                foreach (var weapon in new[] { "m4", "m1911", "r870", "m249", "mortar", "alabong", "frying_pan" })
                {
                    var gun = WeaponVisual.Build(ArmoryCatalog.Weapon(weapon), rig.HandColor);
                    rig.Weapon.AddChild(gun.Root); rig.HideBuiltInWeapon(); rig.BindWeaponGrip(gun);
                    foreach (float wingSize in new[] { .8f, 1.25f, 1f })
                    {
                        rig.ApplyAppearance(new AppearanceProfile { WingSize = wingSize, FeetSize = wingSize, EyeSize = wingSize, BeakSize = wingSize,
                            Body = "#4a9bb2", BodySecondary = "#d2b36a", Vest = true, Hat = 1, Goggles = true });
                        rig.SetLocomotionPose(.08f, 1, false); rig.Animate(.016f, 1, .3f, .1f, new Vector3(2, 0, 1));
                        Check(rig.GripConnectionError < .002f && rig.WeaponArmsVisible, weapon + " wing reaches actual grip");
                        rig.DodgePose(.5f); Check(rig.GripConnectionError < .002f, weapon + " dodge grip");
                    }
                    gun.Root.Visible = false;
                }
                rig.ApplyAppearance(new AppearanceProfile());
                Check(def.ColliderHeight == 2.18f && def.ColliderRadius == .48f && def.MoveSpeed == 4.4f, "gameplay constants unchanged");
            }
            finally { rig.Free(); }

            _stage = new Stage { MapOverride = new LayoutMap(), CharacterOverride = "goose", DirectorEnabled = false,
                ModeOverride = GameMode.Scenario, SquadOverride = 0, ArrivalOverride = false, NextStageOverride = false };
            AddChild(_stage); await Frames(5);
            var player = _stage.Player; player.ManualControl = true; player.Invulnerable = true;
            var start = new Vector3(11, _stage.Tiers.GroundY(new Vector3(11, 0, -2)) + .05f, -2);
            player.TeleportTo(start); player.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
            player.SetControls(Vector2.Right, new Vector3(20, 1, -2), false); await Frames(35);
            Check(player.GlobalPosition.DistanceTo(start) > 1, "real Stage 2 movement"); await Shot("run-gameplay");
            int shots = player.Weapons.ShotsFired;
            player.SetControls(Vector2.Zero, player.GlobalPosition + new Vector3(8, 1, 0), true); await Frames(20);
            Check(player.Weapons.ShotsFired > shots && player.Rig.GripConnectionError < .002f, "real Stage 2 firing and grip");
            await Shot("fire-gameplay"); player.SetControls(Vector2.Zero, Vector3.Zero, false);
            player.SetPhysicsProcess(false); _stage.Cam.SetProcess(false);
            foreach (bool equipped in new[] { false, true })
            {
                player.Rig.ApplyAppearance(new AppearanceProfile { Hat = equipped ? 1 : 0, Vest = equipped, Pack = equipped, Goggles = equipped });
                player.Rig.SetPreviewVisibility(equipped, equipped);
                var focus = player.GlobalPosition + Vector3.Up;
                _stage.Cam.GlobalPosition = focus + new Vector3(2.5f, 1.8f, 4);
                _stage.Cam.LookAt(focus); _stage.Cam.Size = 3.8f;
                player.Rig.Face(_stage.Cam.GlobalPosition - player.GlobalPosition);
                await Frames(3); await Shot(equipped ? "equipped-close" : "base-close");
            }
            GD.Print("GOOSE_CLEAN_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError("GOOSE_CLEAN_FAIL " + e); exit = 1; }
        if (IsInstanceValid(_stage)) _stage!.Free();
        QuitHygiene.Collect(); GetTree().Quit(exit);
    }
}
