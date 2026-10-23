# Stage 2 사다리·곤돌라 모델 개선 — 2026-10-24

사용자 요청의 굵은 적갈색 사다리 참고 이미지와 넓은 다인 곤돌라를 반영했다. 이번 문서는 Blender 모델 계약·실제 생성 결과를 기록한다. 게임 이동·가림·멀티 검증은 해당 런타임 검사 기록과 구분한다. 상세 브리프는 [stage2-landmarks.md](model-briefs/stage2-landmarks.md), 이전 제작 이력은 [묶음 A 검수](stage2-props-a-validation.md)다.

## 현재 치수와 부위

단위 m, 치수 순서는 내보낸 게임 X/Y/Z이며 장식 돌출까지 포함한다.

| 모델 | 실제 외곽 치수 | 삼각형 / 예산 | 재질 | 부위 |
|---|---|---|---|---|
| `ladder_section` | 0.950 × 1.200 × 0.200 | 320 / 600 미만 | 3 | `Keep` |
| `ladder_top` | 0.950 × 1.050 × 0.720 | 360 / 500 미만 | 3 | `Keep` |
| `gondola_cabin` | 6.000 × 3.730 × 5.006 | 1,428 / 3,000 미만 | 4 | `Keep`, `Occlude_Frame` |
| `gondola_station` | 6.850 × 5.600 × 6.200 | 1,376 / 3,000 미만 | 4 | `Keep`, `Occlude_Tower` |

- 사다리 레일은 중심 X ±0.4, 단면 0.15의 모서리를 깎은 사각형이다. `Red` 팔레트를 쓰고, 가로 발판은 `Ochre`, 폭0.8·깊이0.18·두께0.11, 중심 높이0.15/0.45/0.75/1.05다. 기존 좁은 은색 원통 가로대보다 실루엣과 색 차이를 키웠다. Blender Y −0.025의 레일 중심, 후면 고정쇠를 포함한 깊이0.2, 조각 높이/적층 간격1.2는 고정이다.
- 손잡이 최고점은 윗단 바닥보다0.9, 하단 피벗 예외는−0.15다. 레일 중심·본체 단면은 section과 같고 뒤로0.56 휘어 들어간다. 마지막 section 안으로 들어가는 아래0.15 구간은 단면0.141에서0.15로 넓어지는 삽입부로 만들어 표면 중복을 피한다. 노란 표식과 검은 띠는 앞면보다 각각 돌출되어 같은 평면의 도장 면이 없다.
- 객차 바닥은 정확히 X6 × Z5, 두께0.15, y−0.15..0이다. 바닥판은 객차에만 있고 승강장 중앙에는 만들지 않는다. 앞벽0.6, 뒤/옆벽1.0, 기둥2.4, 바퀴 중심3.4는 유지한다. 양옆 ±X 출입구는 Z−1.2..1.2, 폭2.4다. 앞 녹 얼룩 때문에 전체 깊이가 바닥보다0.006 크다. 프레임·팔·바퀴만 가림 부위다.
- 승강장 테두리는6 × 5, 바닥부터0.12 높이다. 뒤 탑의 프레임은 X6 × 깊이1.2 × 높이5이며 Blender Y2.5..3.7에 있다. 바퀴 중심의 게임 좌표는 `(0,5,−3.1)`, 레버 손잡이 중심은 `(−2.65,1.05,2.28)`이다. 받침/출선 팔의 돌출 때문에 외곽 폭은6.85다. 탑만 `Occlude_Tower`에 속한다.
- 루트 원점·미터·Y-up·`plushpanic_outdoor_v1`·부위마다 메시1개·텍스처 없음·재질6개 이하·충돌none 계약을 유지했다. 충돌·승선 판정·동선은 게임 코드가 소유한다.

## 생성·검수

[생성기](../prototypes/outdoor-kit/tools/stage2_props_a.py)는 `--` 뒤 모델 ID를 주면 선택한 GLB/manifest 항목만 내보낸다. `.blend` 라이브러리는 A 전체5종을 보관한다. 이번 실행은 처음4종, 접합부 수정 후 `ladder_top`만 내보냈다. 짚라인을 포함한 다른14종 GLB SHA256와 manifest 항목, 환경 후속3종을 모두 보존했다. 전체 manifest는18종이다. 기존 파일/개인 작업을 되돌리지 않았다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a.py -- ladder_section ladder_top gondola_cabin gondola_station
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a_review.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --factory-startup --python-exit-code 1 --python prototypes/outdoor-kit/tools/stage2_props_a_review.py -- --manifest-merge
```

- 독립 GLB 재가져오기 검사는 파일 구조·삼각형·재질·실측 치수·피벗·가림 부위·사다리 간격/폭/발판 두께·출입구 양쪽 폭2.4와 높이2.18의 빈 공간·승강장 노출 상단 접합부의 같은 평면 중복0쌍을 확인한다.
- 최종 `STAGE2_REVIEW_PASS 5 assets 120 checks 13 renders`, 종료 코드0. 조립 사다리의 앞서 보인 검은 접합 얼룩이 사라진 것과 객차6인 공간·앞뒤 외형을 실제 PNG로 확인했다. 변경 파일 `git diff --check`도 통과했다.
- 최초13장 렌더에서 손잡이 삽입부의 검은 얼룩을 확인했다. 새 사각 단면의 아래0.15가 section과 같은 면을 공유한 것이 원인이다. 삽입부 검사 실패를 기록하고 안쪽 테이퍼를 적용했다. 높이·피벗은 바꾸지 않았다.
- 각 모델 앞뒤, 3단 적층 사다리, 객차6인 크기 비교, 프레임 숨김을 렌더한다. 비교 인형은 높이2.18의 단순 크기 참고용이며 실제 게임 캐릭터·충돌 캡슐을 복제한 모델은 아니다.
- 혼합 manifest의 전체 생성2회와4종 선택 생성1회로 중복/누락 입력 거부, 다른 묶음·메타데이터·선택 제외 짚라인 보존을 확인했다. 실제 작업 팩의 다른14종도 별도 해시/레코드 비교를 통과했다.

로컬 재생성 산출물: [검수 report](../game/output/stage2-props-a-review/report.json), [최종 렌더 로그](../game/output/stage2-traversal-model-quality/render-final.log), [생성 로그](../game/output/stage2-traversal-model-quality/generate.log), [접합부 실패 재현](../game/output/stage2-traversal-model-quality/tongue-before.log), [선택 생성/보존](../game/output/stage2-traversal-model-quality/manifest-merge.log), [실제14종 보존](../game/output/stage2-traversal-model-quality/preservation.log). `output/`은 버전 관리 대상이 아니다.

## 경고와 남은 범위

Blender5.2.2 생성에는 기존 재질 API의6.0 폐기 예정 경고와 기본 브러시 재질 외부 경로를 상대 경로로 바꿀 수 없다는 저장 경고가 있었다. 생성/검수 최종 프로세스는 정상 종료했으며 해당 브러시 재질은 내보낸 GLB 재질에 들어가지 않는다. 젖음/적설 교체는 이번에 검수하지 않았다. 모델 치수 검사는 실제 게임의 다인 이동·승하차·2PC 동기화 통과를 뜻하지 않는다.
