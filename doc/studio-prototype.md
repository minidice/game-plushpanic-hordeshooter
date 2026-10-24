# 로컬 Studio 검수 시제품

작성·검수: 2026-10-24. 결정 T-25. **개발용 시제품**이며 게임 제품 기능이나 외부 배포 사이트가 아니다.

## 목적과 범위

사용자가 승인한 방향: 개발자 PC에서 현재 작업 폴더의 실제 게임 데이터를 읽고, 웹에서 조합 가능한 캐릭터 외형을 확인한다. 커스터마이징 저장·게임 세이브 적용·외부 공유·릴리즈 데이터 읽기는 하지 않는다.

이번 범위는 거위·오리·라쿤·랫서판다의 종 선택, 몸/얼굴/귀·머리/날개·발/꼬리/장비 조작, 회전·확대, 장비·M4 표시, 기본 모습 복원이다. 값은 브라우저 메모리와 렌더 요청에만 존재한다. 종을 바꾸거나 새로고침하면 해당 임시 편집은 사라진다. 프리셋 저장과 애니메이션 재생은 없다.

2026-10-24 후속 요청으로 **캐릭터 / 몬스터 / 오브젝트** 메뉴를 추가했다. 몬스터·오브젝트 도감은 이름/ID 검색, 분류 필터, 항목 선택, 회전·확대, 속성 표와 원본 상세 정보를 제공한다. 최초 도감 검증은 몬스터 15종, 등록 모델 65종 + WorkshopArt 7종 + 기본 소품 생성 예시 6종 = 오브젝트 78종이었다. 목록 수는 현재 로컬 파일에 따라 바뀐다.

같은 날 후속으로 **펫3·드론4·부사수4의 고/저품질 비교**를 추가했다. 하나의 항목에서 같은 시점의 두 렌더와 원본 메시 삼각형 수를 함께 본다. 펫/드론은 실제 별도 GLB, 부사수는 같은 모델의 자동 LOD 설정이다. 현재 로컬 추가 자산을 포함한 검증 시점에는 몬스터15·오브젝트91이며, 품질 때문에 목록을 둘로 복제하지 않는다. [품질 비교 및 후속 상호작용 데이터 계약](object-data-spec.md).

2026-10-24 차단 후속: **철조망은 이동 차단/탄환 통과, 일반 구조물은 양쪽 차단**을 실제 게임 공통 JSON과 로더로 연결했다. 웹은 동일 데이터의 값·프로필·출처·배치 예외를 표시한다. [현재 계약·게임 검증](object-data-spec.md). 파괴/폭발도 후속으로 실제 데이터 연결했다. 균열 콘크리트 벽의 파괴 전/붕괴 중/파괴 후를 선택하고 드럼통의 내구도·폭발 수치를 확인할 수 있다.

### 도감의 데이터 계약

- [`StudioLibrary.cs`](../game/tests/StudioLibrary.cs)가 실제 `MonsterDatabase`와 `PropLibrary.Folders`를 읽는다. 별도 웹용 몬스터/모델 목록을 만들지 않는다. `GET /api/library`로 목록, `POST /api/library/render`로 선택 항목의 실제 렌더를 받는다. URL의 `?id=`로 선택 항목을 다시 열 수 있다.
- 몬스터: `monsters.csv`의 체력·속도·질량·공격력·공격 간격·도달/각성 거리·등급·단차 이동을 표시한다. **모드·인원·등장 배율 적용 전 기본값**이다. 특수 행동 JSON과 보스 페이즈/스킬 JSON도 펼쳐 볼 수 있다. 외형은 `Stage.LookFor`와 실제 Enemy 하위 클래스의 초기화로 생성한다. AI/물리 진행은 중지한다.
- 감염체는 네 플레이어 종의 `MakeZombie`를 선택할 수 있다. 표의 충돌/모델 배율은 명시적으로 **거위 기준**이며 선택 종에 따른 동적 속성 표는 후속이다. 보스 배율과 무장 동물의 M4는 실제 게임 초기화를 따른다.
- 오브젝트: 다섯 자산 폴더의 현재 manifest에서 이름/분류/크기/추천 충돌/가림 부위/모델 경로/삼각형 수 등과 원본 JSON을 표시한다. `PropLibrary.Instance`로 실제 로컬 모델을 불러온다. **추천 충돌은 배치된 맵의 실제 충돌을 의미하지 않는다.** 맵별 상호작용, 스케일, 광원, 재질 교체는 단독 모델에 적용되지 않는다.
- WorkshopArt 7종, 폭발 드럼통/화물 상자/골판지 상자/선반/탄약 보급 외형/조명 기구는 실제 생성 함수를 호출한다. 파라미터가 필요한 소품은 대표 규격/색이라고 표시한다. 폭발과 보급 동작은 실행하지 않는다. 맵의 모든 벽·문·배치 노드를 자동 열거하는 도감은 아니다.
- 도감 카메라는 보이는 메시 경계를 기준으로 자동 맞춤하는 직교 시점이다. 캐릭터 메뉴로 돌아가면 기존 원근 시점/받침대를 복원한다. 항목 변경 시 이전 모델을 제거한다.

