# Studio 표면 프리셋 선택·공유 게인 저장

- 관련: A-26, I-64(부분 해결 상태 유지), C-29(모래주머니 HP50/엄폐 유지), T-25. 기준 `4bacc0d`, 격리 작업트리 `surface-studio-editor`, 전용 포트4329.
- 사용자 후속 승인 범위의 Studio 편집·저장을 구현했다. 오브젝트 종류별 기존 프리셋 선택과 그 프리셋의 공유 `step_db`·`hit_db`·`break_db`만 저장한다. 이름/음원/계열/물리·차단·HP·파괴 설정은 편집하지 않는다. 제작 수치나 음색의 최종 승인이 아니다.

## 변경

- `/objects`의 별도 「표면·소리 편집」에서 종류별 선택과 공유 프리셋 게인을 구분한다. 같은 프리셋을 쓰는 오브젝트·지면·맵 구역 수와 목록을 표시하고, 개별 맵 인스턴스 편집으로 오해하지 않도록 안내한다. 음원 ID는 조회 전용이다.
- 무충돌 장식(null)과 펫/드론/부사수는 조회 전용 안내를 표시한다. 이 항목으로 이동하거나 환경 소품으로 돌아와도 화면이 동작한다. 항목 이동 중 실제 편집 초안은 유지하고, 최신 값 다시 읽기는 초안을 취소한다.
- `GET/POST /api/surfaces`는 고정된 `game/content/data/surface_profiles.json`만 읽고 쓴다. 기존 loopback Host/Origin 제한을 통과해야 한다. 요청 키를 정확히 제한하고 기존 object/profile ID와 유한 게인 -24..+6을 검증한다. 새 ID·null 장식·동료·임의 경로·물리 키는 저장할 수 없다.
- 파일 전체 SHA-256 버전으로 오래된 편집을409로 거부하고, 한 서버 안의 동시 저장을 직렬화한다. 같은 디렉터리의 임시 파일을 완전히 쓰고 flush한 뒤 원자적 rename으로 교체한다. 교체 직전에 파일 버전을 다시 확인한다. `ground`, `layouts`, 미지원 actor 행과 알 수 없는 메타데이터를 포함한 나머지 JSON 값을 보존한다(들여쓰기/줄바꿈은 정규화).
- 저장 성공 후 실제 파일을 다시 읽고 Studio 속성을 갱신한다. `StudioLibrary.Library()`는 `SurfaceProfiles.Reload()` 후 환경 목록을 재구성하므로 프로세스 안의 이전 캐시가 남지 않는다. 파일 저장 성공 후 목록 갱신 실패도 구별해서 안내한다.
- 게임은 기존 세션 데이터를 유지한다. 실행 중인 게임은 재시작해야 하며, 멀티에서는 양쪽 PC에 같은 데이터를 적용해야 한다고 화면에 표시한다. 기존 네트워크 세션의 hot reload를 제공하지 않는다.

## 검사

| 검사 | 결과 |
|---|---|
| 새 API RED | 구현 전 GET `/api/surfaces` 404로 실패 확인. Node24가 실패 종료 중 `UV_HANDLE_CLOSING` assertion을 추가 출력했으며 제품 서버 오류로 취급하지 않음 |
| `STUDIO_URL=http://127.0.0.1:4329 node studio/tests/surface-editor.mjs` | **37 통과**. 실제 저장·파일 재조회·Godot 캐시 재조회, 경계값 -24/+6와 소수, 범위 밖/문자/null/Infinity 거부, 알 수 없는 키/ID/임의 경로, null 장식과 등록된 actor 거부, Host/Origin/Content-Type/크기/JSON 검증, 두 탭 동시 저장200/409, 외부 편집 후409, 미지원 행·메타데이터·ground/layouts 보존, HP50/차단/엄폐 불변, finally에서 원본 바이트와 renderer 캐시 복원. `studio/output/surface-editor-test.log` |
| 기존 `surfaces.mjs` | **93 통과**, 실제 Godot 렌더3장. 프리셋/게인 표시 및 모래주머니 HP50 유지 |
| Studio 기본 명령 `npm --prefix studio run check` | **36 통과**, 실제 캐릭터 렌더13장, 초기화 픽셀 비교·게임/사용자 파일 불변 |
| 기존 `interactions.mjs` | **225 통과**, 실제 소품 렌더2장·픽셀 검사. 상호작용 JSON 불변 |
| 기존 `library.mjs --all` | **425 통과**, 몬스터/오브젝트 **116 렌더·픽셀 검사**. `studio/output/surface-editor-library.log` |
| `SurfaceAudioCheck.tscn` 별도 headless 실행 | **242 통과**. 실제 걷기/사격/엄폐 소유체/일회성 파손 포함. `studio/output/surface-editor-runtime.log` |
| Debug / ExportRelease `dotnet build` | 각각 오류0·경고0. `studio/output/surface-editor-build.log`, `surface-editor-export-build.log` |
| 브라우저 실제 조작 | 프리셋 철판·명중 -4 저장 → 같은 페이지 속성 실제 갱신·HP50 유지. 외부 편집 뒤409 안내와 입력 -2 보존 → 최신 값 읽기 후 원본 sandbag/0 복원. null 조명 및 드론 항목의 폼 비노출·조회 정상. 브라우저 warn/error 없음 |
| 종료·파일 검사 | renderer/runtime 로그에 ERROR/WARNING/leaked 없음. `git diff --check` 통과. 원본 surface/interaction JSON 변경 없음. 포트4329 서버와 자식 renderer 종료, 다른 작업 서버는 조작하지 않음 |

검사 보완: fetch의 Host 헤더가 실제 전송에서 정규화되어 위조 Host 검사가200을 받는 실패를 관찰했다. 제품 Host 검증을 완화하지 않고 `node:http.request`로 실제 위조 헤더를 보내403을 검증했다. 브라우저에서 단순 방문 항목이 저장 후 낡은 초안으로 표시되는 것을 재현했고, 실제 변경 없는 초안은 새 버전으로 갱신하도록 수정한 뒤 같은 조작으로 상태 메시지 없음·저장 비활성을 확인했다.

## 렌더와 남은 제약

- `studio/output/surface-editor-saved.png`: 테스트 fixture로 철판/-4 저장 완료, 공유23종·맵6구역 영향, HP50을 같은 전체 화면에서 확인.
- `studio/output/surface-editor-conflict.png`: 낡은 편집의 저장 거부와 입력 보존.
- `studio/output/surface-editor-restored.png`: 원본 sandbag·게인0 복원 및 실제 소품 렌더. 이 산출물은 로컬 재실행용이며 커밋하지 않는다.
- 웹/게임 테스트가 바꾼 fixture는 원본 바이트로 되돌렸다. 실제 청음·오디오 장치 캡처·장시간 사용·좁은 화면·다른 PC·게임 패키지 전체 export는 이 작업에서 수행하지 않았다. `Integration`은 이 작업에서 실행하지 않았으며 전용 검사 결과와 구분한다.
- optimistic version 검사이며 외부 편집기에 잠금을 강제하지 않는다. 외부 프로세스가 최종 버전 검사와 rename 사이의 매우 짧은 구간에 쓰는 경우까지 협업 트랜잭션으로 보장하지 않는다. Studio 외부 파일 편집과 저장은 동시에 하지 않는다.
- 새 제품 버그/결정을 발급할 근거는 없었다. 기존 A-26의 승인된 후속 편집 범위이며 I-64의 청음·전체3D 믹스 한계나 C-29의 게임 규칙을 바꾸지 않는다. 중앙 규격/색인 갱신은 통합 담당 작업에 포함한다.
