# game-plushpanic-hordeshooter

Isometric Co-op PvE Horde Shooter — 귀엽고 우스꽝스러운 동물 캐릭터로 진행하는 쿼터뷰 호드 슈터 프로토타입.

현재는 Godot **4.7.2 .NET (C#) + Jolt** 기반의 Stage 1 싱글 테스트다. 협동 멀티플레이·네임드 시체 루팅은 후속 개발 항목이다.

## 실행

Windows에서 Godot 4.7.2 .NET과 .NET SDK를 준비한 뒤 `game/Start-Game.cmd`를 실행한다. 스크립트의 기본 Godot 경로와 다른 경우 `game/Start-Game.ps1 -GodotDir '설치 경로'`를 사용한다.

실행·조작·검사 방법: [게임 README](game/README.md).

`game/nuget.config`는 현재 개발 PC의 Godot 로컬 패키지 경로(`D:/Util/Godot/.../GodotSharp/Tools/nupkgs`)를 사용한다. Godot 설치 위치가 다르면 해당 소스도 실제 경로에 맞춰 수정한다. 다른 PC의 초기 설치는 아직 검증하지 않았다.

## 구조와 기록

- `game/`: 게임 소스, 설정 데이터, 실행용 에셋, 검사 장면.
- `prototypes/character-lab/`: 캐릭터 비교/제작 도구와 Blender·GLB 생성 스크립트. [사용법](prototypes/character-lab/README.md).
- [기능 체크리스트](doc/feature-checklist.md): 구현 현황.
- [플레이어 동물 가이드](doc/player-species-guide.md), [몬스터 가이드](doc/monster-design-guide.md): 종별 특징·적 역할·새 모델 추가 양식. 기술 계약은 [모델 규격](doc/character-model-spec.md).
- [결정 기록](doc/decisions.md), [이슈 기록](doc/issues.md): 결정 이유와 버그·제약·재발 방지 이력.
- [작업 규칙](AGENTS.md): 기능 추가·버그 수정 때 함께 갱신할 문서와 검증 기준.

캐시·설치된 의존성·자동 생성 `output/` 폴더는 저장소에 포함하지 않는다. 문서의 출력 이미지·로그 경로는 해당 검사/렌더 장면을 실행하면 생성된다.
