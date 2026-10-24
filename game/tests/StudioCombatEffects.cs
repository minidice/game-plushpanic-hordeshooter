using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HordeShooter.Tests;

public partial class StudioPreview
{
    private static IEnumerable<EffectEntry> CombatEffectEntries()
    {
        using var source = JsonDocument.Parse(FileAccess.GetFileAsString(CombatEffectCatalog.DataPath));
        foreach (var raw in source.RootElement.GetProperty("effects").EnumerateArray())
        {
            var def = CombatEffectCatalog.Current.All[raw.GetProperty("id").GetString()!];
            double duration = (def.Kind == "burst" ? def.Duration : def.PreviewLifetime) + .15;
            yield return new("fx:" + def.Id, def.Name, def.Family == "blast" ? "폭발" : def.Family == "fire" ? "화염" : "독",
                "공통 효과 정의와 실제 게임 외형 생성기를 재생합니다. 피해·충돌·음향·화면 흔들림은 실행하지 않습니다. 표본 반경/시간은 미리보기용이며 실제 전투 값은 발생원이 전달합니다.",
                CombatEffectCatalog.DataPath, CombatProperties(def), duration, raw.Clone());
        }
    }

    private static LibraryProperty[] CombatProperties(CombatEffectDefinition def)
    {
        var properties = new List<LibraryProperty> {
                    new LibraryProperty("효과 ID / 계열", def.Id + " / " + def.Family),
                    new LibraryProperty("생성 방식", def.Kind),
                    new LibraryProperty("발생원 키", string.Join(", ", def.Sources)),
                    new LibraryProperty("사용처", string.Join(", ", def.UsedBy)),
                    new LibraryProperty("기본색 / 강조색", "#" + def.Color.ToHtml(false) + " / #" + def.Accent.ToHtml(false)),
                    new LibraryProperty("파편 / 작은 입자 수", def.Particles.ToString()),
                    new LibraryProperty("미리보기 표본 반경", Number(def.PreviewRadius) + " m (피해 수치 아님)"),
        };
        if (def.Kind != "projectile")
        {
            properties.Add(new("덩어리 수", def.Lobes.ToString()));
            properties.Add(new("퍼짐 / 상승", Number(def.Spread) + " / " + Number(def.Rise)));
        }
        if (def.Kind == "burst")
        {
            properties.Add(new("연기색", "#" + def.SmokeColor.ToHtml(false)));
            properties.Add(new("폭발 재생 시간", Number(def.Duration) + " s"));
            properties.Add(new("폭발 광원 에너지", Number(def.LightEnergy) + " / 동시 최대 " + CombatBurstEffect.MaxLights));
        }
        else properties.Add(new("미리보기 표본 수명", Number(def.PreviewLifetime) + " s (실제 위험 지속시간 아님)"));
        return properties.ToArray();
    }

    private static float CombatFraming(string id)
    {
        var def = CombatEffectCatalog.Current.All[id];
        return def.Kind == "projectile" ? 4.6f : Math.Max(4.6f, def.PreviewRadius * 2.5f);
    }
    private static void CreateCombatEffect(string id, Node3D root)
    {
        var def = CombatEffectCatalog.Current.All[id];
        switch (def.Kind)
        {
            case "burst": CombatBurstEffect.Play(root, def, Vector3.Zero, def.PreviewRadius, id == "rocket" ? Vector3.Right : Vector3.Zero); break;
            case "surface": CombatSurfaceEffect.Create(root, def, def.PreviewRadius, def.PreviewLifetime); break;
            case "projectile":
                var sample = new StudioProjectileSample { Lifetime = def.PreviewLifetime };
                root.AddChild(sample);
                sample.Visual = CombatProjectileEffect.Create(sample, def, def.PreviewRadius);
                sample._Process(0);
                break;
        }
    }
}

/// <summary>Preview-only motion; the attached visual is the actual runtime projectile renderer.</summary>
public partial class StudioProjectileSample : Node3D
{
    public float Lifetime;
    public CombatProjectileEffect Visual = null!;
    private float _age;
    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Lifetime) { QueueFree(); return; }
        float t = _age / Lifetime;
        Position = new(-1.4f + 2.8f * t, .65f + .5f * Mathf.Sin(t * Mathf.Pi), 0);
        Visual.Face(new(2.8f, .5f * Mathf.Pi * Mathf.Cos(t * Mathf.Pi), 0));
    }
}
