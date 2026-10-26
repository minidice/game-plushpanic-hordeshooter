# 안개 바닥 제거와 지속 불꽃 위치 고정

2026-10-27. A-27 후속. 사용자가 안개의 바닥 그림 제거를 요청했고, 불이 여기저기 옮겨 다니는 외형과 위치 변경의 필요성을 질문했다.

## 변경과 이유

- `AreaSurfaceVisual`에서 연무의 지면 면/재질 생성을 제거했다. 화염과 연무 모두 반투명 입자만 남으며 지면 무늬/그을음/흐름 노이즈를 로드하지 않는다. 이전 시안 리소스는 보존한다.
- 이전 불꽃은 `ParticleProcessMaterial`이0.75초 수명마다 새 무작위 위치를 뽑았다. 반복감을 줄이는 연출에는 도움이 될 수 있지만, 지속 연소의 자리가 옮겨 보이는 단점이 있어 이번 요청에는 고정 위치를 선택했다.
- `area_flame_particles.gdshader`: 재생성 횟수인 NUMBER가 아닌 고정 INDEX로 각 불꽃의 수평 위치를 계산한다. 수평/상승 이동 없이 높이±12%, 농도0.47~0.61, 프레임 위상만 변한다. 높이 변화는 밑동을 기준으로 보정한다. [Godot 입자 셰이더 공식 문서](https://docs.godotengine.org/en/stable/tutorials/shaders/shader_reference/particle_shader.html)의 INDEX/NUMBER·TRANSFORM·CUSTOM 계약과 설치된4.7.2 엔진에서 확인했다.
- 명시적 clock과 기존 GPU 시간 진행을 유지해 Studio와 게임의 정지/재개가 일치한다. 불티와 연무는 원래처럼 흐르고 떠오르며, 위험 종료 시 불꽃을 끄고 잔연기만 남긴다. 피해·반경·수명·음향 변경 없음.
- A-27 현재 결정/바뀐 결정·색인, 상세 규격, 기능 체크리스트를 갱신했다. 제작 중 외형/셰이더 수정이며 새 제품 이슈나 결정 ID는 만들지 않았다.

## 검사와 실제 렌더

- Debug 빌드 오류0·경고0.
- 첫 GPU 렌더는 셰이더 보조 함수에서 INDEX를 직접 읽어 `Unknown identifier INDEX` 오류가 발생했다. 검사 항목 통과 문구와 별개로 GPU 오류 로그/불꽃 누락으로 실패 판정했다. start/process에서 INDEX와 출력 변수를 인자로 넘기도록 수정했다. 최초 로그는 `stable-area-first.log`에 보존했다.
- 수정 후 `AreaEffectsCheck` 창1600×900 **36통과**, `CombatEffectsCheck` 창1600×900 **92통과**. 최종 두 로그에 셰이더 오류·제품 오류·종료 경고 없음. 일시정지 중 화면 픽셀 동일, 재개 뒤 잔연기 전환, 밝은/어두운 바닥·원거리·벽/단차·전투 중첩을 확인했다.
- Studio 실제 생성기로 화염7.167초/87프레임, 안개5.2초/79프레임 재생성. 불꽃0.75/1.5/2.25/3.0초 비교 렌더에서 자리가 유지되고 모양만 변하는 것을 확인했다. 안개의 바닥 얼룩이 사라지고 입자가 유지되는 것도 확인했다. 최종 Studio 셰이더 오류 없음.
- 초기 가져오기에서는 이전 로컬 캡처 폴더를 복사하며 함께 복사한 `.import`의 중복 UID 경고가 있었다. 작업용 `output/area-effects/.gdignore`로 생성 산출물을 가져오기에서 제외했다. 게임 에셋 UID 충돌은 아니다. 이 임시 파일은 output과 함께 커밋하지 않는다.
- `git diff --check` 통과. 전체 음향·네트워크·저사양 성능 검사는 재실행하지 않았다.

로컬 산출물: `game/output/area-effects/stable/{fire_pool,smoke_cloud}/`, `stable-fire-fog.gif`, `stable-root-sheet.png`, `stable-area.log`, `stable-combat.log`. output은 커밋하지 않는다.

남은 제약: 고정되는 것은 연소 위치이며 불꽃 텍스처의 끝과 폭은 계속 변한다. 위험 경계를 정확한 선으로 보여 주지 않는다. 벽 우회/다층 판정과 최종 외형 수락은 후속이다.
