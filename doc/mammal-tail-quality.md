# 라쿤·랫서판다 긴 꼬리 (2026-10-26)

사용자가 승인한 세 가지 개선: 중간 부풀음 제거, 완만한 기본 곡선, 뿌리보다 끝이 늦게 따라오고 정지하면 빠르게 잦아드는 움직임. 기존 짧은 꼬리(style3)와 조류 꼬리는 유지한다. 최종 미술 수락과 구현 검증은 구분한다.

## 현재 구현

- 라쿤·랫서판다 긴 스타일0/1/2/4의 단면은 길이80%까지 같은 반지름, 마지막20%에서 둥글게 좁아진다. 기본 반지름은 라쿤0.145m·랫서판다0.18m, 기본 길이는0.95m·1.15m다. 이 수치는 사용자 지정값이 아닌 현재 조형값이다.
- 기본 중심선은 완만하게 내려갔다 올라오며 옆으로0.10m 휜다. 말린 스타일의 추가 굽힘과 기존 스타일별 길이/폭 배수는 유지한다. 같은 프로파일을 Blender 기본 GLB와 C# 커스터마이징 메시에서 사용한다.
- 원래 꼬리 장착점, TailSize0.65–1.3·스타일ID0–4·색/줄무늬 재질2채널을 보존한다. 몸통/얼굴/손발·기존 장비/색 저장 형식·충돌/속도/무기 규칙은 바뀌지 않는다.
- 긴 꼬리에만 고정 루트와3개 굽힘 구간의 작은 GPU 스킨을 사용한다. 전신 관절 리그나 물리 시뮬레이션이 아니며 프레임마다 메시를 새로 만들지 않는다. 이동·방향 전환을 제한된 각도와 지연 필터로 전달하고, 정지·초기화·스타일 전환·순간이동을 처리한다.
- Studio의 dt0 정적 미리보기는 중립 자세다. 게임에서는 기존 재질/그림자·피격·상태이상 표면이 같은 스킨을 따른다. 예전 동물형 감염 모델도 동일 종의 꼬리 생성기를 사용한다.

## 소스와 재현

- 모델: `game/assets/characters/{raccoon,redpanda}/*-clean.glb`
- 생성: `prototypes/grounded-art/tools/build_{raccoon,redpanda}_clean.py`, 공통 `mammal_tail_profile.py`
- 모델 검사: `validate_{raccoon,redpanda}_clean.py`, 독립 단면 검사 `validate_mammal_tail.py`
- 비교 렌더: `render_mammal_tails.py`는 `game/output/mammal-tails/*-before.glb`가 있으면 작업 전 모델과 비교하고, 없으면 fitted 원본을 비교 대상으로 사용한다.
- 런타임: `CharacterTail.cs`, `CharacterTailMesh.cs`, `CharacterTailSkin.cs`, `StatusVisualController.cs`.

## 검증 기록

아래 결과는 실제 실행한 검사와 렌더 기준이다.

- Blender 실제 생성/재가져오기: 랫서판다236개·라쿤322개 검사 통과. 삼각형35,367·34,904개. 이전 clean보다 각각622개 감소.
- 작업 전 clean과 비교해 두 종 모두 꼬리 메시만 변경, 나머지60개 노드의 부모·변환·재질·정점은 동일. 꼬리 피벗/재질 채널도 동일.
- 실제 Blender 뒤쪽 사선 렌더에서 단면·끝마개·줄무늬 연결을 확인했다.

- Debug/ExportRelease 빌드: 오류0·경고0. `MammalTailCheck`7,756 통과(단면/스킨/회전 지연·정지·초기화·색/스타일·상태이상·감염형 포함). 원본 생성기와 대조한 조류10조합/포유류 짧은2조합의 정점·법선 바이트 해시가 동일하다.
- 실제 창 모드 `TailMotionCapture`29 통과,22장과 `runtime-poses.csv`. 같은 카메라/메시/몸 변환에서 스킨만 중립으로 바꾼 비교가 라쿤4,799·랫서판다7,284픽셀 차이를 보여 실제 GPU 표면 변형을 확인했다. 실제 Stage2 이동/회전 후 정지65프레임에서 세 구간 회전이0으로 수렴했다.
- 기존 `TailMeleeCheck`13,976·`StatusVisualCheck`57 통과. 각 종 `*CleanCheck --rig-only`7,133, `CharacterSurfaceCheck`43·`CustomizationToneCheck`89·`ModularCustomization`67·`GroundedArtCheck`212 통과.
- Studio 꼬리 전용53개/26장(기본·스타일0–4×크기0.65/1.3·배색·복원), 기존 전체 커스터마이징 각65개/21장 통과. 두 경로 모두 종별 기본값/복원 이미지 차이0픽셀. 저장 appearance.cfg와 프리셋 JSON도 실행 전후 동일하다.
- 직접 확인한 대표 화면: 두 종 Blender 전후 뒤쪽 사선, 실제 Godot 회전/Stage2, Studio 긴 꼬리 최대 크기·말린 꼬리·배색. 모든 장면을 사용자 최종 아트 수락으로 표시하지 않는다.

독립 런타임 검토 후 조류·짧은 스타일 전환 시 기존 모션 위상까지 초기화되던 회귀를 수정하고 움직이는 스타일 전환 검사로 확인했다. 새 긴 꼬리의 스킨 이력만 초기화한다. 기존 GroundedArt 검사의 기본 꼬리 메시 동일성·fitted 경로 가정도 현재 clean 모델/프로파일·장착점·재질·기본 복원 및 실제 고정 손 계약으로 갱신했다.

### 수정 중 발견 및 실패/경고

