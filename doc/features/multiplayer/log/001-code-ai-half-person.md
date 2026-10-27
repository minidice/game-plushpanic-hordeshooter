# 001 · code · AI 동료는 인원 보정에서 0.5명 (M-34)

- 작성일: 2026-10-27. 종류: code. 관련: **M-34(새)**, M-5 · M-31(인원 보정), D-15, I-123(AI 조준). 주 기록: [레벨 log/010](../../level/log/010-code-stage1-pacing-pressure.md).
- 사용자: "AI는 0.5명으로 세고" (플레이 1 + AI 1에서 "위험한 일이 전혀 없음" 뒤).

## 변경 사항

- `Stage.PartySize`가 소수를 돌려줌: 사람 1명씩 + AI 동료 0.5명씩(`Stage.SquadmateWeight`). 호스트는 로비 인원과 큰 쪽(로비엔 AI 없음). `PartyOverride`도 소수.
- `PartyScaling.For(float)`: 표의 두 줄 사이를 선형 보간(1.5명 = 1인 50% · 2인 70% → 일반 등장 60%, 일반 체력 85%, 네임드 체력 58.5%). `PartyScale.Players`는 소수.
- `party_scaling.csv` 설명 줄: "AI 동료는 0.5명씩 더함 · 사이 값은 보간".

## 검사

- `PartyScaleCheck` 25 통과(새: 1 + AI 1 = 1.5명, 1.5명 줄이 1인 · 2인 줄의 가운데, 다른 사람 생존자는 1명). 그 밖의 회귀는 레벨 log/010.

## 남은 제약

- 0.5라는 값은 AI 조준 문제(I-123)를 고친 뒤 다시 본다.