## 실행

저장소 루트에서:

```powershell
.\studio\Start-Studio.ps1
```

필요: 기존 게임용 Godot 4.7.2 .NET, .NET SDK, Node.js. 웹의 외부 패키지 설치는 필요 없다. 다른 Godot 위치/포트는 `-GodotDir '경로' -Port 4318`로 지정한다.

터미널에 `Studio ready: http://127.0.0.1:4317`이 나오면 해당 주소를 연다. 터미널의 Ctrl+C로 서버와 렌더러를 종료한다. **GPU 렌더를 위해 데스크톱 세션이 필요**하며 Godot headless 실행으로 대체할 수 없다. 렌더 창은 화면 밖에 놓고 실행한다. 웹만 조작하면 된다.

`로컬 데이터 다시 읽기`는 렌더러를 종료하고 현재 C# 소스 빌드 → 변경된 에셋 import → 렌더러 재시작 → 목록 재로딩을 수행한다. 단순 브라우저 새로고침은 현재 렌더러의 데이터와 기본 모습을 다시 불러온다. 파일 변경을 감시하지는 않는다. 코드/자산 편집을 마친 뒤 다시 읽기를 누른다.

## 연결 구조와 원본

- [`studio/server.mjs`](../studio/server.mjs): Node 내장 HTTP 서버. `127.0.0.1`에만 바인딩하며 Host/Origin을 검사한다. 정해진 웹 파일과 API만 제공한다. Godot 자식 프로세스와 stdin/stdout JSON으로 통신한다.
- [`StudioPreview.cs`](../game/tests/StudioPreview.cs): 메인 스레드에서 게임의 `CharacterCatalog`, `AppearanceEditor`, `AppearanceProfile`, `CharacterRig`, `WeaponVisual`을 사용한다. 웹에서 Godot 셰이더를 재구현하지 않는다.
- 종 목록은 `game/content/characters/*.tres`, 선택지/범위/종별 제약/표시 문구는 **실제 AppearanceEditor 컨트롤**에서 추출한다. 게임 메뉴처럼 `test_` 정의를 제외한다. JSON key와 UI control name 사이 변환은 bridge에 있으며 새 control 이름을 만들면 이 변환을 확인해야 한다.
- 메타데이터를 읽는 `AppearanceEditor.ShowRig`에는 존재하지 않는 `res://tests/__studio_unsaved__.cfg` 경로를 제공한다. 편집 신호를 호출하지 않으며 `AppearanceStore.SaveProfile`, `GameSettings.Save`를 실행하지 않는다. 실제 사용자 appearance.cfg를 읽거나 덮어쓰지 않는다.
- 렌더 입력은 새 `AppearanceProfile`에 `TrySet`/`Normalize`로 적용한다. 변경하지 않은 색상은 기본 종 팔레트/부위 연동을 그대로 사용한다.
- 900×900 PNG는 메모리에서 웹으로 전달하며 디스크에 자동 보관하지 않는다. `studio/output/`의 로그와 검사 이미지에 한해 로컬 생성물이 생기며 Git 제외다.

