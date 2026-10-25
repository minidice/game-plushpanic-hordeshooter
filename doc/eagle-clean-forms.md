# 독수리 — 매끈한 데포르메 후속

2026-10-25. 사용자의 다음 추천 작업 진행 요청에 따라 [공통 아트 기준](character-art-direction.md)의 독수리 항목을 이어간다. [이전 독수리 제작 이력](eagle-quality.md)과 [앵무새 첫 적용](parrot-clean-forms.md)을 구분한다. 이번 모델의 개별 아트 최종 수락과는 별개다.

## 범위와 판단

앵무새 다음으로 같은 조류인 독수리에 기준을 적용한다. 작은 검은 눈·매끈한 큰 표면·끝으로 좁아지는 깃·얇은 발·구조가 읽히는 장비를 공유하면서, 독수리의 밝은 머리와 낮은 눈매·금색 갈고리 부리·넓은 날개·발톱으로 종을 구별한다. 기존 두툼한 목깃과 눈 테두리·둥근 발·조끼 파우치가 새 목표와 어긋나는 점을 조형으로 수정한다.

`common_eagle`, 피벗·팔레트 채널·원본 날개 그립·충돌·전투 수치·투구/눈 장구류 제외 정책은 유지한다. 앵무새를 포함한 다른6종 GLB를 다시 만들지 않는다. 모델 폴리곤 수와 실제 호드 FPS는 별개로 기록한다.

## 재현과 증거

- Blender 선택 생성: `--background --python-exit-code 1 --python prototypes/common-monsters/tools/build_common_monsters.py -- --species eagle --no-render`.
- 독립 재가져오기: 같은 Blender 인수와 `validate_common_monsters.py -- --species eagle`.
- 비교: `review_parrot.py -- --species eagle --review-name eagle-clean-forms --capture-baseline`, 이후 `--label after`. 기준은 한 번만 보존하며 기존 `eagle-quality`를 덮어쓰지 않는다. `parrot_contact_sheet.py --species eagle --review-name eagle-clean-forms`로 비교판 생성.
- Godot import/Debug 빌드 후 `ParrotGripCheck.tscn -- --eagle`, `HunterPoseCheck.tscn`, `CommonRosterCheck.tscn`, `ParrotMotionCheck.tscn`, `CommonRosterNetCheck.tscn`.
- 실제 창: `HunterCombatCheck.tscn -- --eagle-stage2`. 기존 Stage2 접근/이동 검수를 독수리에도 실행한다. 자동 입력과 검수 지점 배치를 사용하며 전체 맵 수동 완주와 구분한다.
- Studio: `STUDIO_URL=http://127.0.0.1:4323`, `STUDIO_SPECIES=eagle`의 `studio/tests/hunter-attacks.mjs`,6무기 정지 렌더 및 브라우저 타임라인 확인.

Blender 증거는 `prototypes/common-monsters/output/eagle-clean-forms`, Studio는 `studio/output/eagle-clean-forms`, 게임은 `game/output/eagle-clean-forms`와 `game/output/eagle-clean-*.log`. 모두 재실행으로 생성되는 로컬 산출물이다. 이번 작업 전 수정된 앵무새 모델과 문서를 보존하며 커밋·푸시는 요청되지 않았다.

## 현재 구현

