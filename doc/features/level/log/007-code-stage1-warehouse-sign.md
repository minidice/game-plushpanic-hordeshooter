# 007 · code · Stage 1 창고 간판 랜드마크 배치 (L-33)

- 작성일: 2026-10-27. 종류: code(모델 등록 · 맵 데이터 · 검사). 관련: L-33, 가이드 맵 7.3 #8, [Codex 지시문](../../../model-briefs/stage1-guide-landmark.md), 앞 기록 [006](006-code-stage1-plan-map.md).
- 요청: "확정된 Stage 1 설계안의 맵 변경 8개를 반영해줘 (랜드마크 모델은 Codex 지시문으로)" → "지금 바로 Codex 실행부터 진행해줘".

## 변경 사항

- **모델(Codex)**: 지시문대로 `codex exec -s workspace-write`로 실행. 새 파일 4개만 만듦 — `prototypes/outdoor-kit/tools/stage1_guide_landmark.py`, `game/assets/large-stage-kit/warehouse_sign.glb`, `game/assets/large-stage-kit/manifest_guide.json`, `prototypes/outdoor-kit/source/stage1-guide-landmark.blend`(확인 렌더 2장은 `prototypes/outdoor-kit/output/`, 커밋 안 함). Blender 종료 0, `GUIDE_LANDMARK_BUILT 1`, 삼각형 2,136, 재질 6. 기존 키트 파일은 바뀌지 않음(`git status`로 확인).
- **충돌 추천 수정(Claude)**: Codex의 추천(가운데 1.6 × 2.4 × 1.2 m)은 두 다리(x = ±1.85 m) 사이 빈 곳만 막아 다리와 X자 가새를 통과할 수 있었다 → 다리 · 받침 · 가새를 모두 덮는 **5.3 × 2.4 × 1.2 m**로 `manifest_guide.json`과 생성 스크립트의 같은 값을 고침. 판(높이 4.2 m 이상)은 막지 않는다.
- **등록**: `PropLibrary`가 키트 폴더의 `manifest.json` 뒤에 `manifest_*.json`도 읽는다 — 키트 스크립트가 `manifest.json`을 다시 써도 따로 만든 모델이 빠지지 않게. `surface_profiles.json`에 `warehouse_sign: metal_solid`.
- **맵 코드**: `LayoutMap`이 `model`이 있는 `landmark` 표식을 그 모델의 충돌 있는 소품 + 가림 흐려짐(`PropOcclusion`)으로 짓는다(크레인 · 벙커 정문처럼 이름으로 고르는 기존 표식은 그대로).
- **배치**: `stage1_large_layout.json` 표식 `{"type": "landmark", "pos": [205, -81], "label": "창고 간판", "model": "warehouse_sign"}`, 판 앞 따뜻한 길 안내 조명 `{"pos": [205, -77.5], "color": "ffc46b", "energy": 2.6, "range": 10, "post": false}`. 지시문의 처음 자리 (204, −86)는 렌더에서 판이 화면 위 끝에 걸려 잘려(판 중심 높이 5.5 m라 화면에선 더 위로 보임) 주 경로 쪽으로 5 m 당겼다.
- **가이드**: 간판은 B 거리(x 70 ~ 140)에서 60 m 넘게 떨어져 화면에 들어오지 않는다. 그래서 설계안 가이드의 랜드마크 길 안내를 **B → C**(골목을 나서면 보임)로 옮기고 B · C 메모, `applied`(8개 모두)를 고침. 가이드 맵 7.2 표는 확정 기록이라 B 칸은 취소선 + 이유, C 칸에 추가 표시. 7.3 #8 · 8장, L-33 갱신 줄, 지시문 상태, 체크리스트.
- **검사 · 그림**: `LargeStageCheck`에 "Stage 1 창고 간판이 있고 두 다리 자리는 막히고 앞 길(4 m)은 열려 있다" 1개, `LargeStageCapture`에 `b-warehouse-sign`(200, −76) · `c-alley-east`(186, −70). 평면도 `stage1-large-floorplan.svg` · `stage1-large-guide-plan-floor.svg`(표식 추가), 보드 `stage1-large-guide-plan.svg`(B · C 길 안내) 다시 그림.

## 검사

- 통과: `dotnet build` 오류 0. `LargeStageCheck` `LARGE_STAGE_PASS 123`(새 간판 항목 포함, 위치를 (205, −81)로 옮긴 뒤 다시 실행), `MapCheck` `MAP_CHECK_PASS 24`, `Integration` `INTEGRATION_PASS 149`(모두 헤드리스, 종료 0, 실패 0 · 누수 0). `python tools/level-guide/guide.py check`: 오류 0 · 경고 5(모두 W2, 005와 같음), 종료 0.
- 종료 경고: 새 워크트리의 첫 Godot 가져오기(`--import`)가 가져오기를 마친 뒤 종료 중 세그폴트(139) — 바로 다시 실행하면 종료 0. 이번 변경과 무관한 첫 가져오기 문제로 본다(추정).
- 렌더(창 모드 `LargeStageCapture`, 종료 0): 골목 출구(`b-warehouse-sign`)에서 간판 전체 — 전구 테두리 · 상자 3개 그림 · 동쪽(다음 길 방향) 화살표가 화면 위쪽에 보임. 골목 안(`c-alley-east`, `c-alley`)에서는 아직 화면 밖. 사진은 로컬 `game/output/large-stage1-*.png`(커밋 안 함).

## 남은 제약

- 길 안내 조명은 지금 밝은 Stage 1 렌더에서 차이가 작다 — 어두운 기조(분위기 D안) 적용 뒤 간판 앞 조명과 함께 다시 맞춘다.
- 간판은 골목을 나서는 순간부터 보인다. 골목 안에서부터 보이게 하려면 더 높거나 서쪽 자리가 필요하다(지금은 그대로).
- 실제 손 플레이 확인은 아직.
