# 무기 착용 날개와 조준 자세 후보

2026-10-23. 사용자 요청: 개선한 접힌 날개 느낌을 총을 들었을 때도 유지하고, 별도 후보에서 조준 자세를 비교한다. **기존 게임·이전 후보는 수정하지 않았다.** 제품 기능 변경이 아닌 오프라인 아트 후보다.

## 변경과 비교 기준

기존 접힌 날개를 통째로 돌리면 어깨와 손잡이 위치를 동시에 맞추기 어렵고, 조준할 때 조끼를 뚫거나 총을 잡는 끝이 어색해진다. 개선 후보는 같은 날개 메시를 어깨부터 손잡이까지 곡선으로 굽혔다. 납작한 넓은 면을 유지하고 깃털 끝 세 갈래가 손잡이 바깥을 돌아가게 했다. 대기 자세에서는 날개 중간을 조금 바깥으로 빼 개머리판을 피한다.

기존 코드의 M4 한 손 운용을 참고했다. 오른쪽은 무기용으로 다듬고 왼쪽은 앞서 승인된 접힌 날개를 유지했다. 몸·얼굴·복장·왼쪽 날개 총13개 메시, 기존 어깨 부모 노드·피벗·재질 채널을 보존했다. 날개의 면 연결과 삼각형 수는 이전 개선 후보와 같다.

비교 왼쪽은 **이전 날개 후보를 새 총 위치에 맞춰 통째로 회전시킨 방식**, 오른쪽은 **굽힘과 깃털 끝 감싸기를 적용한 새 후보**다. 게임의 캡슐 팔이나 실제 게임 화면을 재현한 비교가 아니다. 각 쌍의 총 위치·몸·복장·카메라·조명은 동일하다.

| 자세 | 총 위치 (Blender X,Y,Z, m) | 총의 X 회전 | 의미 |
|---|---|---|---|
| ready | (0.52, -0.52, 1.02) | +12도 | 총구를 조금 내린 대기 |
| aim | (0.54, -0.65, 1.22) | 0도 | 앞으로 뻗은 수평 조준 |
| aim_up | (0.54, -0.65, 1.22) | -20도 | 위쪽 조준 |

좌표는 이 아트 후보의 임시 수치다. 게임 무기 위치·조준 동작의 확정 규격이 아니다. 기존 코드의 기본 손잡이 오프셋을 참고해 M4의 로컬 `(0.035, 0, -0.0675)`를 기준점으로 삼았다. 조준경에 눈을 대는 ADS 자세가 아닌 현재 캐릭터 비율의 한 손 조준 표현이다.

## 산출물

- [세 자세 한눈에 보기](output/weapon-wings/pose-lineup.png)
- [수평 조준 전후](output/weapon-wings/comparison-aim-quarter.png), [날개 확대 전후](output/weapon-wings/comparison-aim-detail.png)
- [대기 전후](output/weapon-wings/comparison-ready-quarter.png), [대기 확대 전후](output/weapon-wings/comparison-ready-detail.png)
- [위쪽 조준 전후](output/weapon-wings/comparison-aim_up-quarter.png)
- [수평 조준 측면](output/weapon-wings/comparison-aim-side.png), [위쪽 시점](output/weapon-wings/comparison-aim-top.png)
- [Blender 편집 원본](source/goose-weapon-wings-study.blend): 세 자세의 전후 모델, 기존 M4와 스튜디오 포함. 기본 표시는 `aim_after_` 후보.
- [명세](weapon-wings-manifest.json), [검사 결과](output/weapon-wings/validation.json)

| 자세 | 총 포함 미리보기 | 캐릭터만 | 날개만 |
|---|---|---|---|
| 대기 | [preview](assets/goose-weapon-wings-ready-preview.glb) | [character](assets/goose-weapon-wings-ready-character.glb) | [only](assets/goose-weapon-wings-ready-only.glb) |
| 수평 조준 | [preview](assets/goose-weapon-wings-aim-preview.glb) | [character](assets/goose-weapon-wings-aim-character.glb) | [only](assets/goose-weapon-wings-aim-only.glb) |
| 위쪽 조준 | [preview](assets/goose-weapon-wings-aim_up-preview.glb) | [character](assets/goose-weapon-wings-aim_up-character.glb) | [only](assets/goose-weapon-wings-aim_up-only.glb) |

