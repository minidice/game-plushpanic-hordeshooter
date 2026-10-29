# 021 — Pretendard 공통 UI와 언어별 글꼴

관련: M-42, M-38, T-7. 작업일 2026-10-30. 기준 커밋 `36bda13`, 작업 브랜치 `codex/pretendard-ui`.

커밋 전 최신 main `96094e3`을 fast-forward/autostash로 받아 충돌 없이 반영했다. 최종 빌드와 FontCheck→MenuCheck→Integration은 이 상태에서 순차 재실행했다.

## 변경

- 공식 Pretendard v1.3.9 배포 ZIP에서 Regular/Bold/ExtraBold 정적 OTF와 LICENSE.txt를 원본 그대로 가져왔다. 폰트3개 총4,719,524바이트. `.import`·새 스크립트 `.uid` 포함.
- `content/data/ui_fonts.json`의 kor/eng 각각 body/menu/title 경로. 언어·역할 미설정, 누락/잘못된 파일, 손상된 JSON은 시스템 글꼴 대체. 미지원 글자는 시스템 폴백을 허용한다.
- `UiFonts`의 역할별 공유 FontVariation을 `Loc.LanguageChanged`에서 갱신. 이미 만든 테마·Label3D도 글꼴 교체를 받는다. 기본 Regular, 메뉴 버튼/탭/팝업 Bold, 게임 제목 ExtraBold. 다른 Label/HUD/월드 글씨는 Regular.
- 메뉴·HUD·카드/투표의 기존 공통 테마와 월드 바닥/알림/상태 표식/훈련 허수아비 글씨까지 연결. 무기 이름표는 기존 UiTheme 기본 글꼴 참조로 함께 적용된다. 독립 CanvasLayer의 팀 카드 안내는 Label 생성 시 공유 테마를 지정하고, 스테이지 전환 제목 컨테이너에도 공유 테마를 연결한다.
- 내보내기 필터에 폰트 LICENSE.txt 포함. `Export-Game.ps1`은 실행 파일 옆 `licenses/<폴더>/LICENSE.txt`에도 복사한다.

## 검사와 결과

Godot `4.7.2.stable.mono.official.ed1daf0bf`, .NET8. 같은 worktree에서 import/build는 순차 실행.

| 실행 | 결과 |
|---|---|
| 변경 전 `dotnet build game/HordeShooter.csproj --nologo -v q`, MenuCheck | 빌드 경고0/오류0, 메뉴54 통과 |
| 최초 FontCheck (기존 구현) | 의도한 실패: 번들 Pretendard 계약 미충족, `output/font-red.log` |
| 최종 빌드 | 경고0/오류0 |
| `--headless --editor --import --path game --quit` | 통과, 오류/경고 없음 |
| `--headless --path game res://tests/FontCheck.tscn` | **24 통과**. 한·영, 문자, 역할, 기존 참조 갱신, 독립적 설정, 오류 대체, 독립 HUD/전환 화면. 의도적으로 잘못된 설정3종에 대한 진단 경고3개. 최종 종료/RID 누수 경고 없음 |
| `--headless --path game res://tests/MenuCheck.tscn` | **54 통과**, 종료 경고 없음 |
| `--headless --path game res://tests/Integration.tscn` | **149 통과**, 종료 경고 없음 |
| `--headless --path game res://tests/StageTransitionCheck.tscn` | **227 통과**, 종료 경고 없음 |
| StageTransitionCheck 실제 렌더 1280×720 | **227 통과**, 오류/경고 없음. `transition-3-title.png`에서 한글 스테이지 이름의 Regular 표시 확인 |
| MenuCheck 실제 렌더 1600×900, 1280×720 | 각 **54 통과**, 한·영 홈/설정 및 상세 화면 생성. 오류/경고 없음 |
| InteractionUiCheck 실제 렌더 1280×720, 기본/`-- --english` | 각 **37 통과**, 오류/경고 없음 |
| AssetPathsCheck | **실패**: 기존 `PlaySetupPanel.ArtPath`의 `res://assets` 직접 경로. `git show 36bda13:game/scripts/UI/PlaySetupPanel.cs`에도 동일한 코드이며 이번 변경 없음. 이 검사를 통과했다고 보고하지 않는다 |
| Export-Game.ps1 PowerShell AST 파싱 | 문법 오류0. 전체 배포/ZIP 생성은 미실행 |
| 문서·에셋 검증 | 관련 문서의 로컬 파일 링크57개 누락0, git diff --check 통과. 폰트3개/라이선스1개의 SHA256이 공식 ZIP 원본과 모두 일치 |

