# 002 · art · 숲 바위·이끼·흙길·풀 품질 비교 구역

- 작성일: 2026-10-28. 관련: A-23 · A-24 · A-25 · L-29.
- 사용자: 참고 이미지의 환경 품질을 바위·이끼·흙길·풀로 나눠 멀티에이전트로 개선하는 추천안 승인.
- 범위: **현재 Stage 3**의 숲(예전 Stage 2). `stage3_layout.json`의 x64..108 / z−42..−2, 44×40 m. 첫 경사로·절벽·기존 바위·흙길을 함께 보는 비교 구역이다. 가장자리 4 m는 기존 외형으로 섞인다. 전체 맵 확대는 이번 결과에 포함하지 않는다.
- 작업 트리: `codex/forest-surface-quality`, 기준 main `0fd89e3`. main에 아직 병합하지 않았다.

## 변경

- 절벽: `TerrainEdgeArt`의 기존 시각 메시를 넓은 면·상단 모서리로 정리. 기존 18 cm 이내 외측 돌출, 충돌·경사로·지형 높이 계약은 유지한다. 이끼 윗면은 지형 안쪽으로 넓혀 덮는다.
- 바위 재질: `forest_rock_surface.gdshaderinc`의 어두운 회색 돌, 약하고 끊어진 틈, 경사에 따른 황록색 이끼. 큰 형태를 읽게 하고 미세 노멀맵·전역 후처리는 추가하지 않는다.
- 독립 바위: 비교 구역의 기존 바위 위치에 새 Blender 바위·이끼 GLB를 연결한다. 실제 고정 물리·엄폐·소리 ID는 기존 boulder 그대로, 자식 모델만 교체한다. 다른 맵/구역의 원본 바위는 유지한다.
- 흙길: 기존 경로 폭·거리식을 유지하고, 닳은 가운데·불규칙한 흙 갓길·드문 작은 돌·넓은 색 변화로 표면을 나눈다.
- 풀: 기존 GLB·뿌리 위치·난수 순서·제외 영역·개수·MultiMesh 묶음을 유지한다. 비교 구역에서 크기가 큰 군집과 작은 틈, 길 어깨의 낮은 풀, 어두운 뿌리·밝은 끝·느린 바람을 적용한다.
- 기존 숲 조명/SSAO는 유지한다. 조명 변경으로 형상을 가리지 않도록 같은 카메라·광원으로 비교했다.

## 검증 기록

자동화 검사는 실제 Godot 플레이어 이동·구르기·사격을 실행했다. 수동 플레이나 여러 PC의 협동 검증과 구분한다.

- 기준 렌더: `game/output/forest-expand-surface-baseline-{mid,combat_mid}.png`, `terrain-edge-surface-baseline-wall.png`.
- 1차 통합에서 바위 include의 `world` 인자가 상위 varying과 충돌해 셰이더 컴파일 실패. 인자를 `rock_position`으로 변경했다. 이 실패 실행은 성공 결과에서 제외한다(`terrain-surface-shader-failed.log`).

- Blender 5.2.2: GLB 재가져오기 통과, 앞/뒤 렌더 검토. 이끼가 뒤쪽 돌과 교차한 1차 조형을 실제 돌 삼각형에 맞춰 잘라 수정했다. 최종 1,196삼각형 / 실제 X·Y·Z 2.388×1.329×1.619 m / 바닥 Y=0. 이전 바위의 배치 제외 범위 2.771×1.39×1.911 m와 반경 1.05 m·높이 1.2 m 충돌 계약은 유지한다. `use_nodes`의 Blender 6.0용 폐기 예정 경고만 있으며 실행 성공.
- `dotnet build game/HordeShooter.csproj --nologo -v q`: 오류·경고 0.
- Godot 4.7.2 .NET 가져오기 성공. 최종 GPU 실행 로그에서 ERROR/SHADER ERROR/FAIL/WARNING 없음.
- `TerrainEdgeCheck.tscn -- surface-after`: **45 통과**. 실제 경사로 상·하행/구르기, 못 가는 절벽, 새 바위 1개의 원래 충돌·플로 필드, 렌더 6장. 절벽 22묶음/10,280삼각형, 나무109개/풀24,624묶음으로 기준 실행과 동일.
- `ForestExpansionCheck.tscn`: **69 통과**. 길·구조물·나무 제외, 숲/협곡/야적장 경계, 가림, 실제 이동, 기존 고지대와 새 품질 구역 각각 이동 중인 적6마리 사격 처치. 기존 구역 밖 배치 유지.
- `ForestPerformance.tscn -- surface-baseline` / `-- surface-after`: 두 실행 모두 성공. Forward+, RTX 5070 Ti, 논리 뷰포트1600×900 / 캡처1280×720, VSync off·MaxFps0, 고정 FPS 미사용. 각 시점2초 준비+4초 실시간 측정, 7개 시점, 전투30마리. 단일 PC 한 차례의 전후 측정이며 소폭 차이를 일반 성능 향상으로 해석하지 않는다.

