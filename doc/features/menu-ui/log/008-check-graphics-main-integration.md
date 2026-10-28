# 008 · check · 숲 표면과 그래픽 옵션 메인 통합

- 요청: 현재 숲 표면 개선, 그래픽 옵션·환경 분류, 나무 단순 그림자 수정과 관련 문서를 커밋하고 최신 메인을 확인해 병합·원격 main에 푸시한다.
- 범위: [숲 표면002](../../art-environment/log/002-art-forest-surface-quality.md), [환경 종류003](../../art-environment/log/003-data-environment-categories.md), [그래픽006](006-code-graphics-options.md), [나무 음영007](007-code-tree-simple-shadow.md). 자동 생성 output/.godot 및 개인 첨부 폴더는 커밋하지 않는다.
- 통합 기준: `origin/main`을 fetch해 기준 `0fd89e3`과 동일함을 확인했다. 기존 main의 추적 파일 변경은 없으며 개인 `.codex-remote-attachments/`는 보존한다.
- 병합 전 검증: 최종 C# 빌드 경고0/오류0, 프로젝트 기본 통합 검사 `Integration.tscn` **149 통과**, 종료 코드0·오류/경고 없음(`game/output/graphics-merge-integration.log`). 변경 영역 전용 검사/실제 렌더/성능 결과는 위 작업 기록에 있으며 새 코드를 추가하지 않는 통합 작업에서는 같은 성능 측정을 반복하지 않는다.
- 결정/이슈 검토: 기존 M-37/A-23/A-24/A-25 구현을 통합하는 작업으로 새로운 제품 결정이나 이슈 ID를 추가하지 않는다. 환경 분류의 실제 조명/날씨 효과 연결, 저사양 GPU 및 장시간 다인 검증은 여전히 후속이다.
- 검사 렌더·성능 원자료 보존을 위해 작업 트리를 유지한다.

## 메인 병합 검증

- 구현 커밋 `2be23f4`(61파일)를 최신 main에 fast-forward 병합했다. 충돌 없음. 별도 읽기 전용 코드 리뷰에서 critical/important 결함 없음.
- `D:/Project/Game/game-hordeshooter`에서 다시 C# 빌드 경고0/오류0, Godot import 종료0, 실제 GPU `GraphicsOptionsCheck` **37 통과**·종료0. 로그 `game/output/graphics-main-import.log`, `graphics-main-check.log`; 오류/셰이더 오류/종료 경고 없음.
- 현재 메인 폴더의 `./game/Start-Game.ps1 -Zone 3:B -Squad 0` 실행에도 이번 숲 표면/그래픽 변경이 포함된다. 개인 첨부 폴더는 미추적 상태로 보존했다. 원격 업로드는 강제 푸시 없이 `origin main`을 대상으로 한다.
