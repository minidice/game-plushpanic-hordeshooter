using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class StudioPreview
{
    private const string RosterSource = CommonRoster.DataPath;

    private object RosterCatalog()
    {
        var catalog = CommonRoster.Current;
        var combinations = new List<object>();
        // Derive every offered combination from the game's validator, rather than maintaining web rules.
        foreach (string faction in new[] { "zombie", "hunter", "armed" })
        foreach (string species in catalog.Species.Keys)
        foreach (string weapon in catalog.Weapons.Keys)
        foreach (string palette in catalog.Palettes.Keys)
        {
            try { catalog.ValidateIdentity(new CommonIdentity(faction, species, palette, weapon)); }
            catch (ArgumentException) { continue; }
            combinations.Add(new { faction, species, weapon, palette });
        }
        return new {
        raw = ReadJson(RosterSource), errors = catalog.Errors.ToArray(), combinations,
        source = RosterSource, loadedAt = DateTimeOffset.Now.ToString("O"),
        runtime_sources = new[] { "res://scripts/Enemies/CommonRoster.cs", "res://scripts/Enemies/CommonAppearance.cs", "res://scripts/Enemies/Enemy.cs", "res://scripts/Enemies/RangedEnemy.cs" },
        note = "현재 로컬 공통 카탈로그 · 초기 조정 수치 · 모드/인원 배율 적용 전. 외형은 전투 수치를 바꾸지 않습니다."
        };
    }

    private static object RosterIdentity(CommonIdentity identity) => new {
        faction = identity.Faction, species = identity.Species, palette = identity.Palette,
        weapon = identity.Weapon, variant = identity.Variant, gear = (int)identity.Gear
    };

    private async Task<object> RenderRoster(JsonElement data)
    {
        var catalog = CommonRoster.Current;
        if (catalog.Errors.Count > 0) throw new ArgumentException("공통 카탈로그 오류: " + string.Join("; ", catalog.Errors));
        var identity = new CommonIdentity(data.GetProperty("faction").GetString()!, data.GetProperty("species").GetString()!,
            data.GetProperty("palette").GetString()!, data.GetProperty("weapon").GetString()!,
            data.GetProperty("variant").GetInt32(), (CommonGear)data.GetProperty("gear").GetInt32());
        string mode = data.GetProperty("mode").GetString()!;
        if (mode is not ("individual" or "pack")) throw new ArgumentException("알 수 없는 미리보기 종류입니다.");
        // Validate the requested identity before creating any nodes, including selections ignored by pack composition.
        catalog.Compose(identity);
        string packId = data.TryGetProperty("pack", out var packValue) ? packValue.GetString() ?? "" : "";
        var identities = new[] { identity };
        if (mode == "pack")
        {
            var weapons = identity.Faction == "armed" ? Enumerable.Repeat("mp5", 4).Concat(Enumerable.Repeat("ak47", 6)).ToArray() : new[] { identity.Weapon };
            identities = packId.Length > 0 ? catalog.CreatePack(packId, 10) : catalog.CreatePack(new CommonPack("studio_preview", identity.Faction, identity.Species, identity.Palette, weapons), 10);
        }
        var definitions = identities.Select(catalog.Compose).ToArray();
        var looks = identities.Select(catalog.LookFor).ToArray();
        var update = _viewport.RenderTargetUpdateMode;
        var transform = _camera.Transform; var projection = _camera.Projection;
        float cameraSize = _camera.Size, far = _camera.Far;
        bool platformVisible = _platform.Visible, rigVisible = _rig?.Visible ?? false, subjectVisible = _librarySubject?.Visible ?? false;
        var root = new Node3D { Name = "StudioCommonRoster", ProcessMode = ProcessModeEnum.Disabled };
        try
        {
            if (_rig != null) _rig.Visible = false;
            if (_librarySubject != null) _librarySubject.Visible = false;
            _platform.Visible = false;
            _viewport.AddChild(root);
            var members = new List<object>();
            for (int i = 0; i < identities.Length; i++)
            {
                var enemy = CreateMonsterSubject(root, definitions[i], looks[i]);
                if (mode == "pack") enemy.Position = new Vector3((i % 5 - 2) * 1.65f, 0, (i / 5) * 2.35f);
                var monster = enemy.Monster;
                members.Add(new {
                    identity = RosterIdentity(identities[i]), runtime_type = enemy.GetType().Name,
                    stats = new { hp = enemy.Health, move_speed = enemy.Speed, mass = monster.Mass,
                        attack_damage = monster.AttackDamage, attack_interval = monster.AttackInterval,
                        attack_reach = monster.AttackReach, attack_windup = monster.MeleeWindup, turn_rate = monster.TurnRate,
                        ranged_range = monster.Ranged?.Range ?? 0, projectile_speed = monster.Ranged?.ProjectileSpeed ?? 0,
                        wake_range = monster.WakeRange, behavior = monster.Behavior.ToString(),
                        collider_radius = enemy.Radius, collider_height = Mathf.Max(Mathf.Max(enemy.Definition.ColliderHeight, Enemy.MinHitHeight), enemy.Radius * 2.05f) },
                    model = new { definition = enemy.Definition.ResourcePath, look_id = monster.LookId },
                    visible_modules = Descendants(enemy.Rig).OfType<MeshInstance3D>().Where(n => n.IsVisibleInTree()).Select(n => n.Name.ToString()).ToArray(),
                    weapon_meshes = Descendants(enemy.Rig.Weapon).OfType<MeshInstance3D>().Count(n => n.IsVisibleInTree())
                });
            }
            root.RotationDegrees = new(0, Mathf.Clamp(data.GetProperty("rotation").GetSingle(), -180, 180), 0);
            var bounds = VisualBounds(root);
            FrameLibrary(bounds, data);
            if (mode == "pack") {
                var center = bounds.GetCenter();
                _camera.Position = center + new Vector3(0, .62f, 1).Normalized() * bounds.Size.Length() * 1.5f;
                _camera.LookAt(center);
            }
            return new { png = await CaptureLibrary(), mode, members,
                renderer = "Godot Forward+ · CommonRoster.Compose / LookFor → Enemy.Setup / _Ready",
                pack = packId, composition = string.Join(" + ", identities.GroupBy(i => i.Weapon).Select(g => g.Key.ToUpperInvariant() + " × " + g.Count())),
                note = "정지 외형 검수 · 실제 Enemy 초기화 · 전투/모드/인원 보정과 시간에 따른 행동은 실행하지 않습니다." };
        }
        finally
        {
            if (IsInstanceValid(root)) root.Free();
            _viewport.RenderTargetUpdateMode = update;
            _camera.Transform = transform; _camera.Projection = projection; _camera.Size = cameraSize; _camera.Far = far; _camera.MakeCurrent();
            _platform.Visible = platformVisible;
            if (_rig != null && IsInstanceValid(_rig)) _rig.Visible = rigVisible;
            if (_librarySubject != null && IsInstanceValid(_librarySubject)) _librarySubject.Visible = subjectVisible;
        }
    }
}
