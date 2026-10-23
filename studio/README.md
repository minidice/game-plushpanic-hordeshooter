# Plush Panic Studio

현재 작업 폴더의 게임을 읽는 로컬 검수 시제품. **캐릭터 / 몬스터 / 오브젝트** 메뉴에서 조합 확인, 도감 검색·분류, 실제 외형과 속성을 확인한다. 실제 Godot가 렌더하며 게임 데이터·커스터마이징 저장 파일은 변경하지 않는다.

저장소 루트에서 `./studio/Start-Studio.ps1` 실행 후 [로컬 Studio](http://127.0.0.1:4317)를 연다. 종료는 터미널 Ctrl+C. Godot 위치는 `-GodotDir`, 포트는 `-Port`로 지정한다.

웹의 **로컬 데이터 다시 읽기**로 현재 C# 빌드와 에셋 import를 다시 실행할 수 있다. 페이지/종을 바꾸면 임시 조합은 초기화된다. 실시간 애니메이션 대신 정지 이미지를 갱신한다.

[구조·검증·제약](../doc/studio-prototype.md). Node.js 외 웹 의존성 없음. 별도 서버 배포 없음. `output/`은 로그·검사 이미지용 로컬 산출물이다.

목록은 현재 로컬 자산에서 읽는다. **펫3·드론4·부사수4는 하나의 항목에서 고/저품질을 나란히 비교**한다. 펫/드론은 별도 GLB, 부사수는 자동 LOD 설정이며 원본 삼각형 수를 표시한다. 몬스터는 배율 적용 전 기본값, 모델 충돌은 manifest 추천값이다. 이동/탄환 차단은 실제 `object_interactions.json`과 게임 공통 로더에서 읽는다. 철조망은 이동 차단/탄환 통과, 일반 구조물은 양쪽 차단이며 배치 예외와 출처를 함께 표시한다. 낮은 엄폐물(모래자루 · 콘크리트 방벽)과 파괴(깰 수 있는 강도 · 내구도)도 같은 데이터에서 표시한다(C-29). 부서질 때 폭발은 아직 미연결이다. [현재/후속 계약](../doc/object-data-spec.md).

실행 중 `node studio/tests/smoke.mjs`, `node studio/tests/library.mjs --all`, `node studio/tests/quality.mjs`, `node studio/tests/interactions.mjs`로 검증한다(검사에만 Python + Pillow 필요).
