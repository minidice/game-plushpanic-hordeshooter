# 기술 · 도구 — 이슈 본문

새 이슈는 여기 ID 아래에 쓰고, [전체 색인](../../issues.md)에는 ID · 한 줄 증상 · 상태 · 날짜 · 링크만 한 줄로 적는다. 새 이슈 ID는 전체 번호(`I-126`부터)를 쓰고, **색인에 한 줄을 먼저 추가해 커밋 · 푸시한 뒤** 본문을 쓴다(먼저 예약). 푸시가 막히면 받아서 다음 번호로 바꾼다.

**옛 기록은 옮기지 않고 그대로 있다**(2026-10-26 결정, [기록 문서 나누기](../../records-split-plan.md) 6.4). 이 기능의 2026-10-26 이전 결정 · 이슈 · 확인 기록은 [decisions.md](../../decisions.md) · [issues.md](../../issues.md) · [feature-checklist.md](../../feature-checklist.md)에 있다.

<!-- 새 이슈 예:
## I-126 증상 한 줄
- 증상:
- 원인(추정이면 추정):
- 해결:
- 재발 방지 · 검사 경로:
- 남은 제약:
-->

> 2026-10-26 검토(T-27 공유 꾸러미): 새 이슈 없음. 장면 촬영 도구를 처음 돌릴 때 장면을 바꿀 때마다 `ObjectDisposedException`이 났으나 동료 AI(`SquadBrain`)를 멈추자 사라졌다. 촬영 도구가 Stage를 바로 지우고 새로 만드는 방식에서만 확인했고 게임의 실제 전환에서 나는지는 확인하지 않았다(추정 · 미확인, [log/004](log/004-code-share-pack.md)).

## I-130 Studio를 다시 실행해도 브라우저가 뜨지 않음(렌더러가 멈춘 기존 Studio)

- 증상: `studio/Start-Studio.ps1`을 실행해도 웹 브라우저가 열리지 않는다(사용자 보고, 2026-10-27). 실행기는 "Waiting for existing Studio"에서 3분 기다린 뒤 오류로 끝난다.
- 원인(확인): 2026-10-26 21:39에 켠 Studio 서버(node, 포트 4317)는 살아 있었지만 Godot 렌더러 자식 프로세스가 없어 `/api/health`가 `ready:false`였다. 서버는 렌더러가 꺼지면 `ready`만 false로 두고 다시 켜지 않으며, 실행기는 기존 서버의 `ready:false`를 "시작 중"과 "멈춤"으로 구분하지 못하고 3분 동안 기다리기만 한 뒤 브라우저를 열지 않고 끝났다. 같은 상태를 [log/005](log/005-check-studio-ready-timeout.md)(2026-10-26)에서 진단했으나 고치지 않았다. 렌더러가 처음 꺼진 이유는 이번에도 미확정.
- 해결: ① 서버 `/api/health`에 `renderer`(starting · ready · stopped)와 마지막 `error`를 더함 — 준비된 뒤 렌더러가 꺼지거나 렌더 시간 초과로 멈추면 `stopped`. ② 실행기는 기존 Studio가 `stopped`면 `/api/reload`로 렌더러 재시작(빌드 · 가져오기 · 시작)을 한 번 요청하고 최대 5분 기다린다. ③ 기다려도 준비되지 않으면 오류로 끝내지 않고 경고와 함께 브라우저를 연다(md · 기획서는 렌더러 없이 동작, 미리보기는 페이지의 다시 읽기로 재시도). 서버가 아예 없으면 이전처럼 오류.
- 재발 방지 · 검사 경로: `node studio/tests/launcher.mjs` — "a stopped renderer is restarted once, then the page opens"(가짜 서버). 실제 서버에서 렌더러를 끄면 health `stopped` · `Godot renderer exited (…)`, `/api/reload` 뒤 `ready` 확인([log/008](log/008-code-studio-level-design.md)).
- 남은 제약: 이 수정 전에 켠 Studio(health에 `renderer`가 없음)는 재시작을 요청하지 못하고 3분 뒤 경고와 함께 브라우저만 연다 — 한 번 종료하고 다시 실행해야 한다. 서버 코드가 바뀌어도 이미 켠 서버는 옛 코드 그대로다([log/006](log/006-check-studio-roster-stale-server.md)). 렌더러가 꺼진 근본 원인은 모름.
