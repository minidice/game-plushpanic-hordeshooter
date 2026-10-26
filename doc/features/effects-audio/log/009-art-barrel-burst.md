# 연료통 폭발: 순간 팽창과 개별 잔여 소멸

관련 ID: A-19, A-27, A-28. 사용자 요청: 추천한 연료통 폭발 품질 개선부터 진행.

## 변경

- `BarrelBurstVisual`의 카메라 대면 텍스처 카드로 연료통 외형을 교체. 기존 연무/불꽃 PNG 재사용, 새 이미지 리소스 없음. 새 C#의 `.uid` 포함.
- 짧은 섬광→빠른 화염 팽창→부풀어 오르는 반투명 연기와 서로 다른 수명의 잔불/불티. 바닥 링은 제거.
- barrel 시각 수명1.45→2.4초. 실제 폭발 피해·반경·충돌·사운드 변경 없음. 연료통 외의 폭발/독, 지속 화염·연무는 이번 변경 대상 아님.
- 기존 공통 생성기에서 연결하고 명시적 나이로 전개. 동시24폭발/4광원 제한·독립 시각 RNG·부모와 함께 정리 유지. 무해한 잔불에 새 위험 지대를 만들지 않는다.
- [계획](../barrel-burst-plan.md), [결정 A-28](../decisions.md#a-28-연료통-폭발의-순간-팽창과-잔여-연기), [상세 규격](../../../vfx-quality.md), 기능 체크리스트 갱신. 외형 개선이며 새 제품 버그 발견은 없어 이슈 ID를 만들지 않았다.

## 검증

- **통과:** `dotnet build game --no-restore --nologo -v q` 오류0/경고0.
- **통과:** GPU 창1600×900, fixed-fps60 `CombatEffectsCheck` 최종104개. 실제 발생원 선택/피해 없는 장식/폭발·광원 상한/종료/전투 중첩을 유지하고, 연료통의 무바닥면·화염보다 긴 연기·종료 직전 전체 소멸, 시간 분할과 무관한 표시 위치/크기/농도, 부모 시계 정지와 함께 고정, 부모 제거, duration0.1·lobes0/24·particles0 경계를 추가 확인했다.
- **통과:** headless fixed-fps60 `DestructionCheck`42개. 실제 연료통/폭발·벽 파괴·클라이언트 피해 권한 회귀.
- **실패 후 검사 수정:** 첫 추가 검사는 Godot `QuadMesh`도 `PlaneMesh` 하위형이라는 점을 빠뜨려 입자 카드를 바닥으로 판정했다. `PlaneMesh and not QuadMesh`로 수정. 시간 분할 검사 초안은 이미 숨긴 섬광의 마지막 위치까지 비교해 실패했고, 표시 여부가 같고 보이는 카드의 상태가 일치하는지로 수정했다. 제품 코드 결함을 확인한 실패는 아니다. 이후 최종104개 통과.
- **종료 경고:** 최종 전용/파괴/부하 실행 로그에서 ERROR·WARNING·FAIL 없음. 과거 I-17 종료 이력을 삭제하거나 모든 실행의 안전성을 주장하지 않는다.
- **독립 리뷰:** requesting-code-review 스킬의 읽기 전용 리뷰에서 중요한 결함 지적 없음. 요청된 짧은 수명·0개 경계 검사는 위 후속104개에 포함.

## 실제 렌더와 부하

- Studio 공통 생성기 최종40프레임/2.6초(15fps). 초기 섬광/팽창, 약0.7초의 연기·잔불, 약1.2초의 옅은 연기, 끝의 빈 화면 확인. 게임 카메라의 `vfx-barrel.png`와 `vfx-barrel-tail.png`, 숲의 중첩 부하 화면도 확인했다.
- 비교 GIF의 왼쪽은 이전1.45초 정의로 생성했던 `studio/output/combat-barrel/`, 오른쪽은 현재 `game/output/area-effects/barrel-after/`. 진행 중 별도 `barrel-before/` 캡처는 구형 DLL에 새2.4초 데이터가 읽힌 중간 산출물이므로 최종 비교에서 제외했다.
- RTX5070Ti·Forward+·1600×900·VSync/제한 해제, 고정 적20, 구간6초 중 처음1.5초 제외. `CombatEffectsPerformance` 통과. 기본 p95 **2.12ms**; 지속위험6+초당폭발10 **3.67ms**; 화염/연무1·6·12개+초당폭발10은 각각 **3.30/3.39/3.56ms**. 12개 조건 평균3.00ms/draw calls약1238. 이번 작업 중 변경 전 동일 부하를 재측정하지 않아 개선율 비교는 하지 않는다.

산출물(자동 생성, 커밋 제외):

- `game/output/area-effects/barrel-comparison.gif`, `barrel-sheet.jpg`, `barrel-after/`.
- `game/output/area-effects/barrel-combat-final.log`, `barrel-destruction-check.log`, `barrel-performance.log`.
- `game/output/vfx-performance.json`, `vfx-performance-twelve_areas_ten_bursts_per_second.png`.

## 남은 제약

사용자 최종 외형 수락 전 제작값이다. 카드 기반으로 벽 우회·지형 투영은 하지 않는다. 큰 동시 효과가 시야를 얼마나 가리는지는 실제 장시간 플레이에서 추가 조정할 수 있다. 이번에는 전체 Integration/음향/실제2PC/저사양 검사를 재실행하지 않았다. 전투와 음향 코드/리소스는 수정하지 않았다. 변경은 `codex/area-effects` 작업 브랜치에 기록하며 main 병합/푸시는 이 작업에서 하지 않는다.
