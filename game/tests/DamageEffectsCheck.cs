using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>Accepted damage alone produces feedback; death is once-only and preserves corpses.</summary>
public partial class DamageEffectsCheck : Node
{
    private Stage _stage = null!;
    private int _checks;
    private void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
        _checks++; GD.Print("PASS: " + message);
    }
    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }
    private static Node[] Bursts(Node parent, string name) => parent.GetChildren().Where(n => n.Name == name).ToArray();
    private int Deaths => Bursts(_stage.Effects, "DeathBurst").Length;
    private static Color FlashColor(CharacterRig rig)
    {
        var mesh = rig.Model.FindChildren("*", nameof(MeshInstance3D), true, false).Cast<MeshInstance3D>().First();
        Material material = mesh.MaterialOverlay!;
        while (material.NextPass != null) material = material.NextPass;
        return ((StandardMaterial3D)material).AlbedoColor;
    }
    private static Color RayColor(Node burst) => ((ShaderMaterial)burst.GetNode<MeshInstance3D>("DamageRays").MaterialOverride).GetShaderParameter("tint").AsColor();
    private Enemy Spawn(string id, Vector3 at)
    {
        var enemy = _stage.Spawn(MonsterDatabase.Get(id), at, false);
        enemy.SetPhysicsProcess(false);
        enemy.Rig.ShowAs(VisionSystem.Visibility.Visible);
        return enemy;
    }
    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("DAMAGE_EFFECTS_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); failed = true; }
        QuitHygiene.Collect(); GetTree().Quit(failed ? 1 : 0);
    }
    private async Task Run()
    {
        _stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false, SquadOverride = 0, ReviveOverride = false };
        AddChild(_stage); await Frames(3);
        foreach (var enemy in _stage.AllEnemies.ToList()) enemy.QueueFree();
        var player = _stage.Player;
        player.ManualControl = true;
        player.TeleportTo(new Vector3(14, 0, 0));
        player.SetPhysicsProcess(false);
        _stage.Cam.Close = true;
        _stage.Cam.SnapTo(player.GlobalPosition + Vector3.Right * 2);
        await Frames(3);
        player.TakeDamage(0, Vector3.Right);
        player.Invulnerable = true;
        player.TakeDamage(10, Vector3.Right);
        Check(Bursts(player.Rig, "HitBurst").Length == 0, "ignored player damage has no impact rays");
        player.Invulnerable = false;
        int hp = player.Health;
        player.TakeDamage(2, Vector3.Right);
        Check(player.Health == hp - 2, "hit presentation leaves player damage unchanged");
        Check(Bursts(player.Rig, "HitBurst").Length == 1, "accepted player damage shows impact rays");
        await Shot("player-hit");
        for (int i = 0; i < 20; i++) player.TakeDamage(1, Vector3.Right);
        Check(Bursts(player.Rig, "HitBurst").Length == 1, "rapid hits reuse one burst on each character");
        await Frames(20);
        Check(Bursts(player.Rig, "HitBurst").Length == 0, "player impact rays expire after hits stop");

        var target = Spawn("humanoid", player.GlobalPosition + Vector3.Right * 3);
        var point = target.GlobalPosition + Vector3.Up;
        float enemyHp = target.Health;
        target.TakeHit(new HitInfo(0, Vector3.Right, point));
        target.Shove(Vector3.Right, 0, 0);
        target.Burn(0);
        Check(Bursts(target.Rig, "HitBurst").Length == 0, "zero damage and harmless shoves do not create impact rays");
        target.TakeHit(new HitInfo(1, Vector3.Right, point));
        Check(target.Health == enemyHp - 1 && Bursts(target.Rig, "HitBurst").Length == 1, "accepted enemy hit shows feedback without extra damage");
        await Shot("enemy-hit");
        target.TakeHit(new HitInfo(1, Vector3.Right, point, true));
        var criticalColor = FlashColor(target.Rig);
        Check(criticalColor.R > criticalColor.B * 2 && criticalColor.G > criticalColor.B * 2, "a critical hit changes the body flash to yellow");
        var criticalBurst = Bursts(target.Rig, "HitBurst").Single();
        Check(RayColor(criticalBurst).R == criticalColor.R && RayColor(criticalBurst).G == criticalColor.G && RayColor(criticalBurst).B == criticalColor.B,
            "critical rays use the same yellow as the body");
        await Shot("critical-hit");
        target.TakeHit(new HitInfo(1, Vector3.Right, point));
        Check(RayColor(Bursts(target.Rig, "HitBurst").Single()) == Colors.White && FlashColor(target.Rig).B == 1,
            "a normal hit resets the reused critical effect to white");
        await Frames(20);
        target.Shove(Vector3.Right, 0, 1);
        Check(Bursts(target.Rig, "HitBurst").Length == 1, "damaging shoves use the same hit feedback");
        await Frames(20);
        target.Burn(1);
        Check(Bursts(target.Rig, "HitBurst").Length == 1, "burn feedback stays tied to accepted damage");

        var puppet = Spawn("humanoid", player.GlobalPosition + Vector3.Left * 3);
        puppet.MakePuppet(); float puppetHp = puppet.Health;
        puppet.ShowNetHit(puppet.GlobalPosition + Vector3.Up, Vector3.Right);
        Check(puppet.Health == puppetHp && Bursts(puppet.Rig, "HitBurst").Length == 1, "received enemy hit is presentation only");
        puppet.Die(Vector3.Right, 1);
        Check(puppet.IsDead && Deaths == 1, "received enemy death shares the one-shot death presentation");
        await Frames(25);
        var rig = target.Rig;
        int corpseCount = _stage.Effects.CorpseCount;
        target.TakeHit(new HitInfo(99999, Vector3.Right, point, true));
        Check(target.IsDead && Deaths == 1, "a lethal hit produces one death burst");
        Check(RayColor(Bursts(_stage.Effects, "DeathBurst").Single()).B < .5f && FlashColor(rig).B < .5f,
            "a critical kill keeps the body and death ring yellow");
        Check(_stage.Effects.CorpseCount == corpseCount + 1 && rig.GetParent() is Corpse, "death burst preserves the existing corpse");
        Check(Bursts(rig, "HitBurst").All(n => n.IsQueuedForDeletion()), "death replaces the small impact burst instead of stacking both");
        target.Die(Vector3.Right, 2);
        Check(Deaths == 1, "repeated death notification cannot duplicate the burst");
        await Shot("common-death");
        await Frames(30);
        Check(Deaths == 0 && GodotObject.IsInstanceValid(rig), "death burst expires while the corpse remains");

        var hidden = Spawn("humanoid", player.GlobalPosition + Vector3.Left * 10);
        hidden.Rig.ShowAs(VisionSystem.Visibility.Hidden);
        hidden.TakeHit(new HitInfo(1, Vector3.Right, hidden.GlobalPosition));
        Check(!((Node3D)Bursts(hidden.Rig, "HitBurst").Single()).IsVisibleInTree(), "hit rays cannot reveal a hidden living enemy");
        hidden.Die(Vector3.Right, 0);
        Check(Deaths == 0, "death burst cannot reveal the death of a hidden enemy");

        var named = Spawn("bull", player.GlobalPosition + Vector3.Right * 3);
        var namedRig = named.Rig;
        named.TakeHit(new HitInfo(1, Vector3.Right, named.GlobalPosition, true));
        named.Die(Vector3.Right, 999);
        Check(Deaths == 1 && namedRig.GetParent() is Corpse { Freeze: true }, "named death keeps its fixed corpse policy");
        Check(RayColor(Bursts(_stage.Effects, "DeathBurst").Single()) == Colors.White,
            "a later non-critical death does not inherit a previous critical hit's yellow");
        await Shot("named-death"); await Frames(30);
        var balloon = (BalloonEnemy)Spawn("balloon", player.GlobalPosition + Vector3.Right * 5);
        balloon.TakeHit(new HitInfo(99999, Vector3.Right, balloon.GlobalPosition, true)); balloon.Pop();
        Check(balloon.Popped && Deaths == 1, "balloon's special pop gets exactly one death burst");
        Check(RayColor(Bursts(_stage.Effects, "DeathBurst").Single()).B < .5f, "balloon's special death preserves the killing hit's critical color");
        await Shot("balloon-death"); await Frames(30);

        var cards = new LabCards { Stage = _stage };
        _stage.AddChild(cards);
        cards.Set(LabCard.Balloon, true);
        var cardTarget = Spawn("humanoid", player.GlobalPosition + Vector3.Right * 3);
        cardTarget.AddInflate(1, 4);
        cardTarget.TakeHit(new HitInfo(99999, Vector3.Right, cardTarget.GlobalPosition + Vector3.Up, true));
        Check(cardTarget.IsDead && cards.Pops == 1 && RayColor(Bursts(_stage.Effects, "DeathBurst").Single()).B < .5f,
            "a lethal critical hit stays yellow when the balloon card kills inside the hit callback");
        cards.QueueFree(); await Frames(30);

        player.TakeDamage(99999, Vector3.Right);
        Check(player.IsDead && Deaths == 1 && player.Corpse != null, "player death keeps its spectator corpse and adds one burst");
        player.TakeDamage(99999, Vector3.Right);
        Check(Deaths == 1, "damage to a dead player does not repeat the death effect");
        await Shot("player-death");
        await Frames(30);
        Check(Deaths == 0, "player death burst cleans up after game over");

        _stage.QueueFree(); await Frames(2);
        _stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false, SquadOverride = 1, ReviveOverride = true };
        AddChild(_stage); await Frames(3);
        _stage.Player.TakeDamage(99999, Vector3.Right);
        Check(_stage.Player.IsDowned && !_stage.Player.IsDead && Deaths == 0, "being downed does not play a final death burst");
    }
    private async Task Shot(string label)
    {
        if (DisplayServer.GetName() == "headless") return;
        var before = _stage.ProcessMode;
        _stage.ProcessMode = ProcessModeEnum.Disabled;
        for (int i = 0; i < 2; i++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://output"));
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/damage-fx-{label}.png"));
        _stage.ProcessMode = before;
    }
}
