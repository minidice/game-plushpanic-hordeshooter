# Stage 2 자연스러운 지형 경계 — 2026-10-24

사용자 요청: 첨부 화면의 날카로운 경계를 수정한다. 걸어서 넘는 높이는 매끄럽게 연결하고, 못 넘는 벽은 굴곡이 있는 자연스러운 면으로 만든다.

## 원인과 선택

현재 LayoutMap 경사로는 PrismMesh 한 개/평면 충돌이며 단의 높이 조회는1m 칸 중심 높이다. 경사로 주변8m는 바위 모듈을 제외해 쐐기 옆 삼각형과 평평한 단 벽이 노출된다. 노말맵은 조명 반응만 바꾸므로 실루엣·접속부 높이 문제를 해결하지 못한다.

기존6m 길이/폭/끝 높이/단 연결을 유지하며 Stage2Draft의6개 경사로만 부드러운 높이 곡선으로 변경한다. 실제 메시·충돌·높이 조회가 같은24구간 곡선을 사용한다. 다른 맵의 직선 경사로는 유지한다. 자연 벽/경사로 옆에는 위치에 따른 작은 암반 굴곡과 상단 마감을 생성하고 공통 흙/암반 재질을 쓴다. 메시로 큰 형태를 먼저 해결하며 별도 노말맵 파일은 이번에 추가하지 않는다. 사다리/내려가기/이동 장치와 건물/다리/컨테이너는 제외한다.

## 실행과 검사

- [x] 전용 경계 검사/수정 전 실제 렌더: 첫 숲 경사로·중간 경사로·야적장 진입/막힌 턱.
- [x] 경사로 외형/충돌/높이 조회 일치, 시작/끝 완만한 기울기. 기존 칸 연결·경로 폭 유지.
- [x] 막힌 자연 벽/경사로 옆 굴곡과 재질 연결. 이동 장치/구조물 주변 여유 유지.
- [x] 실제 여섯 경사로 오르막 걷기·내리막 구르기/충돌 광선·벽 통행 차단·렌더, 높이 규격 및 시안/환경 회귀. 전 구간 동선 검수.
- [x] 결정/이슈/체크리스트/상세 규격 갱신, 독립 리뷰. 커밋/푸시는 이 요청에 포함하지 않음.

검토 초점: 보이는 곡선과 충돌/바닥 높이 차이, 상단 접속 턱/옆면 구멍, 벽 마감이 이동 통로 침범, 지형 오버레이 겹침, 맵 밖/건물/이동장치에 자연 암반 적용 오류.

## 현재 계약

- `StageMap.Ramp(..., smooth=false)` / `TierMap.PaintRamp(..., smooth=false)`: 기본은 기존 직선 쐐기/칸 중심 조회. `LayoutMap`만 true를 전달한다.1단1.2m/길이6m/기존 폭은 유지한다.
- 곡면은 `TierMap.TerrainRampProfile(t)`의24개 선형 구간으로 근사한 smoothstep. 같은 정점을 렌더/정적 삼각형 충돌에 쓰며 `GroundY`도 같은 구간을 선형 보간한다. 평지/직선 경사로를 다시 칠하면 곡면 정보가 지워진다. `CellFloor`는 각 칸 중심의 같은 곡면 높이를 보관한다. 정수 단·경사로 표시·연결 비용은 변경하지 않는다.
- 암반은 `TerrainEdgeArt.Rock`으로 생성한다. 자연 벽은0.5m 간격/높이4개 띠, 경사로 옆은 곡면의24구간. 위치 기반 작은 돌출로 연속시키고32m 구역22개 메시(10,040삼각형)에 묶는다. 추가 충돌/매 프레임 처리 없음. 최대18cm 외향 돌출/약7cm 이하 상단 마감은 임시 아트 수치이며 전체 단의 평면 경계를 굽히는 변경은 아니다.
- 다리/감시탑/컨테이너 제외, 경사로 외 이동 연결 선분에서1.2m 여유. 경사로 끝의 면은 양쪽 실제 높이 차이가3.5cm보다 작으면 생략한다. 자연 벽 재질에만 `terrain_rock=true`, 일반 바닥/공용 재질은 기본false. 별도 노말맵/외부 텍스처 없음.
- 나무109그루 유지. 연속 높이 조회로 풀의 `Flat` 판정 결과가 달라져24,717→24,813묶음(같은467개 공간 묶음)으로 변했다. `ForestExpansionCheck`의 전체 실제 풀 좌표/바닥 높이/길 여유/지붕 제외 검사를 다시 통과했다. 풀 배치가 전부 이전과 같다고 주장하지 않는다.

## 재현·검수 결과

