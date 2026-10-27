# 월드 링·범위·공격 예고 외형 정리

관련: **A-19 · C-11 · C-25 · M-24**, [계획](../world-marker-polish-plan.md), [현재 결정·변경 이력](../../effects-audio/decisions.md#a-19-월드-링공격-예고-외형-후속), [표시 규격](../../../vfx-quality.md#월드-링공격-예고-표시--2026-10-27).

## 요청과 변경

사용자가 무기 주변 링, 범위무기 곡사·수류탄 범위, 몬스터 공격 범위도 현재 HUD 방향처럼 고급스럽게 개선해 달라고 요청했다. 기존 `codex/hud-light-polish` 워크트리에서 작업했다.

- `GroundMarker`: 원/직선 범위의 평면 셰이더. 밝은 경계, 약한 번짐, 낮은 내부 채움, 픽셀 미분 기반 가장자리 완화. 셰이더 공유, 각 표시의 메시/머티리얼 재사용. 별도 이미지나 전체 bloom 변경 없음.
- `WeaponPickup`/`LootPickup`: 입체 도넛을 얇은 종류색 링으로 교체. 줍기 상호작용 반경과 표시용 링 반경은 별개다.
- `ThrowPreview`: 착탄 경계는 기존 기준 반경에 고정하고 ±3% 크기 맥박을 제거했다. 밝기만 변화, 최대거리 링은 옅은 청색, 곡선 점은 작고 매끈하게 조정했다.
- `MortarShell`: 주황색 폭발 경계 안에 실제 비행 시간 비율을 채운다. 자기 피해 가능성/착탄 시간 유지.
- `BullEnemy`, `BossEnemy`, `RangedEnemy`, `CaptureEnemy`: 돌진·내려찍기·산개 사격·총격·독·포획 준비를 붉은 경계로 통일. 보스 산개 사격은 처음부터 전체 길이를 보이고 안쪽만 준비 진행에 따라 찬다. 포획 후 실제 연결선과 독/불의 지속효과는 기존 유지.
- 피해·반경·시간·목표·취소·층 높이 판정·네트워크 데이터는 바꾸지 않았다. 새 `.cs.uid`를 함께 관리한다.

## 검사와 실제 렌더

Godot4.7.2 .NET / Windows. 빌드·가져오기·게임 실행은 모두 순차. `game/output/` 로그와 PNG는 생성 산출물이며 커밋하지 않는다.

| 검사 | 결과 | 로그 |
|---|---|---|
| 변경 전 반경 고정 검사 | 의도한 실패: 시간 경과에 착탄 원 크기 변화 | `ground-marker-red.log` |
| Debug 빌드 | 통과, 경고0/오류0 | 명령 출력 |
| headless editor import | 종료0, 오류·경고 없음 | `ground-marker-import.log` |
| `GroundMarkerCheck` 1600×900 실제 렌더 | **29 통과**, 오류·경고 없음 | `ground-marker-render.log` |
| `GroundMarkerCheck` headless / 1280×720 실제 렌더 | **각29 통과**, 오류·경고 없음 | `ground-marker-headless.log`, `ground-marker-render-720.log` |
| `WeaponHandlingCheck` headless | **36 통과**, 오류·경고 없음 | `ground-marker-handling.log` |
| `ModeCheck` headless | **147 통과**, 오류·경고 없음 | `ground-marker-mode.log` |
| `Integration` headless | **149 통과**, 오류·경고·종료 누수 없음 | `ground-marker-integration.log` |

`GroundMarkerCheck`는 실제 발생원을 호출하여 원 크기 고정, 수류탄·곡사 경계와 사거리, 포탄 비행 진행/삭제, 5종 적의 준비·기절 제거, 보스 내려찍기/7개 산개 선의 고정 외곽과 진행, 밝은 높은 층의 투척/적 예고 높이, 적 삭제 정리를 확인한다. `WeaponHandlingCheck`는 실제 투척/곡사 거치·발사·취소를, `ModeCheck`는 보스의 실제 준비·공격·피해·단계 진행을 검사한다.

실제 렌더: `ground-markers-1600/ground-marker-{pickups,grenade,mortar-aim,mortar-incoming,bull,spitter,armed_animal,tether,leaper,slam,fanshot,bright-tier-grenade,bright-tier-danger}.png`. 어두운 창고 바닥과 밝은1.2m 검사용 평면에서 색/경계·좁은 선·겹치는 부채꼴·진행 채움·상자/벽 차폐를 눈으로 확인했다. 밝은 평면은 검사 도구에만 추가하며 맵 데이터는 수정하지 않는다.

1280×720 결과는 `output/ground-marker-*.png`에 저장하며 작은 화면의 산개 선과 밝은 층 위험 원도 확인했다. 최종 전체 검사 로그에 오류·경고·종료 누수 없음.

기능 결정/계획/기록과 효과 규격의 로컬 링크30개 대상 파일 존재 확인, 추가된 절의 앵커·표시 수치와 코드 대조, `git diff --check` 통과.

재현: Godot에 `--path game --max-fps 60 res://tests/GroundMarkerCheck.tscn` 전달. headless는 `--headless`, 렌더는 `--resolution 1600x900` 또는 `1280x720`. 기존 전용 장면도 같은 방식으로 각각 선택한다. `Start-Game.ps1 -Test`는 Integration만 실행한다.

### 검사 작성 중 실패와 수정

- 검사 코드에서 `MonsterDefinition.RangedShot`로 잘못 참조해 빌드 실패1회. 실제 속성 `Ranged`로 수정 후 빌드 경고/오류0.
- 최초29개로 확장하기 전26개 렌더에서 assertion은 통과했지만 보스 수동 진행 시 `Parameter "space" is null` 오류2회가 있었다. 검사에서 Stage의 `ProcessMode.Disabled`로 물리 공간에서 몸체를 제거하고 `MoveAndSlide` 경로를 호출한 것이 원인이다. 검사만 `SetProcess(false)`/`SetPhysicsProcess(false)` 재귀 정지로 바꿔 물리 등록을 유지했다. 최종 렌더에서 해당 오류 없음. 제품 코드나 새 제품 이슈로 기록하지 않는다.

## 검토와 제약

독립 읽기 전용 검토에서 조치할 회귀/결함 없음. 평면 방향·길이/반경, 실제 타이머·피해·네트워크 경로 유지, 기절/종료/삭제 정리와 렌더를 검토했다. A-19 기존 방향의 후속으로 결정 본문/색인, 무기·투척·효과 규격과 체크리스트를 갱신했다. 새 전투 규칙이나 확정 제품 결함을 추가한 작업이 아니므로 새 결정/이슈 ID를 발급하지 않고 기존 이슈 상태를 유지한다.

외형 수치는 최종 수락 전 제작값. 표시 평면은 경사/단차 전체에 투영되지 않으며 기존 발생원 높이 계약을 따른다. 돌진 예고의 기존 길이계수0.7/폭은 정확한 전체 충돌 영역이 아니다. 실제2PC·내보내기·장시간/저사양 성능·모든 배경 조합은 미검증.
