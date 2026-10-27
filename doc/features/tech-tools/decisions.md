# 기술 · 도구 — 결정 본문

새 결정은 여기 ID 아래에 쓰고, [전체 색인](../../decisions.md)에는 ID · 한 줄 요약 · 상태 · 날짜 · 링크만 한 줄로 적는다. 결정 ID는 지금처럼 영역 접두사(D · M · C · T · L · A · Q)를 쓴다.

**옛 기록은 옮기지 않고 그대로 있다**(2026-10-26 결정, [기록 문서 나누기](../../records-split-plan.md) 6.4). 이 기능의 2026-10-26 이전 결정 · 이슈 · 확인 기록은 [decisions.md](../../decisions.md) · [issues.md](../../issues.md) · [feature-checklist.md](../../feature-checklist.md)에 있다.

<!-- 새 결정 예:
## C-33 결정 제목
- 결정:
- 이유:
- 상태 · 날짜:
- 근거 코드 · 규격:
-->

## T-27 피드백 공유 꾸러미

- 결정: 지인에게 모델 피드백을 받을 때 Studio를 밖에 열지 않고, 고른 모델만 담은 **정적 페이지 zip**을 보낸다([studio/share](../../../studio/share/README.md)).
  - 범위(사용자가 추천 범위로 확정): 플레이어 4종 × 맨몸/장비 = P1~P8, 일반 몬스터 M1~M8(무리 7종 + 무장 사자), 배경 오브젝트 O1~O15(야외 · 무대 · 외곽 · 바닥 · Stage 2 소품), 게임 장면 S1~S4. 네임드 · 보스 제외.
  - 카드마다 회전 사진(눈높이 12° · 게임 카메라 55°, 정사영, 16장씩), 3D 보기(GLB, 확대 · 게임 시점 · 게임 크기), 의견 칸(모아서 복사 · 파일 저장). 모든 항목에 번호.
  - md 문서 · 게임 데이터 · 코드 · Studio는 넣지 않는다. 꾸러미 · 사진 · GLB(`studio/share/.build/` · `packs/`)는 커밋하지 않고, 고른 목록 · 만드는 도구 · 안내만 커밋한다.
- 이유: 사용자 요청(2026-10-26) — "md 문서는 공개할 생각없고", 캐릭터 · 배경 모델만 지인 피드백, 보안상 계속 열어 두지 않고 1시간 정도만. 서버 터널보다 파일 전달이 노출 범위가 작고 끝나면 남는 것이 없다. 압축 파일, 확대 · 게임 시점 · 회전 3D가 필요(사용자).
- 상태 · 날짜: `[확정]` 2026-10-26. 첫 꾸러미 `001-models`(31 모델, 장면 4, zip 20.0 MB).
- 근거 코드 · 규격: Studio `share_item`([StudioShare.cs](../../../game/tests/StudioShare.cs), [server.mjs](../../../studio/server.mjs) `/api/share/item`), 장면 [ShareSceneCapture.cs](../../../game/tests/ShareSceneCapture.cs), 만들기 [build-share.mjs](../../../studio/tools/build-share.mjs) · [share-page.html](../../../studio/tools/share-page.html), 목록 [selection.json](../../../studio/share/selection.json). 기록: [log/004](log/004-code-share-pack.md).

## T-28 Studio 기획서 › 레벨디자인 메뉴

- 결정: Studio 위 막대에 **기획서** 메뉴를 두고, 그 아래 첫 항목으로 **레벨디자인**(`/plan/level`, `/plan`은 여기로 이동)을 둔다. 맵(`game/content/data/stages/*_layout.json`)을 골라 두 탭으로 본다.
  - **기능**: 구역마다 크기 · 역할 · 새 경험 · 다시 쓰기 · 섞기 · 길 안내(레벨 가이드 맵, L-33, 설계안 우선 · 없으면 현재 상태), 무리(반응별 무리 수 · 마릿수, 특수 적), 장치 · 표식(맵 표식), 구역 테스트 명령(`-Zone`, 눌러 복사). 맵 요약(구역 수 · 주 경로 길이 · 다음 스테이지 · 가이드 상태)과 가이드 검사 결과.
  - **평면도**: `tools/floorplan/render_floorplan.py`로 그때그때 그린 SVG, 가이드 표시(설계안 · 현재 상태 · 없이) 선택, 확대 · 축소 · 화면 맞춤 · 끌어서 이동 · 새 탭.
  - 렌더러 없이 작업 폴더의 파일을 읽는다(md 메뉴와 같이). 읽기 전용 — 맵 · 가이드는 파일에서 고친다.
- 이유: 사용자 요청(2026-10-27) — "studio에서 기획서 메뉴 하나 생기고 하위로 레벨디자인 메뉴가 기획서의 하위로 생기고 각 맵들을 골라서 기능과 평면도라고 해야하나 지도처럼 러프하게 볼 수 있는 걸 확인할 수 있어야겠어." 가이드 맵 · 평면도가 JSON · SVG 파일로만 있어 한눈에 보기 어려웠다.
- 상태 · 날짜: `[확정]`(메뉴 위치 · 두 탭) 2026-10-27. 표 구성 · 표시 항목은 첫 판.
- 근거 코드 · 규격: [studio/levels.mjs](../../../studio/levels.mjs), [server.mjs](../../../studio/server.mjs) `/plan/level` · `/api/levels*`, [level.js](../../../studio/web/level.js), 검사 [tests/levels.mjs](../../../studio/tests/levels.mjs). 기록: [log/008](log/008-code-studio-level-design.md).
