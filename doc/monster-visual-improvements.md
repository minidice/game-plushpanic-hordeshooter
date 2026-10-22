# 몬스터 표면·접지·사망 동작 개선 — 2026-10-23

사용자가 [점검 결과](monster-visual-review.md)의 추천 순서로 수정을 승인해 게임에 반영했다. 이전 플레이어 아트 통합 변경은 보존했다. 이번 범위는 몬스터 표면 연결, 네임드/보스 접지, 체형별 사망 전환이다. 살아 있는 적의 능력치·공격 판정·네트워크 계약을 변경하지 않았다.

## 적용 내용

### 1. 감축 과정의 표면 틈

`prototypes/character-lab/tools/optimize_game_roster.py`에서 몬스터 부위의 위치 중복 정점을 감축 전에 연결한다. 부위별 원래 열린 경계를 기록하고 감축 뒤 증가하지 않았는지 검사한다. 플레이어 생성 경로는 그대로다. `--monsters-only`로 아래 10종을 재생성했다. UV·재질 슬롯과 런타임 피벗을 유지하며 의도적인 후드 개구부는 막지 않는다.

| 모델 | 삼각형 이전→현재 | 위치 결합 후 열린 경계 이전→현재 |
|---|---:|---:|
| 인간형 | 4,223 → 4,140 | 1,531 → 0 |
| 크롤러 | 3,787 → 3,708 | 1,367 → 0 |
| 끌개 | 3,881 → 3,780 | 1,375 → 0 |
| 덮치는 적 | 3,896 → 3,816 | 1,408 → 0 |
| 토사물 적 | 4,005 → 3,924 | 1,449 → 0 |
| 황소 | 2,644 → 2,592 | 924 → 0 |
| 풍선 | 2,264 → 2,240 | 664 → 0 |
| 황소 두목 | 4,466 → 4,344 | 1,558 → 0 |
| 풍선 대왕 | 3,752 → 3,690 | 522 → 0 |
| 토사물 어미 | 3,352 → 3,282 | 552 → 54 |

토사물 어미 원본에는 뒤쪽 반구 후드와 반원 테두리 등 열린 경계 64개가 있으며 감축 후 54개다. 경계는 구멍 개수가 아니다. `audit_geometry.py --check`는 원본/게임 GLB를 별도로 읽어 위치 중복 결합 후 검사한다. 모델별 기존 삼각형 예산도 모두 통과했다.

### 2. 시체가 뜨는 문제

`Effects.Topple`의 회전된 AABB 기준 보정을 `CorpseSurface`의 실제 바깥 정점 기준으로 교체했다. `Mesh.CreateConvexShape(clean: true, simplify: false)`로 얻은 정점은 메시별 약한 참조 캐시에 저장한다. 내부·중복 정점을 제거하면서 최저점 계산에 필요한 외곽은 유지한다. 매 프레임 전체 GLB 배열을 추출하거나 convex hull을 다시 만들지 않는다.

자세·크기·방향이 바뀐 현재 변환으로 최저점을 계산해 수평 지면에서 0.02m 여유를 두고 정착한다. 기존 시체 기록과 고정된 물리 몸체는 그대로 두고 외형만 보정한다. 위치·사망 yaw·300초 수명·일반 시체와의 분리 정책은 유지했다.

대표 결과: 황소 두목 0.414→0.020m, 끌개 0.370→0.020m, 토사물 어미 0.217→0.020m. 네임드 6종·보스 4종 모두 평지, 높이 2m 수평면, 높이 4m 공중 사망을 서로 다른 yaw에서 검사했다. 전환 중 관통 하한과 정지 후 접지 상한을 실제 메시 정점으로 확인했다.

### 3. 체형별 사망 동작

`CharacterDeathPose`가 현재 자세를 보관하고 기본 자세로 순간 복귀하지 않도록 전환한다. `Enemy.Die`는 공격 로직을 즉시 취소하되 네임드/보스의 현재 팔 자세는 사망 전환에 넘긴다. 마지막 자세에서 다시 기본 자세로 스냅하지 않는다.

| 체형 | 대상 | 동작 / 길이 |
|---|---|---|
| 황소 | 황소·거대 황소 | 앞쪽으로 무게 이동, 팔다리 접힘, 옆으로 쓰러짐·작은 반동 / 0.70초 |
| 무거운 직립형 | 황소 두목 | 주저앉는 압축과 다리 접힘 후 어깨 쪽으로 내려앉음 / 0.80초 |
| 둥근 체형 | 풍선·토사물 적·풍선 대왕·토사물 어미 | 압축, 팔다리 모으기, 옆으로 기울어 정착 / 0.74초 |
| 직립형 | 끌개·덮치는 적·무장 정예 및 기타 네임드 기본값 | 비스듬히 쓰러지며 팔·머리·발의 힘을 늦게 풀기 / 0.70초 |

