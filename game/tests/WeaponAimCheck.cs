using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// Aim cones and aim-point recoil (decisions.md C-30, doc/weapon-aim-reference.md): every gun's hip and aimed cones,
/// the cone closing over aim_time_s and opening twice as fast, bloom kept at half while aimed, the shotgun's pellet cone
/// following the aim, holstering dropping the aim, recoil kicking up and sideways within the data and sliding back once
/// quiet, and the trace Studio draws. Windowed: the reticle at hip, aimed, and hot after a burst (output/aim-*.png).
/// </summary>
public partial class WeaponAimCheck : Node
{
    private int _checks;
    private Stage _stage = null!;
    private bool Windowed => DisplayServer.GetName() != "headless";
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }
    private async Task Frames(int n) { for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("WEAPON_AIM_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("WEAPON_AIM_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Shot(string name)
    {
        if (!Windowed) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://output/aim-{name}.png"));
    }

    private async Task Run()
    {
        // ---------- data ----------
        var db = WeaponDatabase.Current;
        Check(db.Errors.Count == 0, "weapon data loads with the new aim and recoil columns" + (db.Errors.Count > 0 ? ": " + string.Join("; ", db.Errors) : ""));
        var guns = db.Weapons.Values.Where(d => d.Mode == FireMode.Hitscan).ToList();
        Check(guns.All(d => d.AimSpread < d.BaseSpread && d.AimTime > 0 && d.RecoilUp > 0),
            "every gun is tighter aimed than from the hip, takes time to aim and kicks up");
        var m4 = ArmoryCatalog.Weapon("m4");
        var ak = ArmoryCatalog.Weapon("ak47");
        var awm = ArmoryCatalog.Weapon("awm");
        var pistol = ArmoryCatalog.Weapon("m1911");
        Check(awm.AimSpread / awm.BaseSpread < m4.AimSpread / m4.BaseSpread && m4.AimSpread / m4.BaseSpread < pistol.AimSpread / pistol.BaseSpread,
            "aiming matters most for a sniper, then a rifle, least for a pistol");
        Check(ak.RecoilSide > m4.RecoilSide && ak.RecoilUp > m4.RecoilUp, "the AK-47 kicks harder and wider than the M4 (weapons.csv overrides)");
        Check(ArmoryCatalog.Weapon("m1911").DualVariant is not { } dual || dual.AimSpread > pistol.AimSpread, "two pistols aim wider than one");

        // ---------- the cone ----------
        var state = new WeaponState(m4);
        state.Tick(.01f, false);
        Check(Mathf.IsEqualApprox(state.Spread, m4.BaseSpread), $"from the hip the cone is spread_deg ({m4.BaseSpread}°)");
        for (float t = 0; t < m4.AimTime * .5f; t += .01f) state.Tick(.01f, false, aiming: true);
        Check(state.AimProgress > .4f && state.AimProgress < .6f && state.Spread < m4.BaseSpread && state.Spread > m4.AimSpread,
            $"half way through aim_time_s the cone is half way ({state.Spread:0.00}°)");
        for (float t = 0; t < m4.AimTime; t += .01f) state.Tick(.01f, true, aiming: true);
        Check(Mathf.IsEqualApprox(state.Spread, m4.AimSpread), $"fully aimed it is aim_spread_deg ({m4.AimSpread}°), even while walking");
        state.Trigger(true, true);
        state.Tick(.001f, false, aiming: true);
        Check(Mathf.IsEqualApprox(state.Spread, m4.AimSpread + m4.SpreadPerShot * WeaponState.AimBloomShare, .02f) && state.Hot,
            "a shot widens the aimed cone by half its bloom, and the reticle runs hot");
        Check(Mathf.IsEqualApprox(state.Heat, 1), "right after a shot the gun is fully hot");
        for (float t = 0; t < WeaponState.HotSeconds + WeaponState.CoolSeconds * .5f; t += .01f) state.Tick(.01f, false, aiming: true);
        Check(state.Heat > .3f && state.Heat < .7f, $"once it stops the heat cools over {WeaponState.CoolSeconds} s, not at once ({state.Heat:0.00})");
        for (float t = 0; t < m4.AimTime * .5f + .02f; t += .01f) state.Tick(.01f, false);
        Check(state.AimProgress == 0, "letting go opens the cone twice as fast as it closed");
        for (float t = 0; t < 1; t += .01f) state.Tick(.01f, false);
        Check(!state.Hot && Mathf.IsEqualApprox(state.Spread, m4.BaseSpread), "the bloom recovers at spread_recovery_dps");
        for (float t = 0; t < m4.AimTime + .05f; t += .01f) state.Tick(.01f, false, aiming: true);
        state.Holster();
        Check(state.AimProgress == 0, "putting the gun away drops the aim");

        // ---------- recoil ----------
        var recoil = new WeaponRecoil();
        var rng = new RandomNumberGenerator { Seed = 7 };
        recoil.Kick(ak, rng);
        Check(Mathf.IsEqualApprox(recoil.Offset.Y, -ak.RecoilUp) && Mathf.Abs(recoil.Offset.X) <= ak.RecoilSide, "one shot kicks the aim point up by recoil_up, sideways within ±recoil_side");
        recoil.Tick(ak.FireInterval);
        Check(Mathf.IsEqualApprox(recoil.Offset.Y, -ak.RecoilUp), "while the gun keeps firing the kick holds");
        recoil.Tick(WeaponRecoil.QuietAfter + .1f);
        float back = ak.RecoilUp - -recoil.Offset.Y;
        Check(back > 0, $"once quiet it slides back at recoil_return ({back:0.0} px)");
        recoil.Tick(2);
        Check(recoil.Offset == Vector2.Zero, "and comes all the way home");
        var path = WeaponRecoil.Trace(ak, 30);
        Check(path.Count == 30 && path[^1].Y <= -Mathf.Min(WeaponRecoil.MaxOffset * .9f, ak.RecoilUp * 25) && path.All(p => p.Length() <= WeaponRecoil.MaxOffset + .01f),
            $"held for 30 shots it climbs ({path[^1].Y:0} px) but never past {WeaponRecoil.MaxOffset} px");
        Check(WeaponRecoil.Trace(ak, 30).SequenceEqual(path) && !WeaponRecoil.Trace(m4, 30).SequenceEqual(path), "the trace is the same every time for a gun, different between guns");

        // ---------- in a stage ----------
        _stage = new Stage { CharacterOverride = "goose", ThemeOverride = "zombie", DirectorEnabled = false };
        AddChild(_stage);
        await Frames(3);
        var player = _stage.Player;
        player.ManualControl = true;
        player.Invulnerable = true;
        player.Loadout.SetSlot(2, m4);
        player.Weapons.SelectSlot(2);
        await Frames(2);
        var aimAt = player.GlobalPosition + new Vector3(8, 1, 1);
        player.SetControls(Vector2.Zero, aimAt, false);
        await Frames(5);
        await Shot("hip");
        player.HoldAim(true);
        await Frames(Mathf.CeilToInt(m4.AimTime * Engine.PhysicsTicksPerSecond) + 4);
        Check(player.IsAiming && player.Gun.AimProgress >= 1 && Mathf.IsEqualApprox(player.Gun.Spread, m4.AimSpread), "holding aim in the game closes the M4's cone over its aim time");
        await Shot("aimed");
        for (int i = 0; i < 30; i++) { player.SetControls(Vector2.Zero, aimAt, true); await Frames(1); }
        Check(player.Gun.Hot && player.Recoil.Offset.Y < -m4.RecoilUp * 3, $"a burst runs the reticle hot and climbs the aim point ({player.Recoil.Offset.Y:0} px)");
        await Shot("burst");
        player.SetControls(Vector2.Zero, aimAt, false);
        player.HoldAim(false);
        await Frames(60);
        for (int i = 0; i < 20; i++) { player.SetControls(Vector2.Zero, aimAt, true); await Frames(1); }
        Check(player.Gun.Hot && player.Gun.Spread > player.Gun.AimedSpread + 1, "from the hip a burst opens the cone well past the aimed one");
        await Shot("hip-burst");
        player.SetControls(Vector2.Zero, aimAt, false);
        await Frames(60);
        Check(player.Recoil.Offset == Vector2.Zero && !player.Gun.Hot, "a second after the burst the aim point is back and the cone cool");
        // Hit marks: the local player's hit puts a double slash on the reticle; a handful at most.
        // Inside the safe room: the closed door would take the shots.
        var near = player.GlobalPosition + new Vector3(3.5f, 1, .5f);
        var dummy = _stage.SpawnCommon(near with { Y = 0 }, awake: false);
        dummy.ScaleHealth(50);
        await Frames(3);
        dummy.Stun(30);
        player.HoldAim(true);
        await Frames(Mathf.CeilToInt(m4.AimTime * Engine.PhysicsTicksPerSecond) + 4);
        int marksBefore = _stage.Hud.HitMarks;
        for (int i = 0; i < 4 && _stage.Hud.HitMarks == marksBefore; i++) { player.SetControls(Vector2.Zero, near, true); await Frames(2); }
        Check(_stage.Hud.HitMarks > marksBefore, "an aimed hit puts a hit mark on the reticle");
        for (int i = 0; i < 6; i++) { player.SetControls(Vector2.Zero, near, true); await Frames(2); }
        await Shot("hit");
        player.SetControls(Vector2.Zero, near, false);
        player.HoldAim(false);
        for (int i = 0; i < 8; i++) _stage.Hud.ShowHitMarker(false);
        ulong marked = Time.GetTicksMsec();
        Check(_stage.Hud.HitMarks == Hud.MaxHitMarks, $"at most {Hud.MaxHitMarks} hit marks show at once");
        // Hit marks run on real time (UI), not physics frames.
        for (int i = 0; i < 300 && Time.GetTicksMsec() - marked < (ulong)(Hud.HitMarkSeconds * 1000) + 50; i++) await Frames(1);
        Check(_stage.Hud.HitMarks == 0, $"each fades within {Hud.HitMarkSeconds} s");
        // One crit per focus (C-32): the shot after 0.5 s on a target crits, the ones right after do not, and focusing
        // again earns the next.
        if (IsInstanceValid(dummy)) dummy.QueueFree();
        var mark = _stage.SpawnCommon(near with { Y = 0 }, awake: false);
        mark.ScaleHealth(100);
        await Frames(3);
        mark.Stun(60);
        var crits = new System.Collections.Generic.List<bool>();
        void OnHit(Enemy e, HitInfo h) { if (e == mark && h.Shooter == player) crits.Add(h.Crit); }
        _stage.EnemyHit += OnHit;
        var onMark = mark.GlobalPosition + Vector3.Up;
        player.HoldAim(true);
        for (int i = 0; i < 60 && !(player.CritReady && player.FocusTarget == mark); i++) { player.SetControls(Vector2.Zero, onMark, false); await Frames(1); }
        Check(player.CritReady && player.FocusTarget == mark, "half a second on a monster readies a crit");
        for (int i = 0; i < 20; i++) { player.SetControls(Vector2.Zero, onMark, true); await Frames(1); }
        player.SetControls(Vector2.Zero, onMark, false);
        Check(crits.Count >= 2 && crits[0] && crits.Skip(1).All(c => !c), $"only the first shot of the burst crits ({string.Join(" ", crits.Select(c => c ? "C" : "."))})");
        Check(!player.CritReady, "firing spends the focus");
        crits.Clear();
        for (int i = 0; i < 60 && !player.CritReady; i++) { player.SetControls(Vector2.Zero, onMark, false); await Frames(1); }
        player.SetControls(Vector2.Zero, onMark, true);
        await Frames(2);
        player.SetControls(Vector2.Zero, onMark, false);
        Check(crits.Count > 0 && crits[0], "focusing again earns the next crit");
        _stage.EnemyHit -= OnHit;
        player.HoldAim(false);
        var pellets = ArmoryCatalog.Weapon("r870");
        Check(pellets.Pellets > 1 && pellets.AimSpread < pellets.BaseSpread, "the shotgun's pellet cone tightens with aiming (aim_spread_deg below spread_deg)");
    }
}
