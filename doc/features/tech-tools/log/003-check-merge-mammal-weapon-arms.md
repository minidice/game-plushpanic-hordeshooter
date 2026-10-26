# 003 · check · `codex/mammal-weapon-arms` 병합 검증

관련: [작업 방식](../../../work-process.md), [공통 총기 자세](../../../shared-weapon-poses.md), [포유류 팔](../../../mammal-weapon-arms.md)

## 상황

- 2026-10-26, 열린 브랜치 4개를 점검했다.
  - `codex/duck-clean-art` · `goose-clean-art` · `tiger-clean-art`: 이미 main에 들어가 있었다(`b7aecd1` · `0bbbb0a` · `89e1797` · `f4f4fe4`, 뒤이어 `0c71e31` 4종 통합). 병합하지 않는다. 다듬기 전 옛 사본이다.
  - `codex/mammal-weapon-arms`(`93802c8`): 하나만 실제로 안 합쳐져 있었다. 54개 파일이고, Studio 서버 · 화면과 기록 문서를 포함한다.
- 같은 파일을 고칠 Studio · 기록 작업보다 먼저 병합했다(사용자 요청).

## 방법

1. 임시 worktree에서 main에 병합했다. 공유 폴더의 브랜치는 바꾸지 않았다.
2. 충돌은 1곳이었다. `feature-checklist.md` 끝에 양쪽이 덧붙인 확인 기록이 겹쳤고, 둘 다 남겼다.
3. 새 `AssetPathsCheck`가 브랜치의 직접 경로 1곳(`CharacterLionPaw.cs`의 `mammal-arm-segments.glb`)을 잡았다. 이 경로를 `content/data/asset_paths.json`의 `character.mammal_arm_segments`로 옮겼다(`2cd78f3`).
4. 검증을 마친 뒤 main을 앞으로 당기고(fast-forward) 푸시했다(`6f236f8` 병합, `2cd78f3` 경로).

## 검사 (임시 worktree, 모두 통과 · 종료 경고 0)

- 빌드 경고 · 오류 0. 가져오기 오류 0.
- MammalArm 1080, MammalArmStage 530, SharedWeaponPose 1118, LionLimb 4536
- Raccoon · Redpanda clean 각 7615, Duck clean 55, Goose clean 57
- ReloadPose 279, WeaponGrip 187, GroundedArt 212, WeaponAim 32, WeaponHandling 36, Companion 39, AssetPaths 21, Integration 149
- Studio(4334 포트): mammal-arms 51, shared-poses 133, smoke 36, weapons 52

## 남은 것

- Codex 작업 폴더 4개와 브랜치는 그대로 두었다. 지우는 일이라 사용자 확인 뒤 정리한다.
- 처음 검사 목록에 `PlayerMammalCleanCheck`를 넣었는데, 그런 장면은 없었다(종별 장면의 공통 부모). 종별 장면으로 다시 돌렸다.
