# 환경 분류 카탈로그

2026-10-28 사용자 요청에 따른 **분류 데이터 준비**다. [구현 계획](../../../docs/superpowers/plans/2026-10-28-graphics-options.md)의 그래픽 옵션과 함께 준비하되, 이 파일의 환경 분류는 현재 맵의 조명·재질·날씨를 변경하지 않는다. 시간 무작위 선택, 진행 시계, 날씨 파티클, 조명 프리셋 적용은 구현하지 않았다.

데이터: [`environment_categories.json`](../../../game/content/data/environment_categories.json). 로더: [`EnvironmentCategories.cs`](../../../game/scripts/World/EnvironmentCategories.cs). 안정적인 소문자 ID와 `kor` / `eng` 표시 이름을 함께 둔다. `version: 1`은 아래 ID와 필드만 받으며, 항목 누락·중복·오타는 오류다.

| 축 | ID | 뜻 |
|---|---|---|
| `domain` | `indoor`, `outdoor` | 실내 / 실외 |
| `indoor_light` | `dark`, `daylit` | 완전히 어두운 실내 / 햇빛이 조금 들어오는 실내 |
| `outdoor_time` | `dawn`, `morning`, `noon`, `sunset`, `dusk`, `night` | 새벽 / 아침 / 밝은 한낮 / 붉은 노을 / 어두워지는 저녁 / 밤 |
| `weather` | `clear`, `sunny`, `overcast`, `rain`, `snow` | 강수 없음 / 햇빛 / 흐림 / 비 / 눈 |
| `ground` | `dry`, `wet`, `snow_covered` | 마른 바닥 / 젖은 바닥 / 눈 쌓인 바닥 |

실내는 `indoor_light`를 필수로 고르고 `outdoor_time`을 생략하거나 `null`로 둔다. 실외는 그 반대다. 날씨와 바닥 상태는 항상 필요하고 서로 독립적이다. `clear`는 강수가 없는 기본 문맥이고, `sunny`는 햇빛을 강조하는 문맥이다. `dark`는 환경 분위기 의도이며 플레이를 읽을 수 없게 검게 만들라는 실행 명령이 아니다.

실내의 날씨는 **건물 바깥 상황**을 뜻한다. 낮빛이 드는 실내와 바깥의 비를 함께 지정할 수 있으며, `EnvironmentSelection.HasVisiblePrecipitation`은 실외의 `rain` / `snow`일 때만 참이다. 이 값은 향후 소비자를 위한 의미 규칙이며 지금 파티클을 생성하지 않는다. 실내 예제의 바닥은 `dry`지만, 젖은 실내 등의 향후 저작을 막는 추가 물리 제약은 두지 않는다. 시간과 날씨도 별도 저작 축이므로 이 버전은 일조 각도나 지역 기후의 현실성까지 검증하지 않는다.

조합 예제 14개를 `examples`에 둔다. 비 온 뒤는 `weather: clear` + `ground: wet`, 눈이 그친 뒤 쌓인 눈은 `weather: clear` + `ground: snow_covered`다. 계속 내리는 눈은 `weather: snow`, 햇빛 아래 쌓인 눈은 `weather: sunny` + `ground: snow_covered`로 구별한다. 예제는 시작 맵, 랜덤 목록, 전환 순서를 뜻하지 않는다.

로더 사용은 명시적이다. `EnvironmentCategories.Load()` 또는 `FromText(text)` 후 `Errors`를 확인하고, `Validate(EnvironmentSelection)`으로 선택을 검증한다. 실패하면 진단과 빈 카탈로그를 반환해 부분적으로 잘못된 데이터를 노출하지 않는다. 분류·예제·진단은 읽기 전용 컬렉션이며 선택과 이름은 불변 레코드다. 자동 로드 등록이나 `Stage` / `LayoutMap` 참조는 없다. 기존 맵 파일에 필드를 요구하지 않으므로 기존 로딩 경로를 유지한다.

향후 연결 시 맵/구역이 선택값을 명시적으로 저장하고, 저작 도구가 이 카탈로그로 검증한 뒤 별도 렌더링 코드가 적용해야 한다. 그 연결과 실제 조명·날씨·지면 표현은 후속 범위다.

전용 검사: `Godot --headless --path game res://tests/EnvironmentCategoriesCheck.tscn`. 모든 예제, 120개 유효 축 조합, 실내 강수 비표시, 잘못된 조명 조합·ID·필드·버전·JSON·누락 파일을 검사한다.
