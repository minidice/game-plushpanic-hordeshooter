using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class StudioPreview
{
    private sealed record EffectEntry(string id, string name, string category, string summary, string source,
        LibraryProperty[] properties, double duration);
    private sealed record EffectFrame(double time, string png);

    private IEnumerable<object> EffectEntries() => EffectDefinitions().Cast<object>();

    private static IEnumerable<EffectEntry> EffectDefinitions()
    {
        foreach (string weaponId in new[] { "m4", "r870", "awm", "railgun" })
        {
            var weapon = ArmoryCatalog.Weapon(weaponId);
            yield return new("shot:" + weaponId, weapon.DisplayName + " · 발사", "발사",
                "실제 무기 모델과 WeaponShotEffect의 총구 섬광·탄 궤적입니다. 고정된 6m 표본이며 피해·탄약·반동·효과음은 실행하지 않습니다.",
                "res://scripts/Effects/WeaponShotEffect.cs", new[] {
                    new LibraryProperty("무기 데이터", "res://content/data/weapons.csv"),
                    new LibraryProperty("무기 / 분류", weapon.Id + " / " + weapon.Category),
                    new LibraryProperty("발사 방식", weapon.Mode.ToString()),
                    new LibraryProperty("발사 간격", Number(weapon.FireInterval) + " s"),
                    new LibraryProperty("탄 수 / 산탄 퍼짐", weapon.Pellets + " / " + Number(weapon.PelletSpread) + "°"),
                    new LibraryProperty("충전 시간", Number(weapon.ChargeTime) + " s (충전 동작 제외, 발사 순간만)"),
                    new LibraryProperty("셰이더", "res://shaders/weapon_shot.gdshader"),
                }, .3);
        }
        foreach (var (id, name, death, critical) in new[] {
            ("hit", "일반 피격", false, false), ("critical", "치명타 피격", false, true),
            ("death", "처치 섬광", true, false), ("death_critical", "치명타 처치 섬광", true, true) })
            yield return new(id, name, "피격·처치", "실제 거위 리그의 몸 점멸과 DamageBurstEffect입니다. 처치 표본은 시체 물리 없이 섬광만 보여 줍니다.",
                "res://scripts/Effects/DamageBurstEffect.cs", new[] {
                    new LibraryProperty("선·원 지속 시간", death ? "0.32 s" : "0.16 s"),
                    new LibraryProperty("몸 점멸", "0.1 s / 최고 불투명도 0.72"),
                    new LibraryProperty("색", critical ? "#" + DamageBurstEffect.CriticalColor.ToHtml(false) : "#ffffff"),
                    new LibraryProperty("몸 점멸 코드", "res://scripts/Effects/CharacterHitFlashEffect.cs"),
                    new LibraryProperty("셰이더", "res://shaders/damage_burst.gdshader"),
                }, death ? .5 : .3);
        var melee = ArmoryCatalog.Weapon("frying_pan");
        yield return new("melee", "프라이팬 · 휘두르기 궤적", "근접", "실제 MeleeSwingEffect의 속이 빈 초승달 궤적입니다. 공격 판정과 캐릭터 동작은 제외합니다.",
            "res://scripts/Effects/MeleeSwingEffect.cs", new[] {
                new LibraryProperty("무기", melee.Id), new LibraryProperty("범위", Number(melee.MaxRange) + " m"),
                new LibraryProperty("호 각도", Number(melee.MeleeArc) + "°"), new LibraryProperty("지속 시간", "0.16 s"),
                new LibraryProperty("셰이더", "res://shaders/melee_sweep.gdshader"),
            }, .3);
        yield return new("explosion", "폭발 드럼통 · 폭발 효과", "환경", "드럼통 데이터의 반경을 사용하는 실제 Effects.Explosion입니다. 섬광·광원·파편만 재생하며 카메라 흔들림과 피해는 제외합니다.",
            "res://scripts/World/Effects.cs", new[] {
                new LibraryProperty("반경 데이터", Number(ExplosiveBarrel.Radius) + " m"),
                new LibraryProperty("발생 코드", "res://scripts/World/ExplosiveBarrel.cs"),
                new LibraryProperty("설정 출처", ObjectInteractions.DataPath),
                new LibraryProperty("섬광 / 광원", "0.35 s / 0.4 s"),
                new LibraryProperty("파편", "40개 / 수명 0.9 s / 정리 1.4 s"),
            }, 1.4);
        yield return new("puff", "동료 부활 · 작은 팝", "환경", "Companion.Revive에서 사용하는 반경 1m·크림색 표본입니다. 실제 Effects.Puff를 호출합니다.",
            "res://scripts/World/Effects.cs", new[] {
                new LibraryProperty("발생 코드", "res://scripts/Companions/Companion.cs"),
                new LibraryProperty("반경 / 색", "1 m / #fff3c4"),
                new LibraryProperty("섬광 / 파편", "0.28 s / 14개, 수명 0.9 s"),
            }, 1.4);
        yield return new("sonar", "소나 탐지 링", "환경", "실제 Effects.SonarPing의 바닥 링 확대·페이드입니다.",
            "res://scripts/World/Effects.cs", new[] {
                new LibraryProperty("발생 코드", "res://scripts/Player/VisionSystem.cs"),
                new LibraryProperty("지속 시간", "0.8 s"), new LibraryProperty("배율", "0.3 → 1.5"),
            }, 1);
        yield return new("wall", "콘크리트 벽 · 붕괴", "파괴", "실제 BreakableWallArt.PlayBreak의 금 간 패널·낙하 파편·먼지·잔해입니다. 충돌과 폭발 피해는 생성하지 않습니다.",
            "res://scripts/World/BreakableWallArt.cs", new[] {
                new LibraryProperty("벽 크기", Dimensions(new Vector3(BreakableWallArt.Width, BreakableWallArt.Height, BreakableWallArt.Thickness))),
                new LibraryProperty("열리는 통로", Number(BreakableWallArt.OpeningWidth) + " m"),
                new LibraryProperty("붕괴 시간", "0.8 s"), new LibraryProperty("실제 발생 코드", "res://scripts/World/ObjectBody.cs"),
            }, 1.2);
    }

    private sealed class EffectStageMap : StageMap
    {
        public override Rect2 Area => new(-8, -8, 16, 16);
        protected override void BuildMap() { PlayerStart = new(0, 0, -5); }
    }

    private Effects EffectContext(Node3D root, bool explosion)
    {
        if (!explosion)
        {
            var effects = new Effects(); root.AddChild(effects);
            effects.SetProcess(false); effects.SetPhysicsProcess(false);
            return effects;
        }
        // Explosion uses Stage.Cam. Keep an actual minimal Stage, but never run its gameplay loop.
        // Stage initialization reads settings; it does not save settings. Mute only this bridge's
        // master bus while initialization creates/stops its ambience stream, then restore the bus.
        bool muted = AudioServer.IsBusMute(0);
        var stage = new Stage { Name = "EffectContext", MapOverride = new EffectStageMap(),
            ModeOverride = GameMode.Scenario, CharacterOverride = "goose", ThemeOverride = "zombie",
            DirectorEnabled = false, SquadOverride = 0, ReviveOverride = false, ArrivalOverride = false,
            NextStageOverride = false, ProcessMode = ProcessModeEnum.Disabled };
        try
        {
            AudioServer.SetBusMute(0, true);
            root.AddChild(stage);
            foreach (var child in Descendants(stage).ToArray())
            {
                if (child is AudioStreamPlayer audio) audio.Stop();
                if (child is AudioStreamPlayer3D audio3D) audio3D.Stop();
                if (child is CanvasLayer layer) layer.Visible = false;
            }
            foreach (var child in stage.GetChildren().ToArray())
            {
                if (child is WorldEnvironment environment) environment.Free();
                else if (child is Node3D spatial && child != stage.Effects) spatial.Visible = false;
            }
            stage.Cam.Current = false; _camera.MakeCurrent();
            stage.Effects.ProcessMode = ProcessModeEnum.Always;
            stage.Effects.SetProcess(false); stage.Effects.SetPhysicsProcess(false);
            return stage.Effects;
        }
        finally { AudioServer.SetBusMute(0, muted); }
    }

    private void CreateEffect(string id, Node3D root, Effects effects)
    {
        if (id.StartsWith("shot:", StringComparison.Ordinal))
        {
            var weapon = ArmoryCatalog.Weapon(id[5..]);
            var model = WeaponVisual.Build(weapon, null);
            root.AddChild(model.Root); model.Root.Position = new(-3.2f, 1, 0); model.Root.RotationDegrees = new(0, 90, 0);
            var from = model.Muzzles[0].GlobalPosition;
            var random = new RandomNumberGenerator { Seed = 173 };
            for (int i = 0; i < Math.Max(1, weapon.Pellets); i++)
            {
                float yaw = weapon.Pellets > 1 ? random.RandfRange(-weapon.PelletSpread / 2, weapon.PelletSpread / 2) : 0;
                var end = from + Vector3.Right.Rotated(Vector3.Up, Mathf.DegToRad(yaw)) * 6;
                WeaponShotEffect.Play(effects, weapon, from, from, end, false, muzzleFlash: i == 0);
            }
            return;
        }
        if (id is "hit" or "critical" or "death" or "death_critical")
        {
            var rig = CharacterRig.Create(CharacterCatalog.Player("goose")); root.AddChild(rig);
            rig.HideBuiltInWeapon(); rig.RotationDegrees = new(0, -20, 0);
            bool critical = id.Contains("critical", StringComparison.Ordinal);
            if (id.StartsWith("death", StringComparison.Ordinal)) effects.DeathBurst(rig, critical);
            else rig.FlashHit(critical);
            return;
        }
        switch (id)
        {
            case "melee":
                var melee = ArmoryCatalog.Weapon("frying_pan");
                effects.Swoosh(new(0, .5f, -.7f), Vector3.Back, melee.MaxRange, melee.MeleeArc); break;
            case "explosion": effects.Explosion(Vector3.Zero, ExplosiveBarrel.Radius, new Color("ff9a3c")); break;
            case "puff": effects.Puff(Vector3.Up * .6f, 1, new Color("fff3c4")); break;
            case "sonar": effects.SonarPing(Vector3.Zero); break;
            case "wall": BreakableWallArt.Create(root).PlayBreak(); break;
            default: throw new ArgumentException("알 수 없는 이펙트입니다.");
        }
    }

    private async Task<object> RenderEffect(JsonElement data)
    {
        string id = data.GetProperty("id").GetString() ?? "";
        var entry = EffectDefinitions().SingleOrDefault(e => e.id == id) ?? throw new ArgumentException("알 수 없는 이펙트입니다.");
        const int pixels = 640;
        int fps = entry.duration <= .5 ? 60 : 30;
        int steps = Math.Min(47, (int)Math.Round(entry.duration * fps));
        double duration = steps / (double)fps, dt = 1d / fps;
        var size = _viewport.Size; var update = _viewport.RenderTargetUpdateMode;
        var transform = _camera.Transform; var projection = _camera.Projection;
        float cameraSize = _camera.Size, fov = _camera.Fov;
        bool platformVisible = _platform.Visible, rigVisible = _rig?.Visible ?? false, subjectVisible = _librarySubject?.Visible ?? false;
        var root = new Node3D { Name = "StudioEffectSample" };
        var tweens = new List<Tween>();
        var previousTweens = GetTree().GetProcessedTweens().Select(t => t.GetInstanceId()).ToHashSet();
        try
        {
            if (_rig != null) _rig.Visible = false;
            if (_librarySubject != null) _librarySubject.Visible = false;
            _platform.Visible = false;
            _viewport.Size = new(pixels, pixels); _viewport.AddChild(root);
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            float framing = id.StartsWith("shot:", StringComparison.Ordinal) ? 9 : id == "explosion" ? 10 : id == "wall" ? 6.5f : 4.6f;
            Vector3 focus = id == "wall" ? Vector3.Up : Vector3.Up * .65f;
            _camera.Projection = Camera3D.ProjectionType.Orthogonal; _camera.Size = framing;
            _camera.Position = focus + new Vector3(3, 5, 9); _camera.LookAt(focus);
            Mat.Mesh(root, new PlaneMesh { Size = Vector2.One * framing * 1.5f }, new(0, -.04f, 0), Mat.Solid("303c39"));
            var effects = EffectContext(root, id == "explosion");
            CreateEffect(id, root, effects);
            // The actual scripts are stepped manually. Never let slow readbacks or shader uploads
            // advance a 35 ms muzzle flash or consume an entire .16 s hit before its first image.
            var nodes = Descendants(root).ToArray();
            foreach (var node in nodes) { node.SetProcess(false); node.SetPhysicsProcess(false); }
            var animated = nodes.Where(n => n is WeaponShotEffect or DamageBurstEffect or MeleeSwingEffect
                or CharacterHitFlashEffect or BreakableWallVisual).ToArray();
            var particles = nodes.OfType<CpuParticles3D>().ToArray();
            foreach (var particle in particles)
            {
                particle.SpeedScale = 0; particle.UseFixedSeed = true; particle.Seed = 173; particle.Restart(true);
            }
            tweens.AddRange(GetTree().GetProcessedTweens().Where(t => !previousTweens.Contains(t.GetInstanceId())));
            foreach (var tween in tweens) tween.Pause();
            for (int warmup = 0; warmup < 4; warmup++) await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var frames = new List<EffectFrame>(steps + 1);
            for (int frame = 0; frame <= steps; frame++)
            {
                if (frame > 0)
                {
                    foreach (var node in animated)
                        if (IsInstanceValid(node) && !node.IsQueuedForDeletion()) node._Process(dt);
                    foreach (var tween in tweens) if (tween.IsValid()) tween.CustomStep(dt);
                    // Godot 4.7.2 local API: SpeedScale=0 + RequestParticlesProcess is supported seeking.
                    foreach (var particle in particles)
                        if (IsInstanceValid(particle) && !particle.IsQueuedForDeletion()) particle.RequestParticlesProcess((float)dt, 0);
                }
                // Let native particle processing and deferred frees finish before the readback.
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = _viewport.GetTexture().GetImage();
                frames.Add(new(frame / (double)fps, Convert.ToBase64String(image.SavePngToBuffer())));
            }
            return new { id, width = pixels, height = pixels, fps, duration, frames,
                timing = "fixed-step game simulation", renderer = "Godot Forward+", audio = false };
        }
        finally
        {
            foreach (var tween in tweens) if (tween.IsValid()) tween.Kill();
            if (IsInstanceValid(root)) root.Free();
            _viewport.Size = size; _viewport.RenderTargetUpdateMode = update;
            _camera.Transform = transform; _camera.Projection = projection; _camera.Size = cameraSize; _camera.Fov = fov;
            _camera.MakeCurrent(); _platform.Visible = platformVisible;
            if (_rig != null && IsInstanceValid(_rig)) _rig.Visible = rigVisible;
            if (_librarySubject != null && IsInstanceValid(_librarySubject)) _librarySubject.Visible = subjectVisible;
        }
    }
}