스테이징 후 `git diff --cached --check`는 원본 `LICENSE.txt` 22행의 끝 공백1개를 보고한다. 라이선스 원본 보존을 위해 수정하지 않았다. 해당 공급자 파일을 제외한 staged diff의 공백 검사는 통과했다.

최종 실행 로그: `game/output/font-final-{FontCheck,MenuCheck,Integration}.log`, 렌더 로그 `font-menu-render-{1600,1280}.log`, `font-interaction-{kor,eng}.log`. 위 output은 로컬 재생성 산출물이며 커밋하지 않는다.

### 검증 중 수정·리뷰

- 초기 검사에서 ExtraBold를 GetFontWeight()==800으로 판단했으나 Godot은 정적 `Pretendard ExtraBold`를700으로 보고한다. 실제 파일/패밀리 선택을 확인하도록 검사를 수정했다. 폰트 파일은 변경하지 않았다.
- 초기 FontCheck가 검사 노드를 Dispose만 하여 종료 시 ObjectDB/RID 경고를 냈다. 노드 트리를 Free하도록 고친 뒤 최종 검사에는 해당 경고가 없다. 제품 런타임 누수로 기록하지 않는다.
- 독립 코드 리뷰가 캐시된 비폰트 리소스의 `GD.Load<Font>` 캐스트 예외를 지적했다. 캐시된 PackedScene 경로로 **InvalidCastException을 재현**한 뒤, `GD.Load(path) is Font`로 타입 확인하여 시스템 폴백하게 수정했다. 회귀 검사 포함 최종22 통과.
- 전체 UI 경로 점검에서 기존 테마를 상속하지 않는 팀 카드 안내/스테이지 전환 제목을 확인해 함께 연결했다. 전환 제목 폰트의 실패를 먼저 확인했고 독립 HUD 검사도 추가했다. HUD 검사는 테마 상속이 적용되는 실제 트리에 넣어 확인하도록 수정했다. 전체 FontCheck는 최종24 통과이며 개발 중 실패 로그는 `font-transition-red.log`, `font-standalone-red.log`에 남겼다.

## 실제 렌더 확인

- `game/output/font-1280/menu-home.png`, `menu-home-eng.png`: 실제 한·영 메뉴, 굵은 제목/메뉴, 글자 누락·잘림 없음.
- `game/output/font-1600/menu-settings-kor.png`: 설정·캐릭터 설명·조작 안내는 Regular, 선택 항목/버튼 Bold. 레이아웃 유지.
- `game/output/interaction-ui-hold.png`, `interaction-ui-hold-eng.png`: 한·영 HUD/홀드 안내/탄수/알림 표시 확인.
- `game/output/interaction-ui-inventory.png`: 인벤토리·HUD·월드 이름표/바닥 글씨 확인. InteractionUiCheck는 후반 GameSettings.Reset으로 한국어로 돌아오므로 영문 실행의 인벤토리/메뉴 캡처도 한국어이며 영문 검증으로 세지 않는다.
- 1280/1600 메뉴 렌더와 통합 검사가 한 차례 겹쳐 실행되었다. import/build는 겹치지 않았고 각 검사 모두 통과했으나 저장소의 검사 순차 실행 규칙을 놓친 절차상 제약이다. 최종 헤드리스 FontCheck→MenuCheck→StageTransitionCheck→Integration, 이후 StageTransitionCheck 렌더는 순차 재실행했다.

## 기록 검토·남은 제약

- 새 결정 M-42의 번호가 색인에서 비어 있음을 확인하고 사용자 요청/제작값을 구분해 기록했다. UI 규격·다국어 규격·체크리스트 갱신. 기존 게임 동작 결정을 바꾸지 않았다.
- 새 제품 버그를 해결한 작업이 아니라 폰트 기능 추가이며, 개발 중 잡은 캐스트/검사 정리 문제는 위 기록에 남겼다. 새 이슈 ID는 발급하지 않았다. 기존 에셋 경로 검사 불일치는 이 작업에서 수정하지 않았다.
- 정식 일본어/중국어/아랍어 번역, 모든 OS/설치 폰트 조합, 배포 실행 파일/PCK/ZIP은 미검증. 시스템에도 없는 문자까지 보장하지 않는다. 외형은 사용자 최종 수락 전이다.