1. 수정 전 실제 렌더4장으로 첫 숲 경사로에서 첨부 화면과 같은 삼각 옆면/밝은 직선 벽을 확인했다. 전용 검사에서 끝부분 기울기가 완만해야 한다는 조건이 실패했다(`terrain-edge-red.log`). 최초 경사로 수 검사 실패는 Godot 자동 노드명 때문에 생긴 검사 가정 오류로, 실제 메시/명시적 메타데이터 탐색으로 바로잡았다.
2. 최종 `TerrainEdgeCheck` 창 모드41통과/4렌더/종료0/경고 없음.6개 각각에서 시작/끝 기울기, 가로3위치×진행23지점의 물리 광선과 조회 높이 오차1.5cm 미만, 실제 오르막 걷기·내리막 구르기, 막힌 턱 차단,4방향 조회/평지 재칠하기를 검사했다. 모든 방향×걷기/구르기 조합을 전용 검사한 것은 아니다.
3. `Stage2RouteCheck` 창 모드171통과/45렌더/종료0/경고 없음. 실제 자동 입력으로 시작부터 세이프하우스 E닫기 클리어까지 연속366.9초 시뮬레이션, 사다리8회·짚라인1회·곤돌라·187처치·최대 적59·1,214발. 이동 중 순간이동/문 강제 열기 없이 검사했다. 무적 자동 조작이며 수동 체감/난이도 승인이 아니다.
4. 회귀: `ElevationCheck`76, `Stage2EnvironmentCheck`13, `SizeDraftCheck`74, `ForestExpansionCheck` 창65(18렌더) 통과. 각 최종 로그 오류/종료 경고 없음. 기존 다른 맵의 직선 경사로와 건물 바닥/전체 통행·식생을 확인했다. C# 빌드0경고/0오류.
5. 독립 읽기 전용 리뷰: 공유 높이 함수/삼각형 방향/옵션 기본값/재칠하기/암반 제외/실제 전후 화면에 확정 중요 결함 없음. 검사 범위를 ‘양방향 모든 조합’으로 과장하지 않도록 위 실제 조합을 명시했다. 큰 단 윤곽이 여전히 직선인 점과 최종 미술 수락, 저사양·실제 다인/다른 변경 영역은 별도다.
6. `Integration` 최초 병행 실행은74개 통과 뒤 ‘shotgun pellets hit several enemies’에서 실패/종료1, ObjectDB17개 경고. 코드 변경 없이 다른 게임 검사를 끝낸 뒤 단독 재실행하여149통과/종료0, ObjectDB4개 경고였다. 최초 실패 원인은 확정하지 않았고 산탄총/통합 코드를 수정하지 않았다. 이 불안정성을 지형 수정으로 해결했다고 표시하지 않는다. `terrain-edge-integration.log`/`terrain-edge-integration-retry.log`; I-17 경고 상태 유지.
7. 최종 Godot 가져오기 오류/경고 없음. 문서 참조/설정 키/ID 일치와 `git diff --check` 확인. 이번 변경의 별도 프레임 성능 측정은 하지 않았으며 기존 나무 성능표를 새 지형 측정값으로 인용하지 않는다.

### 실제 화면

- [수정 전 경사로](../game/output/terrain-edge-before-ramp-0.png) / [수정 후 경사로](../game/output/terrain-edge-after-ramp-0.png)
- [중간 경사로](../game/output/terrain-edge-after-ramp-1.png), [야적장 진입](../game/output/terrain-edge-after-ramp-2.png), [막힌 벽](../game/output/terrain-edge-after-wall.png)

이미지는 실제 Godot 렌더이며 `output/`은 재실행 산출물로 커밋하지 않는다. 검사 로그는 `game/output/terrain-edge-*.log`.

## 재실행

저장소 루트 PowerShell:

```powershell
dotnet build game/HordeShooter.csproj --nologo -v q
$terrainGodot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $terrainGodot --path game --resolution 1440x900 --fixed-fps 60 res://tests/TerrainEdgeCheck.tscn
& $terrainGodot --headless --path game --fixed-fps 60 res://tests/ElevationCheck.tscn
& $terrainGodot --headless --path game --fixed-fps 60 res://tests/Stage2EnvironmentCheck.tscn
& $terrainGodot --headless --path game --fixed-fps 60 res://tests/SizeDraftCheck.tscn
& $terrainGodot --path game --resolution 1440x900 --fixed-fps 60 res://tests/ForestExpansionCheck.tscn
& $terrainGodot --path game --resolution 1440x900 --fixed-fps 60 res://tests/Stage2RouteCheck.tscn
```

직접 테스트는 `game` 폴더에서 `.\Start-Game.ps1 -Stage2Draft`. 현재 실행 중인 게임은 재시작해야 반영된다.
