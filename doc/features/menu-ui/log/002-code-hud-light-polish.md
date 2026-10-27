# HUD 빛 표현과 강조 정리

관련: **M-33**, [현재 결정·변경 이력](../decisions.md#m-33-장전충전-포인터와-반투명-진행-ui), [범위·계획](../hud-light-polish-plan.md), [무기 규격](../../../weapon-spec.md#64-장전충전-진행-ui-m-33).

## 변경

- 사용자 첨부 UI의 깔끔한 느낌을 반영하라는 요청에 따라 별도 `codex/hud-light-polish` 워크트리에서 작업했다. 제작 방식은 코드 기반으로 선택했다. PNG/새 셰이더/외부 패키지/3D bloom 변경은 없다.
- `HudWeaponAction.cs`: 어두운 고리 안쪽으로 배경을 보이게 하고 흰 중심·청색 번짐을 겹쳐 그린다. 하단 트랙은248×22px, 테두리를 없애고 밝은 채움의 그림자로 부드러운 빛을 만든다. 공유 StyleBox 리소스와 고정12개 호(번짐10개+색1개+중심1개)를 사용한다.
- `HudDock.cs`: 점프 게이지에 같은 빛 표현을 적용하고 공통 HUD 판 테두리 알파를0.26→0.10으로 낮춘다. `HudEquipmentSlot.cs`: 선택 테두리 알파0.50으로 완화한다.
- `Hud.cs`: 장전 포인터 네 점 두께2.5→4px. 장전/재충전 중 탄약 문구는18px 중립색, 평상시 탄약 숫자는28px 및 기존 부족탄 경고다. 취소 시 부족탄 경고가 즉시 복귀한다.
- 무기/도약 타이머, 실제 진행률, 완료·취소·교체·숨김 계약, 명중 표시는 유지한다. 메인 메뉴나 키캡 전체를 재설계한 작업은 아니다.

## 검사와 결과

Godot4.7.2 .NET / Windows. 아래 로그·렌더는 로컬 `game/output/` 산출물이며 커밋하지 않는다.

| 검사 | 결과 | 로그 |
|---|---|---|
| `dotnet build game/HordeShooter.csproj --nologo -v q` | 통과, 오류0/경고0 | 명령 출력 |
| 변경 전 `ReloadUiCheck.tscn` headless /1600×900 | 34/35 통과 | `hud-polish-baseline.log`, `hud-polish-before.log` |
| 새 강조 검사, 변경 전 실행 | 의도한 실패: `reload status does not compete with danger warnings` | `hud-polish-red.log` |
| 변경 후 `ReloadUiCheck.tscn` headless | **38 통과**, 정상 종료 | `hud-polish-headless.log` |
| 변경 후1280×720 영어/창고 본 구역 실제 렌더 | **40 통과**, 정상 종료 | `hud-polish-outdoor.log` |
| 최종 headless editor import | 통과, 종료0 | `hud-polish-final-import.log` |
| 최종 순차 `Integration.tscn` | **149 통과**, 정상 종료 | `hud-polish-integration-serial.log` |
| 최종 순차1600×900 한국어 실제 렌더/연속 프레임 | **40 통과**, 정상 종료 | `hud-polish-render-serial.log` |

최종 가져오기/장전 headless/두 해상도 렌더/순차 통합 로그에 `ERROR`, `WARNING`, `FAIL`, 종료 객체 누수 경고 없음. 변경 문서의 로컬 링크32개 및 `git diff --check` 통과.

재현: `Start-Game.ps1 -Test`는 Integration만 선택하므로 장전 검사는 별도 실행한다. Godot 실행 파일에 `--path game --max-fps 60 res://tests/ReloadUiCheck.tscn`을 전달하며, headless는 `--headless`, 렌더는 `--resolution 1600x900` 또는 `--resolution 1280x720`을 쓴다. 작은 창은 뒤에 `-- --still-only --outdoor --english`를 전달한다. `--outdoor`는 창고 본 구역 위치를 선택하는 기존 플래그다.

### 실패·종료 경고 구분

- 최초 editor import는 자원 가져오기/레이아웃 완료까지 진단 오류·경고가 없었으나 종료 코드 **-1073741819**(0xC0000005)가 나왔다. UI 변경 전 발생했고 원인은 미확정이다. 최종 같은 명령 재실행은 종료0. 환경 전체 문제를 해결했다고 판단하지 않는다.
- 최초 통합 실행149통과와 한국어 렌더40통과 중 실행이 잠시 겹쳤다. 동시 작업 규칙에 맞춰 최종 확인은 두 장면을 순차로 다시 실행하고 별도 로그를 남겼다. 순차 실행도149/40통과다. 최초 결과는 최종 증거로 사용하지 않는다.
- 새 검사 실패는 의도한 UI 강조 변경의 변경 전 확인이다. 게임의 새로운 결함으로 이슈 ID를 만들지 않았다.

## 렌더와 검토

- 변경 전 `hud-polish-before.png`, 변경 후 `reload-ui-half.png`, `reload-ui-outdoor-half.png`.
- `reload-ui-zero.png`, `reload-ui-one-percent.png`, `reload-ui-small.png`, `reload-ui-almost.png`, `reload-ui-complete.png`: 0/1% 및 진입/완료의 채움 길이·모서리·번짐 확인. `outdoor-` 접두 파일도 같은 상태를 기록한다.
- `reload-ui-cancel.png`: 부족탄 경고 복귀. `reload-ui-leap-cooldown.png`: 실제 도약 입력 후 쿨다운. `reload-ui-recharge-hit.png`: 재충전 중 명중 표시. `reload-ui-frame-*.png`: 실제 Godot UI 연속 샘플이며 실시간 손 장전 모션 녹화가 아니다.
- 작은 창의 영어 문구, 밝은 벽이 있는 세이프룸과 어두운 창고 바닥에서 읽힘을 확인한다. 1% 채움 중심이 트랙 범위를 넘지 않고, 네 점과 링 중심은 구분된다. 빛 번짐 자체는 의도적으로 트랙 밖까지 퍼진다.
- 독립 읽기 전용 코드/렌더 검토에서 조치할 문제 없음. 타이머 변경 없음, 취소 후 경고 복귀, 신규 매 프레임 StyleBox 생성 없음 확인. 실제 성능 계측을 수행했다는 뜻은 아니다.

## 기록 검토와 남은 제약

- M-33의 현재 값과 「바뀐 결정」, 전체 색인, 체크리스트, 무기 규격을 갱신했다. 최초 설계/작업 기록은 이력으로 보존하고 현재 규격 링크를 덧붙였다. 기존 방향 안의 외형 보강이라 새 결정 ID가 필요하지 않다.
- 새 제품 버그를 확정한 작업이 아니며 기존 이슈 상태를 바꿀 근거도 없어 이슈 문서는 유지한다.
- 외형 최종 사용자 수락 전 제작안. 키캡/메뉴 전체, 모든 해상도·배경, 실제2PC, 내보낸 빌드, 장시간 성능은 이번 확인 범위 밖이다.
