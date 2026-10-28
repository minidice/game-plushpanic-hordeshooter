# 캠페인·상호작용 후속 main 병합과 Windows 배포 검사

- 날짜: 2026-10-29. 사용자 요청: 커밋·main 병합·푸시, 배포본 첫 멀티 테스트 가능 여부 확인.
- 원격 fetch 후 main/origin/main `901c473`에서 `a5b1a49`를 fast-forward 병합했다. 충돌 없음. 기능 코드·기록은 `a5b1a49`에 함께 포함돼 있다.
- 검사 위치: `D:/Project/Game/game-hordeshooter`. 기존 미추적 `.codex-remote-attachments/`는 건드리거나 커밋하지 않았다. 작업 worktree는 이전 UI 렌더 비교를 위해 유지한다.

## 병합 후 검사

Godot4.7.2 .NET. 가져오기·빌드·검사를 순차 실행했다. 전용 장면은 각각 `Godot --headless --path game res://tests/<장면>.tscn`으로 실행했다.

| 검사 | 결과 | 로그 (`game/output/`) |
|---|---|---|
| 리소스 가져오기 | 종료0 | `campaign-merge-import.log` |
| `dotnet build game/HordeShooter.csproj --nologo -v q` | 오류0·경고0, 종료0 | `campaign-merge-build.log` |
| Integration | 149 통과, 종료0 | `campaign-merge-Integration.log` |
| InteractionUiCheck | 37 통과, 종료0 | `campaign-merge-InteractionUiCheck.log` |
| CampaignPacingCheck | 19 통과, 종료0 | `campaign-merge-CampaignPacingCheck.log` |
| LobbyCheck | 31 통과, 종료0 | `campaign-merge-LobbyCheck.log` |
| NetCodecCheck | 12 통과, 종료0 | `campaign-merge-NetCodecCheck.log` |
| NetSyncCheck 1차 | 실패·종료1: 정지 후 원격 위치 오차0.88m, 기준0.3m | `campaign-merge-NetSyncCheck.log` |
| NetSyncCheck 2차 | 실패·종료1: 적 추적 평균0.39m/최대1.91m, 기준0.3m/1m | `campaign-merge-NetSyncCheck-2.log` |
| NetSyncCheck 3차 | 32 통과, 종료0 | `campaign-merge-NetSyncCheck-3.log` |

동기화 세 실행 사이에 코드·검사 기준·실행 인수를 바꾸지 않았다. 세 번째 통과만으로 앞선 실패가 해결됐다고 처리하지 않는다. 네트워크 타이밍·부하 영향은 가능성이지만 **원인은 미확정**이다. 기존 I-72 검증 한계의 후속 증거로 기록했다. 실패 두 건을 제외한 위 실행에 엔진 오류·종료 경고 없음.

## Windows 배포본

- `Godot --headless --path game --export-release "Windows Desktop" D:/Project/Game/game-hordeshooter/build/campaign-a5b1a49/HordeShooter.exe` 성공·종료0. 로그 `campaign-merge-export.log`.
- 실행 파일·PCK·`data_HordeShooter_windows_x86_64` .NET 데이터 폴더·PLAY-README를 함께 준비했다. 소스 커밋 `a5b1a49`. 빌드 폴더와 ZIP은 생성 산출물로 커밋하지 않는다.
- 배포 EXE를 두 독립 프로세스로 실행했다. 호스트 `--headless --max-fps 60 --quit-after 2100 -- --host --lobby=`, 클라이언트 `--headless --max-fps 60 --quit-after 900 -- --join=127.0.0.1 --lobby=`. 실제 UDP24680 접속, 호스트의 `peer joined`, 클라이언트의 `synced with the host` 확인. 공개 방 목록 등록을 끈 로컬 검사다. 양쪽 종료0.
- 로그: `campaign-release-host.log` · `campaign-release-client.log`, 표준 출력/오류는 각각 `.stdout.log` · `.stderr.log`. 양쪽 종료에 각각 **ObjectDB 잔존2개 경고**가 있었으며 객체별 원인은 식별하지 않았다(I-17 유지). 접속 성공과 종료 경고를 구분한다.

## 판단·남은 범위

첫 2인 테스트용 배포본 생성과 로컬 접속은 확인했다. 두 PC 실제 플레이·인터넷/NAT·방화벽·공개 방 목록·지연/손실·장시간 전투·캠페인 전체 멀티 완주는 미검증이다. 따라서 무문제를 보장하지 않는다. 두 PC에는 같은 ZIP 전체를 풀고, 같은 공유기에서는 방장의 LAN 주소를 사용한다. 다른 집에서 연결할 때는 호스트의 UDP24680 접근 가능 여부가 필요하다(PLAY-README, NetLaunch, PortOpener).

새 게임 동작/수치/결정은 없으며 새 이슈 ID를 발급하지 않는다. 기능 기록018과 UI 기록017은 당시 브랜치 상태를 보존하고, 이번 통합·배포 결과는 이 새 기록에 남긴다.
