# 008 · code · Studio 기획서 › 레벨디자인 메뉴, 브라우저가 뜨지 않던 실행기 (T-28, I-130)

- 작성일: 2026-10-27. 종류: code(Studio 서버 · 웹 · 실행기 · 검사). 관련: T-25(Studio), T-26(md 메뉴), **T-28**(새), **I-130**(새), L-33(가이드 맵), [log/005](005-check-studio-ready-timeout.md) · [006](006-check-studio-roster-stale-server.md).
- 요청: "studio 를 키면 … 자동으로 바로 웹브라우저가 뜨는걸로 알았는데 안뜨네?" · "studio 에서 기획서 메뉴 하나 생기고 하위로 레벨디자인 메뉴가 … 각 맵들을 골라서 기능과 평면도 … 지도처럼 러프하게 볼 수 있는 걸 확인할 수 있어야겠어."

## 변경 사항

- **실행기 · 서버(I-130)**: 원인 — 켜져 있던 Studio(2026-10-26 21:39, node PID 7220)의 Godot 렌더러가 없어 `ready:false`로 멈춰 있었고, 실행기는 그 서버를 3분 기다린 뒤 브라우저 없이 끝났다(005에서 진단만 한 상태와 같음). `server.mjs` health에 `renderer`(starting · ready · stopped) · `error`, `Start-Studio.ps1`은 `stopped`면 `/api/reload`를 한 번 요청하고 최대 5분 기다림, 그래도 안 되면 경고와 함께 브라우저를 연다.
- **기획서 › 레벨디자인(T-28)**: 위 막대에 `기획서`(모든 페이지), `/plan` → `/plan/level`. 새 `studio/levels.mjs`(맵 목록 · 맵 읽기 · 평면도), `web/level.html` · `level.js` · `level.css`.
  - 맵 목록: `game/content/data/stages/*_layout.json` 3개(Stage 1 창고 지구 · 2 숲속 벙커길 · 3 협곡 감시소 길, 이름은 `ui.csv`의 `ui_hud_stage*`), 구역 수 · 주 경로 길이 · 가이드 유무.
  - 기능 탭: 요약 칩, 가이드 검사(`guide.py check`의 그 스테이지 줄), 구역 표 — 역할 · 새 경험 · 다시 쓰기 · 섞기 · 길 안내(가이드 맵), 무리(`_encounters.json`의 무리를 구역 다각형으로 나눠 반응별 무리 수 · 마릿수, 특수 적), 장치 · 표식(맵 표식), 구역 테스트 `-Zone` 명령 복사, 구역 메모 줄. 가이드가 없는 맵(Stage 2 · 3)도 무리 · 장치는 보인다.
  - 평면도 탭: `render_floorplan.py`로 요청마다 다시 그린 SVG(`studio/output/levels/`, 커밋 안 함), 가이드 표시 선택, 확대 · 축소 · 화면 맞춤 · 끌어서 이동 · 새 탭.
- **검사** `studio/tests/levels.mjs`(새, 23개), `launcher.mjs`에 렌더러 멈춤 경우 2개.
- 문서: T-28 · I-130 본문 · 색인, Studio README, 레벨 가이드 맵 5장, 체크리스트.

## 검사

- 통과: `node studio/tests/launcher.mjs` `STUDIO_LAUNCHER_PASS 12`(새 2개 포함). 작업 폴더에서 띄운 Studio(포트 4331)로 `levels.mjs` `STUDIO_LEVELS_PASS 23`, `docs.mjs` `STUDIO_DOCS_PASS 25`, `smoke.mjs` `STUDIO_SMOKE_PASS 36`.
- 실제 서버 확인(4331): 렌더러 프로세스를 끄자 health `{"ready":false,"renderer":"stopped","error":"Godot renderer exited (4294967295)"}` → `/api/reload` 뒤 `{"ready":true,"renderer":"ready"}`.
- 실행하지 않음: 실제 `Start-Studio.ps1`로 멈춘 서버를 다시 살리는 끝까지의 실행(명령 실행 권한 확인에서 막힘 — 가짜 서버 실행기 검사와 위 서버 확인으로 나눠 확인). 사용자가 켜 둔 4317 Studio(옛 코드)는 이 작업에서 건드리지 않았다.
- 화면(내장 브라우저, 1440 × 900): 기능 탭 — Stage 1 구역 9개 표, 가이드 검사 오류 0 · 경고 5, C에 경보 차 · D에 창고 간판 · G에 특수 적. 처음에는 표가 좁아 글자가 한 자씩 꺾여 → 고정 열 너비 · 무리 요약 · 구역 메모를 아래 줄로 고침. 평면도 탭 — Stage 1 설계안 표시 49% 맞춤 · 확대 95%, Stage 3 가이드 없이.

## 남은 제약

- 이미 켜 둔 옛 Studio는 새 메뉴 · 상태 표시가 없다 — 한 번 종료(그 터미널에서 Ctrl+C)하고 다시 실행해야 한다.
- 평면도는 python(표준 라이브러리)이 PATH에 있어야 그려진다. 큰 맵은 그리는 데 1 ~ 2초.
- 무리의 구역 배정은 무리 사각형 가운데 점 기준(경계에 걸친 무리는 한쪽에만).
- 읽기 전용 — 맵 · 가이드는 파일에서 고친다.
