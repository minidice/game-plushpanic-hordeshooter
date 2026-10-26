# 004 · code · 피드백 공유 꾸러미

관련: [T-27](../decisions.md#t-27-피드백-공유-꾸러미), T-25(Studio), C-23(시야). 2026-10-26.

## 요청

지인에게 **캐릭터 모델과 배경 오브젝트 모델만** 피드백 받기. md 문서는 공개하지 않음. 압축 파일로 보내고, 확대 · 게임 시점 · 돌려 보는 3D가 필요. 범위는 추천 범위(P1~P8 · M1~M8 · O1~O15 · S1~S4, 네임드 · 보스 제외).

## 변경

- Studio 렌더러 `share_item`([StudioShare.cs](../../../../game/tests/StudioShare.cs), [StudioPreview.cs](../../../../game/tests/StudioPreview.cs) 분기 한 줄), 서버 POST 허용 목록에 `/api/share/item`([server.mjs](../../../../studio/server.mjs)).
  - Studio 미리보기와 같은 조립(`ReplaceRig` · 기본 외형 · M4 파지 · `SetPreviewVisibility`, `CreateMonsterSubject`, `PropLibrary.Instance`).
  - 8방향 경계를 합친 고정 틀로 두 바퀴(눈높이 12° · 게임 55°) × 16장 정사영 WebP 768px. 틀은 투영 높이 · 폭 중 큰 쪽 × 1.12(처음 3D 대각선 기준은 모델이 작게 나와 고침).
  - GLB는 정면, 보이지 않는 `VisualInstance3D`를 지운 뒤 `GltfDocument`로 저장. 출력은 `studio/share/.build/<이름>/`만, 이름 `[a-z0-9_-]{1,40}`.
- 장면 [ShareSceneCapture.cs](../../../../game/tests/ShareSceneCapture.cs) · `.tscn`(창 모드 전용, 1600×900, HUD 숨김): S1 Stage 1 안전 지대(가까운 카메라) · S2 Stage 2 숲 · S3 일반 몬스터 7종과 전투 · S4 크기 비교.
  - 동료 AI(`SquadBrain`)와 `VisionSystem`을 멈추고, 몬스터는 세운 뒤 몸을 멈추고 리그만 돌려 실루엣에서 다 보이게 한다. 카메라는 `Target`을 비워 지정 위치에 고정.
- 만들기 [build-share.mjs](../../../../studio/tools/build-share.mjs)(`export` · `pack`), 페이지 틀 [share-page.html](../../../../studio/tools/share-page.html), 목록 [selection.json](../../../../studio/share/selection.json), 안내 [studio/share/README.md](../../../../studio/share/README.md). `.gitignore`에 `studio/share/.build/` · `studio/share/packs/`.
  - 페이지는 `file://`로 열린다: three.js r180(`prototypes/character-lab/node_modules/three`)을 `importmap`의 `data:` 모듈로 넣고, 모델은 `models/<이름>.js`(base64)를 필요할 때 `<script>`로 읽는다.
  - 카드: 끌어서 회전 · 휠 확대 · 링 전환 · 3D · 의견 칸(브라우저 저장, 모아 복사 · 파일 저장). 3D: 정면 · 옆 · 뒤 · 게임 시점(55°, -30°) · 게임 크기(넓은 카메라 17 m, 1080p 기준). `#3d=P2:game` 바로 열기, `?selftest` 전체 모델 읽기 검사.
- 문서: [decisions.md](../../../decisions.md) T-27 색인 · [본문](../decisions.md), [issues.md 검토](../issues.md), [feature-checklist.md](../../../feature-checklist.md) Studio 절, [studio-prototype.md](../../../studio-prototype.md), [studio/README.md](../../../../studio/README.md).

## 결과물 (커밋하지 않음)

- `studio/share/packs/001-models/` · `001-models.zip`: 모델 31 · 장면 4, 폴더 34.8 MB, **zip 20.0 MB**. 내용은 `index.html` 1 · WebP 992 · 모델 JS 31 · PNG 4뿐. md · JSON · 코드 없음, 로컬 경로 · `res://` · 사용자 이름 없음(검사함).
- 모델 내보내기: 31개 모두 성공, 개당 약 2.3초. GLB 27 KB(풀) ~ 1.6 MB(장비 플레이어).

## 검사

- C# 빌드: 경고 · 오류 0.
- `node tools/build-share.mjs export`(4336 Studio): 31/31.
- `ShareSceneCapture.tscn`(창 모드): `SHARE_SCENES_PASS 4`, 마지막 실행은 오류 출력 없음. 네 장면 렌더를 눈으로 확인.
- 압축을 새 폴더에 풀어 헤드리스 Chrome `file://…/index.html?selftest`: **`selftest 31/31 webgl`**(모든 GLB 해석, WebGL 생성). 화면 확인: 첫 화면 1400px, `#3d=P2:game`, `#3d=O12:front`, `#3d=M8:gamesize`.
- Studio: `STUDIO_SMOKE_PASS 36`, `STUDIO_DOCS_PASS 25`(둘 다 4336). `share_item` 이름 `../evil` → 거부("공유 이름이 올바르지 않습니다").
- Integration(헤드리스): `INTEGRATION_PASS 149`, 종료 0, 오류 · 누수(ObjectDB · orphan) 출력 0줄. 게임 코드는 바꾸지 않았고(검사 · Studio 파일만) 회귀 확인용으로 돌림.

## 발견 · 남은 제약

- 처음 장면 촬영 때 Stage를 바꿀 때마다 `ObjectDisposedException`(CSharpInstanceBridge.Call) 3번 — 동료 AI를 멈추자 사라짐. 촬영 도구가 Stage를 바로 지우고 새로 만드는 경우에서만 봤고, 게임의 실제 Stage 전환에서도 나는지는 **확인하지 않았다**(추정: 지워진 Stage를 참조하는 `SquadBrain` 호출).
- 처음 S4에서는 플레이어 뒤 몬스터가 시야 원뿔(C-23) 밖이라 실루엣으로 찍혔다. 장면 사진은 시야를 멈춘 연출이라 실제 플레이와 다르다(README 한계에 적음).
- O15 곤돌라 승강장은 바닥이 없는 틀 모델(바닥은 게임이 깖)이라 사진 여백이 크고 틀만 보인다. 경계 높이가 11.25 m로 잡힘(매니페스트 5.6 m) — 사진 틀 크기에만 영향, 원인 미확인.
- 3D 보기는 게임 셰이더 없이 three.js 기본 조명. 색 · 질감은 사진이 기준.
- 휴대폰 폭은 헤드리스 Chrome 최소 창 폭 때문에 확인하지 못함(격자 최소 폭을 `min(…, 100%)`로 고쳐 둠).
- 장면 S3 · S4에 무장 사자(M8)는 없음(무리 7종만).
