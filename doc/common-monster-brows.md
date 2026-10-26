# 일반 동물 눈썹 조합

2026-10-25 · A-25 / C-31 후속. 사용자가 요청한 **앵무새·독수리·사자 눈썹 조합**을 시작으로 순록까지 확대했다. 작은 검은 눈과 매끈한 얼굴을 유지하면서 인상을 구분하는 외형 요소다. 성격 AI나 전투 능력을 뜻하지 않는다.

## 공통 데이터와 적용

- 데이터: [`common_brows.json`](../game/content/data/common_brows.json), 스키마 version 1. `presets` 10개와 `species` 4개를 사용한다.
- 프리셋은 `shape`(straight/arched/tapered), `width`, `thickness`, `angle`, 좌우 `height`/`angle` 차이로 구성한다. 길이 단위는 모델 로컬 미터, 각도는 도 단위다. 차분함·집중·호기심·엄격함·부드러움·의심·경계·단호함·느긋함·익살 10종이며 모두 눈썹이 있다.
- 종별 `left`/`right`는 **Godot Head 로컬 좌표**, `yaw`는 얼굴 곡면에 맞추는 좌우 회전이다. Blender 월드 좌표를 그대로 넣지 않는다. 모양과 조합값은 공통, 부착 위치는 종별이다.
- 기존 `CommonIdentity.Variant` 0–9가 같은 순서의 눈썹 프리셋을 고른다. 무리의 기존 10개 외형 셔플과 생성 시 네트워크 정체성을 재사용하며 매 프레임/피격/늦은 합류에 다시 뽑지 않는다. 눈썹과 배색 미세 변형은 현재 같은 개체 번호에 묶인다.
- [`CommonBrowCatalog`](../game/scripts/Enemies/CommonBrowCatalog.cs)는 누락/중복/알 수 없는 필드, 지원하지 않는 모양·종, 범위 밖 수치와 중복 프리셋을 거부한다. `width` .055–.15, `thickness` .006–.025, 기본 각도 ±25°, 좌우 높이 ±.012, 좌우 각도 ±12°는 입력 검증 한계이며 전체 범위의 아트 적합성을 보증하지 않는다. 출고 조합값은 JSON의 검수된 10종이다.
- [`CommonAppearance`](../game/scripts/Enemies/CommonAppearance.cs)에서 상태이상 부착 전에 두 눈썹을 Head 자식으로 만든다. 낮은 돌출의 곡면 띠 메시와 무광 고정 어두운 재질을 공유한다. 프리셋당 메시 1개(16삼각형), 개체당 두 MeshInstance/32삼각형이며 매 프레임 생성하지 않는다. 기존 모델 GLB를 재내보내지 않는 런타임 외형 모듈이다.
- 기존 머리 동작·피격·상태 표면·시야 숨김에 따른다. 플레이어와 나머지 3종(호랑이·코뿔소·인간 감염체)에는 추가하지 않는다. 충돌/전투/장구류 정책은 변경하지 않는다.

## Studio

일반 몬스터 조합 → 네 종 중 하나 선택 → **개체 외형 프리셋**에서 이름을 선택 → 조합 렌더. 10마리 보기에서는 기존 무리 생성기가 개체 번호를 배정한다.

카탈로그 API는 `brows`와 `brow_source`, 렌더 결과 개체는 실제 해석된 `brows.preset`/`brows.attachment`를 반환한다. 브라우저가 별도로 눈썹을 그리지 않고 실제 Enemy 초기화 경로를 사용한다. 원본 데이터 영역에서도 조합값을 확인할 수 있다. JSON 변경 뒤 **로컬 데이터 다시 읽기**로 렌더러를 재시작한다. `CommonBrowCatalog.Reload()`는 새 카탈로그를 만들 뿐 이미 생성한 개체를 자동 변경하지 않는다.

## 검증과 증거

- Debug/ExportRelease 빌드 오류·경고 0. 눈썹 전용 `CommonBrowCheck` 384: 잘못된 데이터, 3종×4배색×10프리셋, 동일 개체 재적용, 공유 메시/독립 변환, 머리 추종, 화상 표면·피격·숨김·복원·reload.
- `CommonRosterCheck` 264, 실제 localhost ENet `CommonRosterNetCheck` 130: 늦은 합류/실시간 생성의 세 종 눈썹 변환과 메시도 비교한다. 실제 2PC 검사는 아니다.
- Studio `common-brows.mjs` 151: 30개 개별 조합, 세 종×추가 3배색, 10마리 구성, 반복 초기화와 미지원 호랑이를 실제 렌더했다. JSON 원본 불변도 검사했다. 브라우저에서 이름이 표시된 프리셋 선택과 렌더 완료를 확인했다.
- 실제 Stage 2 `HunterCombatCheck --<species>-stage2 --brow-review` 세 종 각각42검사 통과: 추격·몽둥이/칼 타격·활 발사와 취소를 자동 실행한다. 일반 게임 거리의 정면 1장 및 실제 맵 조명에서 검사 카메라로 확대한 3개 프리셋을 추가 캡처한다. 수동 완주나 모든 전투 방향의 표정 인지 검사가 아니다.
- 눈썹 초기안은 길이·두께 차이가 약해 짧고 굵은 형태와 비대칭을 보강했다. 처음부터 실패하는 테스트를 먼저 실행한 방식은 아니며 최종 실제 코드/렌더 검증 결과를 기록한다. 이번 전용 런타임/Stage 2 로그에 오류·종료 경고가 없었다.

