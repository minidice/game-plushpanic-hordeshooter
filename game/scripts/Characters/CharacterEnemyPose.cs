using Godot;
using System.Collections.Generic;

namespace HordeShooter;

public partial class CharacterRig
{
    private readonly List<(Node3D Node, Transform3D Home, float Side)> _enemyArms = new();
    private Transform3D _enemyPoseHome;
    private bool _enemyPoseInitialized;

    /// <summary>Use authored movable arms/wings; primitive models without limbs get two reaching paws.</summary>
    public void InitializeEnemyAttackPose()
    {
        if (_enemyPoseInitialized) return;
        _enemyPoseInitialized = true;
        _enemyPoseHome = Pose.Transform;
        foreach (var child in Model.FindChildren("*", nameof(Node3D), true, false))
        {
            var node = (Node3D)child;
            string name = node.Name.ToString();
            bool namedArm = name.EndsWith("_leftArm") || name.EndsWith("_rightArm") || name.StartsWith("ZombieArm");
            bool primitiveArm = node is MeshInstance3D { Mesh: CapsuleMesh } && node.GetParent() == Deform &&
                Mathf.Abs(InRig(node).Origin.X) > Definition.ColliderRadius * .4f;
            if (!namedArm && !primitiveArm) continue;
            float side = InRig(node).Origin.X < 0 ? -1 : 1;
            _enemyArms.Add((node, node.Transform, side));
        }
        if (_enemyArms.Count != 0) return;

        float radius = Definition.ColliderRadius;
        float height = Definition.ColliderHeight;
        var material = Mat.Solid(Definition.HandColor);
        foreach (float side in new[] { -1f, 1f })
        {
            var arm = Mat.Mesh(Pose,
                new CapsuleMesh { Radius = radius * .22f, Height = radius * 1.35f },
                new Vector3(side * radius * .85f, height * .53f, radius * .55f), material,
                side < 0 ? "EnemyLeftArm" : "EnemyRightArm");
            arm.Rotation = new Vector3(1.2f, 0, side * -.15f);
            _enemyArms.Add((arm, arm.Transform, side));
        }
    }

    /// <summary>Remove the previous overlay before base animation; cancellation also restores immediately.</summary>
    public void ResetEnemyAttackPose()
    {
        if (!_enemyPoseInitialized) return;
        Pose.Transform = _enemyPoseHome;
        foreach (var arm in _enemyArms) arm.Node.Transform = arm.Home;
    }

    /// <summary>Draw back, swing the hands forward at impact, then settle back into locomotion.</summary>
    public void ApplyEnemyAttackPose(float elapsed, float windup, float recovery)
    {
        if (!_enemyPoseInitialized) InitializeEnemyAttackPose();
        float anticipation, strike;
        float pullbackEnd = windup * .72f;
        if (elapsed < pullbackEnd)
        {
            anticipation = Mathf.SmoothStep(0, 1, elapsed / pullbackEnd);
            strike = 0;
        }
        else if (elapsed < windup)
        {
            strike = Mathf.SmoothStep(0, 1, (elapsed - pullbackEnd) / (windup - pullbackEnd));
            anticipation = 1 - strike;
        }
        else
        {
            anticipation = 0;
            strike = 1 - Mathf.SmoothStep(0, 1, (elapsed - windup) / recovery);
        }
        Pose.Position += new Vector3(0, -strike * .045f, strike * .18f - anticipation * .07f);
        Pose.Rotation += new Vector3(strike * .24f - anticipation * .12f, anticipation * -.14f + strike * .10f, 0);

        foreach (var arm in _enemyArms)
        {
            arm.Node.Transform = arm.Home;
            float sign = arm.Node.GetParent() == Pose ? 1 : _forwardSign;
            if (arm.Node is not MeshInstance3D)
            {
                // Authored shoulder anchors stay attached to the torso. Draw the original
                // limb back, then sweep it up and forward; never add a second pair of hands.
                arm.Node.Rotation += new Vector3((anticipation * .45f - strike * 1.05f) * sign,
                    arm.Side * (anticipation * .15f - strike * .12f), arm.Side * anticipation * -.18f);
                continue;
            }
            // Convert meter-sized motion into the arm parent's local scale/orientation.
            var parent = (Node3D)arm.Node.GetParent();
            var offset = new Vector3(arm.Side * anticipation * .07f, anticipation * .12f + strike * .04f,
                strike * .30f - anticipation * .16f);
            arm.Node.Position += InRig(parent).Basis.Inverse() * offset;
            arm.Node.Rotation += new Vector3((-anticipation * .7f + strike * .3f) * sign,
                arm.Side * (-anticipation * .2f + strike * .25f), arm.Side * anticipation * -.35f);
        }
        // Armed specials use their existing connected hands for a short weapon shove.
        if (_heldWeapon != null)
        {
            Weapon.Position += new Vector3(0, anticipation * .05f, (strike * .20f - anticipation * .12f) * _forwardSign);
            UpdateWeaponGrip();
        }
    }

    /// <summary>The bull lowers its head and braces into its collision attack.</summary>
    public void ApplyBullChargePose(float amount)
    {
        Pose.Position += new Vector3(0, -.12f * amount, .08f * amount);
        Pose.Rotation += new Vector3(.34f * amount, 0, 0);
        if (Head != null) Head.Rotation += new Vector3(.22f * amount * _forwardSign, 0, 0);
    }
}
