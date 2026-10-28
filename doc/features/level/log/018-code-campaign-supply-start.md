# 캠페인 탄약 거점 제한·몽둥이 시작

- 날짜: 2026-10-29. 관련: L-12 · L-34. 기준 `901c473`.
- 사용자 요청: 캠페인 탄약 충전 배치를 세이프하우스/구역 사이 거점으로 제한하고, 캠페인1 최초 시작 무기를 몽둥이로 바꾸며 기존 시작 공격력의 절반을 적용한다.

## 변경

메뉴의 캠페인(큰 Stage 1~3) 탄약 더미만 정리했다. 다른 보급 물품과 같은 총을 무기 테이블에서 집을 때 탄약을 채우는 C-33 규칙은 유지한다. 별도 짧은 시나리오·거점·난전 보급은 바꾸지 않았다.

| 스테이지 | 탄약 더미 전 → 후 | 남긴 보급 지점 ID |
|---|---|---|
| 1 | 17 → 5 | `p_base`, `p6_base`, `c1_base`, `guard_post`, `arrival` (P3/P6/C1/E/H) |
| 2 | 5 → 3 | `drain_kit`, `ranger_post`, `arrival` |
| 3 | 5 → 3 | `hatch_kit`, `watch_post`, `safehouse` |

이외 지점의 `ammo` 항목을 제거하고 비게 된 보급 지점만 삭제했다. 남긴 곳은 레이아웃의 안전/정비 거점과 대응한다.

`wooden_club`을 추가했다. 기본 피해8.5로 이전 시작 프라이팬17의 절반이다. 목재 모델·아이콘·한영 이름을 함께 추가했다. `WeaponController.Setup`에서 시나리오 모드의 `LayoutMap.StageNumber == 1`일 때만 기본 보조 슬롯을 바꾸므로 새 원격 생존자도 같은 시작 무기를 받는다. 이후 `StageCarry`가 기존 장비를 적용하고 첫 권총은 정상 획득 경로로 몽둥이를 교체한다. 기존 프라이팬과 다른 모드의 기본 무기는 유지한다.

## 검증

Windows / Godot4.7.2 .NET. 빌드 오류0·경고0, 종료0 (`campaign-change-build.log`). 전용 장면은 `Godot --path game [--headless 또는 --resolution 1600x900] res://tests/<장면>.tscn`으로 따로 실행했다.

| 검사 | 결과 | 로그 (`game/output/`) |
|---|---|---|
| CampaignPacingCheck 실제1600×900 | 19 통과, 종료0 | `campaign-final-CampaignPacingCheck.log` |
| SupplyCheck headless | 12 통과, 종료0 | `campaign-regression-SupplyCheck.log` |
| StageTransitionCheck headless | 225 통과, 종료0 | `campaign-regression-StageTransitionCheck.log` |
| Integration headless | 149 통과, 종료0 | `campaign-final-integration.log` |
| NetSyncCheck headless | 32 통과, 종료0 | `campaign-final-net.log` |

CampaignPacingCheck는 세 보급 JSON의 유효성·허용 탄약 지점, 실제 생성된 탄약 수/좌표, 시작 무기/피해 비율, 원격 생존자, 권총 교체, 다음 스테이지 무기 이월, 다른 모드 기본값과 짧은 시나리오 보존을 확인한다. 변경 전 탄약 정책 검사 실패, 데이터 변경 후 몽둥이 시작 검사 실패, 구현 후 전체 통과를 확인했다 (`campaign-red-CampaignPacingCheck.log`, `campaign-club-red.log`). 최종 실행에서 엔진 오류·종료 경고 없음.

[실제 시작 렌더](../../../../game/output/campaign-start-club.png)에서 몽둥이 모델·슬롯 아이콘·한글 이름을 확인했다. `CampaignPacingCheck` 창 모드 실행으로 재생성하며 `output/`은 커밋하지 않는다. 상호작용 UI 변경 검증은 [메뉴 UI 기록017](../../menu-ui/log/017-code-interaction-progress-dock.md)에 기록했다.

## 기록·남은 제약

기존 L-12/L-34 현재 결정과 이전/이후·사용자 근거를 기록하고 색인·체크리스트·튜토리얼/무기 규격을 갱신했다. 새 결정/이슈 ID는 필요하지 않다. 옛 배치 이력은 보존한다.

전체 캠페인 완주에 따른 탄약 부족/몽둥이 체감 밸런스는 아직 검증하지 않았다. 피해는 기본 데이터 비율 검사이며 난이도·버프를 포함한 모든 적 대상 실타격 비교는 수행하지 않았다. 두 PC 실접속과 패키지 내보내기는 미검증이다. 코드와 기록을 같은 커밋에 포함하고 이번 후속 변경의 main 병합·원격 푸시는 하지 않았다.
