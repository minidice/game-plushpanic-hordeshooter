# HUD·조준·월드 표시 main 병합 검증

관련 **M-33 · C-30 · A-19**. 사용자의 커밋·병합·푸시 요청에 따라 `codex/hud-light-polish`의 `4d8ea63`, `da73d9e`, `cdc786d`를 main에 병합했다. 변경 내용과 최초 검사는 [002](002-code-hud-light-polish.md), [003](003-code-reticle-clarity.md), [004](004-code-world-marker-polish.md)를 따른다.

## 병합과 다른 작업 보존

- fetch 시 로컬/원격 main은 `d59445a`. UI 브랜치는 해당 커밋 위3개 커밋이며 새 원격 변경은 없었다.
- `a634131` 병합 커밋 생성. 병합 트리는 검증한 `cdc786d`와 동일하다. 제품 코드의 추가 수정은 없다.
- main의 다른 작업 문서4개는 경로 한정 stash 후 복원했다. 체크리스트 첫 부분의 양쪽 추가 줄이 겹쳐 모두 보존하여 해소했다. 기존4개 파일의 추가/삭제 줄이 보관 전과 정확히 같은지 검사했고 모두 통과했다. 원래처럼 미스테이징으로 두었으며 기존 미추적 첨부/기획/Studio 기록도 포함하지 않았다. 임시 stash는 복원 검증 후 제거했다.

## 병합된 main에서 재검증

Windows / Godot4.7.2 .NET. 실행 위치 `D:/Project/Game/game-hordeshooter`, 게임 검사와 가져오기는 순차. 로그는 로컬 `game/output/`이며 커밋하지 않는다.

| 검사 | 결과 | 로그 |
|---|---|---|
| `dotnet build game/HordeShooter.csproj --nologo -v q` | 통과, 오류0·경고0 | 명령 출력 |
| 최초 editor import | 비정상 종료 `-1073741819`(0xC0000005), WAV reimport 도중 로그 끝남 | `hud-merge-import.log` |
| 동일 editor import 재실행 | 정상 종료0 | `hud-merge-import-retry.log` |
| `GroundMarkerCheck` 실제1280×720 렌더 | **29 통과** | `hud-merge-ground-render.log` |
| `ReloadUiCheck` headless | **38 통과** | `hud-merge-ReloadUiCheck.log` |
| `WeaponAimCheck` headless | **32 통과** | `hud-merge-WeaponAimCheck.log` |
| `Integration` headless | **149 통과** | `hud-merge-Integration.log` |

최종 재가져오기/게임 검사 로그에 `ERROR`·`WARNING`·실패·종료 누수 없음. `git diff --check` 통과.

실제 `ground-marker-slam.png`에서 붉은 범위 경계·진행 채움·지형 차폐·HUD를 확인했다. 최초 가져오기 비정상 종료 원인은 미확정이며, [002 기록](002-code-hud-light-polish.md)의 같은 종료 코드 이력과 구분해 남긴다. 재실행 통과를 근거로 엔진/환경 문제를 해결했다고 주장하지 않는다.

## 기록 검토·남은 범위

병합·검증만 수행했고 기존 결정/기능 현황을 추가로 바꾸지 않아 새 결정·제품 이슈 ID를 발급하지 않았다. 기존 단차 투영·실제2PC·내보내기·장시간 성능 및 최종 외형 수락 제약은 유지한다. 이번 검증 기록만 별도 커밋하며 다른 작업의 미커밋 문서를 섞지 않는다.
