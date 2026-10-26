# 001 · doc · 기록 문서를 기능별 폴더로 (T-26)

관련: [decisions.md](../../../decisions.md) T-26, [기록 문서 나누기](../../../records-split-plan.md), [AGENTS.md](../../../../AGENTS.md)

## 변경

- 사용자가 설계안의 추천을 모두 확정했다.
  - 기능 폴더는 11개로 한다.
  - 기록 파일 이름은 날짜 대신 **번호**로 정한다(`log/번호-종류-주제.md`). 사용자가 날짜보다 번호를 선호했다.
  - 새 이슈 ID는 전체 번호를 쓰되 먼저 예약한다.
  - 앞으로 쓰는 기록부터 새 구조를 쓴다.
- [`doc/features/`](../../README.md)를 만들었다. 11개 기능 폴더마다 `README.md`(범위 · 관련 ID · 상세 규격 링크), 새 결정 · 이슈 본문을 쓸 `decisions.md` · `issues.md`, 작업 기록 규칙 `log/README.md`를 두었다.
- **옛 기록은 옮기지 않았다.** 대신 다음 세 곳에 "옛 기록은 세 문서에 그대로 있다"는 안내를 남겼다(사용자 요청).
  - `decisions.md` · `issues.md` · `feature-checklist.md`의 맨 위
  - `doc/features/README.md`
  - 각 기능 `README.md` · `decisions.md` · `issues.md` · `log/README.md`
- `AGENTS.md`의 「기능 변경과 버그 수정 기록」을 새 구조로 바꾸었다.
  - 작업 기록은 기능 폴더에 새 파일로 남긴다.
  - 결정 · 이슈 본문은 기능 폴더에 쓰고, 세 문서에는 한 줄 색인만 남긴다.
  - 새 ID를 만들기 전에 색인에서 빈 번호인지 확인한다.
  - 옛 기록은 지우지 않는다.
- `records-split-plan.md` 맨 위에 확정 내용을 적었고, `decisions.md` T-26에 확정 · 적용을 기록했다.

## 왜 바로 필요했나

`codex/mammal-weapon-arms`를 병합할 때 `feature-checklist.md` 끝에서 다시 충돌이 났다. 양쪽이 같은 파일 끝에 "이번 변경 확인 기록"을 덧붙였기 때문이고, 설계안이 짚은 그대로였다([003](003-check-merge-mammal-weapon-arms.md)).

## 확인

- 링크: Studio `md` 메뉴 검사(`studio/tests/docs.mjs`)가 기능 폴더 문서 45개 이상과 색인 · 옛 기록 묶음을 확인했다. 브라우저로 `doc/features/README.md`의 표 · 링크가 렌더되는 것도 보았다.
- ID: 기존 ID는 바꾸지 않았다. 확인하는 중에 C-31이 두 번 쓰였던 것을 찾았다(일반 몬스터 구성과 집중 치명타). 이 중복은 이미 `5af0aa7` 통합에서 치명타를 C-32로 옮겨 정리되어 있었다. 재발을 막으려고 `AGENTS.md`에 "새 ID 전에 색인 확인"을 넣었다.

## 남은 것

- 옛 기록 이동은 나중에, 필요한 기능부터 한다.
- 큰 코드 파일 나누기(T-26 개선 ③)는 아직이다.
