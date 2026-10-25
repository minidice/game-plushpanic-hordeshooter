using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

public sealed partial class CommonRoster
{
    // Colors are immutable cached material variants. Status overlays and hit flash remain rig-local.
    private readonly Dictionary<(ulong, string), Material> _materials = new();

    public void Apply(CharacterRig rig, CommonIdentity id)
    {
        ValidateIdentity(id);
        var palette = Palettes[id.Palette];
        if (id.Faction != "zombie") rig.SetCommonHandColor(new Color(palette.Primary[id.Variant % palette.Primary.Length]));
        var enabled = id.Faction == "zombie" ? Presets[id.Variant].Modules.ToHashSet() : new HashSet<string>();
        if (id.Gear.HasFlag(CommonGear.Vest)) enabled.Add("Gear_Vest");
        if (id.Gear.HasFlag(CommonGear.Glasses)) enabled.Add("Gear_Glasses");
        foreach (var node in rig.Model.FindChildren("*", nameof(Node3D), true, false).OfType<Node3D>())
        {
            string name = node.Name;
            if (Modules.Contains(name) || name is "Gear_Vest" or "Gear_Glasses") node.Visible = enabled.Contains(name);
        }
        foreach (var mesh in rig.Model.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            if (mesh.Mesh == null) continue;
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetActiveMaterial(surface) is not StandardMaterial3D material) continue;
                string name = material.ResourceName;
                string? color = name.StartsWith("common_primary") ? palette.Primary[id.Variant % palette.Primary.Length]
                    : name.StartsWith("common_secondary") ? palette.Secondary
                    : name.StartsWith("common_accent") ? palette.Accent
                    : name.StartsWith("common_cloth") ? palette.Cloth
                    : name.StartsWith("common_pants") ? palette.Pants : null;
                // common_skin and fixed_* preserve anatomy; never tint eyes, beaks, horns or zombie skin.
                if (color == null) continue;
                var key = (material.GetInstanceId(), color);
                if (!_materials.TryGetValue(key, out var copy))
                {
                    var variant = (StandardMaterial3D)material.Duplicate();
                    variant.AlbedoColor = new Color(color); copy = variant; _materials[key] = copy;
                }
                mesh.SetSurfaceOverrideMaterial(surface, copy);
            }
        }
        rig.HideBuiltInWeapon();
        if (id.Weapon == "unarmed") return;
        var built = CommonWeaponVisual.Build(Weapons[id.Weapon], rig.Definition.HandColor);
        built.Root.Scale = Vector3.One / rig.Definition.ModelScale;
        if (!rig.Definition.ModelFacesPositiveZ) built.Root.RotationDegrees = new Vector3(0, 180, 0);
        rig.Weapon.AddChild(built.Root);
        rig.BindWeaponGrip(built);
    }
}

/// <summary>Modern guns use the player's exact model/grip factory; simple hunter implements have dedicated geometry.</summary>
public static class CommonWeaponVisual
{
    public static WeaponVisual.Built Build(CommonWeapon weapon, Color hands)
    {
        if (weapon.SharedWeapon != "") return WeaponVisual.Build(ArmoryCatalog.Weapon(weapon.SharedWeapon), hands);
        var root = new Node3D { Name = "Weapon_" + weapon.Id };
        var wood = Mat.Solid(new Color("765033")); var steel = Mat.Solid(new Color("aab2b6"));
        var dark = Mat.Solid(new Color("30291f"));
        var def = new WeaponDefinition { Id = weapon.Id, Category = weapon.Range > 0 ? WeaponCategory.Rifle : WeaponCategory.Melee,
            Mode = weapon.Range > 0 ? FireMode.Projectile : FireMode.Melee, Length = .8f };
        if (weapon.Id == "club")
        {
            var club = Mat.Mesh(root, new CylinderMesh { TopRadius = .105f, BottomRadius = .045f, Height = .8f, RadialSegments = 8 }, new Vector3(0, 0, .34f), wood);
            club.RotationDegrees = new Vector3(90, 0, 0);
        }
        else if (weapon.Id == "knife")
        {
            Mat.Box(root, Vector3.Zero, new Vector3(.055f, .05f, .18f), dark);
            Mat.Box(root, new Vector3(0, 0, .22f), new Vector3(.065f, .015f, .3f), steel);
            Mat.Box(root, new Vector3(0, 0, .075f), new Vector3(.12f, .025f, .035f), steel);
        }
        else if (weapon.Id == "woodbow")
        {
            // Upright recurved wood bow and taut string, separate from the firearms silhouette.
            var points = new[] { new Vector3(0, -.55f, .08f), new Vector3(0, -.3f, .2f), new Vector3(0, 0, .26f), new Vector3(0, .3f, .2f), new Vector3(0, .55f, .08f) };
            for (int i = 1; i < points.Length; i++) Segment(root, points[i - 1], points[i], .035f, wood);
            Segment(root, points[0], points[^1], .008f, dark);
            Segment(root, new Vector3(0, 0, -.16f), new Vector3(0, 0, .65f), .013f, wood);
        }
        var right = new Node3D { Name = "GripRight", Position = new Vector3(.025f, -.02f, 0) }; root.AddChild(right);
        Node3D? left = null;
        if (weapon.Id == "woodbow") { left = new Node3D { Name = "GripLeft", Position = new Vector3(-.025f, 0, .26f) }; root.AddChild(left); }
        var muzzle = new Node3D { Name = "Muzzle", Position = new Vector3(0, 0, .65f) }; root.AddChild(muzzle);
        return new(root, new List<Node3D> { muzzle }, right, left, def);
    }
    private static void Segment(Node3D root, Vector3 a, Vector3 b, float thickness, Material material)
    {
        var segment = Mat.Box(root, (a + b) * .5f, new Vector3(thickness, thickness, a.DistanceTo(b)), material);
        segment.Quaternion = new Quaternion(Vector3.Back, (b - a).Normalized());
    }
}
