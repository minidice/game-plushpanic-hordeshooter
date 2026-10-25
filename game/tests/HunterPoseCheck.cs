using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;

public partial class HunterPoseCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; }

    public override void _Ready()
    {
        int exit = 0;
        try
        {
            foreach (var species in CommonRoster.Current.Species.Keys.Where(s => s != "human"))
                foreach (string weapon in new[] { "club", "knife", "woodbow" }) Verify(species, weapon);
            GD.Print("HUNTER_POSE_CHECK_PASS " + _checks);
        }
        catch (Exception e) { GD.PushError("HUNTER_POSE_CHECK_FAIL " + e); exit = 1; }
        QuitHygiene.Collect(); GetTree().Quit(exit);
    }

    private void Verify(string species, string weapon)
    {
        var catalog = CommonRoster.Current;
        var identity = new CommonIdentity("hunter", species, "red", weapon);
        var rig = CharacterRig.Create(catalog.LookFor(identity)); AddChild(rig); catalog.Apply(rig, identity);
        try
        {
            const float windup = .5f, recovery = .4f;
            void Sample(float elapsed) { rig.Animate(0, 0, 0, 0, Vector3.Zero); rig.SetHunterAttackPose(weapon, elapsed, windup, recovery); }
            Sample(-1); var rest = rig.Weapon.Transform;
            Sample(.32f); var ready = rig.Weapon.Transform;
            Check(!ready.IsEqualApprox(rest), species + "/" + weapon + " anticipation differs from idle");
            Check(rig.GripConnectionError < .001f, "both hands remain connected during anticipation");
            Sample(windup); var impact = rig.Weapon.Transform;
            Sample(.32f);
            Check(rig.Weapon.Transform.IsEqualApprox(ready), "arbitrary frame sampling is deterministic");
            if (weapon == "club") Check(ready.Origin.Y > impact.Origin.Y + .20f, "club descends from raised windup");
            if (weapon == "knife") Check(ready.Origin.X > impact.Origin.X + .15f, "knife crosses laterally");
            if (weapon == "woodbow")
            {
                var root = rig.Weapon.GetNode<Node3D>("Weapon_woodbow");
                var arrow = root.GetNode<Node3D>("BowNockedArrow");
                var nock = root.GetNode<Node3D>("BowNock");
                var right = root.GetNode<Node3D>("GripRight");
                var left = root.GetNode<Node3D>("GripLeft");
                var leftHome = left.Transform;
                Sample(windup - .0001f);
                Check(arrow.Visible && nock.Position.Z < -.30f, "arrow is nocked until release and string is drawn");
                Check(nock.GlobalPosition.DistanceTo(right.GlobalPosition) < .0001f, "right hand pulls string nock");
                Check(left.Transform.IsEqualApprox(leftHome), "left hand stays on stave");
                foreach (string name in new[] { "BowLowerString", "BowUpperString" })
                {
                    var segment = root.GetNode<MeshInstance3D>(name); var box = (BoxMesh)segment.Mesh;
                    var endpoint = segment.Transform * new Vector3(0, 0, box.Size.Z * .5f);
                    Check(endpoint.DistanceTo(nock.Position) < .0001f, "both string halves terminate at nock");
                }
                Sample(windup); Check(!arrow.Visible, "arrow disappears exactly at release");
                Sample(windup + recovery - .0001f); Check(!arrow.Visible, "arrow stays absent during recovery");
                rig.ResetHunterAttackPose(); Check(arrow.Visible && nock.Position.IsEqualApprox(new Vector3(0, 0, .08f)), "cancellation restores idle geometry immediately");
                Sample(.3f); rig.ResetPose(); Check(arrow.Visible && nock.Position.Z > 0, "full reset restores bow");
            }
            Sample(windup + recovery);
            Check(rig.Weapon.Transform.IsEqualApprox(rest), "recovery finishes at idle");
            Sample(.32f); rig.SetHunterAttackPose(weapon, -1, windup, recovery);
            Check(rig.Weapon.Transform.IsEqualApprox(rest), "negative elapsed cancels immediately");
        }
        finally { rig.Free(); }
    }
}
