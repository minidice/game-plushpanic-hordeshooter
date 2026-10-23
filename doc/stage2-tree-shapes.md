# Stage 2 수관 형태 보완 — 2026-10-24

> 이후 [지형 경계 보완](stage2-terrain-edges.md)에서 경사로 높이 조회가 연속화되어 풀 배치가24,813묶음으로 조정됐다. 나무 형태/109그루는 유지. 아래 수량과 성능은 나무 형태 보완 당시 이력이다.

사용자 요청: 제공한 네 이미지에서는 형태만 참고하고, 현재 침엽수의 품질·따뜻한 황록색·무광 재질로 원형나무를 수정하고 몇 가지 형태를 추가한다. 앞서 제안한 수관의 층·외곽·가지 보완을 실행한다.

## 범위와 구현 선택

- `forest_broadleaf`의 큰 구 5개를 비대칭 3단 둥근 수관과 보이는 가지로 교체한다.
- `forest_oval`: 길쭉한 4단 타원 수관. `forest_fan`: 넓고 굽은 잎을 펼친 작은 나무. 참고 이미지의 광택·고채도·꽃/열매·얼굴은 가져오지 않는다.
- 기존 침엽수/풀은 유지. 세 활엽수는 동일한 vertex-color 재질, Keep/Occlude_Canopy 분리, 줄기 충돌 반경0.25m/높이1.4m를 쓴다. 수관 폭4m 이내, 높이4.6m 이내, 각5,000삼각형 이하를 목표로 한다.
- 기존109그루 위치·배율·방향·풀 위치·경로 여유를 유지하고 활엽수 자리만 세 형태로 분산한다. 새 fan 형태는 드물게 배치한다. 외곽 숲은 개정 broadleaf를 재사용한다. 정식 StageTwoMap/공용 outdoor-kit 나무는 이 변경의 대상이 아니다.
- 큰 구의 분할만 늘리는 방법은 실루엣 문제가 남고, 낱잎을 대량 추가하면 기존 화풍/비용과 맞지 않으므로 층진 중간 크기 면을 사용한다. 세부 형태와 분배 비율은 구현 선택이며 사용자 최종 미술 승인이 아니다.

## 실행/검증 계획

- [x] Blender 생성기 수정, GLB6종 재가져오기·재질·역할·삼각형·경계 검사, 실제 모델 비교 렌더.
- [x] 새2종 가져오기 설정과 맵 연결, 세 형태의 실제 배치 검사. 난수 호출 수와 물리 계약 유지.
- [x] C# 빌드/가져오기, ForestExpansionCheck 실제 자동 보행·구르기·사격 및 렌더, ForestQualityCheck 회귀. 실제 프레임 비용 측정. 전후 시간 비교는 아래 측정 제약에 따라 채택하지 않았다.
- [x] 결정/이슈/체크리스트/숲 규격 기록, 최종 독립 검토. 이번 요청에 커밋/푸시 없음.

검토 초점: 구멍/뒤집힌 잎 면, 둥근 구가 다시 드러나는 실루엣, 낮은 잎과 플레이어 시인성, 가림 역할/줄기 충돌, 새 자산 추가로 기존 팩 누락, 배치 난수 변화 및 렌더 비용.

## 전달 모델과 배치

| 모델 | 형태 | 폭×높이×깊이 m | 삼각형 | 실제 배치 |
|---|---|---|---:|---:|
| forest_pine | 기존4단 침엽수 유지 | 3.502×4.55×3.106 | 2764 | 72 |
| forest_broadleaf | 둥근3단,8갈래 잎 가장자리/가지 | 3.809×4.38×3.300 | 2896 | 18 |
| forest_oval | 긴4단 타원 수관/가지 | 2.886×4.48×2.543 | 3792 | 12 |
| forest_fan | 넓게 굽은 잎14장/아랫면/가지 | 3.567×3.588×3.534 | 2492 | 7 |

풀2종84/60삼각형 유지. stage2-props manifest는22→24종이며 생성기가 자기6개 ID만 갱신한다. 공용5팩 총67종. 나무109/풀24,717/공간 MultiMesh467개 유지. shape 선택은 기존 난수1회에 구간만 추가했으며 풀 난수열을 더 소비하지 않는다. 배치 전후 모든 변환 해시를 별도로 측정한 것은 아니며 코드 경로·수량·경로/구조물/등장점 검사로 확인했다.

큰 구5개를 쓰던 기존 forest_broadleaf GLB와 생성 코드는 교체했다. 원본 `prototypes/outdoor-kit/source/forest-study.blend`, 생성기 `tools/forest_study.py`, 결과 `game/assets/stage2-props/forest_*.glb`를 함께 관리한다. Blender 비교 렌더의 왼쪽부터 기존 침엽수·둥근형·타원형·펼친잎형이다.

## 실행 결과

