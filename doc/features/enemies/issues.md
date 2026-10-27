# 적 — 이슈 본문

새 이슈는 여기 ID 아래에 쓰고, [전체 색인](../../issues.md)에는 ID · 한 줄 증상 · 상태 · 날짜 · 링크만 한 줄로 적는다. 새 이슈 ID는 전체 번호(`I-126`부터)를 쓰고, **색인에 한 줄을 먼저 추가해 커밋 · 푸시한 뒤** 본문을 쓴다(먼저 예약). 푸시가 막히면 받아서 다음 번호로 바꾼다.

**옛 기록은 옮기지 않고 그대로 있다**(2026-10-26 결정, [기록 문서 나누기](../../records-split-plan.md) 6.4). 이 기능의 2026-10-26 이전 결정 · 이슈 · 확인 기록은 [decisions.md](../../decisions.md) · [issues.md](../../issues.md) · [feature-checklist.md](../../feature-checklist.md)에 있다.

<!-- 새 이슈 예:
## I-126 증상 한 줄
- 증상:
- 원인(추정이면 추정):
- 해결:
- 재발 방지 · 검사 경로:
- 남은 제약:
-->

## I-41 화면 안 시체 누적 — 추가 관찰, 2026-10-27

- 과거 시체 보존 정책과 기존 제약은 [전체 이슈 I-41](../../issues.md)에 유지한다.
- 증상: 사운드 검증용 5분 통제 전투에서 같은 장소에 시체가 누적된 후, 약240초 이후 Godot가 `Too many instances using shader instance variables`와 `global_shader_uniforms.instance_buffer_pos.has(p_instance)` 오류를 기록했다. 최종 녹음은 299.989초·초과샘플0이었지만 게임 전체 무오류 통과는 아니다.
- 확인된 원인: 엔진 메시지는 shader instance 버퍼 부족을 가리킨다. 시체 누적과 함께 발생했으나 정확한 mesh/리소스별 사용량과 사운드 변경 전 동일 조건 비교는 미측정이다. 시체/렌더 코드·제한값은 이 사운드 작업에서 수정하지 않았다.
- 해결 상태: 기존 제약 유지·미해결. 사운드 믹스 작업과 분리한 후속 렌더/시체 부하 조사 대상이다. 강제 삭제나 버퍼 증량을 임의로 적용하지 않았다.
- 재현/검사: 창 모드 `AudioCombatSoak.tscn`, `output/soak-combat.log`, 60/240초 PNG. [정확한 조건·수치](../effects-audio/log/003-check-combat-soak-main-integration.md). 무적·체력 회복·탄약 보충·적 교체가 있는 통제 조건으로 일반 캠페인 난이도와는 다르다.

## I-128 CommonRosterFieldCheck 고지 사격 예고 표시를 찾지 못함
- 증상: `CommonRosterFieldCheck`(헤드리스)의 `ElevatedTell`(160행)에서 `Sequence contains no matching element`로 멈춤. 높은 단의 무장 코뿔소(AK47)가 조준을 시작한 뒤 `Effects` 아래 `BoxMesh` 예고 표시를 `Last`로 찾지 못함.
- 원인(추정): 검사가 고정 좌표 `Ground(488,-92)` · `(484,-92)`를 쓰는데 L-30 큰 Stage 2에서 그 자리가 높은 단이 아니게 되었거나, 예고 표시가 `BoxMesh`가 아닌 다른 모양으로 바뀜(I-125 수정 이후 이펙트 작업) — 미확인.
- 확인: 2026-10-27 카메라 기본 시점 작업(L-2) 중 검사 묶음에서 실패. 카메라 변경 전 main `eedab6d`를 별도 worktree로 받아 같은 장면을 돌려도 **똑같이 실패** — 카메라와 무관한 기존 실패([레벨 기록 002](../level/log/002-code-camera-default-view.md)).
- 해결: 아직 없음(열림).
- 재발 방지 · 검사 경로: `Godot --headless --path game res://tests/CommonRosterFieldCheck.tscn`. 고칠 때 좌표를 맵 데이터의 높은 단에서 찾게 하면 맵이 바뀌어도 버팀.
