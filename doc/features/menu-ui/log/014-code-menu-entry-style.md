# 메뉴·게임 진입 전 화면 스타일 통일

사용자 요청: ‘메뉴 ui 와 시작할때 게임 진입전 ui도 같은 일관성 퀄리티…특히 메뉴에 주황색노란색은 안어울리는거 같아’. 관련 M-38 후속, M-0/M-21 메뉴·로비 계약. 기준 `550da20`, 기존 `codex/ui-consistency` worktree에서 이어서 작업했다.

## 변경

- 홈/커스터마이징/출격 준비/멀티 목록/로비/설정/그래픽 화면의 캐릭터 영역과 조작 영역을 같은 둥근 반투명 패널로 묶었다. 제목 정렬·여백을 정리하고 출격/방 시작/준비의 청백색 강조를 명확히 했다.
- `UiTheme`의 공통 스타일을 탭·드롭다운·팝업·슬라이더·스크롤 바·체크 스위치·입력칸까지 확장했다. 일반 버튼뿐 아니라 키보드 포커스/호버/누름/비활성 상태도 같은 색을 사용한다. 메뉴 장식/강조에 주황·노랑을 사용하지 않는다. 팝업 불투명도는 .98로 뒤쪽 문구의 간섭을 줄였다.
- 캐릭터 미리보기의 따뜻한 키 조명과 카키 받침대를 중립 흰색 조명/청회색 받침대로 바꿨다. 캐릭터 재질과 색상 견본은 실제 값을 유지한다. 게임 월드 조명 변경 없음.
- 종 선택을 색 곱셈 대신 버튼의 실제 눌림 상태로 표시한다. 키보드 포커스와 현재 종 선택을 구분하며 기존 선택/저장 경로를 유지한다.
- 메뉴 구조·모드·로비 준비/시작·언어/설정 저장 계약은 유지했다. 새로운 화면이나 별도 로딩 단계를 추가하지 않았다.

## 검증

Windows, Godot4.7.2 .NET. 빌드: `dotnet build game/HordeShooter.csproj --nologo -v q`. 전용 장면은 각각 `Godot --path game [--resolution 1280x720 /1600x900 또는 --headless] res://tests/<장면>.tscn`으로 실행했다.

| 검사 | 결과 | 로그 (`game/output/`) |
|---|---|---|
| 기준 MenuCheck 실제1280×720 | 44 통과, 종료0 | `menu-polish-baseline.log` |
| 최종 빌드 | 오류0·경고0, 종료0 | `menu-polish-build.log` |
| MenuCheck 실제1280×720 | 45 통과, 종료0 | `menu-polish-1280.log` |
| MenuCheck 실제1600×900 | 45 통과, 종료0 | `menu-polish-1600.log` |
| GraphicsOptionsCheck 실제1280×720 | 37 통과, 종료0 | `menu-polish-graphics.log` |
| Integration headless | 149 통과, 종료0 | `menu-polish-Integration.log` |
| LobbyCheck headless | 31 통과, 종료0 | `menu-polish-LobbyCheck.log` |
| AppearanceExpansion headless | 68개 PASS, 완료 마커, 종료0 | `menu-polish-AppearanceExpansion.log` |

이번 실행에서 검사 실패·엔진 오류·종료 경고 없음. `MenuCheck`에 종 선택 지속 검사1개를 추가하고 탭/그래픽/한글 설정/드롭다운 렌더를 보강했다. 1280 렌더 후 팝업 alpha만 .83→.98로 높였으며 최종1600 렌더에서 확인했다. `git diff --check` 통과. Git의 CRLF→LF 정규화 안내는 빌드/게임 종료 경고와 구분한다.

## 렌더 확인

아래는 최종1600×900 실제 렌더. `MenuCheck`를 창 모드로 실행하면 생성되며 `output/`은 커밋하지 않는다.

- [홈](../../../../game/output/menu-home.png), [커스터마이징](../../../../game/output/menu-customize.png), [장비 탭](../../../../game/output/menu-customize-gear.png).
- [출격 준비](../../../../game/output/menu-single.png), [드롭다운](../../../../game/output/menu-squad-popup.png), [멀티 목록](../../../../game/output/menu-multi.png), [로비](../../../../game/output/menu-lobby.png).
- [설정 한글](../../../../game/output/menu-settings-kor.png), [설정 영문](../../../../game/output/menu-settings-eng.png), [그래픽](../../../../game/output/menu-graphics.png).

한글 전체 메뉴와 영문 설정의 텍스트/여백/선택 상태를 확인했다. 캐릭터 고유의 부리·발·장비색은 메뉴 강조색이 아니므로 보존했다.

## 기록·남은 제약

M-38 본문에 이전/이후와 사용자 근거를 보존하고 상위 색인/체크리스트/[공통 규격](../ui-style-spec.md)을 갱신했다. 이번은 기존 스타일 방향의 확장이며 새 결정·이슈 ID는 필요하지 않다. 핵심 게임 방향과 밸런스는 바뀌지 않는다.

두 PC 실제 네트워크·패키지 내보내기·모든 언어/초광폭 화면은 미검증이다. 로비 검사는 같은 PC의 독립 ENet 호스트/클라이언트로 수행했다. 외형 최종 수락은 별도다. 코드와 기록을 같은 후속 커밋에 포함하며 main 병합·원격 푸시는 하지 않았다.
