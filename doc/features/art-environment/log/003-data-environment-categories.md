# 환경 분류 데이터와 명시적 검증

- 범위: [환경 분류 규격](../environment-categories.md), `environment_categories.json`, `EnvironmentCategories.cs`, `EnvironmentCategoriesCheck` 장면을 추가했다. 기존 맵, 실행 조명, 날씨 효과, 무작위 시간 선택은 변경하지 않았다.
- 내용: 실내/실외, 실내 빛, 실외 시간, 날씨, 바닥을 분리했다. 한국어·영어 이름과 14개 예제를 두고, 비 온 뒤/눈 쌓인 뒤를 강수와 잔류 바닥 상태의 조합으로 표현한다. 실내 강수는 바깥 문맥이며 표시 대상으로 계산하지 않는다.
- 분담 단계 검증(통합 전 기록): 검사 소스를 먼저 작성했다. PowerShell `ConvertFrom-Json`으로 JSON 파싱과 14개 예제/각 축 항목 수를 확인했다. `git diff --check`는 통과했다(새 파일은 아직 미추적 상태). 공유 프로젝트 빌드·Godot 실행은 주 작업자 한 명만 수행하므로 이 분담 작업에서는 실행하지 않았다. 이 단계에서는 C# 실행을 하지 않았으며 이후 통합 실행 결과는 아래에 기록했다. 화면을 바꾸지 않는 카탈로그만 추가했으므로 새 렌더 대상은 없다.
- 남은 제약: 분류 데이터의 의미만 준비되었고 맵 저작 UI·조명 적용·날씨 효과·지면 표현 연결은 후속이다. 시간/날씨 조합의 기후 현실성은 제한하지 않는다.
- 통합 검증: 주 작업자의 C# 빌드 오류·경고0, Godot `EnvironmentCategoriesCheck.tscn` **265 통과**. 실제 실행 로그 `game/output/environment-categories-check.log`. 생성된 두 C# `.uid` 포함. 통합 결정 [M-37](../../menu-ui/decisions.md#m-37-개인-그래픽-품질과-명시적-환경-분류).
- 결정/이슈 검토: 사용자 요청과 [구현 계획](../../../../docs/superpowers/plans/2026-10-28-graphics-options.md)을 구체화했다. 새로운 전역 결정 ID나 기존 이슈 변경은 이 분담에서 만들지 않았으며 통합 기록은 주 작업자가 관리한다.
