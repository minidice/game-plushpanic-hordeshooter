# 공통 UI 스타일과 월드 상호작용

사용자 요청: 세 참고 영상의 UI/상호작용 추천안으로 진행하고, 최근 총 조준점 변경을 포함해 전체 UI 품질과 느낌의 일관성을 유지한다. 관련 M-38 · M-33 · M-36 · L-35. 기준 `origin/main bfe6ea2`, 브랜치 `codex/ui-consistency`.

## 변경

- `UiTheme`에 청백색 선택/진행, 어두운 반투명 면, 글자·테두리·둥근 모서리·전환 시간을 모았다. 메뉴/ESC/그래픽 설정/장비/인벤토리/동료 체력 바/도약 표시를 공유 스타일로 정돈했다. 피해·치료·집중·등급 의미색은 유지한다.
- 최근 조준점의 무기별 실루엣, 탄창 소모 방향, 발사 방식, 장전/취소 및 공격 불가 표시는 유지하며 공유 재질을 참조한다. 월드 안내를 조준 레이어 아래에 둔다.
- `HudInteraction`은 발견 표식 → 실제 선택 대상 한 개의 행동/E → 실제 홀드·구조 진행/피격 중단/완료를 표시한다. 접근/이탈은 짧은 페이드, 사용 불가 키는 즉시 흐리게 한다. 긴 제목/화면 경계 보정, 벽·층 너머 발견 표식 제한, 메뉴/삭제/쓰러짐 정리를 추가했다.
- L-35 머리 위 바와 하단 퍼센트 유지. `Stage.HoldSubmitted`는 클라이언트가 제출한 대상을 기억하는 표시용 알림으로, 실제 상태 확인 없이 완료를 만들지 않는다. 한/영 문구 추가.
- `GraphicsOptionsCheck`의 클릭 좌표를 `Viewport.PushInput(..., true)`로 전달한다. 기존 `GetGlobalRect` 논리 좌표를 물리 창 좌표로 주입하던 검사 경로를 수정해 1280/1600 창에서 실제 버튼 클릭을 검사한다.

## 검사

Godot 4.7.2 .NET, Windows, Forward+ / RTX 5070 Ti. 빌드는 `dotnet build game/HordeShooter.csproj --nologo -v q`. 장면은 `Godot --path game [--headless 또는 --resolution 가로x세로] res://tests/<이름>.tscn`으로 각각 실행했다. `Start-Game.ps1 -Test`가 전용 장면을 실행한 것으로 취급하지 않았다.

| 검사 | 결과 | 로컬 로그 (`game/output/`) |
|---|---|---|
| 최종 빌드 | 오류0·경고0, 종료0 | `ui-final-build.log` |
| InteractionUiCheck headless | 34 통과, 종료0 | `ui-review-green.log` |
| InteractionUiCheck 실제1280×720, `-- --english --bright` | 34 통과, 종료0 | `ui-final-interaction-1280.log` |
| InteractionUiCheck 실제1600×900, 한국어 | 34 통과, 종료0 | `ui-final-interaction-1600.log` |
| ReloadUiCheck headless / 실제1280×720 | 38 / 42 통과, 종료0 | `ui-final-ReloadUiCheck.log`, `ui-final-reload-render.log` |
| ReticleCheck 실제1280×720 | 52 통과, 종료0 | `ui-final-ReticleCheck-render.log` |
| MenuCheck headless | 44 통과, 종료0 | `ui-final-MenuCheck.log` |
| GraphicsOptionsCheck 실제1280×720 /1600×900 | 각각37 통과, 종료0 | `ui-final-GraphicsOptionsCheck-render.log`, `ui-final-graphics-1600.log` |
| Integration headless | 149 통과, 종료0 | `ui-final-Integration.log` |
| NetSyncCheck headless | 32 통과, 종료0 | `ui-final-NetSyncCheck.log` |

위 최종 성공 로그에 엔진 오류·종료 경고 없음. `git diff --check`와 변경 문서의 파일 링크·M-38 색인·추가 번역 키의 한/영 일치를 확인했다.

### 실패·리뷰 처리

