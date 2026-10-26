# 기능별 기록

기록 문서를 기능 중심으로 나눈 구조다. 2026-10-26 결정: [decisions.md](../decisions.md) T-26, 설계: [기록 문서 나누기](../records-split-plan.md).

- 기능마다 폴더 하나. 그 안에 결정 본문 · 이슈 본문 · 작업 기록(`log/`, 작업 하나에 파일 하나, 번호 순).
- 위쪽 [decisions.md](../decisions.md) · [issues.md](../issues.md) · [feature-checklist.md](../feature-checklist.md)는 짧은 색인으로 쓴다(새 줄은 ID · 한 줄 요약 · 상태 · 링크).
- **옛 기록은 옮기지 않았다.** 2026-10-26 이전 기록은 위 세 문서에 그대로 있다. 옮기는 것은 나중에, 필요한 기능부터 한다.
- 문서 전체 검색: Studio의 `md` 메뉴(`/docs`).

| 폴더 | 범위 |
|---|---|
| [direction](direction/README.md) | 게임 방향: 게임 방향 · 세계관 · 아트 큰 방향 |
| [multiplayer](multiplayer/README.md) | 멀티플레이: 멀티 · 방(로비) · 동기화 · 인원 보정 |
| [modes-cards](modes-cards/README.md) | 모드 · 카드: 시나리오 · 거점 · 난전 모드, 난전 카드, 소환수 |
| [menu-ui](menu-ui/README.md) | 메뉴 · UI: 메뉴 · HUD · 설정 · 언어 |
| [combat](combat/README.md) | 전투: 무기 · 조준 · 치명타 · 근접 · 반동 · 피격 |
| [enemies](enemies/README.md) | 적: 몬스터 행동 · 등장 · 보스 · 일반 몬스터 조합 |
| [level](level/README.md) | 레벨: 시나리오 맵 · 높이 단 · Stage 1 · 2 |
| [art-characters](art-characters/README.md) | 캐릭터 아트: 플레이어 · 몬스터 모델 · 꼬리 · 장비 · 손 |
| [art-environment](art-environment/README.md) | 환경 아트: 환경 · 소품 · 바닥 · 나무 |
| [effects-audio](effects-audio/README.md) | 이펙트 · 소리: 이펙트 · 상태이상 표시 · 소리 |
| [tech-tools](tech-tools/README.md) | 기술 · 도구: 데이터 · 빌드 · 검사 · Studio · 작업 방식 |