- 매끈하고 길게 이어지는 몸/목, 낮은 작은 검은 눈, 얇은 눈 위 면과 금색 갈고리 부리. 두꺼운 눈 테두리/별도 반짝임과 몸의 봉제 표현을 제거했다.
- 목깃을 얇고 끝으로 좁아지는 겹침으로 변경했다. 첫 날개 시안에서 밝은 긴 깃이 각진 막대처럼 보여, 중간 곡면 단면을 보완하고 넓은 갈색 깃과 밝은 끝으로 다시 정리했다.
- 좁은 발목과 평평한 밑면, 앞쪽3발가락·뒤쪽 발가락·작은 발톱을 제작했다. 양쪽 발바닥 면적 각각 약0.047425m²이며 바닥 아래 정점 없음. 조끼에는 사각 파우치/덮개/여밈과 몸을 감싸는 끈을 적용했다.
- 기본5,220→7,148삼각형, 조끼 포함5,940→8,572, 비활성 안경 포함전체6,304→8,936. GLB166,452→234,664바이트. 기본8천 미만이며 형상 개선으로 비용은 증가했다. 실제 호드 FPS/장시간 부하를 측정한 것은 아니다.
- 최종 SHA256 `4d4ef5c9664e5db0b4552b5d62409f20edd078171ab9ef9cec390fcb8020da9e`. 다른6종(직전 수정 앵무새 포함)은 이번 기준 해시와 동일하다.

## 검증 결과

- 수정 전 Studio3무기 및 실제 Stage2 이동/전투38검사와8장 화면을 보존했다. Blender 같은 조명/카메라 전후22뷰와 독립 재가져오기60검사 통과. 목깃·날개·발/장비를 실루엣/단색/최종색/플레이 크기로 검수했다.
- 머리 최대140mm 이동/0.224rad 기울기의 앞뒤·좌우 스트레스 렌더에서 목 안쪽 겹침이 몸을 가려 노출된 절단면/열린 틈이 보이지 않았다. 최초 스트레스 도구에서 quaternion 노드에 Euler 회전을 설정해 기울기가 적용되지 않았던 검사 오류를 수정하고 XYZ 모드에서 다시 렌더했다. 최종 판단은 재검사 결과에 근거한다. 독수리 `HeadSpringScale`은 기본1을 유지한다.
- 실제 런타임 독수리6무기 그립3,084·수렵 포즈156·일반 몬스터264·앵무새/독수리/사자 머리 이동9,642·localhost ENet108검사 통과. 머리 이동 검사에서 앵무새7mm, 독수리/사자140mm의 기존 표시 동작을 유지했다. 반복 조합/프레임 검사 수이며 개별 기능 개수가 아니다.
- 실제 Stage2 창에서 접근 이동·정지와 몽둥이/칼 명중·빗맞음·기절 취소·활 발사/취소,8장 렌더 포함38검사 통과. 입력을 자동 공급한 실제 게임 장면이며 전구간 수동 플레이가 아니다.
- Studio 황토색6무기 정지 렌더와 붉은색 수렵3무기 각12프레임·소리 API50검사 통과. 브라우저에서 녹색 독수리/나무활 공격 미리보기를 다시 생성하고 타임라인5(준비0.969초)를 확인했다(`studio/output/eagle-clean-forms/browser-bow.png`). 공통 GLB/게임 포즈를 읽으므로 별도 Studio 모델이나 웹 데이터 복제를 추가하지 않았다.
- Debug/ExportRelease 빌드 오류·경고0. 이번 전용/Stage2/네트워크 로그에서 오류·종료 경고 없음. Blender의 `Material.use_nodes` 폐기 예정 안내와 Git CRLF→LF 안내는 성공 여부와 구분한다.
- 독립 모델/코드/통합 검토 통과, 수정이 필요한 주요 결함 없음. 검토자는 최대 변위8뷰·6무기·공격 표본·실제Stage2 8화면, 최종/다른6종 해시와 기존 검사 로그·현재 문서를 확인했다. 검토자가 검사를 별도로 재실행한 것은 아니다.

## 남은 한계

머리·몸은 별도 강체 피벗으로 최대 기울기에서 목깃의 겹침/접합선이 보일 수 있다. 날개 전체의 기존 무기 연결 변형을 사용하며 개별 깃털/손가락 관절은 없다. 평면 발바닥은 경사면 발 IK를 구현한 것이 아니다. 다른 종의 새 기준 적용·최종 사용자 아트 수락·실제2PC·전체 맵 수동 완주·장시간 호드 성능은 후속이다.