웹 3D 근사, 사전 캡처 갤러리, 실제 Godot 연결을 검토했고 사용자가 **실제 Godot 연결의 시제품**을 승인했다. 재질·부위 변형이 게임과 같고 웹에 외형 구현을 복제하지 않기 때문이다. 단점은 Godot/.NET/GPU 실행 의존성과 정지 이미지 갱신 지연이다.

## 외형과 배포 경계

- Forward+와 실제 게임 모델·외형 코드·셰이더를 사용한다. 조명·톤 매핑·카메라는 현재 `MainMenu.BuildPreview`/`UpdatePreviewCamera` 설정에 맞췄다. **전투 맵별 조명/배경을 재현하는 화면은 아니다.** 메뉴 조명 코드가 바뀌면 bridge의 해당 설정도 검토한다.
- 180ms 입력 묶음 처리 후 마지막 조합을 렌더한다. 이전 요청이 늦게 도착해도 새로운 조합의 이미지로 표시하지 않는다. 렌더 중에는 상태를 표시한다. 렌더하지 않을 때 SubViewport 갱신을 중단한다.
- `studio/`는 Godot 프로젝트 밖이다. [`export_presets.cfg`](../game/export_presets.cfg)의 기존 `tests/*` 제외로 전용 장면/소스 리소스를 제외한다. 차단 공통 JSON/로더는 실제 게임에서 사용하므로 게임 구성에 포함된다. [`HordeShooter.csproj`](../game/HordeShooter.csproj)는 Debug 이외 구성에서 `tests/Studio*.cs`를 컴파일하지 않아 ExportDebug/ExportRelease 어셈블리에도 bridge를 넣지 않는다.
- 단일 렌더 요청만 처리하는 로컬 도구다. 메뉴 이동의 목록 요청은 진행 중인 렌더가 끝나기를 기다린다. 여러 브라우저의 동시 렌더는 사용 중 오류가 표시될 수 있다. 실시간 동영상, 여러 사용자, 다른 PC 접속, 맵/아이템 페이지는 이번 범위 밖이다.

## 검증과 재현

Studio 실행 후 다른 조작 없이:

```powershell
node studio/tests/smoke.mjs
node studio/tests/library.mjs --all
node studio/tests/quality.mjs
node studio/tests/interactions.mjs
```

검사 이미지 비교에만 Python + Pillow가 필요하다. 웹 실행에는 필요 없다. 다른 포트는 `STUDIO_URL` 환경변수를 지정한다. [`smoke.mjs`](../studio/tests/smoke.mjs)는 실제 HTTP/Godot 경로로 네 종의 모든 노출 필드 변경, 초기화, 색/모양 차이, 잘못된 입력, 외부 Origin 거부, 파일 경로 제한과 원본/사용자 저장 파일 해시 불변을 확인한다. 이미지 산출물은 `studio/output/`.

2026-10-24 검증:

- API 검사 **36개**, 실제 PNG **13장**. 초기화 비교는 PNG 바이트 일치 대신 실제 픽셀 차이를 검사한다. 중간 실행에서 메시 재생성 후 경계 **1픽셀, 채널 최대 6/255** 차이로 바이트 일치 검사가 실패했다. 허용 기준은 변경 픽셀 비율 0.05% 미만과 채널별 평균 차이 0.02/255 미만이며, 거의 단색인 빈 렌더도 거부한다. 최종 초기화 비교는 3종 차이 0픽셀, 랫서판다 1픽셀·채널 평균 최대 0.000001이었다. `studio/output/smoke.log`.
- 개발 중 테스트용 정의 2개가 목록에 포함되는 것을 검사 실패로 확인하고 실제 메뉴와 동일한 `test_` 필터로 수정했다. 새 제품 버그 ID는 발급하지 않는다.
- `ModularCustomization` **67**, `AppearanceExpansion`, `CustomizationToneCheck` **87** 통과. 전용 로그에 오류·종료 경고 없음. `studio/output/regression-*.log`.
- Debug 및 ExportRelease C# 빌드 오류/경고 0. ExportRelease DLL에서 `StudioPreview` 타입 부재, ExportDebug Compile 항목 제외 확인. 실제 게임 패키지 전체 내보내기는 이번에 실행하지 않았다.
- 브라우저에서 종 변경, 종별 항목, 접힌 귀/넘긴 머리털, 장비 숨김, 색·체형·회전 변경과 실제 렌더, 로컬 데이터 재빌드/재가져오기를 확인했다. 화면 검수에서 이미지가 하단 이름 영역으로 넘치는 CSS 배치를 수정하고 최종 화면을 `studio/output/studio-web.png`로 남겼다.
- 독립 정적 리뷰 후 stdin 오류의 요청 타이머 해제 및 프로세스 귀속 확인, 재로딩 실패의 구체적 오류 보존을 수정했다. 마지막 소스로 API 검사를 재실행했다. 드문 stdin 장애의 실제 주입 재현은 미실행이다.
- 이 PC RTX 5070 Ti에서 최종 API 렌더 약 130–230ms. 입력 지연 180ms와 브라우저 처리 시간은 별도이며 다른 PC 성능 보장이 아니다.
- 게임 전투 코드를 변경하지 않아 `Integration`은 이번에 실행하지 않았다. 다른 PC, 장시간 반복 사용, 전투 장면 조명 비교는 미검증이다. 기존 I-17 등 게임 이슈 상태는 변경하지 않는다.