| 시점 | 평균 프레임 ms 전→후 | p95 ms 전→후 |
|---|---:|---:|
| 숲 저지대 | 2.012 → 1.952 | 2.452 → 2.273 |
| 개선 흙길 | 1.899 → 1.909 | 2.200 → 2.206 |
| 고지대 | 1.917 → 1.975 | 2.218 → 2.333 |
| 서쪽 둑 | 1.939 → 1.981 | 2.267 → 2.377 |
| 동쪽 숲 | 1.946 → 1.989 | 2.269 → 2.411 |
| 야적장 | 1.882 → 1.900 | 2.173 → 2.208 |
| 개선 구역 적30마리 | 2.324 → 2.323 | 3.512 → 3.458 |

흙길 시점 draw calls 713→721, primitives 480,472→475,314. 새 바위의 재질/메시 분리로 호출은 조금 늘고 삼각형은 줄었다. 큰 프레임 비용 변화는 관측되지 않았다. 원자료: `game/output/forest-performance-surface-{baseline,after}.json`.

별도 에이전트의 흙·풀 검토 및 최종 코드/GLB/manifest 검토에서 수정이 필요한 결함 없음. `git diff --check` 통과.

## 렌더와 실행

- [변경 전 같은 카메라](../../../../game/output/forest-expand-surface-baseline-mid.png) · [변경 후](../../../../game/output/forest-expand-surface-after-mid.png)
- [실제 게임 바위](../../../../game/output/terrain-edge-surface-after-surface-boulder.png) · [Blender 확대](../../../../game/output/forest-surface-boulder/boulder-front.png)
- [절벽](../../../../game/output/terrain-edge-surface-after-wall.png) · [개선 구역 적 등장](../../../../game/output/forest-expansion-surface-pilot-enemies.png)
- [Blender 생성기](../../../../prototypes/outdoor-kit/tools/forest_surface_boulder.py), [원본](../../../../prototypes/outdoor-kit/source/forest-surface-boulder.blend), [별도 manifest](../../../../game/assets/stage2-props/manifest_forest_surface.json).

저장소 루트에서 `./game/Start-Game.ps1 -Zone 3:B -Squad 0`으로 숲 시작점에 진입해 경사로를 지나 x84..104 부근에서 비교한다. 최초 작성 이후 main에도 병합했다([통합 검증](../../menu-ui/log/008-check-graphics-main-integration.md)). 전용 렌더 재실행: Godot `--path game --fixed-fps 60 res://tests/TerrainEdgeCheck.tscn -- surface-after`. 성능 검사는 `--fixed-fps` 없이 실행한다.

## 남은 범위

- 이 비교 구역 바깥의 전면 교체, 큰 절벽의 실루엣 재설계, 새로운 나무/지형 배치는 이번 범위 밖이다.
- 현재 절벽은 기존 충돌을 따라가는 얕은 시각 표면이다. 참고 이미지의 두꺼운 바위 무리와 완전히 같은 실루엣은 아니다. 독립 바위에서 큰 형태·모서리·이끼 두께를 검수한다.
- 저사양 GPU·6인 네트워크·전체 캠페인 장시간 부하·사용자 최종 미술 수락은 미검증.
- 기존 A-23/A-24/A-25의 방향을 유지한 후속 품질 구현이다. 새 사용자 확정 결정/새 결함 번호를 만들 이유는 없다. 일시적 통합 셰이더 오류는 이 작업 내 수정·재검증으로 기록한다.
