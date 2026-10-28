# 016 · code · 게임 설정(모드 → 카드 → 난이도)과 설정 정리 (M-39 · M-40)

- 작성일: 2026-10-28. 종류: code(메뉴 · 방 동기화 · 난이도 규칙 · HUD · 그림 연결 · 검사). 관련: M-39 · M-40(새), M-17 · M-18 · T-19 · C-7(바뀜), L-39, C-31, [설계안](../play-setup-plan.md), [그림 지시문](../../../model-briefs/play-setup-cards.md).
- 사용자: "시작 메뉴와 설정을 좀 정리할까? … 시나리오 / 거점 / 난전 가장 큰 메뉴 선택하면 바로 아래 시나리오 중 선택 or 거점 타입별 선택 or 난전 테마 선택 … 그 하단에 난이도 선택, 난이도에 따라 부활 가능 횟수 … 그림은 Codex 시켜 항상." 선택: 난이도 3단계, Codex 일러스트, 시나리오 ① 1 → 2 → 3 그대로, 인게임 표시 설계안대로.

## 변경 사항

- **게임 설정 위젯**(`scripts/UI/PlaySetupPanel.cs`): `PlaySetup`(모드 · 카드 · 난이도)과 `PlaySetupPanel`. 싱글 플레이 페이지와 방 페이지가 같은 위젯을 쓴다(노드 이름 앞에 `Play_` · `Lobby_`).
  - 모드 탭 3개
  - 고른 카드의 큰 그림 + 이름 · 한 줄
  - 그 모드의 카드 줄(그림 아이콘 + 이름)
  - 난이도 3칸과 규칙 한 줄("한 생명에 쓰러짐 N번까지 · AI 동료는 스테이지마다 N번 다시 합류")
- **카드**: 시나리오는 `campaign`(도시에서 강까지 = L-39 큰 캠페인, 지금 큰 Stage 1 → 2 → 3)과 `short`(창고 지구, 지금 기본 소형 맵). 거점 · 난전은 `zombie` · `animals` · `armed`(감염무리 · 수렵단 · 무장단).
- **세션 · 저장**:
  - `GameSession.Campaign` · `Difficulty` · `Themes` · `Campaigns`가 새로 생겼다. `LargeCampaign`은 캠페인 `campaign`이면 참이다.
  - 코드 기본값은 `short`라서 검사 장면 · 바로 연 장면은 그대로 소형 맵이다. 메뉴가 읽는 저장값의 기본은 `campaign`.
  - 저장 키 `menu.campaign` · `menu.difficulty`를 더했다. `options.recoil_scale` · `options.revive`는 없앴다. 잘못된 값은 기본으로 돌아간다.
- **시나리오 적 계열은 맵이 정함**: `StageMap.EnemyTheme`(기본 감염무리)과 `LayoutMap`의 레이아웃 키 `enemy_theme`. `Stage.ThemeId`는 시나리오면 맵의 것, 거점 · 난전이면 카드의 것을 쓴다.
- **방**(`NetLobby`): `Mode` 대신 `Setup`을 쓴다. 명단과 함께 카드 · 난이도를 보내고, `MatchStarted`도 모드 · 카드 · 난이도 · 시드를 함께 보낸다. 방장이 바꾸면 모두의 준비가 풀린다(전처럼). 참가자는 버튼이 꺼져 보기만 한다.
- **난이도**(`Difficulty` 쉬움 · 보통 · 어려움):
  - `squad_rules.json`의 `difficulty` 표(쓰러짐 3 · 2 · 1, 합류 3 · 2 · 1), `SquadRules.Downs` · `Respawns`, `Stage.Difficulty` · `DownsPerLife`.
  - 부활은 실제 게임 장면에서 항상 켜진다(검사 장면은 꺼짐 그대로).
- **HUD**:
  - 이름 옆 남은 쓰러짐 점(`Hud.DownPips`, ● 남음 · ○ 씀)
  - 분대 칸: 서 있으면 점, 죽으면 "N초 뒤 합류 · 남은 N번" / "사망 · 합류 없음"(`Stage.SquadReturn`)
  - 시간 칸 앞 난이도 이름
  - 일시정지 화면 첫 줄에 모드 · 캠페인/적 계열 · 난이도 · 규칙(`SetupLine`)
