# 001 · code · 포유류 무장 팔 비율과 최신 main 통합

- 날짜: 2026-10-26. 관련 결정 A-4, 이슈 I-35. 새 ID 발급 없음.
- 요청: 무장 시 길어지는 팔 개선 후 커밋·최신 main 병합·원격 main 푸시.
- 구현 커밋: `15bf1ec`. 원본 팔 비율 제한, 공통 어깨/몸 자세, 무기 길이·왼손 위치 조정, 조준/구르기/밀치기 파지 보완. [상세 계약·제작 실패·렌더·기존 검사](../../../shared-weapon-poses.md#2026-10-26-짧은-팔-비율-유지-후속).
- 최신 main 기준: `5923b3a`. Studio 공유팩/문서 메뉴/기능별 기록과 중앙 AssetPaths 변경을 자동 병합했다. `CharacterLionPaw`는 main의 `character.mammal_arm_segments` 경로 조회와 새 팔 계산을 함께 유지한다.
- 기록: 이번 새 A-4/I-35 후속 본문을 기능별 decisions/issues로 연결하고 옛 이력은 보존했다. 전투 수치/팔 고정길이의 전용 동작 예외 등 기존 결정은 추가 변경하지 않는다.
- 커밋 전 재검증: Debug 경고0·오류0, Compact11,016 / Shared1,118 / Reload279 / Integration149 통과. 로그 `game/output/compact-merge/pre-*.log`. 검사와 Git 등록 프로세스 종료가 일시 지연됐지만 모든 프로세스의 정상 종료 코드0을 확인했다.
- 남은 제약: 재장전/횡단 신장, 확대 관절선, 독립 가슴/머리 조준·발 IK, 모든 장구류/장시간/실제2PC/성능 검증 및 최종 미술 수락. 이번 통합은 새 모델 제작이나 전체 기능 완성을 뜻하지 않는다.

## 병합 결과 검증

- Debug / ExportRelease 빌드: 오류0·경고0.
- CompactWeaponPose11,016, SharedWeaponPose1,118, WeaponGrip187, ReloadPose279, CompanionModel253, AssetPaths21, Integration149 통과. 로그 `game/output/compact-merge/merged-*.log`.
- AssetPaths의 `no.such.key` 오류1회는 누락 키 처리 검사의 의도된 출력이다. 나머지 실행 실패/종료 경고 없음.
- 실제 창 모드 MammalArmStage539 통과: M4/MP5/AK47 AI 사격, 실제 총구의 탄환 출발, 확대/재장전 렌더 재검증. `game/output/mammal-arms/stage2-*.png` 중 M4 확대를 직접 확인했다.
- Studio 서버/새 검사 스크립트 JavaScript 구문, 기능 기록 상대 링크, `git diff --check` 통과. 이번 병합 검수에서는 Studio22장 전체를 다시 생성하지 않았으며 이전 구현 검수 결과와 구분한다.
- main의 미추적 `.codex-remote-attachments/`는 커밋 대상에서 제외했다. 렌더/로그 보존을 위해 기존 작업 폴더는 유지한다.
