# 모자·갑옷 첫 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** 캠페인과 난전에서 두 장비 슬롯·현장 교체·조건별9종 옵션을 실제 플레이로 비교할 수 있게 한다.
**Architecture:** 장비 데이터와 플레이어별 착용 상태를 분리한다. 조건은 실제 발사 시점에 평가하고 무기 정의 원본은 수정하지 않는다. 바닥 장비의 획득·교체·스테이지 이월은 기존 상호작용과 호스트 권한에 연결한다.
**Tech Stack:** Godot4.7.2 .NET, C#, CSV 데이터, 기존 HUD 컨트롤·Godot 검사 장면.
**Spec:** [장비 설계](equipment-movement-plan.md), [수락한 옵션 표](equipment-option-tuning.md#장비-옵션의-효과와-발동-조건), C-32·C-34.

현재는 **계획 작성 완료·코드 미구현**이다. 사용자가 정한 내용과 아래 AI의 구현 선택을 구분한다. 이 문서가 다른 후보 수치나 무기별 저격 확률의 수락을 뜻하지 않는다.

## Global Constraints

- 캠페인 `GameMode.Scenario`와 난전 `GameMode.Survivor`만 적용. 모드2는 제외.
- 모자·갑옷 각1칸, 여분 보관/내구도 없음. 한 장비는 효과 하나+발동 조건 하나.
- 바닥 장비 하나를 팀이 공유. 먼저 유효하게 획득한 사람이 착용하고 기존 장비는 바닥에 배출, 팀원이 재획득 가능.
- 첫 단계는 아이콘·기능 효과. `CharacterAppearance`의 꾸미기/착용 외형은 변경하지 않음.
- 치명타 확률 상시+3%p/조준+6%p, 치명타 피해 상시+10%/조준+20%/집중+30%, 탄퍼짐 감소 상시10%/조준15%, 반동 감소 상시15%/조준25%. 모두 테스트용 임시값.
- 집중이 차는 중은 조준 중, 집중 조건은 완료된 다음 한 발만. 집중100% 유지·집중 확률 장비 제외.
- 나머지 시야/탄창/장전/거리/방어/상태이상 옵션은 후보로 보존하고 이번9종 구현과 분리. 저격30%는 예시여서 전 무기에 적용하지 않음.

## 첫 구현의 선택 — AI 제안, 사용자 확정과 구분

- 슬롯 배분: 모자에 치명타 확률2종·치명타 피해3종, 갑옷에 탄퍼짐2종·반동2종. 첫 플레이 비교용이며 영구적인 슬롯 제한 방향은 아니다.
- 기본 확률은 무기 고유 규칙이 없는 경우0. 활성 장비 확률을 %p로 더하고0~100% 제한. 집중 보장은 우선 처리하며100% 초과를 추가 피해로 바꾸지 않는다. 무기별 거리 조건은 별도 데이터 확정 후 연결한다.
- 치명타 피해 보너스는 활성 장비 값을 합산해 기존 치명타 피해에 `1+합계`를 곱한다. 기존 카드 배율은 유지한다. 퍼짐/반동 감소율도 장비끼리 합산 후0~100%로 제한하고 기존 값에 곱한다. 현재 슬롯 배분에서는 같은 효과 두 개를 동시 착용할 수 없다.
- 발사별 평가: 실제 탄 한 발의 조준·착용 장비를 고정한다. 산탄은 한 발의 확률 난수를 공유하고, 점사는 각 탄을 별도 평가한다. 집중 보장은 기존 집중 대상/첫 충돌 대상 조건을 유지한다. 다음 탄까지 집중 전용 보너스를 캐시하지 않는다.
- 첫 치명타 적용은 기존 치명타 판정이 있는 직사·근접 경로에 연결한다. 폭발/지속 피해에 새로운 치명타를 추가하지 않는다. 퍼짐 감소는 실제 직사/산탄/현재 퍼짐을 사용하는 발사체 경로에, 반동 감소는 조준점 상하·좌우에만 적용하고 캐릭터 밀려남·반동 복귀 속도는 유지한다.
- 획득은 기존 E 상호작용. 같은 장비를 다시 집으면 변화 없음. 새 게임은 빈 슬롯, 부활은 착용 유지, 캠페인 다음 스테이지는 착용2개를 이월. 예비 보관·독립 영구 창고·새 저장 체계·AI 자동 줍기는 추가하지 않는다.
- 첫 배치는 전용 플레이 장면의 두 모드에9종을 놓아 비교한다. 실제 캠페인 샛길별 배치 좌표·난전 보상 빈도는 이 검사 이후 레벨/모드 보상 작업으로 연결한다. 이 단계만으로 샛길 보상 배치 완료라고 보고하지 않는다.

## Review Focus

- 동시에 같은 바닥 장비를 줍거나 교체 요청을 재전송해도 장비가 복제되지 않음 → Task3/4.
- 집중을 소비하기 전의 단 한 발에만 집중 피해 보너스, 산탄/점사/조준 해제에서 누출 없음 → Task2.
- 같은 무기 정의를 쓰는 다른 플레이어의 퍼짐·반동이 장비 교체로 바뀌지 않음 → Task1/2.
- 단계 이동·재합류 후 개인 착용과 바닥 장비가 일치하며 옛 데이터가 새 장비를 덮지 않음 → Task4.
- 조건부 옵션 설명과 실제 발동 상태가 일치하고 기존 외형·모드2가 변하지 않음 → Task3/5.

## Task1: 장비 데이터와 조건 평가

**Files:** 새 `game/scripts/Equipment/EquipmentDefinition.cs`, `EquipmentCatalog.cs`, `EquipmentLoadout.cs`, `game/scripts/Player/PlayerEquipment.cs`, `game/content/data/equipment.csv`; 새 `game/tests/EquipmentCheck.cs/.tscn`.
**Interfaces:** `EquipmentSlot { Head, Armor }`, `EquipmentCondition { Always, Aiming, FocusedShot }`, `EquipmentEffect { CritChance, CritDamage, SpreadReduction, RecoilReduction }`. 정의는 Id/Slot/Effect/Condition/Value/Icon/NameKey를 가진다. `EquipmentLoadout.Equip(EquipmentDefinition next)`는 이전 정의 또는null 반환. `Evaluate(bool aiming, bool focusedShot)`는 확률 가산·치명타 피해 배율·퍼짐 배율·반동 배율을 값으로 반환. `Player.Gear`가 소유한다.

- [ ] 조건별9종 데이터를 검사에 선언하고 빈 슬롯·잘못된 슬롯/수치·집중 확률 조합 거부를 확인하는 실패 검사를 만든다.
- [ ] `EquipmentCheck` 실행해 장비 기능 부재로 실패함을 확인한다.
- [ ] 데이터 로드·교체·순수 조건 평가를 구현하고 공유 WeaponDefinition을 수정하지 않는다.
- [ ] 비조준/조준/집중 각 상태에서 확률0.03/0.06, 치명타1.1/1.2/1.3, 퍼짐0.90/0.85, 반동0.85/0.75를 검사한다. 다른 Player의 결과가 변하지 않는지 확인한다.
- [ ] 전용 검사 통과·UID·새 작업 기록과 함께 이 작업 파일만 커밋한다.

## Task2: 실제 공격·조준 표시 연결

**Files:** 새 `game/scripts/Equipment/EquipmentCombat.cs`; 수정 `Player.cs`, `Weapons/WeaponController.cs`, `Weapons/WeaponRecoil.cs`, `UI/HudReticle.cs`; 새 `game/tests/EquipmentCombatCheck.cs/.tscn`와 필요 시 `WeaponAimCheck.cs`.
**Interfaces:** `EquipmentCombat.IsCritical(bool guaranteed, float chance, float sample01)`는 보장 여부와 확률을 판정하는 순수 함수. `WeaponRecoil.Kick(WeaponDefinition def, RandomNumberGenerator rng, float scale = 1f)`로 기존 호출 호환. Task1의 Evaluate를 실제 발사/근접 명중 시점에 사용한다.

- [ ] 실패 검사: sample0.029는3%에서true,0.031은false, guaranteed=true는 sample0.999에서도true. 조준 고글은 비조준0/조준0.06. 집중 준비 중과 집중 다음 한 발의 피해를 구별한다.
- [ ] 실패 검사: 집중 피해 장비에서 기본100·치명타 배율2.5이면 집중 한 발325, 이후 일반 치명타250. 보장 한 발을 쏘면 집중을 소비하고 다음 탄에1.3배가 남지 않는다.
- [ ] 발사 단위 난수와 상태를 사용해 직사/산탄/점사/근접의 실제 피해에 연결한다. 새 확률은 권한 있는 시뮬레이션에서 결정하며 클라이언트 표시 난수로 체력을 바꾸지 않는다.
- [ ] 현재 `CritReady`가 IsLocal에 묶여 있는 점을 처리한다. 사람 플레이어의 집중 평가는 오프라인/호스트에서 해당 플레이어의 수신 조준점으로 평가하도록 분리하고 기존0.5초·조준 배율·발사 소비를 유지한다. 원격 플레이어 집중은 Task4에서 실측 검증한다.
- [ ] 퍼짐은 이동·조준·연사 반영 후 실제 발사 각도와 HUD에 같은 배율 적용. 반동은 Kick에 배율 전달. 집중 피해 증가는 임의의 확률 치명타에 붙이지 않는다.
- [ ] 실제 산탄/점사·다른 플레이어·장비 교체·조준 해제 검사와 `WeaponAimCheck`, `MovementEchoCheck` 통과 후 기록·커밋한다.

## Task3: 현장 교체와 두 슬롯 표시

**Files:** 새 `game/scripts/Equipment/EquipmentPickup.cs`, `game/scripts/World/StageEquipment.cs`, `game/scripts/UI/HudGear.cs`; 수정 `World/Stage.cs`, `UI/HudDock.cs`, `UI/HudEquipmentSlot.cs`, `content/lan/{kor,eng}/ui.csv`; 새 `game/assets/icons/equipment/` 아이콘, `game/tests/EquipmentPickupCheck.cs/.tscn`.
**Interfaces:** `EquipmentPickup.Spawn(Stage stage, Vector3 at, EquipmentDefinition def, int netId = 0)`; `Stage.TryEquipPickup(EquipmentPickup pickup, Player who)`는 성공 여부 반환. Task1의 Equip이 반환한 장비를 같은 높이 단의 안전한 바닥에 배출한다.

- [ ] 실패 검사: 빈 슬롯 장착, 기존 장비 교체/배출, 팀원 재획득, 비활성·원거리·쓰러진/죽은 플레이어 요청 거부, 같은 장비 요청 무변화, 중복 요청당 지급1회.
- [ ] 호스트에서만 교체한다. 입력 대상 무효화→장착→기존 장비 배출을 한 번에 수행하고 동일한 Pickup을 무한 공급하는 `WeaponPickup.Infinite` 규칙을 사용하지 않는다.
- [ ] HUD에 모자/갑옷 아이콘·이름·조건/수치를 표시한다. 현재 효과 활성 여부와 착용 여부를 구분하며 단축 사용키·내구도 게이지·예비 슬롯을 추가하지 않는다. 바닥 비교 안내는 현재/교체 장비 효과를 보여준다.
- [ ] 캠페인/난전에서 표시·효과, 모드2에서 숨김·효과 없음, 기존 캐릭터 외형 유지 검사. 한국어/영어1280×720·1600×900 실제 렌더 확인 후 기록·커밋한다.

## Task4: 멀티 동기화·단계 이월

**Files:** 수정 `Net/NetMessages.cs`(NetIds 포함), `Net/NetLobby.cs`, `Net/StageNet.cs`, `Player/PlayerNet.cs`, `World/StageCarry.cs`; 새 `game/tests/EquipmentNetCheck.cs/.tscn`; 수정 `NetCodecCheck.cs`, `doc/multiplayer-sync-design.md`.
**Interfaces:** 스냅샷과 StageCarry에 HeadGearId/ArmorGearId, 바닥 생성 사건에 장비ID/인스턴스NetId/위치. 카탈로그를 DataHash에 포함하고 실행 시점의 Protocol을1 증가시킨다(현재20이라는 값에 의존해 덮어쓰지 않음).

- [ ] 코덱 왕복·빈 슬롯·알 수 없는ID·데이터 불일치 거부 실패 검사부터 작성한다.
- [ ] 실제 ENet 두 월드에서 같은 장비 동시 요청, 교체 배출 재획득, 늦은 합류, 생성/제거 사건 재수신에도 장비 수량 보존을 확인하는 실패 검사를 만든다.
- [ ] 권한 있는 착용 상태와 바닥 장비를 전송하고 환영 상태에 모든 장비를 포함한다. StageCarry의 저장/적용/인코드/디코드에 착용2개를 연결한다.
- [ ] 실제 클라이언트가 조준·집중 후 사격할 때 호스트의 장비 상태/집중/피해와 표시가 맞는지 검사한다. 클라이언트 난수가 피해를 결정하거나 같은 탄이 두 번 적용되지 않는다.
- [ ] 새 검사와 `NetCodecCheck`, `NetSyncCheck`, `CampaignProgressionCheck`를 실행한다. 통과·실패·종료 경고를 구분해 기록하고 프로토콜 문서와 함께 커밋한다.

## Task5: 플레이 비교 장면과 최종 회귀

**Files:** 새 `game/tests/EquipmentLab.cs/.tscn`, `EquipmentLabCheck.cs/.tscn`; 수정 `game/Start-Game.ps1`, `game/README.md`, 기능 결정·현황·새 작업 기록.
**Interfaces:** `Start-Game.ps1 -EquipmentLab`으로 전용 장면 실행. 장면에서 캠페인/난전 규칙 선택,9종 바닥 장비, 움직이는 표적·실제 적, 상시/조준/집중 판정 결과를 관찰할 수 있게 한다. 일반 게임 HUD에 디버그 수치를 노출하지 않는다.

- [ ] 전용 장면에서9종 모두 획득 가능·슬롯별 교체·모드 전환 후 상태 초기화·기본 외형 불변을 검사한다.
- [ ] 변경 후 빌드/가져오기와 Equipment 전용 검사들, Integration·WeaponAimCheck·MovementCheck·MovementNetCheck·NetCodecCheck·NetSyncCheck를 실행한다. 앞 작업 이후 바뀌지 않은 검사는 불필요하게 반복하지 않는다.
- [ ] 실제 창에서 아이콘 식별·조건 설명·두 슬롯과 기존 HUD 겹침 여부, 조준/반동/집중 한 발의 체감을 확인한다. 최종 플레이 수치 수락은 사용자 플레이 후다.
- [ ] 리뷰에서 확률 판정 단위·원격 집중·교체 중 중복 지급·공유 정의 오염을 확인한다. 기록·체크리스트·UID를 검토해 커밋하고 테스트 창을 실행한다.

## 실행 명령과 완료 기준

PowerShell에서 `GODOT_DIR`을 이 PC의 Godot .NET 경로로 지정한다. `dotnet build game/HordeShooter.csproj --nologo -v q` 후 같은 엔진으로 `--headless --editor --import --path game --quit`를 실행한다. 전용 검사는 `--headless --path game res://tests/EquipmentCheck.tscn`처럼 각 장면을 별도로 실행한다. 빌드/가져오기/검사는 병렬 실행하지 않는다.

새 검사 장면은 실패 시0이 아닌 종료 코드와 FAIL 원인을, 완료 시 각 이름에 맞는 PASS 표식을 남긴다. 실행 안 한 검사는 완료로 표시하지 않는다. `Start-Game.ps1 -Test`는 Integration만 실행한다.

완료는9종 실제 효과·장착/교체·두 모드·멀티/이월·렌더·플레이 장면까지 확인한 상태다. 샛길별 보상 배치와 난전 획득 빈도, 나머지 후보 옵션, 무기별 거리 조건 수치, 캐릭터 외형 연동은 별도 후속이며 완료 범위에 포함하지 않는다.

## 계획 자체 검토

- 확정 사항은 Global Constraints와 Task1~5에 대응한다. 슬롯 배분·합산·판정 단위·시작/이월·배치 범위는 AI 선택으로 표시했다.
- 정의/착용 상태→공격 적용→현장 교체/UI→멀티/이월→플레이 검증 순서이며 Task의 인터페이스는 앞 Task를 참조한다.
- 집중100%와 실효 없는 확률 옵션 제외를 유지한다. 현재 원격 집중 제약을 놓치지 않도록 Task2/4에 포함했다.
- 검사는 계획이며 아직 실행하지 않았다. 저장 체계를 새로 만들거나 실제 레벨 보상 배치가 완료됐다고 가정하지 않는다.
