using Godot;
using System;

namespace HordeShooter;

/// <summary>
/// Visual half of a character: instantiates the model from a <see cref="CharacterDefinition"/>, finds its
/// movable parts, and animates them procedurally (no skeleton). Missing parts are replaced with empty
/// pivots, so any GLB — or none at all — can be dropped in.
///
/// Hierarchy: Rig (yaw, set by owner) → Pose (fallback body) → Model (forward fix + scale) → parts.
/// The rig's own forward is +Z.
/// </summary>
public partial class CharacterRig : Node3D
{
    public CharacterDefinition Definition { get; private set; } = null!;
    public Node3D Pose { get; private set; } = null!;
    public Node3D Model { get; private set; } = null!;
    public Node3D Body { get; private set; } = null!;
    public Node3D Deform { get; private set; } = null!;
    public Node3D? Head { get; private set; }
    public Node3D LeftFoot { get; private set; } = null!;
    public Node3D RightFoot { get; private set; } = null!;
    public Node3D Weapon { get; private set; } = null!;
    public Node3D Muzzle { get; private set; } = null!;
    public bool IsPlaceholder { get; private set; }

    private Transform3D _bodyHome, _leftHome, _rightHome, _weaponHome, _headHome;
    private Vector3 _deformHome;
    private float _clock;
    private Vector3 _lastVelocity, _headOffset, _headVelocity;
    private float _forwardSign;

    public static CharacterRig Create(CharacterDefinition def)
    {
        var rig = new CharacterRig { Name = "Rig" };
        rig.Build(def);
        return rig;
    }

    private void Build(CharacterDefinition def)
    {
        Definition = def;
        HandColor = def.HandColor;
        Pose = new Node3D { Name = "Pose" };
        AddChild(Pose);

        IsPlaceholder = def.Model == null;
        Model = def.Model?.Instantiate<Node3D>() ?? PlaceholderModel.Build(def);
        Model.Name = "Model";
        Model.Scale = Vector3.One * def.ModelScale;
        if (!def.ModelFacesPositiveZ) Model.RotationDegrees = new Vector3(0, 180, 0);
        Pose.AddChild(Model);
        _forwardSign = def.ModelFacesPositiveZ ? 1 : -1;

        Body = Find(def.BodyNode, "Body") ?? Pose;
        Deform = Find(def.DeformNode, "Deform") ?? Body;
        Head = Find(def.HeadNode, "Head");
        LeftFoot = Find(def.LeftFootNode, "LeftFoot") ?? Pivot(Model, "LeftFoot", new Vector3(-.2f, 0, 0));
        RightFoot = Find(def.RightFootNode, "RightFoot") ?? Pivot(Model, "RightFoot", new Vector3(.2f, 0, 0));
        Weapon = Find(def.WeaponNode, "Weapon") ?? Pivot(Model, "Weapon", new Vector3(0, def.ColliderHeight * .45f, 0) / def.ModelScale);
        Muzzle = Find(def.MuzzleNode, "Muzzle")
            ?? Pivot(Weapon, "Muzzle", new Vector3(0, 0, (def.ColliderRadius + .4f) * _forwardSign) / def.ModelScale);

        PrepareTail();
        PrepareEars();
        PrepareBodyTones();
        PrepareColors();
        ApplyBodyTones(new AppearanceProfile());
        if (def.IsZombie) Zombify(def);
        HidePart(def.Id + "_beret");
        HidePart(def.Id + "_headset");
        HidePart(def.Id + "_vestPadded");
        HidePart(def.Id + "_crestShort");
        HidePart(def.Id + "_crestSwept");

        _bodyHome = Body.Transform;
        _deformHome = Deform.Scale;
        _leftHome = LeftFoot.Transform;
        _rightHome = RightFoot.Transform;
        _weaponHome = Weapon.Transform;
        if (Head != null) _headHome = Head.Transform;
    }