황소 두목은 기존 다리 형상을 유지하면서 `boss_leftLeg`·`boss_rightLeg` 피벗을 추가했고, 각 발을 다리 아래에 뒀다(`boss-models.mjs`). 황소와 중간 보스의 기존 다리/꼬리 피벗도 사망 동작에 사용한다. 무장 정예는 사망 시 조준용 셰이더 팔을 숨기고 실제 휴식 팔을 사용하며 총을 몸 옆으로 내린다. 풍선은 기존 `Pop` 경로에서 머리를 숨긴 채 남은 몸만 쓰러진다. 일반 적은 기존 물리 시체 동작을 유지한다.

## 검증

- 수정 전: `audit_geometry.py --check`가 10종의 추가 열린 경계로 실패. `MonsterDeathCheck` 63개 검사 실행에서 황소·끌개·덮치는 적·토사물 적·황소 두목·거대 황소·토사물 어미의 공중 간격 실패를 확인했다.
- 최종 C# 빌드: 경고 0, 오류 0. Godot 리소스 가져오기 성공.
- 표면: 생성기 부위별 열린 경계·노드·삼각형/재질/두께 단언, 독립 GLB 검사 10종 통과.
- `MonsterDeathCheck`: **97 통과**, 최종 실행 종료 경고 없음. 실제 정점 접지·관통, 높은 수평면·공중 사망, 사망 기록/몸체 고정, 체형별 자세 차이, 공격 중 사망의 팔 자세 연속성을 포함한다.
- `CorpseReactionCheck` **29**, `CorpseTierCheck` **36**, `EnemyExpansion` **60**, `InfectedAlignmentCheck` **25**, `Integration` **149**, `ModeCheck` **147** 통과. `EnemyAttackCheck` **79**, `HitFlashCheck` 통과.
- 실제 GPU 렌더: `MonsterReviewCapture -- --after` **70장**, `MonsterLineupCapture` **11장** 완료 및 정측면/낮은 시점 확인. 비교 시트 3종을 실제 렌더로 작성했다. 촬영 시점·카메라·조명·크롭은 비교 열마다 동일하다. 바닥 페인트 색은 무작위여서 전후 다르다.
- 종료 경고(I-17): CorpseTierCheck ObjectDB 11개, Integration 4개, ModeCheck 6개, HitFlashCheck 2개. 개별 자원 종류는 이번에 분해 조사하지 않았으며 기능 통과와 자원 정리 경고를 구분한다. 중간 MonsterDeathCheck 실행에 있던 ObjectDB 2개는 최종 실행에서는 없었다.
- 생성 파일과 무관한 개인 출력은 `output/`에 두며 커밋하지 않는다. 이번 작업에서 커밋·푸시는 하지 않았다.

초기 10종 연속 생성/사망 준비는 현재 PC의 headless 검사에서 60.74ms, 후속 두 묶음 28.78/20.29ms였다. **스폰·자원 로드·효과를 포함한 묶음 시간**으로 hull이나 애니메이션만의 비용이 아니다. 병렬 검사 중 짧은 표본이며 GPU 프레임/저사양 성능 보증으로 사용하지 않는다.

## 전후 비교와 재실행

- [사망·접지 전후 비교](../game/output/monster-review-after/death-before-after.png)
- [생존 상태 표면 전후 비교](../game/output/monster-review-after/surface-before-after.png)
- [체형별 사망 순서](../game/output/monster-review-after/collapse-sequence.png)
- 수정 전 보존: `game/output/monster-review-before/captures/`, `capture.log`. 수정 후: `game/output/monster-review-after/`, `monster-after-capture.log`.
- 기능 로그: `game/output/monster-fix-<장면명>.log`, 반응 검사는 `monster-fix-reaction.log`. 수정 전 실패 로그: `monster-surface-red.log`, `monster-death-red.log`.

```powershell
# 모델 원본이 없다면 character-lab에서 export-game-roster.mjs의 해당 종 옵션으로 먼저 재생성
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python prototypes/character-lab/tools/optimize_game_roster.py -- --monsters-only --no-preview
python prototypes/monster-review/tools/audit_geometry.py --check
dotnet build game/HordeShooter.csproj --nologo -v q
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --path game --quit
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path game res://tests/MonsterDeathCheck.tscn
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --path game res://tests/MonsterReviewCapture.tscn -- --after
python prototypes/monster-review/tools/make_before_after.py
```

비교 시트 재생성은 보존한 수정 전 이미지가 필요하다. 이전 자산·코드의 별도 체크아웃 없이 현재 코드만으로 수정 전 이미지를 다시 만들 수는 없다. 원본 모델 출력과 렌더는 생성물이며 저장소에는 생성 도구·게임용 GLB·검사 장면을 보관한다.

## 남은 제약

경사·계단·여러 높이의 지형을 가로질러 몸 전체가 밀착하는 래그돌은 아니다. 사망점의 수평 높이에 맞춘 외형 보정이며, 네임드 시체 위치의 실제 네트워크 공유/루팅은 기존 I-43 범위로 남는다. 이번에는 표면과 동작을 개선했으며 모든 종의 실루엣·장식·재질을 새로 디자인한 것은 아니다. 장시간 다수 네임드 사망, 낮은 사양, 서로 다른 두 PC의 시각 일치와 실제 플레이 체감은 추가 확인 대상이다.
