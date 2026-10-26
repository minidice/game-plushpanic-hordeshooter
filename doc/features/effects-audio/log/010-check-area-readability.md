# 화염·연무 중첩 가독성과 낮은 렌더 비용 조건

관련 ID: A-27, A-28, 기존 I-7/I-121. 사용자에게 현재 화염·연무·연료통 외형을 수락받은 뒤, 남아 있던 계획 5단계 검수를 보완한다. 이 기록은 전용 검사 추가이며 제품 외형·판정·소리 변경이 아니다.

## 변경과 재현 조건

- `AreaEffectsReadabilityCheck.tscn`을 추가했다. 실제 Stage/거위 플레이어/HUD 조준점, 일반 적3개와 실제 BossEnemy의 내려찍기 예고를 사용한다. `StartSkill(Slam)` 후 실제 물리 처리를 windup의35%만큼 진행하고 장면 처리를 고정한다. 붉은 원은 기존 보스 공격 예고이며 제거된 화염의 바닥 그림을 복구한 것이 아니다.
- 반경2.5m, 시각 나이1.5초의 화염·연무를 같은 슬롯에서1개(화염1),6개(각3),12개(각6)로 겹친다. 같은 프로세스에서 기준 화면→효과 화면→효과 숨김 화면을 비교한다. 별도의 위험 노드/피해를 만들지 않는다.
- 기본/낮은 렌더 비용 조건은 각각 별도 실행하고 각 실행의 기준 화면과만 비교한다. 고정하기 전 적의 대기 자세는 실행 사이에 조금 달라질 수 있으므로 두 조건의 화면이 픽셀 단위로 같은 장면이라는 뜻은 아니다. 조건 사이의 픽셀 차이나 면적 차이를 품질 저하율로 계산하지 않는다.
- `--reduced-render-cost`는 이 검사 프로세스의 3D 렌더 크기만0.67배로 낮추고 MSAA를 끈다. 출력/HUD는1600×900 그대로다. 제품 메뉴나 사용자 설정 파일에 저장하지 않으며 VFX 전용 LOD도 추가하지 않는다.
- 각 픽셀의 평균 RGB 절대 차이가3/255보다 큰 면적을 화면 변화 면적의 보조치로 기록한다. 이는 투명 입자의 실제 알파 면적, GPU overdraw, 가림 비율 또는 인간의 인지 점수가 아니다. 가독성은 캡처를 별도로 열어 확인했다.

## 실행 결과

기준 커밋8d2a16a의 독립 worktree. RTX5070Ti, Vulkan Forward+, 창1600×900, fixed-fps60. 아래 결과는 같은 GPU의 렌더 조건 비교이고 실제 저사양 기기의 검수가 아니다.

| 검사 | 결과 |
|---|---|
| Debug 빌드 | 오류0·경고0 |
| Godot 신규 worktree 가져오기 | 종료0, 오류·경고 없음 |
| 기본 조건: 3D scale1/MSAA4x |16항목 통과 |
| 낮은 렌더 비용 조건: scale0.67/MSAA 없음 |16항목 통과 |
| 두 조건 기준 화면 반복/효과 숨김 복원 |각각 차이0픽셀 |
| 전용 실행 종료 |ERROR·WARNING·FAIL 없음. 과거 I-17 전체 해결로 해석하지 않음 |

| 중첩 | 기본 변화 면적 | 낮은 렌더 비용 변화 면적 |
|---|---:|---:|
| 화염1 |11,386px /0.791% |11,994px /0.833% |
| 화염3+연무3 |78,414px /5.445% |80,106px /5.563% |
| 화염6+연무6 |179,303px /12.452% |182,495px /12.673% |

기본1/6/12장과 낮은 렌더 비용12개 화면을 실제로 열어 확인했다. 중첩이 커져도 플레이어의 몸 윤곽, 보스, 흰 조준점과 붉은 보스 예고 경계가 보인다. 안개가 일부 바닥과 몸 하단을 가리고 불꽃이 빽빽해지는 정도는12개에서 커진다. 낮은 렌더 비용 조건은3D 윤곽이 더 거칠고 부드러워지지만 동일 단서가 남는다. 기존 시야 규칙으로 왼쪽 일반 적은 실루엣이며 효과 때문에 강제로 투시시킨 것이 아니다. 자동 수치만으로 가독성 통과를 판정하지 않는다.

새 결정/버그가 필요한 제품 변경이나 새 결함은 발견하지 않았다. 따라서 새 ID를 발급하지 않고 기존 계획의 검수 근거로 남긴다.

## 재실행

저장소 루트 PowerShell에서 한 번에 하나씩 실행한다.

```powershell
dotnet build game/HordeShooter.csproj --nologo -v q
$godot = 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
& $godot --path game --fixed-fps 60 res://tests/AreaEffectsReadabilityCheck.tscn
& $godot --path game --fixed-fps 60 res://tests/AreaEffectsReadabilityCheck.tscn -- --reduced-render-cost
```

산출물은 `game/output/area-readability/{normal,reduced}/`의 `baseline.png`, `baseline-repeat.png`, `areas-{1,6,12}.png`, `baseline-after.png`, `coverage.json`. 실행 로그는 `game/output/area-readability/{import,normal,reduced}.log`에 보존했다. 생성 산출물은 커밋하지 않는다.

## 남은 범위

정지한 표본 한 카메라/바닥에서의 비교다. 화면 차이를 재현하기 위해 처리/시각 시계를 멈춘 것이므로 실제 전투 움직임의 읽기, 장시간 세션, 모든 카메라·바닥, 실제2PC/6인·저사양 하드웨어를 보증하지 않는다. 프레임 시간·draw call 측정은 기존 `CombatEffectsPerformance`의 별도 실행을 따른다. 실제 연막탄/AI 시야 차단·벽 우회·지형 투영·다층 피해와 음향은 범위 밖이다.
