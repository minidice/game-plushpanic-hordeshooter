# 사망 체액 main 병합 검증

관련 **A-19**, [구현·최초 검사](017-code-body-fluid.md). 사용자의 커밋·병합·푸시 요청에 따라 `a599e29`를 main에 병합했다.

## 통합

- fetch 시 로컬/원격 main은 `eedab6d`, 작업 브랜치의 추가 커밋은 `a599e29` 하나였다.
- 병합 커밋 `7c0d100`. 결과 트리는 `a599e29`와 동일하며 제품 코드의 추가 수정은 없다.
- main의 다른 작업 문서4개를 경로 한정 stash 후 충돌 없이 복원했다. 보관 전/복원 후 추가·삭제 줄의 동일성4개 검사를 통과했다. 원래 미스테이징 상태를 유지했으며 기존 미추적 첨부·기획·Studio 기록을 포함하지 않았다. 확인 후 임시 stash를 제거했다.
- 다른 Godot 실행은 별도 camera worktree에 속함을 명령줄로 확인했다. main에서는 빌드/가져오기/검사를 순차 실행했다.

## 병합된 main에서 실행한 검사

Windows / Godot4.7.2 .NET, `D:/Project/Game/game-hordeshooter`. 로그·PNG는 로컬 `game/output/` 산출물이며 커밋하지 않는다.

| 검사 | 결과 | 로그 |
|---|---|---|
| Debug 빌드 | 통과, 오류0·경고0 | 명령 출력 |
| headless editor import | 정상 종료0 | `fluid-merge-import.log` |
| `BodyFluidCheck` 실제1280×720 | **14 통과** | `fluid-merge-render.log` |
| `DestructionNetCheck` headless | **29 통과** | `fluid-merge-DestructionNetCheck.log` |
| `Integration` headless | **149 통과** | `fluid-merge-Integration.log` |

이번 가져오기/게임 검사 로그에 오류·경고·실패·종료 누수 없음. `git diff --check` 통과.

실제 `body-fluid-palette-wet.png`에서 밝은/어두운 바닥의3색 고임·방울·반사 표현을 확인했다. 셰이더/이미지 생성 경로가 정상이며 기존1600×900 검수와 같은 외형이다.

## 기록·제약

병합·검증만 수행해 기존 결정/이슈 ID와 기능 현황은 추가 변경하지 않았다. 이 기록만 별도 커밋하여 다른 작업의 미커밋 문서를 섞지 않는다. 평면의 단차 투영 제한·피어별 자국 모양 차이·실제2PC/장시간/저사양 성능·최종 외형 수락은 [017 기록](017-code-body-fluid.md)의 범위를 유지한다.
