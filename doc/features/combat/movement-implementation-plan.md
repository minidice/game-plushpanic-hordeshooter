# 걷기·자동 달리기·구르기 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 장비 슬롯 없이 합의한 이동과 구르기를 실제 플레이할 수 있게 한다.
**Architecture:** Player의 이동 상태와 충전 타이머를 새 partial 파일로 분리하고 기존 입력·무기·피격·이동 장치에 연결한다. HUD는 실제 남은 충전과 다음 회복을 표시한다. 기존 소유자 이동/호스트 피해 권한을 유지한다.
**Tech Stack:** Godot 4.7.2 .NET, C#, 전용 Godot 검사 장면.
**Spec:** [장비·이동 설계](equipment-movement-plan.md) 중 이동·구르기만.

## Global Constraints
- 걷기3.6m/s, 자동 달리기4.2m/s, 대기2초. 무기·상태·카드 배율은 기존 효과를 유지한다.
- 구르기2회, 종료부터 한 칸씩6초. 두 번째 구르기 중 기존 회복은 계속되며 초기화하지 않는다.
- 기본 피해 무적 없음. 일반 피격은 피해·달리기 대기 갱신만, 구르기 동작은 계속.
- 장비 슬롯·옵션·유료·추가 충전 아이템은 범위 밖. 수치는 임시이며 기록과 함께 커밋.

## Review Focus
- 일반 근접 적의 명중 경로 자체가 구르기를 무시하지 않는지.
- 발사 입력만이 아니라 실제 점사/충전 발사에도 걷기 대기가 갱신되는지.
- 쓰러짐·사망·곤돌라의 구르기 취소가 충돌 마스크와 충전을 고장내지 않는지.
- 멀티 피해 수신과 대기 중 입력이 있는 충전 보정이 소유자의 상태를 되돌리지 않는지.
- 남은 충전이 있는 동안 HUD가 재사용 불가로 보이지 않는지.

## Task 1: 이동 상태·충전·표시·동기화와 플레이 검증

**Files:** Player.cs, 새 PlayerMovement.cs, PlayerNet.cs, PlayerDowned.cs, PlayerGondola.cs, WeaponController.cs, Enemy.cs, HudDock.cs, 필요 시 NetMessages.cs/NetLobby.cs; tests/MovementCheck.cs/.tscn, Integration.cs 및 관련 회귀 검사; 게임 README·설계·결정·작업 기록.
**Interfaces:** Player의 기존 PressRoll/SetControls/HoldAim/TakeDamage를 유지. RollCharges(int), RollRechargeLeft(float), AutoRunDelayLeft(float), IsAutoRunning(bool)를 제공. RollCooldownLeft는 기존 진단 호환용 다음 칸 회복시간. HUD는 RollCharges와 RollRechargeLeft를 사용.

- [x] 기준 Integration 실행 결과 확인.
- [x] MovementCheck에서 실제 이동 속도, 조준 유지/해제, 실제 발사/피격 대기, 연속2회·3번째 거절, 종료부터 회복·추가 사용 중 지속·다음 칸 회복, 일반 피격 중 동작 지속을 검증하고 변경 전 실패 확인.
- [x] PlayerMovement에 이동 제한 대기와 충전 로직을 구현하고 실제 무기 발사 및 피격에 연결.
- [x] 일반 적의 근접 명중과 Player의 피해 차단에서 구르기 무적을 제거. 기존 특수 강제 이동/잡기 면역은 이번 범위에서 유지하고 기록.
- [x] 충전 HUD, 멀티 피격/충전 동기화, 취소·부활 처리를 연결. 변경 전 규칙을 검사하던 Integration을 새 규칙으로 갱신.
- [x] MovementCheck, Integration, EnemyAttackCheck, WeaponAimCheck, NetCodecCheck, NetSyncCheck, TraversalQualityCheck, ReloadUiCheck 실행. 전용 검사 창 렌더와 HUD 확인.
- [x] 새 컨텐츠 없이 변경 범위·문서·UID를 검토하고 새 리뷰어의 코드 검토를 받는다. 결과를 기록하고 커밋.
- [x] 검증한 버전을 실제 창으로 실행해 사용자에게 플레이 경로 제공.

## 실행 기록과 구현 판단
- 작업 브랜치: codex/auto-run-roll-charges. 기반99a1361. 사용자 구현 요청에 따라 현재 채팅에서 직접 실행한다.
- Ruling: 첫 생성은 충전2회·걷기로 시작, 부활은 소비한 충전을 유지하고 대기를 갱신, 사망/다운은 충전을 정지한다. 기존 객체 생애주기를 유지하며 무료 충전 악용을 피한다.
- Ruling: 치료·장전·투척 조준·충전 공격·상호작용의 기존 감속/입력 잠금을 유지하며 해당 상태에서는 달리기 전환을 막는다.
- Ruling: 무기/카드 보정식은 유지하되 일반 이동은4.2m/s 상한으로 제한한다. SMG110%가4.62m/s로 감염체4.55m/s를 넘는 것이 검사에서 재현되어, 좀비보다 느려야 한다는 사용자 요구를 우선했다. 카드별 양수 이동 보너스는 상한 아래에서만 유효하며 구르기 거리 효과는 유지한다.
- Ruling: 보통 피격 외 Launch/잡기 처리 변경은 미정이므로 기존 특수 면역을 유지한다. 보통 근접 피해는 별도로 검사한다.
- 진행: 기준 Integration149 통과. MovementCheck의 걷기·NetSyncCheck의 충전 수신·MovementCheck의SMG 속도 상한이 변경 전 각각 실패했으며 해당 경로를 구현했다. 구현 후 기본 이동21·코덱24·네트워크35·근접79·조준32·통합149·이동 장치55 통과. 추가 상한/스냅샷 입력보호와 최종 렌더까지 확인했다. 최종 결과는 작업 기록006에 보존한다.
- 리뷰 후 판단: 지연 특수무기 추가 발사는 원문 요구의 마지막 실제 발사 기준에 포함되므로 리뷰의 Minor를 이번 기능의 계약 누락으로 재분류해 수정한다. MovementEchoCheck에서 추가 발사 직후 대기1.40초(기대1.9초 이상)를 재현한 뒤 Echo에도 대기 갱신을 연결한다.
- 검사 구성 판단: 새 멀티 검사를 적이 살아 있는 NetSyncCheck 중간에 넣으면 기존 보간 및 타이밍 검사에 간섭할 수 있어 별도 MovementNetCheck로 분리했다. 최종 전용12개·기존NetSync32개 통과. 중간 보간/피격 실패 원인을 제품 버그 해결로 단정하지 않고 작업 기록에 보존한다.