    /// <summary>Strip gear, tint every mesh, hunch forward and reach out with stubby arms.</summary>
    private void Zombify(CharacterDefinition def)
    {
        foreach (var name in def.GearNodes) HidePart(name);
        HideBuiltInWeapon();
        var tint = def.ZombieOverlay;
        foreach (var mesh in Model.FindChildren("*", nameof(MeshInstance3D), true, false))
            ((MeshInstance3D)mesh).MaterialOverlay = tint;
        if (Model is MeshInstance3D rootMesh) rootMesh.MaterialOverlay = tint;

        Body.Rotation += new Vector3(.22f * _forwardSign, 0, 0);
        if (IsPlaceholder) return;
        float h = def.ColliderHeight / def.ModelScale, r = def.ColliderRadius / def.ModelScale;
        var armMat = Mat.Solid(def.HandColor.Lerp(new Color(def.ZombieTint, 1), .45f));
        foreach (var side in new[] { -1f, 1f })
        {
            // MakeZombie changes the definition ID, so find the original species arm by suffix.
            if (Model.FindChild(side < 0 ? "*_leftArm" : "*_rightArm", true, false) is Node3D) continue;
            // Not in the tree yet, so compose local transforms instead of using global ones.
            var at = InRig(Deform).AffineInverse() * InRig(Model) * new Vector3(side * r * .7f, h * .5f, r * .8f * _forwardSign);
            var arm = Mat.Mesh(Deform, new CapsuleMesh { Radius = r * .18f, Height = r * 1.3f }, at, armMat, "ZombieArm");
            arm.RotationDegrees = new Vector3(80 * _forwardSign, side * 8, 0);
        }
    }

    private Transform3D InRig(Node3D node)
    {
        var t = Transform3D.Identity;
        for (Node? n = node; n != null && n != this; n = n.GetParent())
            if (n is Node3D n3) t = n3.Transform * t;
        return t;
    }

    public void HidePart(string name)
    {
        if (!string.IsNullOrEmpty(name) && Model.FindChild(name, true, false) is Node3D node) node.Visible = false;
    }

    /// <summary>The game attaches its own weapons; hide whatever gun the model was built with.</summary>
    public void HideBuiltInWeapon() => HidePart(IsPlaceholder ? PlaceholderModel.PlaceholderWeaponNode : Definition.BuiltInWeaponNode);

