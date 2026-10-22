using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// AI squadmates (doc/ai-squad-plan.md): spawning, roles and weapon pools, following without leading,
/// fighting, rescuing a grabbed teammate, healing, pickups and catching up from the walked trail.
/// Windowed runs also save output/squad-*.png.
/// </summary>
public partial class SquadCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }
    private float Seconds(float s) => s;

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("SQUAD_CHECK_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("SQUAD_CHECK_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task<Stage> NewStage(int squad)
    {
        if (_stage != null && IsInstanceValid(_stage)) { _stage.QueueFree(); await Frames(2); }
        _stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false, SquadOverride = squad };
        AddChild(_stage);
        await Frames(3);
        _stage.Player.ManualControl = true;
        _stage.Player.Invulnerable = true;
        return _stage;
    }

    private async Task Run()
    {
        var db = SquadDatabase.Current;
        Check(db.Errors.Count == 0 && db.Roles.Count == 3, "squad roles and rules load without errors" + (db.Errors.Count > 0 ? ": " + string.Join("; ", db.Errors) : ""));
        Check(db.Roles.All(r => r.PrimaryPool.All(p => p.Weapon is not ("alabong" or "mortar" or "awm" or "m82"))), "no rocket, mortar or bolt sniper in any NPC pool");

        var stage = await NewStage(0);
        Check(stage.Players.Count == 1 && stage.Squad.Count == 0, "a test stage has no squadmates unless asked");

        stage = await NewStage(3);
        Check(stage.Players.Count == 4 && stage.Squad.Count == 3, "three squadmates join the player");
        Check(stage.Squad.Select(b => b.Role.Id).Distinct().Count() == 3, "three squadmates take one role each");
        foreach (var brain in stage.Squad)
        {
            var bot = brain.Bot;
            Check(bot.IsBot && bot.ManualControl && Mathf.IsEqualApprox(bot.DamageMultiplier, brain.Role.DamageMultiplier) && !bot.CritReady, $"{bot.Name}: AI survivor, softer damage, no crits");
            Check(bot.Loadout.Primary is { } p && brain.Role.PrimaryPool.Any(w => w.Weapon == p.Def.Id) && bot.Loadout.Secondary.Def.Id == brain.Role.Secondary,
                $"{bot.Name}: weapons come from the {brain.Role.Id} pool");
            Check(bot.Definition.Id != stage.Player.Definition.Id && bot.FindChild("Ears", true, false) == null, $"{bot.Name}: another species, no audio listener");
        }
        await Frames(2);
        Check(stage.Hud.FindChild("SquadPanel", true, false) is Control { Visible: true } panel && panel.FindChildren("Squad_*", "", true, false).Count == 3, "HUD lists the three teammates");

        // Follow along the street without ever walking ahead of the leader (no zombies around for this part).
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        await Frames(2);
        stage.LeaveSafeRoom();
        var leader = stage.Player;
        float worstLead = float.MinValue;
        for (int i = 0; i < 60 * 6; i++)
        {
            leader.SetControls(new Vector2(1, 0), leader.GlobalPosition + new Vector3(10, 1, 0), false);
            await Frames(1);
            float lp = stage.Progress(leader.GlobalPosition);
            foreach (var brain in stage.Squad) worstLead = Mathf.Max(worstLead, lp - stage.Progress(brain.Bot.GlobalPosition));
        }
        leader.SetControls(Vector2.Zero, leader.GlobalPosition + new Vector3(10, 1, 0), false);
        await Frames(60 * 2);
        float farthest = stage.Squad.Max(b => b.Bot.GlobalPosition.DistanceTo(leader.GlobalPosition));
        Check(leader.GlobalPosition.X > 18 && farthest < 7f, $"squadmates keep up while the leader walks (farthest {farthest:0.0} m)");
        Check(worstLead < 1.5f, $"squadmates never get ahead of the leader on the route (worst {worstLead:0.0} m ahead)");
        Check(stage.Trail.Count > 10, "the leader's walked trail is recorded");
        await Shot("squad-follow");

        // Fight: the squad kills a zombie the player never shoots.
        var zombie = stage.Spawn(MonsterDatabase.Get("humanoid"), leader.GlobalPosition + new Vector3(9, 0, 0), awake: true);
        for (int i = 0; i < 60 * 8 && IsInstanceValid(zombie) && !zombie.IsDead; i++) await Frames(1);
        Check(!IsInstanceValid(zombie) || zombie.IsDead, "squadmates kill an approaching zombie on their own");

        // Rescue: a grabbed squadmate is freed by the others.
        var held = stage.Squad[0].Bot;
        var tether = (CaptureEnemy)stage.Spawn(MonsterDatabase.Get("tether"), held.GlobalPosition + new Vector3(6, 0, 1.5f), awake: true);
        await Frames(2);
        // The others would shoot the tether during its windup (good play); hold them for this check.
        foreach (var other in stage.Squad) if (other.Bot != held) other.Bot.Launch(Vector3.Zero, 2.5f);
        tether.StartWindup(held);
        for (int i = 0; i < 60 * 3 && !held.IsRestrained; i++) await Frames(1);
        Check(held.IsRestrained && tether.Held == held, "a tether really grabs a squadmate (windup, then hold)");
        for (int i = 0; i < 60 * 10 && held.IsRestrained; i++) await Frames(1);
        string state = string.Join("; ", stage.Squad.Select(b => $"{b.Bot.Name} {b.Doing} {b.Bot.Gun.Def.Id} mag {b.Bot.Gun.Mag}/{b.Bot.Gun.Reserve} target {(b.FightTarget == tether ? "tether" : b.FightTarget?.Name ?? "-")} at {b.Bot.GlobalPosition}"))
            + (IsInstanceValid(tether) ? $" | tether hp {tether.Health} at {tether.GlobalPosition}" : "");
        Check(!held.IsRestrained && (!IsInstanceValid(tether) || tether.IsDead), "the other squadmates kill the captor and free their teammate" + (held.IsRestrained ? " — " + state : ""));

        // Heal when hurt and nothing is close.
        var patient = stage.Squad[1].Bot;
        patient.Medkits = 1;
        patient.TakeDamage(patient.MaxHealth * .7f, Vector3.Left, 0);
        int hurt = patient.Health;
        for (int i = 0; i < 60 * 5 && patient.Medkits > 0; i++) await Frames(1);
        Check(patient.Medkits == 0 && patient.Health > hurt, "a hurt squadmate uses its medkit when it is safe");

        // Pickups: a squadmate without a medkit walks over and takes one lying near the group.
        var taker = stage.Squad[2].Bot;
        taker.Medkits = 0;
        foreach (var other in stage.Squad) if (other.Bot != taker) other.Bot.Medkits = 1;
        var medkit = new Interactable { Name = "MedkitTest", Prompt = "ui_use_medkit", Radius = 1.6f, Pickup = PickupKind.Medkit };
        stage.AddChild(medkit);
        medkit.GlobalPosition = taker.GlobalPosition + new Vector3(-2.5f, 0, 1.5f);
        stage.Interactables.Add(medkit);
        for (int i = 0; i < 60 * 6 && taker.Medkits == 0; i++) await Frames(1);
        Check(taker.Medkits == 1 && !medkit.Enabled, "a squadmate walks over and picks up a nearby medkit by itself");
        var weapon = stage.Interactables.OfType<WeaponPickup>().First();
        Check(!stage.TakePickup(weapon, taker), "a squadmate never takes a floor weapon");
        // The supply kind decides, not the prompt's text key (I-94 ②): a renamed key still works, a look-alike key does not.
        var renamedAmmo = new Interactable { Prompt = "ui_use_ammo_renamed", Pickup = PickupKind.Ammo };
        var lookAlike = new Interactable { Prompt = "ui_use_medkit" };
        Check(Stage.IsAmmo(renamedAmmo) && !Stage.IsMedkit(renamedAmmo) && !Stage.IsMedkit(lookAlike) && !Stage.IsAmmo(lookAlike),
            "supplies are told apart by their kind, not their prompt text key");
        renamedAmmo.Free();
        lookAlike.Free();

        // Catch up: a squadmate stuck far behind and off screen reappears on the walked trail.
        var straggler = stage.Squad[2];
        straggler.Bot.TeleportTo(new Vector3(6, 0, 0));
        stage.Layout.StartDoor.SetOpen(false, 0); // shut in the safe room: it cannot walk back by itself
        for (int x = (int)leader.GlobalPosition.X; x < (int)leader.GlobalPosition.X + 26; x++)
        {
            var next = new Vector3(x, 0, leader.GlobalPosition.Z);
            if (stage.Flow.IsBlocked(next)) break;
            leader.TeleportTo(next);
            await Frames(1);
        }
        leader.SetControls(Vector2.Zero, leader.GlobalPosition + Vector3.Right * 5, false);
        stage.Cam.SnapTo(leader.GlobalPosition);
        int before = straggler.Teleports;
        for (int i = 0; i < 60 * 10 && straggler.Teleports == before; i++) await Frames(1);
        float gap = straggler.Bot.GlobalPosition.DistanceTo(leader.GlobalPosition);
        var rules = SquadDatabase.Current.Rules;
        Check(straggler.Teleports > before && gap >= rules.CatchupBehindMin - 1 && gap <= rules.CatchupBehindMax + 2,
            $"a far, unseen squadmate catches up to the nearest off-screen walked spot ({gap:0.0} m from the leader)");
        Check(!stage.Effects.IsOnScreen(straggler.Bot.GlobalPosition + Vector3.Up, 1.2f) || DisplayServer.GetName() == "headless", "it reappears off screen");
        Check(stage.Progress(straggler.Bot.GlobalPosition) >= stage.Progress(leader.GlobalPosition) - 1f, "it reappears behind the leader, never ahead");
        Check(stage.Trail.Any(p => p.DistanceTo(straggler.Bot.GlobalPosition) < .6f), "it reappears on a spot the leader already walked");
        stage.Layout.StartDoor.SetOpen(true, 0);

        await ReviveChecks();
        await SquadShot();
    }

    private async Task ReviveChecks()
    {
        var rules = SquadDatabase.Current.Rules;

        // Revive off (test default): 0 health is death, no respawn.
        var stage = await NewStage(2);
        var victim = stage.Squad[0].Bot;
        victim.TakeDamage(9999, Vector3.Left, 0);
        await Frames(2);
        Check(!stage.ReviveEnabled && victim.IsDead && !victim.IsDowned && stage.PendingRespawns == 0, "with revive off a squadmate dies at 0 health and stays dead");

        // Revive off: a dead human ends the run even with squadmates standing.
        stage.Player.Invulnerable = false;
        stage.Player.TakeDamage(9999, Vector3.Left, 0);
        await Frames(2);
        Check(stage.State == StageState.Dead, "the human dying is game over even with squadmates standing");

        // Revive on.
        _stage.QueueFree(); await Frames(2);
        _stage = new Stage { CharacterOverride = "goose", DirectorEnabled = false, SquadOverride = 2, ReviveOverride = true };
        AddChild(_stage); await Frames(3);
        stage = _stage;
        stage.Player.ManualControl = true;
        stage.Player.Invulnerable = true;
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        await Frames(2);
        var downed = stage.Squad[0].Bot;
        var helper = stage.Squad[1].Bot;
        downed.TakeDamage(9999, Vector3.Left, 0);
        await Frames(2);
        Check(stage.ReviveEnabled && downed.IsDowned && !downed.IsDead && downed.DownCount == 1 && stage.AlivePlayers.Contains(downed), "with revive on a squadmate goes down instead of dying");
        Check(stage.Squad[0].Doing == SquadBrain.Activity.Down, "a downed squadmate does nothing but wait");
        for (int i = 0; i < 60 * (int)(rules.ReviveTime + 4) && downed.IsDowned; i++) await Frames(1);
        Check(!downed.IsDowned && downed.Health == rules.ReviveHealth, $"the other squadmate walks over and picks it up ({downed.Health} health)");

        // Two squadmates never stack on one patient: it still takes the full revive time.
        foreach (var e in stage.AllEnemies.ToList()) e.QueueFree();
        var third = stage.AddSurvivor(CharacterCatalog.Player("duck"), helper.GlobalPosition + new Vector3(0, 0, 1.2f), true, "Squad3", new AppearanceProfile());
        stage.Squad.Add(SquadBrain.Attach(stage, third, stage.Squad[1].Role, 2));
        await Frames(2);
        downed.TakeDamage(9999, Vector3.Left, 0);
        await Frames(1);
        helper.TeleportTo(downed.GlobalPosition + new Vector3(1f, 0, 0));
        third.TeleportTo(downed.GlobalPosition + new Vector3(-1f, 0, 0));
        int frames = 0;
        for (; frames < 60 * 10 && downed.IsDowned; frames++) await Frames(1);
        Check(!downed.IsDowned && frames >= 60 * rules.ReviveTime * .9f, $"two squadmates beside one patient still take the full {rules.ReviveTime:0} s ({frames / 60f:0.0} s)");
        downed.ResetDowns();
        third.TeleportTo(new Vector3(60, 0, 0));
        third.Launch(Vector3.Zero, 30f);

        // A patient on the far side of a wall and a doorway: the reviver walks around to them.
        stage.LeaveSafeRoom();
        await Frames(2);
        downed.TeleportTo(new Vector3(6, 0, 2.5f));
        helper.TeleportTo(new Vector3(15, 0, 3.5f));
        stage.Player.TeleportTo(new Vector3(18, 0, 0));
        await Frames(2);
        downed.TakeDamage(9999, Vector3.Left, 0);
        for (int i = 0; i < 60 * 12 && downed.IsDowned; i++) await Frames(1);
        Check(!downed.IsDowned, "a squadmate walks through the doorway to pick up a teammate behind a wall");
        downed.ResetDowns();

        // A hit on the reviver starts the bar over.
        downed.TakeDamage(9999, Vector3.Left, 0);
        await Frames(1);
        var me = stage.Player;
        me.Invulnerable = false;
        helper.TeleportTo(new Vector3(40, 0, 0)); // keep the AI helper out of reach
        stage.Squad[1].Bot.Launch(Vector3.Zero, 6f);
        me.TeleportTo(downed.GlobalPosition + new Vector3(.8f, 0, 0));
        me.SetControls(Vector2.Zero, downed.GlobalPosition, false);
        me.HoldRevive(true);
        await Frames(60);
        float before = downed.ReviveProgress;
        Check(before > .15f && stage.RevivePromptTarget == downed, $"holding E beside a downed teammate fills the bar ({before:P0})");
        me.TakeDamage(5, Vector3.Left, 0);
        await Frames(1);
        Check(downed.ReviveProgress < before * .5f, "a hit on the reviver starts the bar over");
        await Frames(60 * (int)(rules.ReviveTime + 1));
        Check(!downed.IsDowned, "the player can pick a squadmate up too");
        if (downed.DownCount < 2) { downed.TakeDamage(9999, Vector3.Left, 0); await Frames(1); downed.Revive(); }
        Check(downed.DownCount == 2 && downed.OnLastDown, "two downs in one life means the next fall is death");
        me.HoldRevive(false);
        me.Invulnerable = true;

        // Third fall is death; with respawns left the squadmate walks back in later.
        string fallenName = downed.Name;
        downed.TakeDamage(9999, Vector3.Left, 0);
        await Frames(2);
        Check(downed.IsDead && stage.PendingRespawns == 1 && stage.RespawnsUsed(0) == 1, "falling on the last down is death, and a respawn is queued");
        me.TeleportTo(new Vector3(40, 0, 0));
        for (int x = 40; x < 70; x++) { me.TeleportTo(new Vector3(x, 0, 0)); await Frames(1); }
        stage.Cam.SnapTo(me.GlobalPosition);
        stage.ExpediteRespawnsForTest();
        for (int i = 0; i < 60 * 3 && stage.PendingRespawns > 0; i++) await Frames(1);
        var back = stage.Squad.Find(b => b.Slot == 0)?.Bot;
        Check(stage.PendingRespawns == 0 && back != null && back != downed && !back.IsDead && back.Health == rules.RespawnHealth,
            $"the fallen squadmate rejoins with {rules.RespawnHealth} health");
        Check(stage.Progress(back!.GlobalPosition) >= stage.Progress(me.GlobalPosition) - 1 && stage.Trail.Any(p => p.DistanceTo(back.GlobalPosition) < .6f),
            "it rejoins on the walked route behind the player");
        int bots = stage.Players.Count(p => p.IsBot);
        Check(!IsInstanceValid(downed) && stage.Players.Contains(back) && bots == stage.Squad.Count, "the new squadmate replaces the fallen one");

        await Frames(2);
        Check(stage.Hud.FindChild("SquadPanel", true, false) is Control squadPanel && squadPanel.FindChildren("Squad_*", "", true, false).Count == bots
              && squadPanel.FindChild("Squad_" + fallenName, true, false) == null, "the HUD row of the fallen squadmate is replaced by the new one");

        // Bleeding out: no one comes, hits make it faster, and death is reported once.
        var lonely = stage.Squad.Find(b => b.Slot == 1)!.Bot;
        int deaths = 0;
        lonely.Died += () => deaths++;
        foreach (var b in stage.Squad) if (b.Bot != lonely) { b.Bot.TeleportTo(new Vector3(90, 0, 0)); b.Bot.Launch(Vector3.Zero, 40f); }
        lonely.TakeDamage(9999, Vector3.Left, 0);
        await Frames(1);
        Check(lonely.IsDowned, "a squadmate alone goes down while the player stands");
        for (int i = 0; i < 60 * 12 && !lonely.IsDead; i++) { lonely.TakeDamage(5, Vector3.Left, 0); await Frames(1); }
        await Frames(30);
        Check(lonely.IsDead && deaths == 1, $"hits bleed a downed squadmate out faster, and death fires once ({deaths})");

        // Medkits reset the down count.
        back.TakeDamage(9999, Vector3.Left, 0);
        await Frames(1);
        back.Revive();
        back.Medkits = 1;
        back.PressHeal();
        for (int i = 0; i < 60 * 4 && back.Medkits > 0; i++) await Frames(1);
        Check(back.DownCount == 0, "a medkit resets the down count");

        // The player going down is not game over while a squadmate can pick them up.
        me.Invulnerable = false;
        foreach (var b in stage.Squad) b.Bot.TeleportTo(me.GlobalPosition + new Vector3(-2, 0, 0));
        me.TakeDamage(9999, Vector3.Left, 0);
        await Frames(2);
        Check(me.IsDowned && stage.State == StageState.Playing, "the player going down is not game over while squadmates stand");
        for (int i = 0; i < 60 * (int)(rules.ReviveTime + 4) && me.IsDowned; i++) await Frames(1);
        Check(!me.IsDowned && stage.State == StageState.Playing, "a squadmate picks the player up");

        // Everyone down at once is game over.
        foreach (var b in stage.Squad) b.Bot.TakeDamage(9999, Vector3.Left, 0);
        me.TakeDamage(9999, Vector3.Left, 0);
        await Frames(2);
        Check(stage.State == StageState.Dead, "all survivors down or dead is game over");
    }

    private async Task SquadShot()
    {
        if (DisplayServer.GetName() == "headless") return;
        var stage = await NewStage(3);
        stage.LeaveSafeRoom();
        var leader = stage.Player;
        leader.TeleportTo(new Vector3(22, 0, .5f));
        foreach (var (brain, i) in stage.Squad.Select((b, i) => (b, i))) brain.Bot.TeleportTo(new Vector3(20, 0, -1.5f + i * 1.5f));
        leader.Weapons.Pickup(ArmoryCatalog.Weapon("m4"), null);
        stage.Cam.SnapTo(leader.GlobalPosition);
        for (int i = 0; i < 10; i++) stage.SpawnCommon(new Vector3(32 + i % 3 * 1.3f, 0, -2 + i / 3 * 1.4f), true);
        leader.SetControls(Vector2.Zero, new Vector3(32, 1, 0), true);
        await Frames(55);
        await Shot("squad-fight");
    }

    private async Task Shot(string name)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://output/" + name + ".png"));
    }
}
