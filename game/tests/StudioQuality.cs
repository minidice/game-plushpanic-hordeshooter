using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

public partial class StudioPreview
{
    private void AddQualityEntries()
    {
        // Discover existing paired files, never invent a low-quality version of a prop.
        foreach (var file in DirAccess.GetFilesAt("res://assets/pets").Where(f => f.EndsWith("_low.glb")).Order())
        {
            string id = file[..^8];
            AddQualityEntry("pet:" + id, Loc.Text("companion_pet_" + id), "소환수 · 펫", "res://scripts/Pets/PetModel.cs",
                "별도 GLB", "펫 본체의 실제 고품질·감량 모델입니다. 같은 카메라·조명·자세로 비교합니다.",
                new() { new("고품질 모델", $"res://assets/pets/{id}.glb"), new("저품질 모델", $"res://assets/pets/{id}_low.glb") });
        }
        foreach (var id in DroneModel.BodyIds)
            AddQualityEntry("drone:" + id, Loc.Text("companion_drone_" + id), "소환수 · 드론", "res://scripts/Companions/DroneModel.cs",
                "별도 GLB", "드론 본체의 실제 고품질·감량 모델입니다. 장비를 붙이지 않은 기본형입니다.",
                new() { new("고품질 모델", $"res://assets/drones/{id}.glb"), new("저품질 모델", $"res://assets/drones/{id}_low.glb") });
        foreach (var preset in ReadJson(SidekickModel.PresetsPath).EnumerateArray())
            AddQualityEntry("sidekick:" + Text(preset, "id"), Loc.Text(Text(preset, "name")), "소환수 · 부사수", SidekickModel.PresetsPath,
                "자동 LOD", "같은 원본 모델의 LOD 설정 비교입니다. 고품질 LodBias=4, 저품질=0.5. 현재 거리에서는 외형 차이가 작거나 없을 수 있습니다. 별도 감량 GLB가 아닙니다.",
                new() { new("종", Text(preset, "species")), new("무기", Text(preset, "weapon")), new("크기 배율", preset.GetProperty("scale").ToString()) }, preset.Clone());
    }

    private void AddQualityEntry(string id, string name, string category, string source, string mode,
        string summary, List<LibraryProperty> properties, JsonElement? raw = null)
    {
        properties.Insert(0, new("품질 방식", mode));
        properties.Add(new("구현 범위", "시각 모델 · AI/전투/파괴 데이터 없음"));
        _objectEntries!.Add(new(id, name, category, source, summary, properties,
            raw.HasValue ? new() { new("실제 프리셋", raw.Value) } : new(), Array.Empty<LibraryVariant>(), raw, mode));
    }

    private static Node3D QualityModel(string id, PetModelQuality quality)
    {
        string value = id[(id.IndexOf(':') + 1)..];
        if (id.StartsWith("pet:")) return PetModel.Create(value, quality);
        if (id.StartsWith("drone:")) return DroneModel.Create(value, quality);
        if (id.StartsWith("sidekick:")) return SidekickModel.Create(value, quality);
        throw new ArgumentException("품질 비교를 지원하지 않는 항목입니다.");
    }

    private static int BaseTriangles(Node root)
    {
        int count = 0;
        foreach (var node in Descendants(root).OfType<MeshInstance3D>())
        {
            if (node.Mesh is not { } mesh || !node.IsVisibleInTree()) continue;
            for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                var arrays = mesh.SurfaceGetArrays(surface);
                int indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array().Length;
                count += (indices > 0 ? indices : arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array().Length) / 3;
            }
        }
        return count; // Imported base meshes; not actual automatic-LOD draw primitives or an FPS claim.
    }

    private async Task<object> RenderQualityPair(LibraryEntry entry, JsonElement data)
    {
        var high = QualityModel(entry.id, PetModelQuality.High);
        _librarySubject!.AddChild(high);
        var low = QualityModel(entry.id, PetModelQuality.Low);
        _librarySubject.AddChild(low);
        _librarySubject.RotationDegrees = new Vector3(0, Mathf.Clamp(data.GetProperty("rotation").GetSingle(), -180, 180), 0);
        // Fit once to the union, then keep framing identical for both captures.
        var bounds = VisualBounds(_librarySubject);
        int highTriangles = BaseTriangles(high), lowTriangles = BaseTriangles(low);
        FrameLibrary(bounds, data);
        low.Visible = false;
        string highPng = await CaptureLibrary();
        high.Visible = false; low.Visible = true;
        string lowPng = await CaptureLibrary();
        return new { png = highPng, kind = "object", id = entry.id,
            bounds = new { size = V(bounds.Size), center = V(bounds.GetCenter()) },
            comparison = new[] {
                new { quality = "high", label = "고품질", png = highPng, baseTriangles = highTriangles },
                new { quality = "low", label = "저품질", png = lowPng, baseTriangles = lowTriangles },
            } };
    }
}
