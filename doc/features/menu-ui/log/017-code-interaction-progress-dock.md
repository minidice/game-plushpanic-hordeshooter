# 상호작용 진행을 무기 슬롯 위 한 곳으로 통합

- 날짜: 2026-10-29. 관련: M-38 · L-35. 기준 커밋 `901c473`.
- 사용자 요청과 `D:/Recored/중복정보.mp4`의 중복 표시를 확인했다. 영상은 참고 자료이며 구현 지시로 취급하지 않았다.

## 변경

- 대상 옆 안내·머리 위 게이지·하단 퍼센트의 중복을 없앴다. 대기 중에는 대상 옆 행동/E 안내, 실제 홀드 중에는 같은 패널을 무기 슬롯 바로 위로 옮겨 큰 게이지와 %를 표시한다.
- 진행 패널은 논리 폭460px, 바 높이24px, 제목22px/상태20px이며 기존 청백색 발광·반투명 재질을 사용한다. 피격 중단·실제 완료 피드백도 같은 위치에서 표시한다. 취소를 완료로 표시하지 않는다.
- 진행 중 별도 하단 장전/충전 바와 대상 연결선을 숨긴다. 무기의 실제 진행 시계와 조준점은 유지한다. 상태 경고와 효과 아이콘은 진행 패널 위로 이동한다.
- 실제 `HoldProgress`/`ReviveProgress`와 호스트 완료 확인을 계속 사용한다. 입력·피격 취소·거리 이탈 규칙은 바꾸지 않았다. 현재 문 홀드는 이동이 가능하고 범위를 벗어나면 취소되므로, 사용자 설명을 근거로 이동 잠금을 새로 넣지는 않았다.

## 검증

Godot4.7.2 .NET / Windows. `dotnet build game/HordeShooter.csproj --nologo -v q`, 전용 장면은 `Godot --path game [--resolution 1600x900 또는 1280x720 또는 --headless] res://tests/<장면>.tscn`으로 각각 실행했다.

| 검사 | 결과 | 로그 (`game/output/`) |
|---|---|---|
| 빌드 | 오류0·경고0, 종료0 | `campaign-change-build.log` |
| InteractionUiCheck 실제1600×900 한글 | 37 통과, 종료0 | `campaign-final-InteractionUiCheck.log` |
| InteractionUiCheck 실제1280×720 영문·밝은 배경 (`-- --english --bright`) | 37 통과, 종료0 | `campaign-final-interaction-small.log` |
| ReticleCheck 실제1600×900 | 52 통과, 종료0 | `campaign-final-reticle.log` |
| ReloadUiCheck headless | 38 통과, 종료0 | `campaign-regression-ReloadUiCheck.log` |
| Integration headless | 149 통과, 종료0 | `campaign-final-integration.log` |
| NetSyncCheck headless | 32 통과, 종료0 | `campaign-final-net.log` |

변경 전 새 단일 게이지 검사가 실패한 것을 확인하고 구현 후 통과했다. 독립 리뷰에서 마지막 쓰러짐 경고의 겹침을 발견하여 실제 쓰러짐/구조 상태로 재현했다. 경고 충돌 검사가 수정 전 실패(`campaign-overlap-red.log`), 위치 수정 후 통과한다. 초기 재현용 동료 없는 장면은 전멸 상태가 되어 테스트 구성을 보완했다. 이는 최종 검사 실패와 구분한다. 최종 실행에서 엔진 오류·종료 경고 없음. Git의 CRLF→LF 안내는 파일 정규화 안내다.

## 렌더·기록·제약

- 실제 렌더: [진행·상태 경고](../../../../game/output/interaction-ui-hold.png), [피격 중단](../../../../game/output/interaction-ui-interrupted.png), [작은 화면·영문](../../../../game/output/interaction-ui-hold-eng-bright.png). 창 모드 검사로 재생성하며 `output/`은 커밋하지 않는다.
- 기존 M-38/L-35 현재 규격과 변경 이력, 상위 색인·체크리스트·가이드 경험 설명을 갱신했다. 새 기능 방향이나 별도 기존 제품 버그 ID를 만들 필요가 없는 요청 변경이다.
- 두 PC 실접속·초광폭/모든 언어·패키지 내보내기는 미검증이다. NetSyncCheck는 같은 PC의 ENet 호스트/클라이언트 검사다. 최종 체감·외형 수락은 후속이다.
- 코드와 기록은 같은 커밋에 포함한다. 이번 후속 변경은 작업 브랜치에 보관하며 main 병합·원격 푸시는 하지 않았다.