- 처음 실제 Stage에서 월드 안내 부재를 실패로 확인한 뒤 구현했다(`ui-red.log`). 완료 표시가 반복되는 상태, 빈 구조 대상 이름도 실패로 재현 후 수정했다(`ui-state-red.log`, `ui-name-red.log`).
- 독립 리뷰의 두 발견: 클라이언트 요청 후 늦은 상태 확인에서 완료 누락, 타인이 구조 중일 때 로컬 피격을 구조 취소로 오표시. `ui-remote-red.log`, `ui-revive-red.log`로 재현하고 제출 대상 기억/실제 구조 소유자 구분을 추가했다. 최종34검사에 두 회귀를 포함한다.
- 조준 전용 검사에서 새 발견 표식이 조준 격리 배경 위로 그려졌다. 월드 안내를 조준 아래로 정렬한 후52검사 통과. 무기 조준 형상을 변경해 검사를 맞추지 않았다.
- 그래픽 검사 headless 실행은 나무 그림자의 MultiMesh 변환을 단위행렬로 읽어 크기 검사에 실패했다. 기준 코드에서도 동일 실패를 별도 재현했다(`ui-baseline-graphics.log`). 실제 렌더에서는 정상 변환과 그림자 크기를 확인했다. headless 그래픽 검사를 통과했다고 주장하지 않는다.
- 실제1280 창의 그래픽 검사 클릭 실패는 검사 입력 좌표 문제로 확인했다. 논리 좌표 주입 수정 후 두 창 크기에서37검사 통과. 제품의 그림자/입력 규칙 변경은 없다.

## 실제 렌더

아래 산출물은 무시되는 `game/output/`에 있으며 `InteractionUiCheck`를 실행하면 재생성된다.

- [상호작용과 장전](../../../../game/output/interaction-ui-hold-and-reload.png): 청백색 진행 재질, 머리 위 바·대상 안내·최근 장전 링의 일관성.
- [영문·밝은 배경](../../../../game/output/interaction-ui-hold-and-reload-eng-bright.png):1280×720에서 글자·진행 대비.
- [피격 중단](../../../../game/output/interaction-ui-interrupted.png), [구조](../../../../game/output/interaction-ui-revive.png), [발견](../../../../game/output/interaction-ui-discovery.png), [긴 제목](../../../../game/output/interaction-ui-long-label.png).
- [메인 메뉴](../../../../game/output/interaction-ui-menu.png), [인벤토리](../../../../game/output/interaction-ui-inventory.png), [ESC 그래픽 설정](../../../../game/output/interaction-ui-pause-graphics.png): 공통 면/선택/글자 확인.

영문 실행은 상호작용 장면을 영문으로 확인한다. 이후 메뉴 검사 초기화는 기본 한국어로 돌아오므로 모든 메뉴의 영문 검증으로 확대하지 않는다.

## 기록 검토·남은 제약

- [공통 규격](../ui-style-spec.md), [설계·실행 계획](../interaction-ui-plan.md), 기능 결정 M-38/상위 색인/체크리스트/무기 규격/L-35 표시 설명을 함께 갱신했다. 기존 M-33/M-36/L-35 계약 변경 없음. 개발 중 발견을 수정·검증한 기록이며 별도 새 제품 이슈 ID를 만들지 않았다.
- 실제 E 사용 거리/벽/층 판정, 협동 권한, 원격 진행 동기화는 유지한다. 발견 표식 가시성 제한과 실제 E 선택 안내를 구분한다. NetSyncCheck와 클라이언트 요청/확인 시뮬레이션은 통과했지만 두 PC의 실제 지연/끊김 검사는 미실행이다.
- 표시 치수·거리·시간은 제작값이며 사용자 최종 외형 수락은 별도다. 모든 전투 조합에서 자동 무겹침을 보장하지 않는다. 패키지 내보내기/장시간 부하는 이번 검사 범위 밖이다.
- 코드와 기록은 같은 기능 커밋으로 보관한다. main 병합·원격 푸시는 이 기록에 포함하지 않는다. worktree는 렌더 비교와 후속 조정을 위해 유지한다.