2026-10-24 도감 확장 검증:

- [`library.mjs`](../studio/tests/library.mjs) **354검사**, 몬스터/오브젝트 전체 **93종 렌더·픽셀 검사** 통과. 현재 CSV의 ID 목록, manifest의 모든 자산/크기, 코드 생성 소품, 특수 정보, 임의 경로 거부, 렌더 중 메뉴 목록 요청을 검사. `studio/output/library-test.log` 및 `library-*.png`.
- 최초 `/api/library` 미구현 404를 확인한 뒤 구현했다. 물 타일 PNG는 정상 그림이어도 7,978바이트로 크기 기반 검사에 실패했다. 실제 이미지 확인 후 파일 용량 대신 900×900 해상도·RGB 표준편차 > 3으로 빈 렌더를 검사한다. 모든 93개 통과.
- 독립 정적 리뷰에서 이전 페이지 렌더가 진행 중일 때 새 메뉴의 목록 요청이 503으로 실패하는 경로를 확인했다. 겹치는 렌더/목록 요청 실패를 재현하고 `readCatalog` 직렬 대기를 추가했다. 검사에서는 이전 POST가 먼저 도착하도록 한 뒤 두 메뉴의 GET을 겹친다(완전 동시 전송은 GET이 먼저 도착할 수 있어 시나리오가 다름).
- 확장 후 캐릭터 **36검사·13렌더** 재통과, 초기화 픽셀 비교와 원본/사용자 저장 파일 해시 불변. `studio/output/smoke-library-regression.log`. Debug/ExportRelease 빌드 오류·경고 0, ExportDebug compile 목록에서 Studio 소스 제외 확인. 전체 게임 export·Integration은 이번 확장에서도 미실행.
- ExportRelease DLL에서도 Studio 타입 부재 확인. 렌더러 누적 로그에 `ERROR:`/`WARNING:`/`leaked` 없음. 문서 상대 링크 8개 검사 통과. Git diff 검사는 내용 오류 없이 CRLF→LF 안내만 출력했다.
- 실제 브라우저에서 메뉴 이동, 감염체 라쿤 선택, 보스 필터, 곤돌라 ID 검색, 속성/원본 JSON 펼침, 오브젝트 회전, 로컬 데이터 다시 읽기 후 선택 복원과 메뉴 잠금을 확인했다. 넓은 화면에서 모델/속성을 나란히 배치하고 좁은 화면은 세로 배치한다. `studio/output/studio-monsters.png`, `studio/output/studio-objects.png`. 좁은 화면 실제 렌더 검수는 미실행이다.

2026-10-24 품질 비교 후속 검증:

