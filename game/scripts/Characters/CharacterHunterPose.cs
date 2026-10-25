using Godot;

namespace HordeShooter;

public partial class CharacterRig
{
    private Node3D? _bowNock, _bowArrow;
    private MeshInstance3D? _bowUpperString, _bowLowerString;

    public static float HunterSwingStart(string weapon, float windup) => windup * (weapon == "club" ? .76f : .66f);

    private void BindHunterBowGeometry()
    {
        var root = _heldWeapon!.Root;
        _bowNock = root.GetNodeOrNull<Node3D>("BowNock");
        _bowArrow = root.GetNodeOrNull<Node3D>("BowNockedArrow");
        _bowUpperString = root.GetNodeOrNull<MeshInstance3D>("BowUpperString");
        _bowLowerString = root.GetNodeOrNull<MeshInstance3D>("BowLowerString");
    }

    /// <summary>
    /// Visual-only, absolute sampling after Animate. The strike/release marker is exactly windup;
    /// negative elapsed cancels immediately. No accumulated clock, recoil or generic shove is applied.
    /// </summary>
    public void SetHunterAttackPose(string weapon, float elapsed, float windup, float recovery)
    {
        if (_heldWeapon == null || _heldWeapon.Definition.Id != weapon || weapon is not ("club" or "knife" or "woodbow")) return;
        ResetHunterAttackPose();
        if (elapsed < 0 || elapsed >= windup + recovery) return;
        windup = Mathf.Max(.001f, windup);
        recovery = Mathf.Max(.001f, recovery);
        if (weapon == "woodbow")
        {
            float draw = elapsed < windup ? Ease(elapsed / windup) : 0;
            float settle = elapsed < windup ? 1 : 1 - Ease((elapsed - windup) / recovery);
            Weapon.Position += new Vector3(0, .055f * settle, .025f * settle);
            SetHunterBowGeometry(draw, elapsed < windup);
        }
        else
        {
            var restHand = new Vector3(.51f, .90f, .28f);
            var restAngles = new Vector3(-.30f, .20f, -.12f);
            bool club = weapon == "club";
            var raisedHand = club ? new Vector3(.43f, 1.30f, .09f) : new Vector3(.61f, 1.01f, .20f);
            var raisedAngles = club ? new Vector3(-1.45f, .12f, -.18f) : new Vector3(-.12f, 1.20f, -.12f);
            var hitHand = club ? new Vector3(.39f, .83f, .47f) : new Vector3(.28f, 1.00f, .48f);
            var hitAngles = club ? new Vector3(.72f, -.12f, .04f) : new Vector3(.03f, -.70f, .04f);
            var followHand = club ? new Vector3(.35f, .75f, .42f) : new Vector3(.16f, .97f, .40f);
            var followAngles = club ? new Vector3(.93f, -.16f, .08f) : new Vector3(.10f, -1.10f, .09f);
            float ready = HunterSwingStart(weapon, windup);
            float follow = recovery * (club ? .24f : .15f);
            Vector3 hand, angles;
            if (elapsed < ready)
                Blend(restHand, raisedHand, restAngles, raisedAngles, elapsed / ready, out hand, out angles);
            else if (elapsed < windup)
                Blend(raisedHand, hitHand, raisedAngles, hitAngles, (elapsed - ready) / (windup - ready), out hand, out angles, false);
            else if (elapsed < windup + follow)
                Blend(hitHand, followHand, hitAngles, followAngles, (elapsed - windup) / follow, out hand, out angles);
            else
                Blend(followHand, restHand, followAngles, restAngles, (elapsed - windup - follow) / (recovery - follow), out hand, out angles);
            // Keep the weapon rotation centered on the actual wrist, as in player melee poses.
            var gripLocal = InRig(Weapon).AffineInverse() * InRig(_heldWeapon.RightGrip).Origin;
            var basis = Basis.FromEuler(angles);
            Weapon.Transform = new Transform3D(basis, hand - basis * gripLocal);
        }
        UpdateWeaponGrip();
    }

    /// <summary>Cancellation, rebinding and corpse/reset paths restore the nocked idle bow immediately.</summary>
    public void ResetHunterAttackPose()
    {
        ResetHunterBowGeometry();
        if (_heldWeapon?.Definition.Id is not ("club" or "knife" or "woodbow")) return;
        Weapon.Transform = _weaponHome;
        ApplyCarryPose(0);
        UpdateWeaponGrip();
    }

    private void ApplyHunterBowCarryPose()
    {
        // Left hand holds the stave; the right hand draws back toward the right shoulder.
        // Both endpoints stay within the compact common-animal shoulder reach.
        Weapon.Position = new Vector3(-.12f, 1.02f, .34f);
        Weapon.Rotation = Vector3.Zero;
    }

    private void ResetHunterBowGeometry() => SetHunterBowGeometry(0, true);

    private void SetHunterBowGeometry(float draw, bool arrowVisible)
    {
        if (_bowNock == null || !IsInstanceValid(_bowNock) || _heldWeapon == null) return;
        var nock = new Vector3(.18f * draw, 0, .08f - .40f * draw);
        _bowNock.Position = nock;
        _heldWeapon.RightGrip.Position = nock;
        if (_bowArrow != null)
        {
            _bowArrow.Visible = arrowVisible;
            _bowArrow.Position = nock;
            // The shaft passes through the bow's center even as the drawing hand moves laterally.
            _bowArrow.Quaternion = new Quaternion(Vector3.Back, (new Vector3(0, 0, .65f) - nock).Normalized());
        }
        PoseBowString(_bowLowerString, new Vector3(0, -.55f, .08f), nock);
        PoseBowString(_bowUpperString, new Vector3(0, .55f, .08f), nock);
    }

    private static void PoseBowString(MeshInstance3D? segment, Vector3 tip, Vector3 nock)
    {
        if (segment?.Mesh is not BoxMesh mesh) return;
        segment.Position = (tip + nock) * .5f;
        segment.Quaternion = new Quaternion(Vector3.Back, (nock - tip).Normalized());
        mesh.Size = new Vector3(.008f, .008f, tip.DistanceTo(nock));
    }
}