재현(저장소 루트):

```powershell
dotnet build game/HordeShooter.csproj --configuration Debug --nologo -v q
$browGodot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $browGodot --headless --path game --scene res://tests/CommonBrowCheck.tscn
& $browGodot --headless --path game --scene res://tests/CommonRosterNetCheck.tscn
& $browGodot --path game --scene res://tests/HunterCombatCheck.tscn --resolution 1440x900 --fixed-fps 60 -- --parrot-stage2 --brow-review
# 위 parrot을 eagle/lion으로 바꿔 반복
$env:STUDIO_URL='http://127.0.0.1:4323' # 실행한 Studio 포트에 맞춘다
node studio/tests/common-brows.mjs
```

로컬 산출물: `game/output/common-brows/*.log`, `game/output/hunter-combat-*-stage2-brows-*.png`, `studio/output/common-brows/`의 개별 렌더/JSON/검수 표/브라우저 화면. 자동 생성 output은 커밋하지 않는다.

독립 코드 검토에서 차단 결함이 없었다. 최초 구현 단계에서는 커밋/푸시하지 않았으며, 이후 사용자 요청에 따른 main 통합 검수는 아래와 같다.

## 남은 범위

- 일반 쿼터뷰 거리에서는 세밀한 프리셋 차이가 작게 보이며 확대/정면에서 더 잘 읽힌다. 적의 종류나 공격 신호를 눈썹에 의존하지 않는다.
- 무장·피격에 따른 감정 애니메이션, 눈 깜박임, 독립된 눈썹 난수/네트워크 값, 나머지 종 확장은 후속이다. 이번에는 태어날 때 정해지는 외형이다.
- 새 메시 비용은 위 기하량만 확인했다. 장시간 호드/저사양 성능, 실제 2PC, 패키지 export와 최종 사용자 아트 수락은 남는다.


## 세 동물 모델·눈썹·Studio main 통합 검수 — 2026-10-25

- 사용자 요청 범위: 이번 앵무새·독수리·사자 모델/생성 도구, 눈썹 공통 데이터/런타임, Studio, 관련 문서와 검사. 참고 이미지 첨부 폴더·자동 생성 output·개인 설정은 제외한다.
- 최신 origin/main 확인 시 기준은 `8f73b44`로 작업 브랜치와 동일했다. 기존 원격 이력을 보존하는 정상 main 통합/푸시 대상으로 확인했다.
- 새 검증: Debug/ExportRelease 오류·경고0, Integration149, CommonBrow384, CommonRoster264, ParrotMotion9,642, 조류 그립 각3,084, LionLimb4,464, HunterPose156, localhost ENet130 통과. Integration 종료 ObjectDB4개는 기존 I-17이며 전용 검사 로그는 종료 경고 없음.
- Blender 원본 GLB 재가져오기 앵무새69·독수리60·사자74 통과. 최초 세 종 통합 인자는 해당 조합 이름의 audit 파일이 없어 실패했으며, 실제로 생성해 둔 종별 audit로 다시 실행해 통과했다. 재생성이나 GLB 수정은 하지 않았다.
- Studio 눈썹151·공통 roster82 통과. 문서 상대 경로500개 확인. 직전 구현에서 검수한 30조합 및 실제 Stage2 세 종 각각42검사의 모델/런타임을 그대로 통합한다. 이번에는 수동 플레이·장시간 성능·실제2PC를 추가 실행한 것이 아니다.
- 로그: `game/output/animal-main-integration/`, `prototypes/common-monsters/output/animal-main-<species>.log`, `studio/output/animal-main-brows.log`, `studio/output/animal-main-roster.log`.

## 순록 확대 — 2026-10-25 후속

공통10프리셋과 Variant 매핑은 유지하고 `reindeer` 부착점만 추가했다. Head 로컬 좌우 `(±.20,.46,.25)`, yaw30°. 작은 눈 위의 얼굴 면에 붙이고 뿔/귀와 겹치지 않는 위치로 조정했다. 인간 감염체의 기존 얼굴 눈썹은 GLB에 남으며 동물 공통 모듈 대상은 아니다.

최종 CommonBrow503·Studio199·localhost ENet130, 실제 Stage2 순록42 통과. 위 세 종의 과거 검사 수치는 당시 이력이며 현재4종 검증과 구분한다. [순록·감염체 모델/검증·한계](reindeer-zombie-clean-forms.md).