- 신규 품질 항목이 없는 상태에서 검사 실패 확인 후 `StudioQuality.cs`로 실제 팩토리를 연결. `quality.mjs` **33검사·11쌍/22장** 통과: 실제 `_low.glb`/부사수 프리셋 목록, 감량 메시/픽셀 차이, 단일 모델로 복귀, 설정·외형·원본 프리셋 파일 해시 불변. `studio/output/quality-test.log`, `quality-*.png`.
- 현재 로컬 전체 **106항목·395검사** 재통과, 캐릭터 **36검사** 재통과. `studio/output/library-quality-regression.log`, `studio/output/smoke-quality-regression.log`.
- Debug/ExportRelease 빌드 오류·경고0. 실제 게임의 모델/전투 코드는 수정하지 않아 이번 품질 작업에서는 PetModelCheck/CompanionModelCheck/Integration/전체 export는 미실행. 독립 정적 리뷰에서 추가 확정 결함 없음.
- 당시 품질 비교 단계에서는 상호작용 공통 데이터가 없음을 표시했다(현재는 아래 차단/파괴 후속 구현 참조). `미연결`은 `통과`나 `파괴 불가`를 뜻하지 않는다. 이동/탄환 차단, 공격 종류/등급/내구도, 파괴 시 폭발은 [후속 계약 초안](object-data-spec.md)에 분리했으며 전투 연결 완료로 표시하지 않는다.
- 브라우저에서 펫3종이 품질별 중복 없이 나오고 두 이미지가 회전 입력에 함께 바뀌는 것을 확인했다. 비교 grid의 최소 콘텐츠 높이 때문에 390px 영역에 이미지가 505px로 넘치는 문제를 실제 화면/DOM 크기로 확인하고, 고정 영역·`minmax(0,1fr)`로 수정(최종 영역/내용 모두390px). 같은 확대값의 고/저 렌더를 `studio/output/studio-quality.png`로 저장했다. 좁은 화면·실제 전투 조명 비교는 미검증이다.

<a id="main-integration-20261024"></a>
## 2026-10-24 Stage 2·Studio main 통합 재검증

사용자 요청으로 두 세션의 누적 작업을 함께 커밋/원격 main에 반영한다. 원격 기준 `bb5ba52`를 fetch하여 기존 로컬 기준과 동일함을 확인했다. 기존 기능·임시 수치는 유지하고 `StageTransitionCheck`의 낡은 자산 수 기대만67개로 수정했다. 위 개별 작업 당시의 검증 이력은 보존하며, 아래가 현재 통합 소스의 재검증 결과다.

| 검사 | 이번 결과 |
|---|---|
| Debug / ExportRelease 빌드 | 모두 오류0·경고0 |
| Godot import | 종료0·오류/경고 없음 |
| ObjectInteractionCheck | 30 통과 |
| TraversalQualityCheck | 55 통과 |
| TreeAssetCheck / Python validate_revised_trees | 35 통과 / 공용5종·구형 자산/생성 경로 제거 확인 |
| StageTransitionCheck | 200 통과 (기존65→현재67개 자산 기대 갱신) |
| WorkshopArtCheck | 420 통과 |
| Integration (다른 검사와 겹치지 않고 실행) | 149 통과, 종료 ObjectDB4개 경고(I-17) |
| TerrainEdgeCheck / ForestExpansionCheck | 창 모드41/65 통과, 실제 경사 이동·벽 차단·숲 사격 및22장 렌더 |
| Studio smoke | 36 통과·13장 렌더 |
| Studio library --all | 395 통과·106종 렌더/픽셀 검사 |
| Studio quality | 33 통과·11쌍/22장 픽셀 검사 |
| Studio interactions | 131 통과·2장 픽셀 검사 |
| ExportRelease compile 목록 | Studio 소스 제외·ObjectInteractions 포함 확인 |

게임 로그는 `game/output/merge-*.log` 중 위 장면 이름, 웹 로그는 `studio/output/merge-{smoke,library,quality,interactions}.log`. Studio는 전용 포트4319에서 실행하고 `STUDIO_URL=http://127.0.0.1:4319`로 기존 검사 명령을 순차 실행했다. 게임 전용 검사는 `--headless --fixed-fps 60`, 두 지형/숲 검사는 창 모드 `--resolution 1440x900 --fixed-fps 60`, Integration은 `--headless`로 고정 FPS 없이 실행했다. 모든 검사 종료0이며 Integration 외 이번 게임 전용/가져오기에는 오류·종료 경고가 없다. 경사로/새 수관과 Studio 철망 렌더를 직접 열어 확인했다.

