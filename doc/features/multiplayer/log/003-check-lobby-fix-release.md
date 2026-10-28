# 로비 시작 수정 main 통합·배포

- 2026-10-29, I-133. `321a468` main에 예약 `586ed4b`와 수정 `a25bd69`를 충돌 없이 fast-forward 병합했다. 기존 `.codex-remote-attachments/`는 보존했다.
- main에서 Godot4.7.2 .NET 가져오기 종료0, C# 빌드 오류0·경고0·종료0, LobbyCheck31 통과·종료0을 확인했다. `game/output/lobby-fix-main-import.log`, `lobby-fix-main-build.log`, `lobby-fix-main-LobbyCheck.log`.
- Windows Release export 종료0. `game/output/lobby-fix-main-export.log`. 실행 파일·PCK·.NET 데이터 폴더·PLAY-README·BUILD.txt를 묶은 `build/HordeShooter-lobby-fix-a25bd69-windows.zip`(소스 `a25bd69`)을 준비했다. 생성물은 커밋하지 않는다. 이전 배포본은 덮어쓰지 않았다.
- 두 프로세스의 실제 메뉴→맵 로딩→동기화→로비 복귀→재시작(headless 및 Forward+1280×720) 검사는 같은 코드의 작업 트리에서 실행했다. 상세 실패/통과 이력은 [002](002-code-lobby-loading-timeout.md). 이 새 ZIP으로 서로 다른 두 PC에서 플레이한 것은 아니다.
- 두 실행 파일 모두 새 ZIP 전체를 푼 폴더에서 실행해야 한다. 로딩 시간 때문에 끊긴 I-133 수정이며 기존 위치 오차 간헐 실패(I-72)나 모든 멀티플레이 문제의 해결을 의미하지 않는다. 새 결정·이슈 ID는 필요하지 않은 통합 검증이다.
