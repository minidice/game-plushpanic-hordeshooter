# 019 · ESCAPE FUR DEAD 타이틀

관련 M-0. 작업일 2026-10-29.

- 사용자 결정: 게임 타이틀을 ESCAPE FUR DEAD로 변경한다. 캐릭터/배경 품질과 Tripo 사용에 대한 질문은 검토 요청이며 자산 교체 승인으로 해석하지 않는다.
- 변경: 홈의 `ESCAPE` / `FUR DEAD` 두 줄 제목, 한글/영문 상세 메뉴 제목, 런타임 창 제목, Windows export 제품명/파일 설명. 홈은 공통 `ui_menu_title`을 읽는다. 기존 52px/2줄 영역을 유지한다.
- 기존 `application/config/name`, C# assembly, 실행 파일 경로는 유지한다. 특히 프로젝트 이름을 바꿔 기존 `user://` 설정/외형 저장 경로가 달라지지 않게 한다. 패키지명 변경이나 저장 파일 이주는 이번 범위가 아니다.
- 검증: `dotnet build game/HordeShooter.csproj --nologo -v q` 오류0·경고0. Godot4.7.2 .NET `--path game --resolution 1600x900 res://tests/MenuCheck.tscn` 54검사 통과·종료0. `game/output/menu-title-escape-fur-dead.log`에 오류/종료 경고 없음. 실제 홈 렌더에서 두 줄 제목과 메뉴의 간격/잘림을 확인했다. [홈 렌더](../../../../game/output/menu-home.png).
- 기록: M-0 기존 항목에 사용자 선택 이름과 이전 가칭을 기록하고 색인/체크리스트/UI 규격을 갱신한다. 단순 표시명 변경으로 신규 버그/별도 결정 ID는 필요하지 않다. 핵심 게임 방향·기능 규칙 변경 없음. 이전018 기록은 당시 가칭 사용의 이력으로 보존한다.
- 남은 범위: 새 이름으로 패키지 재내보내기, 로고 미술, 캐릭터/배경 자산 품질 개선은 이번에 수행하지 않았다. Tripo 계정/결제/외부 업로드/모델 생성은 하지 않았다.
