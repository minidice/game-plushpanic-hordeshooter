using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class StudioPreview
{
    private sealed record LibraryProperty(string label, string value);
    private sealed record LibraryDetail(string title, JsonElement data);
    private sealed record LibraryVariant(string id, string name);
    private sealed record LibraryInteraction(string source, string profile, bool blocks_movement, bool blocks_projectiles);
    private sealed record LibraryEntry(string id, string name, string category, string source, string summary,
        List<LibraryProperty> properties, List<LibraryDetail> details, LibraryVariant[] variants, JsonElement? sourceData = null,
        string qualityMode = "single", LibraryInteraction? interaction = null);
    private List<LibraryEntry>? _monsterEntries, _objectEntries;
    private readonly Stage _lookStage = new(); // Never enters the scene tree; no game/session/save side effects.
    private static string Number(float n) => n.ToString("0.###", CultureInfo.InvariantCulture);
    private static float[] V(Vector3 v) => new[] { v.X, v.Y, v.Z };
    private static string Dimensions(Vector3 v) => $"{Number(v.X)} × {Number(v.Y)} × {Number(v.Z)} m";
    private static JsonElement ReadJson(string path)
    { using var doc = JsonDocument.Parse(Godot.FileAccess.GetFileAsString(path)); return doc.RootElement.Clone(); }
    private static string Text(JsonElement item, string key, string fallback = "") =>
        item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private object Library()
    {
        if (_monsterEntries == null) BuildMonsterEntries();
        if (_objectEntries == null) BuildObjectEntries();
        return new { monsters = _monsterEntries, objects = _objectEntries, loadedAt = DateTimeOffset.Now.ToString("O"),
            warnings = MonsterDatabase.Current.Errors.Concat(PropLibrary.Errors).ToArray() };
    }

    private void BuildMonsterEntries()
    {
        _monsterEntries = new();
        var rows = Csv.Parse(Godot.FileAccess.GetFileAsString(MonsterDatabase.TablePath)).rows;
        foreach (var m in MonsterDatabase.Current.Monsters.Values)
        {
            var look = MonsterLook(m, "goose");
            var props = new List<LibraryProperty> {
                new("ID", m.Id), new("등급", m.Tier switch { MonsterTier.Boss => "보스", MonsterTier.Named => "네임드", _ => "일반" }),
                new("행동 유형", m.Behavior.ToString()), new("기본 체력", Number(m.Hp)),
                new("이동 속도", Number(m.MoveSpeed) + " m/s"), new("질량 계수", Number(m.Mass)),
                new("기본 공격력", Number(m.AttackDamage)), new("공격 간격", Number(m.AttackInterval) + " s"),
                new("공격 도달 거리 (몸 사이)", Number(m.AttackReach) + " m"), new("각성 거리", Number(m.WakeRange) + " m"),
                new("단차 이동", m.Climb switch { MonsterClimb.Ladder => "사다리", MonsterClimb.Jump => "점프", MonsterClimb.Fly => "비행", _ => "벽 기어오르기" }),
                new("시체 정책", m.KeepsCorpse ? "네임드·보스 고정 시체" : "일반 물리 시체"),
                new("외형 정의 ID", m.LookId), new("모델 배율", Number(look.ModelScale)),
                new("정의 충돌 반경", Number(look.ColliderRadius) + " m"), new("정의 충돌 높이", Number(look.ColliderHeight) + " m"),
                new("실제 캡슐 높이", Number(Mathf.Max(Mathf.Max(look.ColliderHeight, Enemy.MinHitHeight), look.ColliderRadius * 2.05f)) + " m"),
            };
            var details = new List<LibraryDetail>();
            var row = rows.First(r => r.Cells.GetValueOrDefault("id") == m.Id);
            foreach (string trait in row.Cells.GetValueOrDefault("traits", "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                details.Add(new("특수 행동 · " + trait, ReadJson(MonsterDatabase.TraitDir + "/" + trait + ".json")));
            if (m.Behavior == MonsterBehavior.Boss)
                details.Add(new("보스 페이즈·스킬", ReadJson(BossDatabase.Dir + "/" + m.Id + ".json")));
            _monsterEntries.Add(new(m.Id, m.DisplayName, props[1].value, MonsterDatabase.TablePath,
                "모드·인원·등장 배율을 곱하기 전 기본 수치입니다." + (m.MirrorPlayer ? " 감염체 외형은 선택한 플레이어 종을 따릅니다. 충돌/배율 표는 거위 기준입니다." : ""),
                props, details, m.MirrorPlayer ? _characters.Select(d => new LibraryVariant(d.Id, Loc.Text(d.DisplayName))).ToArray() : Array.Empty<LibraryVariant>()));
        }
    }

    private CharacterDefinition MonsterLook(MonsterDefinition monster, string variant)
    {
        if (!monster.MirrorPlayer) return _lookStage.LookFor(monster);
        var species = _characters.SingleOrDefault(c => c.Id == variant) ?? throw new ArgumentException("알 수 없는 감염체 종입니다.");
        return CharacterDefinition.MakeZombie(species, CharacterCatalog.Enemy(monster.LookId));
    }

    private void BuildObjectEntries()
    {
        _objectEntries = new();
        foreach (var folder in PropLibrary.Folders)
        {
            var source = folder + "/manifest.json";
            foreach (var asset in ReadJson(source).GetProperty("assets").EnumerateArray())
            {
                var id = Text(asset, "id");
                var info = PropLibrary.Find(id);
                if (info == null) continue;
                var p = new List<LibraryProperty> { new("ID", id), new("분류", Text(asset, "category", "모델 소품")),
                    new("크기 · X / Y / Z", Dimensions(info.Dimensions)), new("추천 충돌 형상", info.Collision.Kind),
                    new("가림 대상 부위", info.OcclusionParts.Length == 0 ? "등록 없음" : string.Join(", ", info.OcclusionParts)),
                    new("모델 파일", info.File) };
                if (info.Collision.Size != Vector3.Zero) p.Add(new("추천 충돌 크기", Dimensions(info.Collision.Size)));
                if (info.Collision.Radius > 0) p.Add(new("추천 충돌 반경", Number(info.Collision.Radius) + " m"));
                if (info.Collision.Height > 0) p.Add(new("추천 충돌 높이", Number(info.Collision.Height) + " m"));
                if (asset.TryGetProperty("triangles", out var tris)) p.Add(new("삼각형 수 · manifest", tris.ToString()));
                if (asset.TryGetProperty("bounds_min_y", out var minY)) p.Add(new("최저 높이 · manifest", minY + " m"));
                _objectEntries.Add(new(id, Text(asset, "name", id), Text(asset, "category", "모델 소품"), source,
                    Text(asset, "note") + " 모델 단독 미리보기입니다. 차단 속성은 게임 공통 기본값이며, 장식 배치(solid:false)·별도 충돌·크기 변경은 맵 배치에 따릅니다.",
                    p, new() { new("원본 자산 속성", asset.Clone()) }, Array.Empty<LibraryVariant>(), asset.Clone()));
            }
        }
        foreach (var kind in Enum.GetValues<WorkshopArt.Kind>())
        {
            string name = kind switch { WorkshopArt.Kind.Workbench => "작업대", WorkshopArt.Kind.ToolChest => "공구 서랍장",
                WorkshopArt.Kind.PartsShelf => "부품 선반", WorkshopArt.Kind.Generator => "발전기", WorkshopArt.Kind.CableReel => "케이블 릴",
                WorkshopArt.Kind.PipeRack => "파이프 적재대", _ => "안전 게시판" };
            _objectEntries.Add(new("workshop:" + kind, name, "코드 생성 · 작업장", "res://scripts/World/WorkshopArt.cs",
                "현재 게임의 WorkshopArt 생성 함수를 그대로 실행합니다. 바닥 소품과 벽 장식의 충돌은 StageMap의 배치 함수가 결정합니다.",
                new() { new("종류", kind.ToString()), new("정의 크기 · X / Y / Z", Dimensions(WorkshopArt.Size(kind))), new("생성 방식", "절차적 메시 · 실제 게임 재질") },
                new(), Array.Empty<LibraryVariant>()));
        }
        AddRuntime("barrel", "폭발 드럼통", "ExplosiveBarrel.cs", "실제 ExplosiveBarrel.Create의 외형·충돌을 사용합니다. 폭발은 실행하지 않습니다.",
            new LibraryProperty("폭발 반경", Number(ExplosiveBarrel.Radius) + " m"));
        // These are factory examples, explicitly not a claim about every placement's size or collision.
        AddRuntime("cargo", "화물 상자 · 대표 규격", "StageArt.cs", "StageArt.Cargo의 2 × 1.5 × 1.4m 예시입니다. 실제 배치마다 크기·색이 다릅니다.");
        AddRuntime("carton", "골판지 상자 · 대표 규격", "StageArt.cs", "StageArt.Carton의 0.6m 예시입니다. 실제 배치마다 크기·색이 다릅니다.");
        AddRuntime("shelf", "창고 선반 · 대표 규격", "StageArt.cs", "StageArt.Shelf의 길이 3m 예시입니다. 실제 배치마다 길이와 상자 구성이 다릅니다.");
        AddRuntime("ammo", "탄약 보급 외형", "StageArt.cs", "StageArt.Ammo의 실제 외형입니다. 보급량과 사용 규칙은 배치 및 게임 모드가 결정합니다.");
        AddRuntime("fixture", "조명 기구 · 대표 색", "StageArt.cs", "StageArt.Fixture의 실제 메시입니다. 밝기·광원 범위는 맵 배치에 따라 달라집니다.");
        for (int i = 0; i < _objectEntries.Count; i++)
        {
            var entry = _objectEntries[i];
            var info = PropLibrary.Find(entry.id);
            var rule = info == null ? null : ObjectInteractions.Current.ForProp(info);
            entry.properties.Insert(2, new("이동 차단", rule == null ? "공통 데이터 미연결 · 맵 배치 코드에서 결정" : rule.BlocksMovement ? "차단" : "통과"));
            entry.properties.Insert(3, new("탄환 차단", rule == null ? "공통 데이터 미연결 · 맵 배치 코드에서 결정" : rule.BlocksProjectiles ? "차단" : "통과"));
            if (rule != null)
            {
                entry.properties.Add(new("차단 프로필", rule.Profile));
                entry.properties.Add(new("차단 데이터 출처", ObjectInteractions.DataPath));
                entry.properties.Add(new("차단 적용 범위", "StageMap.Prop 기본 배치 · 장식(solid:false) 제외. 별도 충돌은 맵 코드 확인"));
                entry.details.Add(new("게임 공통 차단 데이터", ReadJson(ObjectInteractions.DataPath)));
                _objectEntries[i] = entry with { interaction = new(ObjectInteractions.DataPath, rule.Profile, rule.BlocksMovement, rule.BlocksProjectiles) };
            }
            entry.properties.Add(new("파괴·폭발 규칙", entry.id == "runtime:barrel"
                ? "현재 코드: 피격·주변 폭발·황소 충돌로 점화. 공통 데이터 미연결"
                : "공통 데이터 미연결 · 미정(파괴 불가를 뜻하지 않음)"));
            entry.properties.Add(new("파괴에 필요한 공격·내구도", "공통 데이터 미연결 · 후속 정의"));
        }
        AddQualityEntries();
        _objectEntries = _objectEntries.OrderBy(e => e.category).ThenBy(e => e.name).ToList();
    }

    private void AddRuntime(string id, string name, string file, string summary, params LibraryProperty[] extra) =>
        _objectEntries!.Add(new("runtime:" + id, name, "코드 생성 · 기본 소품", "res://scripts/World/" + file, summary,
            new[] { new LibraryProperty("생성 방식", "실제 게임 생성 함수"), new LibraryProperty("게임 내 배치", "맵별 크기·수량·상호작용 별도") }.Concat(extra).ToList(),
            new(), Array.Empty<LibraryVariant>()));

    private void ClearLibrarySubject()
    {
        if (_librarySubject == null) return;
        _viewport.RemoveChild(_librarySubject); _librarySubject.QueueFree(); _librarySubject = null;
    }

    private async Task<object> RenderLibrary(JsonElement data)
    {
        Library();
        string kind = data.GetProperty("kind").GetString()!, id = data.GetProperty("id").GetString()!;
        var entries = kind == "monster" ? _monsterEntries! : kind == "object" ? _objectEntries! : throw new ArgumentException("알 수 없는 목록입니다.");
        var entry = entries.SingleOrDefault(e => e.id == id) ?? throw new ArgumentException("목록에 없는 항목입니다.");
        ClearLibrarySubject();
        if (_rig != null) { _viewport.RemoveChild(_rig); _rig.QueueFree(); _rig = null; }
        _platform.Visible = false;
        _librarySubject = new Node3D { ProcessMode = ProcessModeEnum.Disabled };
        _viewport.AddChild(_librarySubject);
        if (entry.qualityMode != "single") return await RenderQualityPair(entry, data);
        if (kind == "monster")
        {
            var m = MonsterDatabase.Get(id);
            var def = MonsterLook(m, data.TryGetProperty("variant", out var variant) ? variant.GetString() ?? "goose" : "goose");
            Enemy enemy = m.Behavior switch { MonsterBehavior.Shooter or MonsterBehavior.Spitter => new RangedEnemy(),
                MonsterBehavior.Balloon => new BalloonEnemy(), MonsterBehavior.Bull => new BullEnemy(), MonsterBehavior.Boss => new BossEnemy(),
                MonsterBehavior.Tether or MonsterBehavior.Leaper => new CaptureEnemy(), _ => new Enemy() };
            enemy.Setup(_lookStage, m, def, false);
            _librarySubject.AddChild(enemy); // Calls the same _Ready that adds weapons and attack posture in game.
            enemy.Rig.Rotation = Vector3.Zero;
            enemy.Rig.ShowAs(VisionSystem.Visibility.Visible);
        }
        else if (id.StartsWith("workshop:")) WorkshopArt.Create(_librarySubject, Enum.Parse<WorkshopArt.Kind>(id[9..]));
        else if (id.StartsWith("runtime:"))
        {
            switch (id[8..])
            {
                case "barrel":
                    var barrel = ExplosiveBarrel.Create(_lookStage, Vector3.Zero);
                    barrel.Freeze = true; barrel.Reparent(_librarySubject, false); _lookStage.Props.Remove(barrel); break;
                case "cargo": StageArt.Cargo(_librarySubject, new Vector3(2, 1.5f, 1.4f), "71816c"); break;
                case "carton": StageArt.Carton(_librarySubject, Vector3.Zero, Vector3.One * .6f); break;
                case "shelf": StageArt.Shelf(_librarySubject, 3, new[] { "af885b", "8a785b" }); break;
                case "ammo": StageArt.Ammo(_librarySubject); break;
                case "fixture": StageArt.Fixture(_librarySubject, "ffe2b8"); break;
            }
        }
        else _librarySubject.AddChild(PropLibrary.Instance(id) ?? throw new ArgumentException("모델을 가져오지 못했습니다."));
        _librarySubject.RotationDegrees = new Vector3(0, Mathf.Clamp(data.GetProperty("rotation").GetSingle(), -180, 180), 0);
        var bounds = VisualBounds(_librarySubject);
        FrameLibrary(bounds, data);
        return new { png = await CaptureLibrary(), kind, id, bounds = new { size = V(bounds.Size), center = V(bounds.GetCenter()) } };
    }

    private void FrameLibrary(Aabb bounds, JsonElement data)
    {
        var center = bounds.GetCenter();
        float radius = Mathf.Max(bounds.Size.Length() / 2, .1f);
        float zoom = Mathf.Clamp(data.GetProperty("zoom").GetSingle(), .75f, 1.45f);
        _camera.Projection = Camera3D.ProjectionType.Orthogonal;
        _camera.Size = radius * 2.3f / zoom;
        _camera.Far = Mathf.Max(100, radius * 8);
        _camera.Position = center + new Vector3(0, .35f, 1).Normalized() * radius * 3;
        _camera.LookAt(center, Vector3.Up);
    }

    private async Task<string> CaptureLibrary()
    {
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = _viewport.GetTexture().GetImage();
        return Convert.ToBase64String(image.SavePngToBuffer());
    }

    private static Aabb VisualBounds(Node root)
    {
        Aabb? total = null;
        foreach (var node in Descendants(root))
        {
            if (node is not VisualInstance3D visual || !visual.IsVisibleInTree() || node is not (MeshInstance3D or MultiMeshInstance3D)) continue;
            var box = visual.GlobalTransform * visual.GetAabb();
            total = total == null ? box : total.Value.Merge(box);
        }
        return total ?? throw new InvalidOperationException("보이는 메시가 없습니다.");
    }
}
