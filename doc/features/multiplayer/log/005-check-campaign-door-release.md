# 캠페인 문 동기화 수정 main 통합·배포

- 2026-10-29, I-134. main `ae78ce5`에 예약 `be166ff`와 수정 `2036531`을 fast-forward 병합했다. 기존 `.codex-remote-attachments/`는 보존했다.
- main에서 Godot4.7.2 .NET 가져오기 종료0, C# 빌드 오류0·경고0·종료0, CampaignDoorNetCheck13 통과·종료0을 확인했다. `game/output/campaign-door-main-import.log`, `campaign-door-main-build.log`, `campaign-door-main-check.log`.
- Windows Release export 종료0, 로그에 엔진 오류·경고 없음. `game/output/campaign-door-main-export.log`. 배포 EXE headless 시작/종료 검사도 종료0·표준 오류 없음(`campaign-door-package-smoke*.log`). 이 시작 검사가 배포본 멀티 완주를 검증한 것은 아니다.
- `build/HordeShooter-campaign-door-2036531-windows.zip`(105,734,922바이트)을 생성했다. ZIP 무결성과 EXE·PCK·.NET DLL·BUILD.txt 포함을 확인했다(190항목). PLAY-README도 포함한다. 이전 배포본은 덮어쓰지 않았으며 생성물은 커밋하지 않는다.
- 양쪽 모두 ZIP 전체를 새 폴더에 풀어 실행해야 한다. 프로토콜18이므로 이전 빌드와 혼합 접속할 수 없다. I-133 로비 로딩 수정도 포함한다. 두 PC 인터넷 플레이와 I-72 위치 오차는 미검증/별도이며, 새 결정·이슈를 추가할 필요 없는 통합 검증이다. [원인과 회귀 검사](004-code-campaign-door-sync.md).
