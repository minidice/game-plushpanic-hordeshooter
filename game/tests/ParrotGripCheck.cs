using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;

// Regression: equipping a parrot used to discard the authored feather surfaces for a flat capsule.
public partial class ParrotGripCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; }

    public override void _Ready()
    {
        int exit = 0;
        try
        {
            foreach (string palette in new[] { "red", "green" })
            foreach (string weapon in new[] { "club", "knife", "woodbow", "mp5", "ak47", "m4" }) Verify(palette, weapon);
            GD.Print("PARROT_GRIP_CHECK_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError("PARROT_GRIP_CHECK_FAIL " + e); exit = 1; }
        QuitHygiene.Collect(); GetTree().Quit(exit);
    }

    private void Verify(string palette, string weapon)
    {
        bool hunter = weapon is "club" or "knife" or "woodbow";
        var catalog = CommonRoster.Current;
        var identity = new CommonIdentity(hunter ? "hunter" : "armed", "parrot", palette, weapon);
        var rig = CharacterRig.Create(catalog.LookFor(identity)); AddChild(rig); catalog.Apply(rig, identity);
        try
        {
            var right = (MeshInstance3D)rig.Model.FindChild("RightForearm", true, false);
            var left = (MeshInstance3D)rig.Model.FindChild("LeftForearm", true, false);
            foreach (var (arm, side) in new[] { (right, "right"), (left, "left") })
            {
                var source = (MeshInstance3D)rig.Model.FindChild("common_parrot_" + side + "Arm_mesh", true, false);
                Check(arm.Mesh == source.Mesh, weapon + ": equipped wing retains the complete authored feather mesh");
                Check(!source.IsVisibleInTree() && arm.IsVisibleInTree(), "exactly one visible wing per side");
                Check(arm.Mesh.GetSurfaceCount() >= 2, "primary and accent feather surfaces remain distinct");
                for (int i = 0; i < source.Mesh.GetSurfaceCount(); i++)
                    Check(arm.GetActiveMaterial(i) == source.GetActiveMaterial(i), "authored surface palette is retained");
            }
            var idleRight = right.Transform; var idleLeft = left.Transform;
            void Sample(float elapsed)
            {
                rig.Animate(0, 0, 0, 0, Vector3.Zero);
                if (hunter) rig.SetHunterAttackPose(weapon, elapsed, .5f, .4f);
                CheckGeometry(rig, right, left, weapon, hunter && weapon != "woodbow");
            }
            foreach (float elapsed in new[] { -1f, 0f, .16f, .32f, .49f, .5f, .6f, .89f, .9f }) Sample(elapsed);
            if (!hunter)
            {
                var state = new WeaponState(ArmoryCatalog.Weapon(weapon), 0, 30); state.StartReload();
                for (int i = 0; i < 20; i++)
                {
                    state.Tick(state.Def.ReloadTime / 22, false);
                    rig.SetReloadPose(.1f, state); rig.Animate(.016f, 1, .4f, .6f, Vector3.Zero);
                    CheckGeometry(rig, right, left, weapon, false);
                }
            }
            rig.ResetPose();
            Check(right.Transform.IsEqualApprox(idleRight) && left.Transform.IsEqualApprox(idleLeft), "reset restores both authored wing poses without drift");
            rig.ZiplinePose(false, .4f, .5f);
            Check(right.IsVisibleInTree() && left.IsVisibleInTree(), "shared traversal keeps both authored wings visible");
            rig.EndZiplinePose(); rig.ResetPose();
            Check(right.Transform.IsEqualApprox(idleRight) && left.Transform.IsEqualApprox(idleLeft), "shared traversal restores authored weapon wings");
            rig.Weapon.Visible = false; rig.UpdateWeaponGrip();
            Check(!right.IsVisibleInTree() && !left.IsVisibleInTree(), "holstered weapon hides its replacement wings");
            rig.Weapon.Visible = true; rig.UpdateWeaponGrip();
            rig.ShowAs(VisionSystem.Visibility.Hidden);
            Check(!right.IsVisibleInTree() && !left.IsVisibleInTree(), "hidden actor cannot leak authored wings");
            rig.ShowAs(VisionSystem.Visibility.Silhouette);
            Check(right.MaterialOverride == CharacterRig.SilhouetteMaterial && left.MaterialOverride == CharacterRig.SilhouetteMaterial, "silhouette includes authored wings");
            // A fresh visible rig exercises the normal status/flash path without waiting on reveal animation.
        }
        finally { rig.Free(); }
        VerifyEffects(identity);
    }

    private void CheckGeometry(CharacterRig rig, MeshInstance3D right, MeshInstance3D left, string weapon, bool oneHand)
    {
        var root = (Node3D)rig.Weapon.FindChild("Weapon_*", false, false);
        foreach (var (arm, grip) in new[] { (right, root.GetNode<Node3D>("GripRight")), (left, oneHand ? null : root.GetNode<Node3D>("GripLeft")) })
        {
            string side = arm == right ? "right" : "left";
            var source = (MeshInstance3D)rig.Model.FindChild("common_parrot_" + side + "Arm_mesh", true, false);
            var shoulder = source.GetParentNode3D().GlobalPosition;
            var points = Enumerable.Range(0, arm.Mesh.GetSurfaceCount()).SelectMany(i => arm.Mesh.SurfaceGetArrays(i)[(int)Mesh.ArrayType.Vertex].AsVector3Array()).Select(p => arm.GlobalTransform * p).ToArray();
            Check(points.All(p => p.IsFinite() && rig.ToLocal(p).Length() < 2.5f), weapon + ": wing stays finite and inside compact actor bounds");
            if (grip != null)
            {
                Check(points.Min(p => p.DistanceTo(grip.GlobalPosition)) < .12f, weapon + ": real authored feather vertices reach the weapon grip");
                var axis = (grip.GlobalPosition - shoulder).Normalized();
                Check(points.Min(p => (p - shoulder).Dot(axis)) > -.085f, weapon + ": proximal feathers do not protrude behind the anatomical shoulder");
                Check(points.Min(p => p.DistanceTo(shoulder)) < .10f, weapon + ": actual proximal wing geometry stays attached to anatomical shoulder");
                Check(points.Max(p => (p - grip.GlobalPosition).Dot(axis)) < .12f, weapon + ": distal feathers remain compact around the hand");
            }
            Check(Mathf.Abs(arm.Basis.Determinant()) > .025f, weapon + ": wing retains volume at every attack/reload phase");
        }
        Check(rig.GripConnectionError < .002f, weapon + ": wrist endpoints agree with real weapon markers");
    }

    private void VerifyEffects(CommonIdentity identity)
    {
        var rig = CharacterRig.Create(CommonRoster.Current.LookFor(identity)); AddChild(rig); CommonRoster.Current.Apply(rig, identity);
        try
        {
            var arm = (MeshInstance3D)rig.Model.FindChild("RightForearm", true, false);
            var status = StatusVisualController.Attach(rig);
            status.SetStates(new[] { new StatusVisualState("poison", 5, 1) });
            var layer = arm.GetNode<MeshInstance3D>("StatusSurface");
            Check(layer.Mesh == arm.Mesh && layer.IsVisibleInTree() && layer.GlobalTransform.IsEqualApprox(arm.GlobalTransform), "status layer follows the actual authored geometry and pose");
            var material = arm.GetActiveMaterial(0);
            rig.FlashDeath();
            Check(arm.MaterialOverlay != null && arm.GetActiveMaterial(0) == material, "hit flash overlays without replacing feather colors");
            status._Process(0);
            Check(!layer.Visible, "hit flash suppresses overlapping status surface");
            rig.GetNode<CharacterHitFlashEffect>("HitFlash")._Process(.2);
            status._Process(0);
            Check(arm.MaterialOverlay == null && layer.Visible && arm.GetActiveMaterial(0) == material, "flash expiration restores status and original palette");
            status.Clear();
        }
        finally { rig.Free(); }
    }
}
