# game-plushpanic-hordeshooter

Isometric Co-op PvE Horde Shooter — 귀엽고 우스꽝스러운 동물 캐릭터로 진행하는 쿼터뷰 호드 슈터 프로토타입.

현재는 Godot **4.7.2 .NET (C#) + Jolt** 기반의 Stage 1 싱글 테스트다. 협동 멀티플레이·네임드 시체 루팅은 후속 개발 항목이다.

## 실행

Windows에서 Godot 4.7.2 .NET과 .NET SDK를 준비한 뒤 `game/Start-Game.cmd`를 실행한다. PC별 설치 경로는 사용자 환경변수 `GODOT_DIR`로 지정한다([설정 방법](game/README.md#pc별-godot-경로)). 한 번만 다른 경로로 실행하려면 `game/Start-Game.ps1 -GodotDir '설치 경로'`를 사용한다.

실행·조작·검사 방법: [게임 README](game/README.md).

`game/nuget.config`는 `%GODOT_DIR%/GodotSharp/Tools/nupkgs`에서 로컬 패키지를 읽는다. PC마다 `GODOT_DIR`만 설정하면 공통 설정 파일을 수정할 필요가 없다. `Start-Game.ps1`과 `Export-Game.ps1`은 선택한 설치 경로를 빌드 프로세스에도 전달한다. 배포에는 같은 버전의 .NET Export Templates도 설치해야 한다([배포 환경 안내](game/README.md#pc별-godot-경로)). `dotnet build`를 직접 실행하거나 에디터에서 빌드할 때는 해당 프로세스에 `GODOT_DIR`이 설정되어 있어야 한다. C 드라이브 설치 PC에서 패키지 복원·빌드 경고 0개, 오류 0개를 확인했다([기록](doc/features/tech-tools/log/011-code-nuget-godot-dir.md)).

## 구조와 기록

- `game/`: 게임 소스, 설정 데이터, 실행용 에셋, 검사 장면.
- `prototypes/character-lab/`: 캐릭터 비교/제작 도구와 Blender·GLB 생성 스크립트. [사용법](prototypes/character-lab/README.md).
- [기능 체크리스트](doc/feature-checklist.md): 구현 현황.
- [용어 사전](doc/glossary.md): 모드·캠페인·스테이지·구역의 구분과 2단계/3단계 비교.
- [시나리오 레벨 디자인 목표](doc/scenario-level-design.md): 시간·전투 흐름·아이템 배치 추천 초안. **거점·난전(구 생존·서바이버)에는 적용하지 않음.** 기존 확정 기준은 [레벨 디자인 규칙](doc/level-design-rules.md).
- [플레이어 동물 가이드](doc/player-species-guide.md), [몬스터 가이드](doc/monster-design-guide.md): 종별 특징·적 역할·새 모델 추가 양식. 기술 계약은 [모델 규격](doc/character-model-spec.md).
- [멀티 2인 동기화](doc/multiplayer-sync-design.md): 누가 결정하고 무엇을 보내는지, 검사 방법, 구현 결과(MVP, 명령줄 실행).
- [결정 기록](doc/decisions.md), [이슈 기록](doc/issues.md): 결정 이유와 버그·제약·재발 방지 이력.
- [작업 규칙](AGENTS.md): 기능 추가·버그 수정 때 함께 갱신할 문서와 검증 기준.

캐시·설치된 의존성·자동 생성 `output/` 폴더는 저장소에 포함하지 않는다. 문서의 출력 이미지·로그 경로는 해당 검사/렌더 장면을 실행하면 생성된다.
