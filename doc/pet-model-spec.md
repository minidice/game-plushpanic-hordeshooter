# 사족 펫 모델과 공용 등 무기

2026-10-22, A-20. 모드에 사용할 거북이·강아지·토끼의 **모델 라이브러리**다. 기본형은 무기가 없고, 같은 등 무기를 종 간에 교체한다. 참고 영상 `동물참고2.mp4`, `따라다니는개.mp4`에서 읽히는 큰 머리·짧은 다리·작은 등 장비의 비율을 반영했다. 강아지의 짙은 등/황갈색 얼굴·다리 배색, 거북이·토끼 조형과 무기 종류는 이번 제작안이며 사용자 최종 아트 확정은 아니다.

소환 카드, 추종 AI, 공격·피해, 모드 배치, 세이브·네트워크는 연결하지 않았다. [M-27 계획](onslaught-cards-plan.md)의 소환수 전체를 완료한 것으로 해석하지 않는다. 기존 이족 플레이어/적 목록에도 등록하지 않는다.

## 에셋

실행용 GLB는 `game/assets/pets/`, 재현 가능한 Blender 생성 원본은 [build-pets.py](../prototypes/character-lab/tools/build-pets.py)와 [pet_shapes.py](../prototypes/character-lab/tools/pet_shapes.py)다. 생성 시 수정 가능한 `.blend`도 `prototypes/character-lab/output/pets/`에 저장한다. `output/`은 로컬 산출물이라 커밋하지 않는다. GLB와 가져오기 설정, 생성 도구는 버전 관리한다.

| ID | 형태 개선 | 고품질 삼각형 | 저품질 삼각형 | 메시 / 재질 |
|---|---|---:|---:|---:|
| `turtle` | 큰 둥근 머리·낮은 눈, 짧은 목·발, 통통하고 짧은 등껍질 | 29,824 | 9,538 | 7 / 7 |
| `dog` | 연결된 목·가슴·주둥이·발목의 곡면 밀도 개선, 접힌 귀와 표면 무늬 | 23,084 | 7,385 | 7 / 4 |
| `rabbit` | 큰 머리·볼, 짧은 몸통·주둥이·귀와 둥근 작은 발 | 25,404 | 8,126 | 7 / 6 |
| `twin_blaster` | 공용 쌍열 총신·회전 받침 | 1,200 | 동일 모듈 | 3 / 5 |
| `rocket_pod` | 공용 네 발 로켓 포드·회전 받침 | 1,884 | 동일 모듈 | 3 / 6 |

사용자가 동시에 최대 6마리를 전제로 형태 품질을 우선하도록 요청하여 이전 8,500삼각형 제한을 변경했다. **현재 제작 예산**은 고품질 본체 최대 30,000, 저품질 최대 10,000, 무기 최대 3,500삼각형·재질 8개 이하다. 하드웨어 성능 한계로 확정한 수치는 아니다. 고품질은 `{species}.glb`, 저품질은 같은 조형에서 감량한 `{species}_low.glb`이며 본체 삼각형이 약 68% 줄어든다. 양쪽 피벗·소켓 변환은 같다. 최대 6은 이번 제작/측정 전제이며 소환 수 제한을 제품 코드에 구현한 것은 아니다.

저품질은 메시 밀도만 낮추고 재질·그림자 설정은 유지한다. 메시 하나에 여러 재질 표면이 있으므로 메시 수는 드로 콜 수와 다르다. 모든 펫의 `.glb.import`가 [pet_import.gd](../game/assets/pets/pet_import.gd)를 실행하여 정점 색이 있는 표면의 색 사용을 활성화한다.

## 귀여움 우선 조형 기준 (2026-10-22 후속)

사용자 목표는 **귀여운 사족 펫**이다. 아기 봉제인형처럼 큰 머리/둥근 볼/짧은 몸과 발/편안한 표정을 우선한다. 무기 없는 기본형을 실제 카메라 크기에서 봐도 얼굴과 통통한 실루엣이 먼저 읽히는지 검수한다. 면 수·확대 디테일만으로 귀여움이나 최종 아트 승인을 판단하지 않는다.

이번 거북이·토끼 수정은 배색보다 비율을 조정했다. 아래 수치는 조형 파라미터이며 귀여움의 보편적인 공식이나 사용자 확정 밸런스가 아니다.