    private Node3D? Find(string configured, string fallback)
    {
        foreach (var name in new[] { configured, fallback })
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (Model.Name == name) return Model;
            if (Model.FindChild(name, true, false) is Node3D node) return node;
        }
        return null;
    }

    private static Node3D Pivot(Node3D parent, string name, Vector3 position)
    {
        var node = new Node3D { Name = name + "Pivot", Position = position };
        parent.AddChild(node);
        return node;
    }

    /// <summary>Turn the rig to look along a world direction (Y ignored).</summary>
    public void Face(Vector3 direction)
    {
        direction.Y = 0;
        if (direction.LengthSquared() < .0001f) return;
        Rotation = new Vector3(0, Mathf.Atan2(direction.X, direction.Z), 0);
    }

    public Vector3 Forward => GlobalTransform.Basis.Z.Normalized();

    /// <param name="moveAmount">0..1 share of full walking speed.</param>
    /// <param name="kick">0..1 recoil.</param>
    /// <param name="hurt">0..1 recent damage squash.</param>
    /// <param name="velocity">World velocity, drives the head spring.</param>
    public void Animate(float dt, float moveAmount, float kick, float hurt, Vector3 velocity)
    {
        ResetHunterBowGeometry();
        var def = Definition;
        _clock += dt * def.StepRate;
        float step = Mathf.Sin(_clock);
        float bob = Mathf.Abs(step) * .05f * moveAmount;
        float waddle = Mathf.DegToRad(def.WaddleDegrees) * Mathf.Cos(_clock) * moveAmount;

        Body.Transform = _bodyHome;
        Body.Position += Vector3.Up * bob;
        Body.Rotation += new Vector3(-kick * .12f * _forwardSign, 0, waddle);

        LeftFoot.Transform = _leftHome;
        LeftFoot.Position += new Vector3(0, Mathf.Max(0, step) * .12f, step * .14f) * moveAmount;
        RightFoot.Transform = _rightHome;
        RightFoot.Position += new Vector3(0, Mathf.Max(0, -step) * .12f, -step * .14f) * moveAmount;

        Weapon.Transform = _weaponHome;
        Weapon.Position += new Vector3(0, 0, -kick * .1f * _forwardSign);
        ApplyCarryPose(kick);

        Deform.Scale = _deformHome * new Vector3(1 + hurt * .08f, 1 - hurt * .14f, 1 + hurt * .08f);

        UpdateHeadSpring(dt, velocity, kick);
        UpdateTail(dt, moveAmount);
        UpdateWeaponGrip();
    }

    /// <summary>Bobble-head: a damped spring pulls the head back while the body accelerates.</summary>
    private void UpdateHeadSpring(float dt, Vector3 velocity, float kick)
    {
        if (Head == null || dt <= 0) return;
        var accel = (velocity - _lastVelocity) / dt;
        _lastVelocity = velocity;
        accel.Y = 0;
        var force = -_headOffset * 220f - _headVelocity * 14f - accel * .9f - Forward * kick * 9f;
        _headVelocity += force * dt;
        _headOffset += _headVelocity * dt;
        _headOffset = _headOffset.LimitLength(.14f);

        Head.Transform = _headHome;
        if (Head.GetParent() is Node3D parent)
        {
            var local = parent.GlobalTransform.Basis.Inverse() * _headOffset;
            Head.Position += local;
            var tilt = GlobalTransform.Basis.Inverse() * _headOffset;
            Head.Rotation += new Vector3(tilt.Z * 1.6f * _forwardSign, 0, -tilt.X * 1.6f * _forwardSign);
        }
    }

    /// <summary>Kick the head spring (e.g. when hit) for an extra wobble.</summary>
    public void Jolt(Vector3 worldImpulse) => _headVelocity += worldImpulse;

    public void ResetPose()
    {
        EndZiplinePose();
        ResetReloadPose();
        ResetEnemyAttackPose();
        ResetHunterAttackPose();
        _meleePoseTime = -1;
        ResetTail();
        Body.Transform = _bodyHome;
        Deform.Scale = _deformHome;
        LeftFoot.Transform = _leftHome;
        RightFoot.Transform = _rightHome;
        Weapon.Transform = _weaponHome;
        if (Head != null) Head.Transform = _headHome;
        _headOffset = _headVelocity = _lastVelocity = Vector3.Zero;
        Pose.Transform = Transform3D.Identity;
        ApplyCarryPose(0);
        UpdateWeaponGrip();
    }

    /// <summary>
    /// A limp corpse pose instead of the standing one (I-83): the head droops forward and rolls to <paramref name="side"/>
    /// (−1 or 1), the feet splay out and the arms flop. Built from the parts every model has (head, feet) and the arm
    /// nodes some have; nothing here can be undone, so it is only for corpses.
    /// </summary>
    public void ApplyDeathPose(float side)
    {
        float f = _forwardSign;
        float r = Definition.ColliderRadius / Mathf.Max(Definition.ModelScale, .001f);
        if (Head != null) Head.Transform = _headHome * new Transform3D(Basis.FromEuler(new Vector3(.55f * f, 0, .35f * side)), Vector3.Zero);
        LeftFoot.Transform = Splay(_leftHome, -1, r);
        RightFoot.Transform = Splay(_rightHome, 1, r);
        Weapon.Transform = _weaponHome * new Transform3D(Basis.FromEuler(new Vector3(-.5f * f, 0, .4f * side)), Vector3.Zero);
        foreach (var s in new[] { -1f, 1f })
            if (Model.FindChild(s < 0 ? "*_leftArm" : "*_rightArm", true, false) is Node3D arm)
                arm.Rotation += new Vector3(0, 0, s * .9f);
        // Zombies' reaching arms drop.
        foreach (var child in Deform.GetChildren())
            if (child is MeshInstance3D arm && arm.Name.ToString().StartsWith("ZombieArm"))
                arm.RotationDegrees = new Vector3(15 * f, arm.RotationDegrees.Y * 3, 0);
    }

    private static Transform3D Splay(Transform3D home, float side, float radius) =>
        home * new Transform3D(Basis.FromEuler(new Vector3(0, 0, side * .5f)), new Vector3(side * radius * .15f, radius * .05f, 0));

    /// <summary>Squash used while lying on the ground as a corpse.</summary>
    public void SetSquash(float amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        Deform.Scale = _deformHome * new Vector3(1 + amount * .3f, 1 - amount * .5f, 1 + amount * .3f);
    }
}
