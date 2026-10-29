# 019 · code · 바닥 장비 줍기 · 교체와 장비 칸 HUD (C-34 · C-36 · M-41, 구현 계획 Task 3)

- 작성일: 2026-10-30. 종류: code(바닥 장비 · 교체 · HUD · 개발자 모드 표시 · 아이콘 연결 · 검사). 관련: C-34, C-36(옵션 비공개), M-41(개발자 모드), [구현 계획](../equipment-implementation-plan.md) Task 3, [아이콘 지시문](../../../model-briefs/equipment-icons.md), [기록 017](017-code-equipment-data.md) · [018](018-code-equipment-combat.md).
- 사용자: "Task3진행".

## 변경 사항

- **바닥 장비** `Equipment/EquipmentPickup.cs`(새):
  - (장비, 옵션) 한 쌍을 들고 바닥(높이 단의 바닥)에 놓인다. 생김새는 둥근 고리(모자 하늘색 · 갑옷 보라색), 떠서 흔들리는 아이콘, 가까이 가면 보이는 이름.
  - E 안내: 일반 화면은 "○○ 착용 (모자/갑옷)"만. **개발자 모드**에서는 "바닥: 옵션 / 지금: 내 같은 칸 옵션"을 더한다(이 PC 플레이어 기준, 매 프레임 갱신).
- **교체** `World/StageEquipment.cs`(새, `Stage` 부분):
  - `GearEnabled`: 캠페인 · 난전만, 거점(모드 2)은 없음.
  - `SpawnGear(id, at)`: 호스트에서 옵션을 정해 내려놓는다.
  - `TryEquipPickup(pickup, who)`(호스트 · 혼자만):
    - 거르는 것: 이미 사라진 장비(같은 요청 반복), 꺼진 장비, 손 닿는 거리(반경 + 0.5 m) 밖, 쓰러짐 · 죽음 · 붙잡힘, AI 동료, 아이템과 옵션까지 똑같은 장비.
    - 순서: 바닥 장비를 먼저 목록에서 빼고(복제 방지) → 착용 → 같은 칸의 이전 장비를 **같은 옵션 그대로** 발밑(뒤쪽 0.9 m, 막히면 제자리)에 내려놓는다. 팀원이 다시 주울 수 있다.
    - 무기의 무한 공급(`Infinite`) 규칙은 쓰지 않는다.
- **HUD** `UI/HudGear.cs`(새, `Hud` 부분):
  - 체력 줄 옆에 모자 · 갑옷 두 줄(아이콘 · 칸 이름 · 장비 이름, 빈 칸은 "비어 있음"과 흐린 윤곽).
  - **개발자 모드에서만** 그 아래 옵션 줄: "● 조준 중 · 치명타 확률 +6%p", ●는 지금 조건이 맞음, ○는 아님.
  - 거점에서는 숨긴다. 표시 판단은 `Hud.GearInfoVisible` 한 곳(지금은 개발자 모드)이다.
  - 단축키 · 내구도 · 예비 칸은 없다.
- **옵션 문장** `Equipment/EquipmentText.cs`(새): 조건 이름 + 효과 이름 + 값(`+6%p` / `+20%`)을 이어 붙인다. 지금 켜졌는지는 사격과 같은 기준으로 본다(조준 중, 집중 한 발 = 집중 준비).
- **개발자 모드 값** `GameSession.DeveloperMode`(저장 안 함). 게임 설정 체크박스와 방 동기화는 Task 4에서 한다.
- **아이콘**:
  - Codex가 만든 `prototypes/ui-icons/tools/equipment_icons.py`를 실행해 `game/assets/icons/equipment/` 9장을 만들었다.
  - `equipment.csv`의 아이콘 칸을 채웠다.
- 언어(kor · eng `equipment.csv`): `hud_gear_head` · `hud_gear_armor` · `hud_gear_empty` · `fmt_use_pickup_gear` · `fmt_gear_compare` · `fmt_gear_option` · `fmt_gear_value_pp` · `fmt_gear_value_pct`.

## 검사

- 실패 먼저: 검사를 먼저 쓰고 빌드 → `EquipmentPickup` 없음으로 빌드 실패(CS0246) 확인.
- `EquipmentPickupCheck`(새) 헤드리스: 첫 실행 종료 0, `EQUIPMENT_PICKUP_PASS 25`.
  - 모드: 캠페인 · 난전은 장비 있음 · HUD 칸 보임, 거점은 없음 · 숨김.
  - 줍기 · 교체:
    - 빈 칸 착용과 같은 요청 반복 무시
    - 두 번째 모자로 교체 → 이전 모자가 같은 옵션으로 발밑 바닥에 떨어짐 → 팀원이 줍기
    - 갑옷은 따로
  - 거부: 똑같은 장비, 거리 밖, 꺼짐, 클라이언트 역할, 쓰러진 사람.
  - 일반 화면: HUD에 이름만, 옵션 줄 없음. HUD · 일시정지 화면 · 바닥 안내 어디에도 옵션 문장이 없다.
  - 개발자 모드: 칸마다 옵션 줄, 바닥 안내에 바닥/지금 비교, 조준 옵션은 비조준 ○ · 조준 ●.
- 창 모드 첫 실행 실패: 떨어진 모자가 팀원의 손 닿는 거리 밖이라 "팀원이 줍기"가 거부됐다. 확인하려는 것은 옵션 유지와 팀원 획득이라, 팀원을 모자 옆으로 옮겨 놓고 줍게 검사를 고쳤다. 고친 뒤 창 모드 · 헤드리스 모두 25 통과.
- 렌더(창 모드, 1600 × 900, `output/gear-hud-normal.png` · `gear-hud-dev.png` · `-eng`):
  - 일반: 장비 칸에 아이콘 · 이름만 보인다.
  - 개발자 모드 첫 렌더에서 옵션 줄이 "…"로 잘렸다. 옵션 줄의 자르기와 최소 폭을 빼서 줄이 넓어지게 고쳤다. 다시 렌더해 한국어 · 영어 모두 끝까지 보이는 것을 확인했다.
  - 바닥 장비는 고리와 아이콘(초록 비니)이 보인다. 겹친 글자는 검사가 연달아 집으며 띄운 획득 글자다.
- `InteractionUiCheck`(창 모드): 37 통과.
- 회귀(헤드리스, 모두 종료 0 · 실패 0 · 누수 0):
  - 장비: `EquipmentCheck` 51 · `EquipmentCombatCheck` 25 · `EquipmentPickupCheck` 25
  - `WeaponAimCheck` 32 · `ModeCheck` 147 · `SquadCheck` 52
  - 네트워크: `NetSyncCheck` 32 · `PartyNetCheck` 37 · `OnslaughtNetCheck` 29
  - `Integration` 149
  - 장비 줍기 · 모드 · 통합 · 동기화 검사는 오류 줄 0.

## 남은 제약

- 멀티 동기화는 Task 4다. 클라이언트에서는 바닥 장비 생성 · 교체 · 착용이 아직 보이지 않는다. 지금은 호스트 · 혼자만 동작한다. 개발자 모드 체크박스와 방장 따름도 Task 4다.
- 실제 캠페인 · 난전에 장비를 놓는 것은 랩 장면(Task 5)과 아이템 배치(L-40) 작업이다. 그래서 지금 일반 플레이에서는 장비를 만날 수 없다.
- 장비 이름 · 아이콘 · 고리 색은 첫 결과다. 사용자 피드백에 따라 Codex로 고친다. 장비를 쓴 캐릭터 외형 변경은 후속 단계다(C-34).