- 거북이: 두개골 가로 반경0.325→0.43m, 볼 확대. 머리 피벗 앞쪽0.69→0.58m로 목을 짧게, 등껍질 앞뒤 반경0.74→0.64m, 발 피벗 높이0.29→0.21m. 둥근 작은 발톱과 간단한 입선.
- 토끼: 두개골 가로 반경0.355→0.445m, 볼 확대. 엉덩이 앞뒤 반경0.46→0.35m, 발 피벗 높이0.30→0.24m, 귀 길이0.79→0.65m. 주둥이/앞니/뒷발을 줄이고 머리 높이를 낮춰 총신 간격을 유지한다.
- 두 종 눈의 가로/세로를22% 키우고 머리 안에서 낮게 배치했다. 색·공용 무기·소켓 변환·걷기 로직·강아지 모델은 유지했다. 관절 피벗은 새 비율에 맞춰 바뀌었으며 같은 종 고/저 피벗은 일치한다.

동일 조명 전후 비교를 위해 수정 직전 GLB를 `game/output/pets/cute-before/`에 보존한다. `PetQualityCapture.tscn -- --cute --before`는 거북이/토끼만 이전 위·수정 아래로 비교하고 `--cute --walk`는 고/저4방향·두 무기·48프레임 걷기를 저장한다. `--cute --before --game-scale`은1600×900·55도·직교크기17의 게임 카메라 기준 비교다. 결과는 `cute-shape-comparison*.png`, `cute-quality-tiers*.png`, `cute-tier-walk-*.png`. 이전 파일이 없으면 비교는 실패한다. `--perf`는 이 비교 옵션들과 조합하지 않는다.

후속 검사: GLB8종·예산/피벗 통과, 빌드 경고/오류0, PetModelCheck185 통과. 실제4방향·두 장비·걷기·게임 크기 전후 렌더 확인. 전용 검사/가져오기/렌더 종료 경고 없음. Blender6.0 제거 예정 경고는 유지. 조형과 캡처만 변경하여 메뉴·전투·멀티·6마리 성능은 재측정하지 않았다. 아래 성능 표는 **이전 조형 단계**의 측정 기록이다. 최종 귀여움 평가는 사용자 확인 전이다.

## 좌표·교체 계약

- GLB/Godot: 미터, +Y 위, +Z 정면, 기본 자세 발바닥 Y=0. 루트 배율 1.
- `Body` 아래 `Head`, `Tail`, `FrontLeft`, `FrontRight`, `RearLeft`, `RearRight`, `BackSocket`. 스켈레톤 없이 부품 피벗으로 움직인다. 좌우는 +Z 정면 모델 로컬 X 음수/양수다.
- `BackSocket`은 보이지 않는 빈 노드. 위치는 거북이 `(0,1.025,-0.23)`, 강아지·토끼 `(0,0.95,-0.23)`. 회전 0, 배율 1. 장착 규격 이름은 `quadruped_back_v1`.
- 무기 원점은 받침판 바닥 중심. 무기 루트를 `BackSocket`에 **추가 위치·회전·배율 보정 없이** 붙인다. 받침판도 무기에 포함하므로 제거하면 기본형에 장비가 남지 않는다.
- 무기 내부 `Yaw` → `Pitch` → `Muzzle` 구조. `Muzzle`은 Pitch 로컬 `(0,0.025,0.65)`, +Z 발사축. 쌍열은 `MuzzleLeft/Right`의 X=-0.14/+0.14도 제공한다. 로켓 포드의 `Muzzle`은 대표 발사 기준점이며 개별 탄 발사 동작은 미구현이다.
- 현재 정면 중립 자세에서 머리·귀와 간격을 맞췄다. 향후 자유 회전/고저각 조준을 구현할 때는 머리·귀·꼬리와의 간섭 및 사격 제한각을 별도로 정해야 한다.

## 연결 예시

[PetModel.cs](../game/scripts/Pets/PetModel.cs)는 모델 생성·교체·시각용 걷기 자세만 담당한다.