- **설정 정리**:
  - 게임플레이 칸(사격 반동 밀림 슬라이더 · 부활 켜기)을 없앴다. `RecoilScale`은 1로 고정했고, 검사는 지금처럼 바꿔 쓴다.
  - 싱글 페이지의 테스트 옵션(스테이지 테마 · F8 탈출)을 없앴다. F8은 실행 옵션 `Start-Game.ps1 -DebugEscape`(`--debug-escape`)로 쓰고, 기본은 꺼짐이다(전에는 켜짐).
- **문구**(kor · eng):
  - 새 키: `play_*` · `difficulty_*` · `fmt_difficulty_rule` · `fmt_pause_setup` · `fmt_hud_squad_return` · `hud_squad_no_return`
  - 바꾼 키: `fmt_hud_timer`(난이도 `{3}`), 홈 설명 두 줄
  - 없앤 키: 설정 · 테스트 옵션 키 10개
- **그림**: Codex가 `prototypes/ui-icons/tools/play_setup_cards.py`를 썼고(새 파일 하나, `py_compile`만), Claude가 실행해 단언을 통과했다(`PLAY_CARDS_BUILT 8`). 결과는 `game/assets/ui/play-setup/play_*.png` 640 × 400 8장. 시나리오는 해 질 녘 도시 · 창고와 숲, 거점은 발전기 진지, 난전은 달밤 풀밭 + 보스이고, 적 계열마다 무리 모양이 다르다.

## 검사

- 빌드: 오류 0.
- 종료 코드 0, 실패 0, 누수 0:
  - `MenuCheck` 48(창 모드, 새 항목): 시나리오 카드 2 · 거점/난전 카드 3, 캠페인 고르면 큰 스테이지, 적 계열 카드, 모드를 오가도 카드 기억, 난이도 기본 보통 · 어려움 규칙, 테스트 옵션 없음, 방의 같은 설정, 설정에 반동 · 부활 없음, 캠페인 · 난이도 · 적 계열 저장 · 복원 · 잘못된 값
  - `SquadCheck` 52(새: 난이도별 쓰러짐 · 합류와 HUD 점)
  - `LobbyCheck` 31 · `NetSyncCheck` 32 · `PartyNetCheck` 37 · `OnslaughtNetCheck` 29 · `NetCodecCheck` 12
  - `EnemyExpansion` 60 · `ModeCheck` 147 · `LargeStageCheck` 160 · `Integration` 149 · `InteractionUiCheck` 34(창 모드)
- `StageTransitionCheck`:
  - 처음 실패: 소품 목록 85개(기준 84)였다. 원인은 main에서 따로 합쳐진 두 변경이다. 도시 조각 7종(`0fd89e3`, 기준 77 → 84)과 숲 표면 돌 `surface_boulder` 1종(`2be23f4`, `stage2-props/manifest_forest_surface.json`)이 겹쳤다.
  - 각각은 통과했지만 합쳐진 main에서는 실패하던 것이다. 기준을 85로 고치고 225 통과.

## 렌더

- `output/menu-single.png` · `menu-lobby.png` · `menu-settings-kor.png` · `menu-home.png`: 카드 그림 · 난이도 · 규칙 줄, 설정은 언어 · 그래픽 · 소리만.
- `output/interaction-ui-revive.png` · `interaction-ui-complete.png`: 시간 칸 "보통 · 00:14 · 처치 0", 이름 옆 "거위 ●●", 동료 칸.
- 처음 렌더에서 카드 줄 그림이 작아 카드 높이 92 → 128, 아이콘 폭 168로 키움.

## 남은 제약

- 일시정지 화면 첫 줄과 동료 "N초 뒤 합류" 문구는 코드 · 검사로만 확인했다. 렌더로는 아직 보지 못했다.
- 큰 캠페인(`campaign`)을 방(멀티)에서 시작하는 실제 2인 플레이는 아직 해 보지 않았다. 전에는 "싱글 전용"이었고, 이제 참가자도 카드를 받아 같은 맵을 짓는다.
- 난이도는 아직 부활 횟수만 바꾼다(적 행동 차이는 나중, 난이도 방향 B안).
- 카드 그림은 첫 결과다. 사용자 피드백에 따라 Codex로 고친다.
