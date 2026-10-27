# 환경 아트 — 결정 본문

새 결정은 여기 ID 아래에 쓰고, [전체 색인](../../decisions.md)에는 ID · 한 줄 요약 · 상태 · 날짜 · 링크만 한 줄로 적는다. 결정 ID는 지금처럼 영역 접두사(D · M · C · T · L · A · Q)를 쓴다.

**옛 기록은 옮기지 않고 그대로 있다**(2026-10-26 결정, [기록 문서 나누기](../../records-split-plan.md) 6.4). 이 기능의 2026-10-26 이전 결정 · 이슈 · 확인 기록은 [decisions.md](../../decisions.md) · [issues.md](../../issues.md) · [feature-checklist.md](../../feature-checklist.md)에 있다.

<!-- 새 결정 예:
## C-33 결정 제목
- 결정:
- 이유:
- 상태 · 날짜:
- 근거 코드 · 규격:
-->

## A-30 Stage 1 도시 바닥: 도로 · 보도 · 콘크리트, 풀 없음

- 결정: Stage 1(도시) 바닥을 단색 판 대신 **도로처럼** 그린다. 풀은 깔지 않는다(틈에서 난 작은 잡초만).
  - **길(L-38 B · D · F 길)**: 닳은 아스팔트(보수 자국 · 균열 · 기름 얼룩), 벽 쪽에 **보도블록 띠**(주 길 2.4 m, 옆길 1.4 m, 교차 골목은 없음) · **연석** · 연석 옆 배수 홈, 주 경로를 따라 **중앙 점선**(3 m 칠, 3 m 빔).
  - **작은 구역 · 주머니 · 기지**: 3 m 콘크리트 판(줄눈 · 얼룩).
  - **담 뒤 빈 땅**: 풀 대신 자갈 공터.
  - **바닥 조각(Codex, `assets/urban-bits` 7종)**: 맨홀 · 빗물받이(연석 옆) · 쓰레기 · 낙엽 · 잔해 · 틈 잡초 · 물웅덩이 — 충돌 · 그림자 없는 장식.
- 이유: 사용자(2026-10-28) — "스테이지 1은 바닥 퀄리티만 먼저 스테이지 3 정도 퀄리티까지만 개선", "길 주변 정리 도로처럼 처리? 여긴 풀은 안 어울리겠지? 모델링은 모두 Codex로".
- 상태 · 날짜: `[확정]` 방향, 폭 · 색 · 밀도는 `[임시]` · 구현 2026-10-28([log/001-code-stage1-urban-ground.md](log/001-code-stage1-urban-ground.md)).
- 근거 코드 · 규격: `game/scripts/World/LayoutMapUrbanGround.cs`, `game/shaders/urban_ground.gdshader` · `road_marking.gdshader`, [모델 지시문](../../model-briefs/urban-ground-bits.md), `prototypes/outdoor-kit/tools/urban_ground_bits.py`. A-24(야외 바닥은 그려서 섞는다)의 도시판.