```csharp
var pet = PetModel.Create("rabbit"); // 저장된 품질, 활성 상태에서 설정 변경도 반영
// PetModel.Create("rabbit", PetModelQuality.Low): 전역 설정을 따르지 않는 명시적 품질
AddChild(pet);
pet.SetWeapon("twin_blaster");
pet.SetWeapon("rocket_pod"); // 기존 장착 모델 즉시 제거
pet.SetWeapon(null);         // 무기 없는 기본형
pet.SetQuality(PetModelQuality.Low); // 무기 인스턴스·루트 변환·걷기 자세 유지
pet.Pose(elapsedSeconds, movingBlend); // 이동 중 1, 정지 0
```

거북이는 느린 교차 걸음, 강아지는 교차 걸음과 꼬리 흔들기, 토끼는 앞발/뒷발 쌍의 도약형 자세를 제공한다. 발·몸·머리가 변해도 무기는 몸의 소켓을 따라간다. `Pose`는 절대 시간과 0~1 혼합값을 받으며, 실제 위치 이동·바닥 대응 IK·충돌·속도 규칙은 담당하지 않는다. 외부에서 조작하던 Body/Head/Tail/발 자세는 다음 `Pose` 호출에서 기준 자세로 복원된다.

## 품질 설정

설정 → **소환수 모델 품질**에서 고품질(기본) / 저품질(가벼움)을 선택한다. `GameSettings.PetQuality`는 `user://settings.cfg`의 `[graphics] pet_quality`에 0=High, 1=Low로 저장한다. 키가 없거나 범위 밖 정수면 고품질이다. 기존 테스트의 사용자 파일 보호 계약을 유지한다.

품질을 생략해 생성한 `PetModel`은 트리에 들어올 때 현재 설정을 다시 적용하고, 활성 중 설정 변경도 반영한다. 트리에서 나가면 이벤트를 해제한다. 명시적 품질로 생성한 프리뷰/검사 모델은 전역 설정을 따르지 않는다. 직접 `SetQuality`를 호출해도 이 구독 정책은 바뀌지 않는다. 자동 메시 LOD도 유지하며 High의 `LodBias`는 4, Low는 1이다. 기존 무기 객체와 그 내부 상태는 교체하지 않는다.

## 재생성·검사·미리보기

