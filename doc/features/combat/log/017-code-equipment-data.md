# 017 · code · 장비 옵션 데이터 다섯 표와 조건 평가 (C-34 · C-36, 구현 계획 Task 1)

- 작성일: 2026-10-30. 종류: code(데이터 · 불러오기 · 착용 · 평가 · 검사). 관련: C-34(두 슬롯 · 9개 테스트 조합), **C-36**(다섯 표 · 고정 값 + 그룹 비중), [설계](../item-option-data.md) 2 · 3장, [구현 계획](../equipment-implementation-plan.md) Task 1.
- 사용자: "커밋하고 장비 구현 계획 Task 1(옵션 데이터 표와 조건 평가)까지 이어서 진행".

## 변경 사항

- **데이터** `game/content/data/equipment/`(새, 각 파일 둘째 줄에 한국어 설명 주석):
  - `option_stats.csv`: 효과 4개 — `crit_chance`(pp, 0~100) · `crit_damage`(pct, 0~100) · `spread_reduction` · `recoil_reduction`(pct, 0~90), 모두 `head|armor`.
  - `option_conditions.csv`: 조건 3개 — `always` · `aiming` · `focused_shot`, 모두 변수 없음(`none`).
  - `options.csv`: C-34에서 수락한 9개 조합(치명타 확률 3 · 6, 치명타 피해 10 · 20 · 30, 탄퍼짐 10 · 15, 반동 15 · 25), 모두 등급 1.
  - `option_groups.csv`: 장비마다 한 줄 그룹 9개(그래서 옵션이 고정), 비중 1.
  - `equipment.csv`: 모자 5(날선 모자 · 조준 고글 · 행운 모자 · 사냥꾼 모자 · 집중 머리띠), 갑옷 4(안정 조끼 · 사수 조끼 · 버팀 조끼 · 견착 멜빵). 이름은 테스트용 임시, 아이콘 칸은 비어 있음(Task 3에서 Codex).
  - 언어: `content/lan/{kor,eng}/equipment.csv`(효과 · 조건 · 장비 이름). CSV `.import`는 기존 데이터처럼 `keep`.
- **코드** `game/scripts/Equipment/`(새):
  - `EquipmentDefinition.cs`: `EquipmentSlot` · `EquipmentEffect` · `EquipmentCondition` · `OptionUnit` · `OptionParam`, 기록형 `OptionStat` · `OptionCondition` · `OptionDefinition` · `GroupEntry` · `EquipmentDefinition` · `GearItem`(장비 + 옵션 한 쌍).
  - `EquipmentDatabase.cs`: 다섯 표 불러오기(오류는 모으고 그 줄만 뺌), `Roll(group, rng)`(비중), `Create(id, rng)`(바닥에 생길 때 옵션 결정).
    - 효과 · 조건 ID는 코드 열거형의 snake_case와 같아야 한다.
    - 조건의 변수 종류도 코드와 같아야 한다.
    - 거르는 것: 범위 밖 값, 변수 없는 조건에 변수, 집중 한 발 + 치명타 확률, 없는 효과 · 조건 · 옵션 · 그룹, 비중 0 이하, 그룹 안 같은 옵션 중복, 슬롯에 못 붙는 효과, 언어 키 없음, 알 수 없는 열, 중복 ID.
  - `EquipmentLoadout.cs`: 머리 · 갑옷 두 칸, `Equip`은 같은 칸의 이전 장비(옵션 그대로)를 돌려줌. `Evaluate(aiming, focusedShot)` → `GearBonus`(치명타 확률 0~1 가산 · 치명타 피해 1+합 · 퍼짐 1−합 · 반동 1−합, 같은 효과는 더한 뒤 제한). 무기 정의는 건드리지 않는다.
  - `Player/PlayerEquipment.cs`: `Player.Gear`(사람마다 빈 착용).
- 아직 실제 사격 · HUD · 줍기 · 동기화에 연결하지 않았다(Task 2 ~ 4).

## 계획과 다르게 한 것(AI 판단)

- 계획의 `EquipmentCatalog.cs` · `OptionCatalog.cs` 둘 대신 `EquipmentDatabase.cs` 하나로 했다. 저장소의 다른 데이터(`ThrowableDatabase` · `MonsterDatabase`)와 같은 이름 · 모양(`Current` · `FromText` · `Errors`)이다.
- 설계 초안의 `option_stats.csv` `apply` 열은 넣지 않았다. 효과마다 어떻게 적용되는지(가산 · 배율)는 코드가 정하므로, 데이터에 두면 코드와 어긋날 수만 있다. `unit`은 표시용으로 남겼다.
- 계획 Task 1의 "`seconds` 조건의 빈 변수 거부"는 지금 `seconds` 조건이 코드에 없어 직접 시험하지 못했다. 같은 검사 경로(조건의 변수 종류가 코드와 다르면 거부, 변수 없는 조건에 변수가 있으면 거부)는 시험했다. 첫 시간 조건을 추가할 때 함께 시험한다.

## 검사

- 실패 먼저: 검사를 먼저 쓰고 빌드 → `EquipmentDatabase` · `GearBonus` · `GearItem` 없음으로 빌드 실패(CS0246) 확인.
- 빌드: 오류 0.
- `EquipmentCheck`(새, 헤드리스): 종료 0, 실패 0, 누수 0, `EQUIPMENT_PASS 51`.
  - 실제 데이터 오류 0, 9종(모자 5 · 갑옷 4), 한 줄 그룹, 집중 확률 옵션 없음, 이름 키.
  - 9종 각각 비조준 · 조준 · 집중 한 발의 값: 확률 0.03 / 0.06, 치명타 1.1 / 1.2 / 1.3, 퍼짐 0.90 / 0.85, 반동 0.85 / 0.75. 각 장비가 자기 효과만 바꾸는지도 확인했다.
  - 모자 + 갑옷 함께, 빈 칸 · 교체(이전 장비가 옵션 그대로 돌아옴), 두 사람의 착용이 따로임, 무기 정의 불변, `Player.Gear`.
  - 3 : 1 비중 그룹 4000번 → 첫 옵션 2954번(73.9%, 75% ± 3% 안), `Create`가 그룹 안 옵션을 붙임, 없는 장비는 null.
  - 잘못된 데이터 15종이 오류로 보고됨.
- `Integration`: 종료 0, 실패 0, 누수 0, `INTEGRATION_PASS 149`.
- Godot 가져오기(`--headless --editor --import`): 종료 0. 새 CSV는 `keep`으로 남았다.
- 렌더: 화면 변경이 없어 하지 않았다.

## 남은 제약

- 수치는 C-34 테스트 값(최종 밸런스 미검증), 장비 이름 · 효과 범위(최대값)는 임시.
- 실제 사격 연결(Task 2), 줍기 · 교체 · HUD(일반 / 개발자 모드 두 화면, Task 3), 동기화 · 이월 · 개발자 모드 체크박스(Task 4), 랩 장면(Task 5)은 남았다.