커밋에는 실행 에셋·Blender 원본·생성 도구·Studio 소스·검사·문서를 포함한다. `output/`, 캐시, 개인 참고 이미지 첨부는 제외한다. 이번에 새 성능 측정·전체 동선 장면·실제2PC·전체 패키지 export·브라우저 수동 조작 재검수는 실행하지 않았으며 기존 작업의 해당 검증과 구분한다. 이전 산탄 검사 간헐 실패(I-65), ObjectDB(I-17), 다리 원인 미확정(I-116)은 그대로 남긴다.


## 2026-10-24 파괴·폭발 후속

실제 `ObjectInteractions`의 파괴 타입·내구도·자체 폭발을 표시한다. **균열 콘크리트 벽**은 `intact`/`breaking`/`broken`을 같은 프레이밍으로 확인하고, 실제 배치 JSON도 펼쳐 읽는다. **폭발 드럼통**은 장식용 barrel과 구분해 실제 HP10/반경4.6/피해계수125/충격계수140/지연0.12를 보여준다. 게임 데이터 파일을 웹에 복제하거나 저장하지 않는다.

`destruction.mjs`24·`interactions.mjs`192·`library.mjs`200(8종)·`quality.mjs`33(11쌍) 통과, 실제 브라우저에서 파괴 후 상태 선택과 양쪽 속성 표를 확인했다. API/모델3상태 PNG는 `studio/output/destruction-wall-*.png`, 브라우저는 `studio/output/studio-destruction-wall.png`. [게임 검증과 제한](object-data-spec.md#파괴-벽폭발-후속-검증-2026-10-24)을 함께 본다. Debug/ExportRelease 빌드 통과, Studio 배포 제외 유지.


## 2026-10-24 이펙트·사운드 검수

이펙트와 사운드도 웹에서 확인하고 싶다는 사용자 요청에 따라 `/effects`, `/sounds` 메뉴를 추가한다. 현재 작업 폴더의 실제 코드/데이터를 읽는 T-25 원칙을 유지한다.

- **이펙트**: 실제 Godot 효과 생성기·셰이더로 짧은 프레임 시퀀스를 만들고 브라우저에서 재생/일시정지/느리게 보기/타임라인/반복을 제공한다. 모든 전투 상황을 자동 수집하는 목록은 아니며, 등록한 효과의 대표 조건을 표시한다. 원본 효과 함수를 복제한 웹 애니메이션을 만들지 않는다.
- **사운드**: `SoundRecipes.All` 전체를 열거하고 실제 `Sfx`가 선택한 합성 PCM 또는 `content/sounds`의 교체 음원을 듣는다. 사용자가 재생하기 전 자동 출력하지 않는다. 원음·기준 피치1의 미리듣기로, 레시피 음량(dB)/피치 흔들림/버스/거리/음성 한도는 속성으로 표시하며 원음에는 적용하지 않는다. 게임 혼합 결과의 음량 검수는 `AudioMixCheck`와 실제 게임에서 한다.
- 합성음은 실제 PCM을 메모리에서 WAV로 감싼다. 교체 파일은 게임이 선택한 원본 `.ogg`→`.wav`→`.mp3` 우선순위를 따른다. 브라우저 반복은 파일 전체이며 원본의 부분 루프 지점과 다를 수 있다.
- **조회 전용**: 설정·외형 세이브·음원/게임 데이터 저장 없음. 페이지 이동/항목 전환/재로딩 시 재생을 멈추고 이전 음원 URL을 해제한다. 여러 탭의 동시 렌더는 기존과 같이 지원하지 않는다.
- **구조**: `StudioEffects.cs`, `StudioSounds.cs`는 Debug 전용 partial bridge. `GET /api/media`, `POST /api/effects/render`, `POST /api/sounds/preview` 사용. 입력 ID는 등록 목록으로 제한하고 임의 파일 경로는 받지 않는다. 웹은 `media.html/js/css`, 기존 Node loopback 서버에 추가한다. ExportRelease/ExportDebug에서 Studio 소스 제외를 유지한다.

검사 스크립트: `node studio/tests/media.mjs --all`. 원본/사용자 파일 해시, 실제 레시피 목록, 모든 음원의 PCM 길이·비무음·피크와 메타데이터, 이펙트 프레임 변화·시간 순서·PNG 크기, 잘못된 ID를 검사한다. 실행 결과는 완료 후 아래에 기록한다.


### 이펙트·사운드 실행 결과

- `studio/tests/media.mjs --all`: **312검사**, 현재 SoundRecipes 전체43종·등록 효과13종 통과. 실제 PCM16 길이·피크·비무음, metadata/loop/source 일치, 프레임 시각·640px 크기·이미지 변화, 잘못된 ID/타입 거부, 원본과 `settings.cfg`/`appearance.cfg` 해시 불변. `studio/output/media-test.log`.
- 실제 Vulkan Forward+ 렌더를 확인했다. 첫 프레임 M4 총구 섬광, 피격선, 폭발 파편을 육안 검수. 캡처는 640×640, 짧은 효과60fps/긴 효과30fps, 최대43장/1.4초이며 실제 캡처 소요시간과 재생 시간을 구분한다. 고정간격으로 실제 노드 `_Process`·트윈·CPU파티클을 진행하고 카메라/뷰포트/기존 모델을 복원한다.
- 브라우저에서 0.25배 반복·일시정지·0.2초 타임라인 이동, wall 검색, 렌더 중 사운드 메뉴 이동, 원음 준비 후 자동 재생 없음, 권총 및 반복 앰비언스의 재생/일시정지를 확인했다. 기본 브라우저 디코드 readyState4, 재생시간 진행 및 오류 없음. 청각적 믹스 품질 평가를 수행했다고 표현하지 않는다. `studio/output/studio-effects.png`, `studio/output/studio-sounds.png`.
- 기존 `smoke.mjs`36(캐릭터13렌더), `library.mjs`200(8렌더), `quality.mjs`33(11쌍), `destruction.mjs`24(3렌더) 통과. 효과 촬영 뒤 기존 모델/카메라가 복원됨. `studio/output/media-regression-*.log`.
- Debug/ExportRelease 빌드 오류·경고0, ExportRelease와 ExportDebug Compile에서 Studio 소스0개. 독립 정적 리뷰에서 출처14경로 존재·재생 시간/상태 복원·비동기 선택·URL해제·CSP 확인. 작은 팝 설명을 실제 Companion.Revive의 동료 부활 표본으로 정정했다.
- 현재 교체 음원 파일이 없어 override파일의 실제 브라우저 재생은 미검증이다. 선택 경로는 게임 Sfx 로더를 사용한다. 현재 데이터는 합성음만이며 사운드 규격의 오래된44종 표기를 현재43종으로 고쳤다. 전체 게임 Integration·실제 3D거리/버스 믹스·휴대폰 실기기·전체 패키지 export·장시간 메모리 검사는 이번 UI 확장에서 미실행. 게임 전투/오디오 제품 코드 자체는 이번 확장에서 변경하지 않았다.

### 폭발·화염·독 공통 데이터 후속 — 2026-10-24

- `combat_effects.json` schema1의 11효과를 자동 등록한다. 이전 단일 `explosion` 표본은 `fx:blast` 및 계열별 목록으로 교체해 현재 효과는23종, 음원43종이다. 이전 `id=explosion` 북마크는 새 목록에서 다시 선택해야 한다.
- 발생원/사용처·해당 kind의 시각 설정·미리보기 조건을 표시한다. **공통 효과 원본 데이터**를 펼치면 게임과 같은 JSON 레코드를 확인한다. 저장 기능은 없으며 데이터 변경 후 Studio의 최신 코드/데이터 다시 읽기를 사용한다.
- 실제 `CombatBurstEffect`·`CombatSurfaceEffect`·`CombatProjectileEffect`로 생성. 피해/음향/카메라 흔들림을 재현하지 않는 시각 검수 표본이다. 독 비행 궤적만 Studio 표본이며 같은 외형을 실제 방향에 맞춰 회전한다.
- 짧은 효과60fps, 보통30fps, 2초 초과는5~15fps로 낮춰 **전체 수명**을 캡처한다. 원본 수명 끝에서0.15초 여유, 표본 수명은 실제 DPS/위험 지속시간과 구분. 최신 결과는 [효과 규격](vfx-quality.md)의 검증 기록을 따른다.
