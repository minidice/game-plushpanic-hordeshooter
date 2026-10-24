using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HordeShooter.Tests;

/// <summary>
/// Studio's weapon library (decisions.md T-25, C-30): every weapon as the game resolves it (type defaults with the
/// weapon's own cells, dual variants too), numbers derived from it, and the recoil path from the game's own
/// <see cref="WeaponRecoil.Trace"/>. Read only; nothing here is shown in the game.
/// </summary>
public partial class StudioPreview
{
    private sealed record WeaponRow(string group, string label, string value);
    /// <summary>The recoil trace fires one magazine, at most this many shots (at least five).</summary>
    public const int TraceShots = 60;

    private object Weapons()
    {
        var entries = new List<object>();
        foreach (var def in ArmoryCatalog.Weapons.Values.OrderBy(d => d.Slot).ThenBy(d => d.Category).ThenBy(d => d.Id))
        {
            entries.Add(WeaponEntry(def));
            if (def.DualVariant is { } dual) entries.Add(WeaponEntry(dual));
        }
        return new { weapons = entries, loadedAt = DateTimeOffset.Now.ToString("O"), traceShots = TraceShots,
            warnings = WeaponDatabase.Current.Errors.ToArray() };
    }

    private static string CategoryName(WeaponCategory c) => c switch
    {
        WeaponCategory.Melee => "근접", WeaponCategory.Pistol => "권총", WeaponCategory.Smg => "기관단총", WeaponCategory.Rifle => "돌격소총",
        WeaponCategory.Lmg => "경기관총", WeaponCategory.Shotgun => "산탄총", WeaponCategory.Sniper => "저격", _ => "특수무기",
    };

    /// <summary>Width of a cone of <paramref name="degrees"/> at <paramref name="meters"/>.</summary>
    private static float Width(float degrees, float meters) => 2 * meters * Mathf.Tan(Mathf.DegToRad(degrees * .5f));

    private object WeaponEntry(WeaponDefinition d)
    {
        bool melee = d.Mode == FireMode.Melee;
        float rps = 1 / Mathf.Max(.001f, d.FireInterval) * Mathf.Max(1, d.BurstCount);
        float dump = d.MagSize > 0 ? d.MagSize / rps : 0;
        float sustained = d.MagSize > 0 ? d.Damage * d.MagSize / (dump + d.ReloadTime) : d.Damage * rps;
        var rows = new List<WeaponRow>
        {
            new("기본", "ID", d.Id), new("기본", "분류 ID", d.TypeId), new("기본", "칸", $"{(int)d.Slot}번"), new("기본", "발사", d.Mode.ToString()),
            new("기본", "연사", d.Automatic ? "예" : "아니오"), new("기본", "탄 종류", d.Caliber == "" ? "—" : d.Caliber),
            new("화력", "대미지(한 발 · 산탄은 합계)", Number(d.Damage)), new("화력", "발사 간격", Number(d.FireInterval * 1000) + " ms"),
            new("화력", "초당 발사 / 분당", $"{Number(rps)} / {Mathf.RoundToInt(rps * 60)}"),
            new("화력", "초당 대미지(DPS)", Number(d.Damage * rps)),
            new("화력", "장전 포함 초당 대미지", Number(sustained)),
            new("화력", "탄창 · 비우는 시간", d.MagSize > 0 ? $"{d.MagSize}발 · {Number(dump)} s" : "—"),
            new("화력", "재장전", Number(d.ReloadTime) + " s" + (d.ShellReloadTime > 0 ? $" (한 발 {Number(d.ShellReloadTime)} s)" : "")),
            new("화력", "관통 · 치명 배율", $"{d.Pierce} · ×{Number(d.CritMultiplier)}"),
            new("사거리", "유효 / 최대", $"{Number(d.EffectiveRange)} / {Number(d.MaxRange)} m"),
            new("사거리", "조준 시 유효 / 최대", $"{Number(d.EffectiveRange * d.AimRangeMultiplier)} / {Number(d.MaxRange * d.AimRangeMultiplier)} m"),
            new("사거리", "최대에서 대미지", Number(d.FalloffMin * 100) + "%"),
            new("조준", "비조준 퍼짐 (spread_deg)", Number(d.BaseSpread) + "°"),
            new("조준", "조준 퍼짐 (aim_spread_deg)", Number(d.AimSpread) + "°"),
            new("조준", "조준 시간 (aim_time_s)", Number(d.AimTime) + " s"),
            new("조준", "이동 시 + · 발당 + · 최대", $"{Number(d.MoveSpread)}° · {Number(d.SpreadPerShot)}° · {Number(d.MaxSpread)}°"),
            new("조준", "퍼짐 회복 (spread_recovery_dps)", Number(d.SpreadRecovery) + "°/s"),
            new("조준", "10 m에서 퍼짐 폭 (비조준 / 조준)", $"{Number(Width(d.BaseSpread, 10))} / {Number(Width(d.AimSpread, 10))} m"),
            new("조준", "20 m에서 퍼짐 폭 (비조준 / 조준)", $"{Number(Width(d.BaseSpread, 20))} / {Number(Width(d.AimSpread, 20))} m"),
            new("반동", "수직 (recoil_up)", Number(d.RecoilUp) + " px/발"),
            new("반동", "수평 ± (recoil_side)", Number(d.RecoilSide) + " px/발"),
            new("반동", "복귀 (recoil_return)", Number(d.RecoilReturn) + " px/s"),
            new("반동", "몸 밀기 (recoil_power)", Number(d.RecoilPower)),
            new("움직임", "이동 속도 · 조준 중", $"{Number(d.MoveSpeedScale * 100)}% · {Number(d.AimMoveScale * 100)}%"),
            new("움직임", "조준 시 화면 (aim_view)", Number(d.AimView)),
            new("움직임", "소음 반경", Number(d.Noise) + " m"),
        };
        if (d.Pellets > 1) rows.Add(new("화력", "산탄 수 · 원뿔", $"{d.Pellets} · {Number(d.PelletSpread)}°"));
        if (d.BurstCount > 1) rows.Add(new("화력", "점사", $"{d.BurstCount}발 · {Number(d.BurstInterval * 1000)} ms"));
        var trace = melee ? new List<Vector2>() : WeaponRecoil.Trace(d, Math.Clamp(d.MagSize, 5, TraceShots));
        return new
        {
            id = d.Id, name = d.DisplayName, category = CategoryName(d.Category), type = d.TypeId, dual = d.Dual, melee,
            summary = $"{CategoryName(d.Category)} · {(int)d.Slot}번 칸 · {(d.Automatic ? "연사" : "단발")}",
            source = "game/content/data/weapons.csv (빈칸은 weapon_types.csv의 분류 기본값)" + (d.Dual ? " · weapon_traits/dual_wield" : ""),
            rows,
            cone = new { hip = d.BaseSpread, aim = d.AimSpread, move = d.MoveSpread, max = d.MaxSpread, perShot = d.SpreadPerShot,
                effective = d.EffectiveRange, range = d.MaxRange, aimRange = d.AimRangeMultiplier },
            recoil = new { up = d.RecoilUp, side = d.RecoilSide, back = d.RecoilReturn, max = WeaponRecoil.MaxOffset,
                trace = trace.Select(p => new[] { MathF.Round(p.X, 1), MathF.Round(p.Y, 1) }).ToArray() },
        };
    }
}
