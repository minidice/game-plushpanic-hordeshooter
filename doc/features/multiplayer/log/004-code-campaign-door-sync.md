# 캠페인 C1 출구 문 상태 동기화 수정

- 2026-10-29, I-134. 사용자 영상과 “큰 거리 끝, 골목으로 넘어가는 기지” 설명을 근거로 캠페인1 C1 출구를 조사했다. `be166ff`로 이슈 번호를 먼저 예약·푸시했다.
- 문과 경보를 함께 넣던 U8 비트 필드가 9번째 이후 상태를 잘랐다. C1 출구는 인덱스8이며, 호스트만 열린 상태가 됐다. 문/경보별 개수 U16과 상태 U8 배열로 교체하고 수신 개수 검증 및 프로토콜18을 적용했다.
- 수정 전 가장 큰 일반 캐릭터 거위(2.18m)는 같은 출구를 통과했지만 클라이언트 오리의 문은 닫혀 있었다. 높이가 원인일 때1.2배로 넓히자는 조건에 해당하지 않으므로 맵 높이는 변경하지 않았다. 기존 동기화 계약의 결함 수정으로 새 결정 ID는 필요하지 않다.

## 검증

Godot4.7.2 .NET / Windows. 가져오기·빌드·검사는 순차 실행했다. ENet 검사는 한 장면 안에 독립된 월드의 호스트/클라이언트를 만든다.

| 실행 | 결과 | `game/output/` 근거 |
|---|---|---|
| 수정 전 CampaignDoorNetCheck | 실패·종료1: C1 클라이언트 열림 상태 미전달, 호스트 거위 통과 성공 | `campaign-door-red.log` |
| 수정 후 같은 headless 검사 | 13 통과·종료0 | `campaign-door-green.log` |
| 최종 가져오기·C# 빌드 | 종료0, 빌드 오류0·경고0 | `campaign-door-import.log`, `campaign-door-final-build.log` |
| 최종 창 모드 CampaignDoorNetCheck | 13 통과, 실제 거위·오리 통과 및 10개 문/경보 독립 동기화 | `campaign-door-render.log` |
| LobbyCheck / NetCodecCheck / NetSyncCheck | 각각31 / 12 / 32 통과 | `campaign-door-LobbyCheck.log`, `campaign-door-NetCodecCheck.log`, `campaign-door-NetSyncCheck.log` |

최종 검사 로그에 엔진 오류·종료 경고 없음. `campaign-door-client-passed.png`(1600×900)에서 클라이언트 오리가 출구 밖으로 나온 실제 렌더를 확인했다. 독립 읽기 전용 코드 리뷰에서 중요한 결함 없음. 테스트 장면·C#·UID를 함께 추가했다.

## 남은 범위

두 PC/인터넷 지연·손실 환경과 전체 캠페인 완주는 미검증이다. I-72의 간헐 위치 오차는 별도이며 이번 NetSyncCheck 통과로 해결됐다고 처리하지 않는다. 전체 통로 높이1.2배 계약을 새로 도입한 변경도 아니다. 새 빌드끼리 접속해야 한다. 이슈 색인/본문·멀티 규격·체크리스트를 함께 갱신했다.
