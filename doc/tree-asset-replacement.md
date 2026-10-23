# 공통 나무 원본 교체 — 2026-10-24

사용자가 비교 이미지의 NEW CANOPY / NEW PINE으로 모든 모델 로드 경로를 통일하고 이전 나무가 다시 쓰이지 않도록 요청했다. 이전에는 `StageMap.Prop`의 `StageTwoMap` 분기만 `FoliageArt.ReplaceTree`를 호출했다. `LayoutMap`, 직접 `PropLibrary.Instance`, `Scatter`, 도감은 오래된 GLB를 그대로 읽었다.

## 변경 계약

- `pine_tall`, `pine_small`, `broadleaf`, `round_oak`, `round_linden`의 **실제 GLB 정점**을 현재 `FoliageArt`의 승인된 가지층/수관으로 교체했다. ID와 파일 경로, 추천 cylinder 충돌 수치는 유지한다. pine_small은 0.68배, round_linden은 0.8배, round_oak은 broadleaf 변형 2이다.
- 조형 원본은 `prototypes/outdoor-kit/source/tree-geometry.json`. `TreeAssetExport.tscn`이 C# 생성 메시의 위치·법선·색·인덱스를 기록한다. Blender `revised_trees.py`는 이를 그대로 변환하며, 줄기 `Keep`과 기존 이름의 `Occlude_*` 4구역으로 삼각형을 분배한다. GLB 바닥 피벗 계약에 맞춰 최저 정점을 y=0에 둔다.
- 나뭇잎/줄기의 opaque PBR을 유지한다. 기존 foliage 셰이더의 고정 색 변화만 정점색에 계산하며 조명/그림자는 굽지 않는다. GLB는 2재질·5메시이다. Godot 4.7.2에서 COLOR_0는 읽지만 재질의 vertex-color 플래그가 꺼지는 현상을 검사로 확인해, 다섯 `.glb.import`에 `tree_material_import.gd`를 연결했다. 임포트 결과의 실제 PBR 재질에 플래그를 저장하므로 직접 로드와 Scatter 모두 동일하다.
- pine 두 종 2,384삼각형, broadleaf 계열 세 종 2,600삼각형. 이전 각각 8,692 / 11,868에서 감소했다. 이 수치로 전체 장면 성능을 보증하지 않는다.
- `StageMap`의 맵별 치환과 `FoliageArt.ReplaceTree`를 제거했다. 경계 숲의 기존 절차 생성/LOD는 유지한다. `FoliageStudy`는 현재 GLB와 현재 절차 원형을 비교하며 오래된 `output/foliage-baseline-*.tres`를 읽지 않는다.

## 이전 자산 및 재생성 경로 제거

- 다섯 기존 GLB를 같은 경로에서 교체하고 `source/outdoor-library.blend`의 이전 수목 객체·사용하지 않는 메시 데이터를 제거했다.
- 사용 중인 게임 코드에 참조가 없는 `edge_forest_pine`, `edge_forest_round`, `edge_forest_low`, `edge_backdrop`의 GLB와 `.import`, manifest 항목을 삭제했다. `edge_kit.py`의 해당 생성 함수/등록을 삭제하고 `source/edge-kit.blend`도 남은 덤불·암벽만으로 다시 만들었다. 현재 edge-kit은 2종이며 6종 기록은 과거 제작 이력이다.
- `nature_shapes.py`의 이전 leaf-spray / leaf-pad 나무 생성 코드를 제거하고 새 원형 소비자로 연결했다. `build_models.py`는 공통 게임 자산 경로에 출력하며 `-- --trees-only`로 다섯 나무만 갱신한다.
- 원본 자산은 `game/assets/outdoor-kit` 한 곳이다. 독립 미리보기는 `canonical_assets.gd`로 같은 GLB를 읽고, 도감 다운로드와 복사 도구도 같은 원본을 사용한다. 존재하지 않던 별도 `pack/`을 재생성해서 이전 나무를 복원하지 않는다.
- 도감의 다섯 개별 나무 이미지는 현재 GLB 실제 렌더로 갱신했다. 기존 비교/풍경/가림 이미지는 역사 이미지라고 표시했다. 현재 나무 카드에서는 오래된 ghost 썸네일로 전환하지 않는다. 부위별 가림은 독립 미리보기에서 검사한다.

## 검증 및 재현

2026-10-24 로컬 Windows / Godot 4.7.2 .NET / Blender 5.2.2:

1. `dotnet build game/HordeShooter.csproj --no-restore`: 오류 0, 경고 0. 병행 작업 중 CharacterRig 메서드가 저장되기 전 빌드 1회는 실패했고, 해당 작업 완료 후 정상 빌드를 사용했다.
2. `--headless --path game res://tests/TreeAssetExport.tscn`: 원형 추출 통과. 변경 전 GLB로 `TreeAssetCheck` 실행 시 첫 직접 로드 형상 비교에서 의도대로 실패. 실패 당시 임시 노드 정리 누락 경고는 검사에서 정리하도록 수정했다.
3. Blender `--background --python prototypes/outdoor-kit/tools/build_models.py -- --trees-only`: 5종 출력 통과. `edge_kit.py`: 남은 2종 출력 통과. Blender의 `Material.use_nodes` 폐기 예정 경고는 남는다.
4. Godot `--headless --editor --import --path game --quit`: 통과, 최종 로그 ERROR/WARNING 없음.
5. `--headless --path game res://tests/TreeAssetCheck.tscn`: **35검사 통과**, 종료 경고 없음. 직접 PackedScene·PropLibrary의 정점을 승인 원형과 양방향 대조(임포트 압축 허용 2mm), 정확한 삼각형 수, 부위 4개, 줄기, 무충돌, opaque 정점색 PBR, 실제 Scatter 2인스턴스/5파트를 검사한다.
6. 창 모드 `--path game --disable-vsync --resolution 1600x900 res://tests/TreeAssetCheck.tscn`: **35통과**. `game/output/tree-assets-revised.png` 및 개별 `tree-<id>.png` 생성, 실제 외형 확인.
7. 같은 창 옵션으로 `FoliageStudy.tscn`: **80통과**, `canopy2-study-lit.png`와 실루엣 생성. GLB/절차 원형의 새 수관과 가지층이 일치하는 것을 직접 확인했다.
8. `python prototypes/outdoor-kit/tools/validate_pack.py`: **32 GLB 통과**. `validate_revised_trees.py`: 새 조형 메타데이터·예산·구형 부품 이름 부재·삭제 자산/생성 함수/중복 pack 부재 통과.
9. Godot `--headless --path prototypes/outdoor-kit --script res://preview/validate_import.gd`: **32종 역할·치수·피벗 통과**. HTML 도감은 canonical manifest로 32카드 재생성했다.
10. `python prototypes/outdoor-kit/tools/validate_delivery.py`: **32카드·154링크, 32썸네일, 현재 지원하는 8종 ghost 이미지, 복사 도구 무쓰기 미리보기/해시 일치/덮어쓰기 거부 통과**. 복사 시 새 Godot import 스크립트와 `.import` 설정도 함께 전달한다. `git diff --check` 공백 오류 없음(LF 정규화 안내만 출력).

남은 제약: 포터블 PBR 정점색은 절차 셰이더의 픽셀별 색 계산과 미세한 보간 차이가 있다. 나무 자동 가림·날씨 시스템·저사양/6인 장시간 성능은 이번 교체의 완료 범위가 아니다. 출력 PNG/로그는 로컬 재생성 산출물이다. 갱신된 도감 개별 이미지 외 기존 전체 풍경 이미지는 현재 자산 상태 증명이 아니다.