- 독립 모델 검토에서 기본 끝 색이 어두워지는 회귀를 발견했다. 원래 밝은 끝을 보존하기 위해 긴 스타일만7밴드로 맞추고 실제 끝점 면이 재질0인지 검사했다. 두 모델 재생성/재검사와 범위 재검토 통과. 이는 기존 줄무늬 인상을 유지하기 위한 보정이며 색 저장 형식은 그대로다.
- 런타임 최초 검사는 중간 부풀음/루트 굵기 때문에 실패했다. 구현 중 dt0의 이중 초기화로 회전 이력이 지워진 문제와 끝점 부동소수 오차로 남는 미세 반지름을 수정해 전용 검사 통과. 테스트용 캐시 재질 해제와 GPU 가중치 양자화 허용오차 오류는 검사 구현 오류로 분리했다.
- `Integration.tscn`은123개 검사 이후 `shots kill the enemy`에서 실패했다(`Integration.cs:441`). 실패 시 플레이어는 test_capsule, 적은 plush이며 goose 전환 이전이다. 원인 및 꼬리 변경과의 독립성은 확정하지 않았다. 전체 통합 통과로 기록하지 않으며 이 작업에서 전투 코드는 수정하지 않았다. 로그 `game/output/mammal-tails/integration.log`.
- 꼬리/근접 검사 종료 ObjectDB2개, 실패한 Integration 종료13개 누수 경고가 남는다. 기존 I-17 해결로 표시하지 않는다. Blender 두 생성기는 각각 use_nodes의 Blender6.0 폐기 예정 경고3건과 함께 정상 종료했다.
- `CustomizationModularCheck.tscn`이라는 없는 장면명으로 실행한 최초 시도는 명령 오류다. 실제 `ModularCustomization.tscn`으로 다시 실행해67개 통과했다.

### 재실행 명령

저장소 루트 PowerShell에서 `$godot`을 로컬 Godot 실행 파일 경로로 지정한다.

```powershell
dotnet build game/HordeShooter.csproj --nologo -v q
dotnet build game/HordeShooter.csproj --nologo -v q -c ExportRelease
& $godot --headless --path game --editor --import --quit
& $godot --headless --path game res://tests/MammalTailCheck.tscn
& $godot --path game res://tests/TailMotionCapture.tscn
& $godot --headless --path game res://tests/TailMeleeCheck.tscn
& $godot --headless --path game res://tests/StatusVisualCheck.tscn
$env:STUDIO_URL='http://127.0.0.1:4323'
Invoke-RestMethod "$env:STUDIO_URL/api/reload" -Method Post -ContentType application/json -Body '{}'
node studio/tests/mammal-tails.mjs
python studio/tests/compare-render.py studio/output/mammal-tails/raccoon-default.png studio/output/mammal-tails/raccoon-reset.png
python studio/tests/compare-render.py studio/output/mammal-tails/redpanda-default.png studio/output/mammal-tails/redpanda-reset.png
```

생성기/검사/렌더 Python은 Blender의 `--background --python-exit-code 1 --python <파일>`로 실행한다. `game/Start-Game.ps1 -Test`는 Integration만 실행하므로 위 전용 장면을 대신하지 않는다. 산출물은 `game/output/mammal-tails`, `studio/output/mammal-tails`, 두 종 clean output 폴더에 생성되며 버전 관리하지 않는다.

최종 독립 검토에서 이번 꼬리 변경의 추가 조치 사항은 발견되지 않았다. 검토자는 기존 실행 결과와 실제 이미지·코드를 확인했으며 검사를 중복 실행하지 않았다. 상태이상 스킨 연결은 구조 검사로 확인했고, 움직이는 상태이상 표면 자체의 별도 렌더는 이번 증거에 포함되지 않는다.

## 한계

꼬리와 벽/장비 사이의 물리 충돌, 개별 털, 전신 스켈레톤은 추가하지 않는다. 실제2PC·저사양 대규모 성능·패키지 export는 이번 검증 범위 밖이다. 긴 꼬리 굽힘은 장식이며 게임 판정을 바꾸지 않는다.

## 2026-10-26 main 통합 재검증

사용자 커밋·병합·푸시 요청에 따라 현재 변경 전체를 재확인했다. origin/main `2e75562`는 작업 기준과 같아 원격 선행 변경은 없었다. 이 단계에서는 제품 동작을 추가 수정하지 않았다.

- Debug/ExportRelease 빌드: 오류0·경고0.
- `Integration.tscn` 단독 실행:149개 통과, 종료0. 앞선123개 이후 사격 처치 실패는 재현되지 않았으나 원인/발생 조건은 아직 미확정이다. 이전 실패 기록을 삭제하거나 해결 확정으로 바꾸지 않는다. 종료 ObjectDB4개 경고가 남는다.
- 전용 재실행: MammalTail7,756, GroundedArt212, StatusVisual57, CommonRosterNet130, RaccoonClean/RedpandaClean `--rig-only` 각각7,133, 창 모드 ReindeerZombie8,024·CommonBrow503 통과. 이 전용 로그들은 오류·종료 경고 없음.
- 실행 중인 Studio4323: 꼬리53/26장·순록/감염체140·눈썹199 통과, 두 종 꼬리 기본/복원0픽셀 차이.
- staged 파일에서 output·.godot·node_modules·bin/obj·참고 첨부/개인 파일 제외 및 diff 공백 검사 확인. 로그 `game/output/main-integration-2026-10-26/`는 자동 산출물로 커밋하지 않는다.

제품 외형은 앞선 Blender/게임/Studio 검수 결과를 사용하며 이번 추가 창 모드 검사도 실제 렌더를 생성했다. 실제2PC·대규모 성능·패키지 export·최종 사용자 아트 수락은 추가 검증하지 않았다.
