# UI 일관성·메뉴 후속 main 통합 검사

사용자 요청: 지금까지 UI 변경을 main에 병합하고 푸시. 관련 M-38 · M-33 · M-36 · L-35.

- 원격을 fetch한 결과 로컬 main과 origin/main은 `bfe6ea2`로 일치했다. `codex/ui-consistency`의 `550da20`(공통 스타일·월드 상호작용)과 `6a0a309`(메뉴·진입 전 화면)을 main에 fast-forward 병합했다. 충돌 없음.
- main 작업 폴더 `D:/Project/Game/game-hordeshooter`에서 아래 검사를 순차 실행했다. 기존 미추적 `.codex-remote-attachments/`는 변경·커밋하지 않았다.

## 검증

- `game/Start-Game.ps1 -Test`: 빌드 오류0·경고0, 리소스 가져오기 오류·경고 없음, Integration **149 통과**, 종료0. `game/output/ui-main-integration.log`, `game/output/import.log`.
- 별도 전용 장면을 Godot4.7.2 .NET / Forward+ /1280×720 실제 창으로 실행했다.
  - `MenuCheck.tscn`: **45 통과**, 종료0. `game/output/ui-main-MenuCheck.log`.
  - `InteractionUiCheck.tscn`: **34 통과**, 종료0. `game/output/ui-main-InteractionUiCheck.log`.
  - `ReticleCheck.tscn`: **52 통과**, 종료0. `game/output/ui-main-ReticleCheck.log`.
  - `ReloadUiCheck.tscn`: **42 통과**, 종료0. `game/output/ui-main-ReloadUiCheck.log`.
  - `GraphicsOptionsCheck.tscn`: **37 통과**, 종료0. `game/output/ui-main-GraphicsOptionsCheck.log`.
- 위 검사 로그에 엔진 오류·종료 경고 없음. `git diff --check` 통과.
- 실제 main 렌더 [출격 준비](../../../../game/output/menu-single.png), [상호작용·장전](../../../../game/output/interaction-ui-hold-and-reload.png)을 열어 공통 청백색 선택/진행 및 패널 배치를 확인했다. 산출물은 `output/`에만 보관한다.

## 기록 검토·남은 범위

기능 결정·규격·변경 기록은 두 기능 커밋에 포함돼 있다. 이번은 통합 재검증으로 새로운 제품 결정·이슈 ID나 기능 완료 상태 변경이 없다. 실제 두 PC 지연·패키지 내보내기·장시간 부하는 이번 통합 검사 범위 밖이다. 작업 worktree는 이전 렌더 비교와 후속 시각 조정을 위해 유지한다.