각 자세는 별도의 정적 GLB다. 날개만 파일은 좌우2메시·2,448삼각형, 캐릭터 파일은14메시·34,298삼각형, 총 포함 미리보기는18메시·38,102삼각형이다. 총은 `prop_` 노드로 분리했고 character/only 파일에는 넣지 않았다. `WingShoulder`, `WingWrist`, `WeaponGripReference`는 편집용 참고 노드이며 런타임 계약에 새 필드를 추가한 것이 아니다.

## 검사와 한계

최종 생성·렌더·GLB 재수입 검사가 정상 종료했다. 실제 GLB 9개의 검사 **418항목 통과**. 원본 렌더22장, 전후 비교11장과 세 자세 모음1장을 생성했다. 전신·확대·측면·위쪽 시점으로 형태를 시각 검토했다.

- 보호 대상58파일의 SHA-256 일치: 이전 GLB·Blender 원본, 게임 거위·M4, 참조한 무기 팔/무기 표시 소스. 기존 생성기도 수정하지 않았다.
- 메시·삼각형 수, 축과 경계, 부모와 원래 어깨 피벗, 참고 위치와 총구 위치, 총 포함/제외 구성을 실제 내보낸 GLB 재수입으로 확인했다.
- 몸·복장·얼굴·왼쪽 날개13메시의 월드 정점 위치를 이전 후보와 양방향 최근접 거리로 비교했다. 오차 허용은0.00001 m다.
- 좌우 날개는 닫힌 표면이며, 면적0인 면과 인접하지 않은 면의 자기 교차가 없다.
- 세 자세 모두 좌우 날개와 조끼 표면 교차0쌍. 몸에 붙는 날개 뿌리는 일부 몸 안에 들어가므로 모든 몸 표면과 비접촉을 주장하지 않는다.
- 손잡이 기준점과 날개 표면의 거리는 대기 약3.83 cm, 수평 약2.36 cm, 위쪽 약2.33 cm다. 이는 손잡이 중심에서 표면까지의 거리이지 손잡이와 날개가 떨어진 공기 틈의 크기가 아니다.
- 날개와 총의 접촉은 남긴다. 교차하는 날개 면의 중심이 손잡이 기준점에서10 cm 이내인지 검사했고 실제 최대는 약6.77 cm였다. 개머리판·총몸 쪽의 비의도적 교차는 곡선과 배치 보정으로 제거했다. 접촉 검사 수치는 표면 교차 쌍이며 물리 충돌 시험은 아니다.
- 초기 조형에서 끝 방향의 급격한 변화와 개머리판 간섭을 발견해, 매끄러운 곡선·곡면 방향으로 바꾸고 대기 자세를 바깥으로 보정했다. 최종 후보에서 자기 교차와 손잡이 밖 교차를 재검사했다.
- 최초 전체 검사에서 M4 `Recesses and rubber`의 면적0에 가까운 면208개가 발견됐다. 원본 M4를 직접 읽어 동일한 수임을 확인했다. 새 날개 문제와 구분하며, 게임 무기나 복사한 미리보기 총을 임의 수정하지 않았다. 검증기는 원본의 동일 수가 보존되는지 별도로 확인한다. 최종 실행 로그에 Python 예외나 종료 경고는 없지만, 이 기존 무기 품질 제약은 남아 있다.

연속 보간·사격 반동·재장전·다른 총·양손 운용·체형 변경·Godot 런타임 성능은 미검증이다. 세 GLB를 바로 게임에 넣으면 자동으로 자세가 전환되는 구성은 아니다. 게임의 `CharacterWeaponGrip.cs`는 여전히 별도 캡슐 팔을 생성하므로 실제 적용은 그 경로의 후속 작업이다.

`doc/decisions.md`, `doc/issues.md`, `doc/character-model-spec.md`와 기존 날개 결과를 검토했다. 게임 기능·동작·설정 계약이 바뀌지 않았고 사용자가 기존 게임 보존을 명시했으므로 제품 결정·이슈 문서는 변경하지 않았다. 검토 이유와 후보의 문제·해결·남은 한계는 이 독립 기록에 남긴다. 게임 검사는 실행하지 않았다.

## 재생성

저장소 루트 PowerShell에서 실행한다. 새 후보와 비교 산출물은 `prototypes/grounded-art/` 안에만 저장한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/build_weapon_wings.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' -b --python-exit-code 1 --python prototypes/grounded-art/tools/validate_weapon_wings.py
python prototypes/grounded-art/tools/compare_weapon_wings.py
```

빠른 시안 확인은 생성 명령 뒤에 `-- --preview`를 붙인다. 수평 조준 전신/확대4장만 렌더하므로 전체 비교는 기본 생성 명령 후 실행한다. `output/`은 재생성 가능한 로컬 산출물이다.
