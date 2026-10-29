# 이동 구현·장비 설계 main 병합 검증

- 요청: 현재 작업 커밋·머지·푸시. 작업 브랜치는 모든 변경이 이미 커밋된 깨끗한 상태였다.
- 병합: `codex/auto-run-roll-charges`의 이동 구현과 장비 설계9개 커밋을 main `99a1361`→`df25700`으로 fast-forward 병합했다. 원격 main에는 이후 변경이 없었고 충돌도 없었다. 실제 장비 코드는 아직 미구현이다.
- 통과: 병합된 main에서 빌드 경고0·오류0, Godot 가져오기, Integration149·MovementCheck22·MovementEchoCheck1·MovementNetCheck12·NetCodecCheck24. 별도60fps 조건에서 NetSyncCheck32·EnemyAttackCheck79·WeaponAimCheck32. 통과 실행 로그에 ERROR/WARNING 또는 종료 경고 없음.
- 실패: 기본 무제한 프레임 NetSyncCheck가 평균0.46m/최대1.93m로 실패·종료1. 코드/인수 변경 없는 단독 재확인에서도 평균5.77m/최대10.62m로 실패·종료1. 당시 최신 수신 표본 시각이 렌더 시각보다 뒤처졌고 실제 표본 시각과의 위치 오차는 더 작았다. 이것만으로 원인 확정은 하지 않는다.
- 진단: 기존 미해결 I-72에 같은 간헐 실패가 기록되어 있다. 이번 병합은 StageNet/NetTrack/NetSyncCheck를 변경하지 않았다. 통과할 때까지 같은 조건을 반복하지 않고60fps로 조건을 고정해 한 차례 비교했다. 원래 평균0.3m/최대1m 기준을 유지했다.60fps 통과를 I-72 해결로 취급하지 않는다.
- 산출물: 기본 실행 `game/output/movement-merge-*.log`, 단독 실패 `movement-merge-recheck-NetSyncCheck.log`,60fps 비교 `movement-merge-60fps-*.log`. 로컬 산출물이므로 커밋하지 않는다.
- 문서 검사: 병합된 장비 설계·수치·구현 계획·결정의 상대 링크 대상과 git diff --check 통과. 이번 병합에서는 렌더를 다시 실행하지 않았으며 기존006의 실제 화면 확인을 유지한다.
- 결정·이슈: 기능 결정 변경 없음. 기존 I-72에 재현 결과를 추가하고 미해결 상태 유지. 새 이슈 ID는 발급하지 않는다.
- 남은 제약: 무제한 프레임 멀티 위치 검사 실패/I-72, 실제 두 PC·인터넷 검증, 장비 코드 구현은 후속이다. 작업 폴더는 후속 장비 구현과 기존 로컬 검사 산출물 보존을 위해 유지한다.