저장소 루트에서 설치된 Blender/Godot 실행 파일 경로로 실행한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/character-lab/tools/build-pets.py
& 'D:/Util/Python314/python.exe' prototypes/character-lab/tools/validate-pets.py
dotnet build game/HordeShooter.csproj --nologo -v q
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path game --quit
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path game res://tests/PetModelCheck.tscn
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --path game res://tests/PetQualityCapture.tscn -- --walk
```

검사(2026-10-22): GLB 8종 구조·색·예산과 고/저 피벗 일치, 빌드 경고/오류 0, `PetModelCheck` 185개, `MenuCheck` 44개, `Integration` 148개 통과. 기본형/두 무기/품질 전환 중 같은 무기·총구·루트·걷기 유지, 설정 저장·복원·범위 밖 값, 활성 모델의 설정 변경과 생성 후 트리 진입 전 설정 변경을 검사한다. `PetQualityCapture`는 6마리 고/저 4방향, 두 무기 장착, 48프레임 걷기를 실제 Forward+로 렌더한다. 메뉴도 실제 렌더로 배치 확인했다.

로컬 결과는 `game/output/pets/quality-tiers*.png`, `tier-walk-*.png`다. `--walk`를 생략하면 정적 비교만 저장한다. 변경 전 GLB 3종을 `game/output/pets/quality-tier-before/`에 보존한 경우 `--before`로 동일 조명·카메라 전후 비교 `shape-comparison*.png`를 만든다. 이전 파일이 없으면 실패하며 임의 비교 모델을 만들지 않는다. 기존 `CapturePets`의 9종 장착표와 강아지 1차 비교 도구도 남긴다.

### 6마리 단독 렌더 측정

같은 장면을 `--perf` / `--perf --low`로 각각 실행한다. 세 종 각 2마리+로켓 포드, 걷기, 1600×1000, Forward+, 그림자 사용, VSync 해제, 90프레임 준비 후 4초간 프레임 간격을 측정했다. NVIDIA RTX 5070 Ti에서 얻은 **단독 장면 측정**이며 GPU 실행 시간만을 측정한 값은 아니다.

| 수준 | 평균 프레임 간격 | P95 | 렌더 프리미티브 | 드로 콜 |
|---|---:|---:|---:|---:|
| 고품질 | 0.635 ms | 0.909 ms | 164,064 | 127 |
| 저품질 | 0.608 ms | 0.927 ms | 52,174 | 123 |

`performance-high.json`/`performance-low.json`에 장치·샘플 수와 조건을 저장한다. 프리미티브/드로 콜은 마지막 측정 프레임의 엔진 모니터 값이며 자동 LOD·그림자 등을 포함한다. 이 PC의 시간 차이는 작고 P95는 저품질이 약간 높아, 삼각형 감소를 같은 비율의 FPS 향상으로 해석하지 않는다. 적·사격 효과·실제 모드·멀티 부하와 저사양 PC는 미검증이다. 저품질은 전역 그래픽 프리셋이 아닌 소환수 모델 옵션이다. A-21에서 같은 설정을 드론·부사수에도 확장했다([규격](companion-model-spec.md)).

Blender 5.2에서는 기존 파이프라인과 같은 `Material.use_nodes`의 6.0 제거 예정 경고가 나온다. 생성 실패와 구분하며 Blender 6.0 이식 시 수정한다. 엔진 전용 검사·렌더에는 종료 경고가 없었다. 통합 검사에서는 종료 ObjectDB 4개 경고가 남았다(I-17, 객체별 원인 미식별). `.blend`에 베이크한 애니메이션 클립은 없고 걷기 샘플은 Godot `Pose`에서 재생한다.

## 이전 단계 이력: 강아지 품질 개선 1차 (2026-10-22)

아래는 2단계 품질 제작 이전 기록이며 수치·범위는 위 현재 규격으로 대체됐다. 사용자가 참고 영상보다 모델 품질이 낮다고 피드백했다. 먼저 강아지 한 종을 비교 가능한 개선판으로 제작했다. 거북이·토끼와 무기 모양은 그대로이며 개선판의 아트 방향은 사용자 최종 확정 전이다.

- 머리/주둥이, 목/가슴/몸통, 다리/발을 **같은 관절 그룹 안에서** 복셀 리메시·평활화해 접합선을 줄인다. 머리와 몸, 네 다리 사이의 관절 피벗은 남아 있다. 전체를 스킨 메시로 합친 것은 아니다.
- 타원 귀를 곡선형 접힌 귀로 변경. 코·입선과 가늘어지는 곡선 꼬리 추가. 분리된 크림색 덩어리 대신 완성된 표면에 정점 색을 입혀 짙은 등과 황갈색 얼굴/다리를 연결한다.
- 고밀도 조형 후 감량한다. 첫 시안 12,486삼각형은 기존 예산 검사에서 실패해 8,062로 줄였으며 기존 8,500 예산은 유지한다. 조형/색 경계의 미세한 품질과 무기 자유 조준 간섭은 후속 조정 대상이다.
- Godot 4.7.2 가져오기에서 `COLOR_0`가 있는데 공유 털 재질의 정점 색 사용이 꺼지는 것을 확인했다. `dog.glb.import`가 [pet_import.gd](../game/assets/pets/pet_import.gd)를 호출해 색 배열이 있는 표면에 `vertex_color_use_as_albedo`를 켠다. 직접 GLB를 배치해도 적용되며 PetModel에만 의존하지 않는다. GLB와 `.import` 설정 및 스크립트를 함께 유지한다.
- 부분 재생성: Blender 명령 마지막에 `-- dog`를 붙인다. 당시 인자가 없으면 5종 전체를 재생성했다. 현재는 고/저 펫 6개와 무기 2개를 생성한다.
- 비교 캡처: 변경 전 GLB를 `game/output/pets/quality-before/dog.glb`에 보존한 다음 `CapturePets.tscn -- --dog-quality` 실행. 같은 조명·카메라로 `dog-quality-comparison.png`와 `dog-quality-side.png`를 만든다. 이전 파일이 없으면 비교를 꾸미지 않고 오류로 종료한다.
- 검사: 빌드 경고/오류 0, GLB 5종 검사, `PetModelCheck` 71개(정점 색의 재질 사용/명암 차이와 실제 등 표면에 받침이 닿는지 추가), 4방향·걷기 72프레임·전후 쿼터뷰/측면 실제 렌더 통과. 종료 경고 없음. 통합 전투 검사는 변경 범위 밖이라 재실행하지 않았다.
