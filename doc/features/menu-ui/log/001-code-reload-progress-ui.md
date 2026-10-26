# 장전 포인터 전환과 반투명 진행 UI

관련: **M-33**, 기존 C-30·C-32, [결정](../decisions.md#m-33-장전충전-포인터와-반투명-진행-ui), [설계](../reload-ui-design.md), [무기 규격](../../../weapon-spec.md#64-장전충전-진행-ui-m-33).

## 변경

- 사용자 제공 `D:/Recored/커스텀/집라인/장전.mp4`의5–7초를30fps 원본에서 추출해 확인했다. 약5.7초부터 작은 네 점/원형 진행 호, 하단 둥근 트랙의 청백색 채움이 보인다. 수치나 원본 이미지 자체를 복사하지 않았다.
- 조준 눈금이 작은 대각 네 점으로 변하며 반투명 장전 링이 나타난다. 하단에는 같은 진행값의 둥근 게이지와 한국어/영어 퍼센트 문구가 보인다. 레일건/박격포 발사 준비, 난전 특수무기 재충전에도 같은 스타일을 사용한다.
- `HudWeaponAction.cs`: 전환 상태와 벡터 렌더. `Hud.cs`: 상태 연결·기존 눈금 모핑·옛 막대 제거. 안티앨리어싱 도형과 공유 `StyleBoxFlat`이 UI 리소스다. 별도 비트맵/외부 라이브러리는 추가하지 않았다.
- `WeaponState.ReloadCompleted`는 실제 최종 삽입의 완료 사실만 보존한다. 탄수·시간·사격 가능 판정은 그대로다. 완료 직후 같은 틱에 발사해도100%를 놓치지 않으며, 취소는100%가 되지 않는다.
- 조준/퍼짐/집중 치명타/명중 표시와 산탄총 중간 사격은 유지했다. 상태이상 아이콘은 진행 스트립 위로 옮겨 겹침을 피한다.

## 검사와 결과

Godot4.7.2 .NET, Windows. 아래 명령은 저장소 루트의 `game`을 `--path`로 지정했다. Godot 실행은 순차로 수행했다.

| 검사 | 최종 결과 | 로그 (`game/output/`) |
|---|---|---|
| `dotnet build game/HordeShooter.csproj --nologo -v q` | 통과, 오류0/경고0 | 실행 출력 |
| `--headless --editor --import --path game --quit` | 통과, 오류/경고 없음 | `reload-ui-final-import.log` |
| 변경 전 `Integration.tscn` | 149통과 | `reload-ui-baseline.log` |
| `--headless res://tests/ReloadUiCheck.tscn` | **34통과** | `reload-ui-check.log` |
| `--resolution 1600x900 --max-fps 60 res://tests/ReloadUiCheck.tscn` | **35통과**, 한국어 실제 렌더 | `reload-ui-render.log` |
| `--resolution 1280x720 --max-fps 60 res://tests/ReloadUiCheck.tscn -- --still-only --outdoor --english` | **35통과**, 영어/창고 본 구역 실제 렌더 (`--outdoor`는 세이프룸 밖 위치를 선택하는 검사 플래그) | `reload-ui-outdoor-render.log` |
| `--headless --max-fps 60 res://tests/WeaponAimCheck.tscn` | **32통과** | `reload-ui-WeaponAimCheck.log` |
| `--headless res://tests/ReloadPoseCheck.tscn` | **279통과** | `reload-ui-ReloadPoseCheck.log` |
| 변경 후 `--headless --max-fps 60 res://tests/Integration.tscn` | **149통과** | `reload-ui-integration.log` |

최종 로그에서 `ERROR`, `WARNING`, `FAIL`, 종료 객체 누수 경고 없음. 이는 위 실행 결과이며 기존 I-17 전체 해결 판정이 아니다. ExportRelease·실제2PC·전체 전용 장면·장시간 성능은 실행하지 않았다.

### 실패와 수정 이력

- 먼저 실제 HUD에 하단 진행 라벨이 없다는 검사 실패를 확인한 뒤 구현했다: `reload-ui-red.log`.
- 테스트 데이터의 없는 무기 ID `rocket`을 실제 `alabong`으로 수정했다. 별도 최종 쉘 검사에서 직전 사격 쿨타임이 남은 상태를 재사용한 실패는 새 무기 상태로 분리했다. 게임 쿨타임을 바꾸지 않았다.
- 독립 코드 검토3건을 모두 수정했다. 99.6%에서 취소하면 반올림100%가 되던 오류는 완료 플래그로 구분(`reload-ui-review-red.log`). 완료 틱의 사격으로 탄수가 같아 완료를 놓치던 오류는 무기의 완료 사실을 사용(`reload-ui-heldfire-red.log`). 재충전 중 명중 표시가 생략되던 오류는 링과 함께 그리도록 복원했다.
- 마지막 항목은 실제 픽셀 검사를 추가했다. 처음에는 움직이는 배경이 잘못 양성을 만들 수 있어 장면 처리를 멈추고 조준점만 다시 그리게 수정했다. 수정 전 밝아진 픽셀0개로 실패(`reload-ui-hit-red.log`), 수정 후1600×900에서297개/1280×720에서224개로 통과했다. 위 값은 같은 화면에서 명중 표시 전후의 밝기 차이다.
- 새 기능 제작 중 발견·수정한 문제이며 기존 제품 버그로 새 이슈 ID를 만들지 않았다. 기존 이슈 상태를 바꿀 근거도 없어 이슈 색인은 유지했다.

## 렌더와 남은 제약

- 확인 이미지: `reload-ui-ready.png`, `reload-ui-enter.png`, `reload-ui-half.png`, `reload-ui-complete.png`, `reload-ui-cancel.png`, `reload-ui-charge.png`, `reload-ui-recharge-hit.png`, `reload-ui-outdoor-half.png`.
- `reload-ui-frame-*.png`는 무기 타이머를 일정 간격으로 샘플링한 실제 Godot 화면이다. `reload-ui-preview.gif`는 해당 프레임을 묶은 UI 전환 미리보기이며 실시간 플레이/손 장전 애니메이션 녹화가 아니다. 손 모션은 기존 `ReloadPoseCheck`로 회귀 검사했다.
- 세이프룸 벽/바닥, 창고 본 구역 배경과 두 해상도에서 링·퍼센트·채움이 보이고 도크와 겹치지 않음을 확인했다. 모든 해상도/배경/상태이상 아이콘 조합을 검증했다고 확대하지 않는다.
- 벡터 크기·알파·전환 시간은 제작값이며 외형 최종 수락 전이다. 한 발 장전은 전체 탄창이 아니라 현재 삽입 주기를 보여 준다. 새 취소 키나 액티브 리로드는 없다.