- Blender5.2.2 실제 실행, GLB6종 독립 재가져오기/정점색/Keep/수관/삼각형·치수·바닥 피벗 검사 통과. Cycles 실제 렌더1장. 최종 상부 곡면은 높이를 선형에서 sin 곡선으로 바꿔 침엽수의 뾰족한 실루엣과 구분했다.
- C# 빌드:0경고/0오류. Godot4.7.2 가져오기:오류/경고 없음. 새2종 `.glb.import`에 기존 `forest_material_import.gd` 연결.
- 실패 확인: 연결 전 `tree-shapes-red.log`에서 `forest_oval`의 실제 배치 검사 실패. 구현 후53통과, 형태별 근접/가림 추가 후65통과. 최종 곡면 수정 후 같은 창 검사를 다시 실행해65통과·종료0·오류/경고 없음을 확인했다.
- `ForestExpansionCheck` 창 모드:65통과·18렌더. 세 형태가 각각 로드/배치되고 범위/물리/정점색/가림 역할을 지킨다. 실제 게임 이동/구르기·야적장 하강 경사로·6마리 사격 처치, 캐릭터 가림 시 수관 투명화/줄기 불투명/이탈 후 복원을 검사한다. 자동 입력 플레이이며 사람이 손으로 조작한 검수는 아니다.
- `ForestQualityCheck` 헤드리스:21통과. 종료 시 ObjectDB2개 경고는 I-17 기존 범주로 남기며 성공과 구분한다. 전체 Stage2RouteCheck/Integration은 이전 확대 작업의 기록이며 이번 모델 보완에서는 재실행하지 않았다.
- 읽기 전용 독립 리뷰: 생성기/manifest/import/배치 난수/검사와 실제 비교·근접·가림 화면에서 확정 결함 없음. 둥근 상부 곡면 최종 보완은 작성자가 Blender 비교와 게임 근접 화면을 다시 확인하고 창 검사65통과로 검증했다. 참고 이미지와의 정확한 유사성·사용자 미술 수락은 자동 검토로 확정하지 않는다.
- Blender 경고: Material.use_nodes의6.0 폐기 예정, 기본 Grease Pencil 브러시2개 경로 상대화 경고. 실제 납품 GLB는 외부 이미지/브러시를 쓰지 않는다. 게임 로드에는 해당 경고 없음.

### 실제 렌더

- [Blender 네 형태 비교](../game/output/forest-blender/models.png)
- [둥근형](../game/output/forest-expansion-forest_broadleaf-detail.png), [타원형](../game/output/forest-expansion-forest_oval-detail.png), [펼친잎형](../game/output/forest-expansion-forest_fan-detail.png)
- [펼친잎형 가림](../game/output/forest-expansion-forest_fan-fade.png), [고지 적 시인성](../game/output/forest-expansion-high-enemies.png)

`output/` 이미지는 실제 Blender/Godot 렌더이며 재실행으로 생성한다. 별도 합성/이미지 생성기로 만든 미리보기가 아니다. 파일은 버전 관리하지 않는다.

## 실제 렌더 비용

RTX5070Ti, Forward+, 내부1600×1000, VSync해제/MaxFps0, 고정FPS없이 시점마다 준비2초+측정4초. 마지막 시점은30마리 실제 AI와 플레이어 이동이며 사격/폭발은 포함하지 않는다. 최종 모델에서 다른 테스트/Blender/빌드와 겹치지 않게 단독 측정했다.

| 시점 | 평균 프레임 ms | p95 ms | 평균 draw calls |
|---|---:|---:|---:|
| low | 1.70 | 2.08 | 810 |
| mid | 1.64 | 2.01 | 770 |
| high | 1.63 | 1.99 | 742 |
| west | 1.64 | 1.99 | 717 |
| east | 1.70 | 2.05 | 813 |
| yard | 1.62 | 1.97 | 690 |
| combat_mid | 1.94 | 3.04 | 903 |

맵 생성3579ms, 단회 측정/캐시 영향 포함. `forest-performance-shapes-after.json`과 `tree-shapes-perf-after.log`. ms는 벽시계 프레임 간격이며 GPU 전용 시간은 아니다. 초기 `shapes-before` 측정도 보관했지만 뒷부분에 빌드/헤드리스 검사 실행이 겹쳤으므로 정밀한 전후 시간 비교/회귀율로 사용하지 않는다. 이 PC의 측정 구간에서 큰 프레임 지연은 관측하지 않았으며 저사양/실제6인/장시간 성능 보장은 아니다.

## 재현 명령

저장소 루트 PowerShell:

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python prototypes/outdoor-kit/tools/forest_study.py
dotnet build game/HordeShooter.csproj --nologo -v q
$treeGodot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $treeGodot --headless --editor --path game --import
& $treeGodot --path game --resolution 1440x900 --fixed-fps 60 res://tests/ForestExpansionCheck.tscn
& $treeGodot --headless --path game --fixed-fps 60 res://tests/ForestQualityCheck.tscn
& $treeGodot --path game --resolution 1440x900 res://tests/ForestPerformance.tscn -- shapes-after
```

직접 실행은 `game` 폴더에서 `.\Start-Game.ps1 -Stage2Draft`. 재생 중인 게임은 다시 실행해야 새 모델이 로드된다. 남은 범위는 사용자 최종 형태/미술 수락, 저사양/다인 성능, 공용 outdoor-kit/정식 StageTwoMap의 별도 확대다.
