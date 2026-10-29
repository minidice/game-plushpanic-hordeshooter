# 012 · code · 배포 실행기의 PC별 Godot 경로와 Windows 템플릿

- 작성일: 2026-10-29. 관련: I-46, 선행 기록 [010](010-code-godot-dir-environment.md) · [011](011-code-nuget-godot-dir.md).
- 사용자 요청: 배포 스크립트도 GODOT_DIR을 읽도록 수정, 현재 PC에 필요한 템플릿 설치, 개발 실행과 배포 빌드 확인.
- 결정·이슈 검토: 기존 I-46의 PC별 빌드 환경 후속으로 기록한다. 사용자가 선택한 경로 우선순위를 배포에도 적용하며 게임 규칙이나 배포 승인 조건은 바꾸지 않는다. 새 결정·이슈 ID는 만들지 않는다.

## 원인과 변경

- MiniToolManage의 프로젝트 경로는 C 드라이브 저장소로 올바르게 설정돼 있었다. 배포 오류 로그는 Export-Game.ps1의 D 경로 Join-Path 실패였다. 이전 채팅은 Start-Game.ps1과 nuget.config만 수정했으며 배포 실행기는 포함하지 않았다.
- [Export-Game.ps1](../../../../game/Export-Game.ps1): 명시한 -GodotDir → 프로세스 GODOT_DIR → 기존 D 경로 순서로 선택한다. 캠페인 검사에 들어가기 전 선택한 경로를 GODOT_DIR에 전달하고, finally에서 기존 환경변수와 작업 위치를 복원한다. 개인 C 경로를 공통 코드에 고정하지 않았다.
- 첫 표준 배포 재시도에서 NetCodecCheck는 23개 통과했지만 검사 실행기가 ExitCode=null로 읽어 실패했다. Windows PowerShell 5.1에서 동일 Godot 실행·표준출력 리디렉션으로 재현: 핸들 미보관은 null, 실행 직후 Handle 보관은 0. 단순 powershell 자식에서는 재현되지 않았으며 실제 Godot 조건으로 확인했다.
- [Run-CampaignMultiplayerChecks.ps1](../../../../game/tests/Run-CampaignMultiplayerChecks.ps1)와 [Run-LobbyStartCheck.ps1](../../../../game/tests/Run-LobbyStartCheck.ps1)은 자식 실행 직후 프로세스 핸들을 보관해 종료 코드를 읽을 수 있게 하고, 정리 시 Dispose한다. 검사 생략·실패 무시·시간 제한 완화는 하지 않았다.
- 루트·게임 README에 배포 환경변수 적용, 템플릿 조건, MiniToolManage/터미널 재시작을 안내했다.

## 현재 PC 설치

- [Godot 공식 4.7.2 다운로드](https://godotengine.org/download/archive/4.7.2-stable/)의 .NET 템플릿 링크로 받은 Godot_v4.7.2-stable_mono_export_templates.tpz 사용. 아카이브 version.txt는 4.7.2.stable.mono.
- `%APPDATA%/Godot/export_templates/4.7.2.stable.mono`에 필요한 Windows x64 디버그·릴리즈 EXE 및 콘솔 래퍼, version.txt, icudt_godot.dat를 설치했다. 다른 플랫폼 템플릿은 설치하지 않았다. 사용자 설치 파일은 Git에 포함하지 않는다.

## 검증

- 수정 전 경로 선택 재현 실패: C 경로 GODOT_DIR을 줘도 D 경로 선택.
- 수정 후 경로 선택: 환경변수 선택·명시 옵션 우선·미설정 D 기본값 및 구문 검사 통과.
- Windows PowerShell 5.1에서 실제 배포 실행기를 검증 경계까지 실행: 명시 경로가 하위 검사에 전달됨, 의도한 검사 중단 후 기존 값/미설정 환경변수와 작업 위치 복원 통과. 초회 검사 도구의 상태 변수 범위 오류는 도구에서 고쳐 재실행했다.
- Start-Game.ps1 -Test: 빌드 경고 0/오류 0, 에셋 가져오기 완료, Integration 149개 통과, 종료 코드 0.
- Start-Game.ps1 일반 실행: 빌드 경고 0/오류 0, 실행기 종료 0, 게임 창 `Horde Shooter - Stage 1 (DEBUG)` 생성 확인. Windows 화면 캡처 도구는 두 번 시간 초과되어 화면 픽셀 확인은 이 경로에서 하지 못했다. 검사 용도로 시작한 게임 프로세스만 종료했다.
- 종료 코드 수집 수정 후 표준 배포의 단일 프로세스 검사 16종과 헤드리스 로비·캠페인 두 프로세스 검사는 전부 종료 0·엔진 오류 0·경고 0이었다. 다만 창 모드에서 클라이언트의 `stage 1: both loaded and released`가 60초 내 성립하지 않아 실패했고, 같은 조건의 별도 재시도도 같은 지점에서 실패했다. 호스트의 후속 `hatch enabled for party` 실패는 클라이언트 이탈 뒤 발생했다. 실패한 표준 보고서는 `game/output/export-gate-render-failed.json`, 첫 재시도 로그는 `export-render-retry-*`로 남겼다.
- 원인 확인을 위해 [CampaignProgressionCheck.cs](../../../../game/tests/CampaignProgressionCheck.cs)의 시간 초과 메시지에 현재 장면·준비 여부·참가자 수·일시정지·수신량·FPS를 추가했다. 검사 조건·대기 시간·게임 동작은 바꾸지 않았다.
- 진단 메시지 추가 후 창 모드 재검사(`export-render-diagnostic-*`)는 호스트·클라이언트 모두 CAMPAIGN_PROGRESSION_PASS, 종료 0, stderr 0바이트. 첫 스테이지 진입·스테이지 2 재접속·스테이지 3 곤돌라 동기화·전체 캠페인 후 로비 복귀까지 확인했다. 재시도 통과만으로 앞선 초기 준비 시간 초과의 근본 원인이 해결됐다고 보지 않는다.
- 실제 Forward+ 렌더: `campaign-ladder-approach-host.png`, `campaign-gondola-client.png`를 열어 사다리 주변 지형·생존자·HUD와 곤돌라 안의 두 생존자를 확인했다. Windows 캡처 도구와 별개로 게임 자체에서 생성한 PNG이다.
- 커밋 전 확인: 남아 있는 표준 배포 보고서 `campaign-validation.json`은 `Passed=false`이며 현재 소스 지문과도 다르다. 따라서 최신 소스의 표준 배포 성공을 확인한 것으로 취급하지 않는다. 창 모드 초기 준비 시간 초과의 근본 원인과 최신 소스의 전체 배포 검증은 남은 제약이다. 검사 실패를 우회하지 않으며, 이번 커밋·푸시는 환경 설정·검사 실행기 보완·진단 메시지를 저장하는 작업이다.
- 커밋 대상 전체 변경에서 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File game/Start-Game.ps1 -Test` 재실행: 빌드 경고 0/오류 0, 에셋 가져오기 완료, `INTEGRATION_PASS 149`, 종료 코드 0. 변경한 PowerShell 스크립트 4개의 구문 검사, 변경 문서의 로컬 파일 링크, `git diff --check` 통과. 이 검사는 전체 배포·창 모드 캠페인 검사를 대체하지 않는다. 사용자가 이 채팅의 실행·NuGet 설정뿐 아니라 배포·멀티플레이 검사 변경까지 함께 커밋·푸시하도록 명시했다.
