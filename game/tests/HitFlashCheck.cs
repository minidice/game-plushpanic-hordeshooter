using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;

public partial class HitFlashCheck : Node
{
    public override void _Ready()
    {
        try
        {
            var species = CharacterCatalog.Player("goose");
            var zombie = CharacterDefinition.MakeZombie(species, species);
            foreach (var definition in new[] { species, zombie })
            {
                var rig = CharacterRig.Create(definition);
                var other = CharacterRig.Create(definition);
                AddChild(rig); AddChild(other);
                if (!definition.IsZombie)
                    rig.ApplyAppearance(new AppearanceProfile { Body = "d06040", Hat = 2, Vest = false });
                var meshes = Meshes(rig);
                var originals = meshes.Select(m => m.MaterialOverlay).ToArray();
                var surfaces = meshes.Select(m => m.Mesh == null ? null : m.GetActiveMaterial(0)).ToArray();
                var visible = meshes.Select(m => m.Visible).ToArray();
                var otherOverlays = Meshes(other).Select(m => m.MaterialOverlay).ToArray();
                var bodyColor = rig.ChannelColor("body");

                rig.FlashHit();
                var timer = rig.GetNode<Node>("HitFlash");
                var flashOverlay = meshes[0].MaterialOverlay;
                var white = LastPass(flashOverlay!);
                Require(white.AlbedoColor == new Color(1,1,1,CharacterHitFlashEffect.PeakAlpha), "hit begins with a soft white overlay");
                Require(meshes.Where((m, i) => m.MaterialOverlay == originals[i]).Count() == 0, "all character meshes receive flash");
                Require(Meshes(other).Select(m => m.MaterialOverlay).SequenceEqual(otherOverlays), "second rig retains its overlays");
                if (definition.IsZombie)
                {
                    Require(((StandardMaterial3D)flashOverlay!).AlbedoColor == definition.ZombieTint, "zombie tint remains below the white pass");
                    Require(definition.ZombieOverlay.NextPass == null, "shared zombie overlay was not mutated");
                }
                timer._Process(.05);
                Require(white.AlbedoColor.A > 0 && white.AlbedoColor.A < 1, "white overlay fades during the hit");
                rig.FlashHit();
                Require(meshes[0].MaterialOverlay == flashOverlay && white.AlbedoColor.A == CharacterHitFlashEffect.PeakAlpha, "repeated hits reuse and restart the existing effect");
                timer._Process(.2);
                for (int i = 0; i < meshes.Length; i++)
                {
                    Require(meshes[i].MaterialOverlay == originals[i], "exact original overlay is restored");
                    Require(meshes[i].GetActiveMaterial(0) == surfaces[i], "surface materials remain unchanged");
                    Require(meshes[i].Visible == visible[i], "flash never changes gear visibility");
                }
                Require(rig.ChannelColor("body") == bodyColor, "appearance colors survive hit");
                Require(!timer.IsProcessing(), "idle flash stops processing");

                rig.FlashHit();
                var corpse = new Corpse { Rig = rig };
                AddChild(corpse);
                rig.Reparent(corpse);
                timer._Process(.2);
                Require(meshes.Select(m => m.MaterialOverlay).SequenceEqual(originals), "corpse transfer still restores materials");
                rig.FlashHit();
                corpse.Free(); // Active flash teardown must not access freed meshes.
                other.Free();
            }
            var bull=CharacterRig.Create(CharacterCatalog.Enemy("bull"));AddChild(bull);
            bull.InitializeEnemyAttackPose();
            var fallback=bull.Pose.GetNode<MeshInstance3D>("EnemyLeftArm");
            var fallbackHome=fallback.MaterialOverlay;
            bull.FlashHit();
            Require(fallback.MaterialOverlay!=fallbackHome,"generated enemy arms flash with the body");
            bull.GetNode<Node>("HitFlash")._Process(.2);
            Require(fallback.MaterialOverlay==fallbackHome,"generated arms restore their material");
            bull.Free();
            CheckDamageAcceptance();
            GD.Print("HIT_FLASH_PASS");
            QuitHygiene.Collect();
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void CheckDamageAcceptance()
    {
        var stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false };
        AddChild(stage);
        foreach (var enemy in stage.GetNode<Node3D>("Enemies").GetChildren()) enemy.Free();
        stage.Enemies.Clear();
        var player = stage.Player;
        player.ManualControl = true;
        player.SetPhysicsProcess(false);
        int health = player.Health;
        player.TakeDamage(0, Vector3.Zero);
        player.TakeDamage(-1, Vector3.Zero);
        player.Invulnerable = true;
        player.TakeDamage(1, Vector3.Zero);
        Require(player.Health == health && !player.Rig.HasNode("HitFlash"), "ignored player damage cannot flash");
        player.Invulnerable = false;
        player.TakeDamage(1, Vector3.Zero);
        Require(player.Health == health - 1 && player.Rig.GetNode<Node>("HitFlash").IsProcessing(), "accepted player damage flashes the rig");
        player.Rig.GetNode<Node>("HitFlash")._Process(.2);

        var target = stage.SpawnEnemy<Enemy>(MonsterDatabase.Get("plush"), new Vector3(20, 0, 0), false);
        target.SetPhysicsProcess(false);
        float enemyHealth = target.Health;
        target.Stun(.1f);
        target.Shove(Vector3.Right, 1, 0);
        target.TakeHit(new HitInfo(0, Vector3.Right, target.GlobalPosition));
        target.Burn(0);
        Require(target.Health == enemyHealth && !target.Rig.HasNode("HitFlash"), "stuns and zero-damage enemy hits cannot flash");
        target.TakeHit(new HitInfo(1, Vector3.Right, target.GlobalPosition));
        var timer = target.Rig.GetNode<Node>("HitFlash");
        Require(target.Health == enemyHealth - 1 && timer.IsProcessing(), "accepted enemy hit flashes the rig");
        timer._Process(.2);
        target.Shove(Vector3.Right, 1, 1);
        Require(timer.IsProcessing(), "damaging shove flashes the rig");
        timer._Process(.2);
        target.Burn(1);
        Require(timer.IsProcessing(), "accepted burn damage flashes the rig");
        stage.Free();
    }

    private static MeshInstance3D[] Meshes(CharacterRig rig) => rig.Model.FindChildren("*", nameof(MeshInstance3D), true, false).Cast<MeshInstance3D>().ToArray();
    private static StandardMaterial3D LastPass(Material material)
    {
        while (material.NextPass != null) material = material.NextPass;
        return (StandardMaterial3D)material;
    }
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
}
