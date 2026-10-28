# 로비 동시 시작 중 연결 해제 수정

- 날짜: 2026-10-29. 관련 I-133, 기존 계약 M-21(방·준비·시작). 기준 `321a468`.
- 사용자 제보: 로컬 PC에서 두 배포본을 로비부터 함께 시작하면 참가자가 메뉴/로비로 돌아가며, 시작 후 합류는 된다. 새 ID I-133은 색인 확인 후 `586ed4b`로 먼저 예약·푸시했다.

## 원인·변경

큰 맵을 동기로 생성하는 동안 ENet 응답 처리가 멈춰 연결이 끊겼다. `lobby-start-timing-*.log`에서 호스트 맵 생성 약5.8초, 클라이언트 생성 끝의 연결 해제 및 메뉴 복귀를 확인했다. 로비/로딩 대기를 최소30초·최대60초로 분리하고 첫 스테이지 동기화 완료 시 최소5초·최대30초로 돌린다. 로비로 돌아오면 다시 로딩 대기를 적용한다. `NetLaunch`의 한 보조 함수와 `NetLobby`/`StageNet` 생명주기 연결만 바꿨다.

`LobbyStartCheck.cs/.tscn`와 `Run-LobbyStartCheck.ps1`을 추가했다. 실제 메뉴의 준비/시작 버튼을 거쳐 Stage에 들어가는 두 프로세스 검사다. 사용자 실행 중인 게임의24680 포트를 건드리지 않도록26789 포트를 쓰며, 테스트 호스트 설치에만 private 상태를 reflection으로 연결한다. 설정 파일도 별도다. 같은 연결에서 로비 복귀 후 두 번째 시작까지 검사한다. 진단용 제품 로그는 제거했고 검사 자체의 장면 전환 로그는 남겼다.

## 검증

Windows / Godot4.7.2 .NET. 같은 workspace의 가져오기·빌드·검사는 순차 실행했다. 단, 두 프로세스 전용 검사는 필요한 호스트/클라이언트 한 쌍을 함께 실행한다.

| 실행 | 결과 | `game/output/` 로그 |
|---|---|---|
| 수정 전 두 프로세스 로비 시작 | 실패·양쪽 종료1, 클라이언트 Stage 미진입 | `lobby-start-red-*`, `lobby-start-probe-*`, `lobby-start-probe2-*` |
| 수정 전 시점 진단 | 클라이언트 연결 해제 후 메뉴 복귀·종료1 | `lobby-start-timing-*` |
| 수정 직후 같은 실제 시작 | 양쪽 통과·종료0 | `lobby-start-green-*` |
| 최종 C# 빌드 | 오류0·경고0·종료0 | `lobby-start-final-build.log` |
| 리소스 가져오기 | 종료0 | `lobby-fix-import.log` |
| `Run-LobbyStartCheck.ps1 -Prefix lobby-start-two-rounds` | headless 두 라운드 양쪽 통과·종료0 | `lobby-start-two-rounds-*` |
| `Run-LobbyStartCheck.ps1 -Prefix lobby-start-render -Windowed` | 실제 Forward+1280×720 두 라운드 양쪽 통과·종료0 | `lobby-start-render-*` |
| LobbyCheck headless | 31 통과·종료0 | `lobby-fix-LobbyCheck.log` |
| NetCodecCheck headless | 12 통과·종료0 | `lobby-fix-NetCodecCheck.log` |
| NetSyncCheck headless | 32 통과·종료0 | `lobby-fix-NetSyncCheck.log` |

최종 두 라운드 검사 표준 오류에 엔진 오류·종료 경고 없음. 독립 읽기 전용 리뷰에서 중요한 결함 없음. UI 외형 변경은 없으며 창 모드에서 실제 렌더링 부하를 포함해 전환을 검사했다. 이전 실패를 삭제하거나 통과로 표현하지 않았다.

## 기록·제약

이슈 색인/본문, 멀티 규격, 기능 체크리스트를 갱신했다. 기존 로비에서 함께 시작하는 계약의 버그 수정이므로 새 게임 방향·결정 ID는 필요하지 않다. 맵 비동기 생성, 두 PC 인터넷 플레이, 지연/손실 조건은 이번 범위 밖이다. 로딩 단계의 실제 단절 감지는 최대60초까지 늦어질 수 있다. 기존 I-72 위치 오차의 간헐 실패를 해결한 변경이 아니다.
