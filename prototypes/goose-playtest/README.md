# 거위 게임 적용 테스트

Godot 4.7.2 .NET / C# + Jolt에서 실제로 조작하는 로컬 테스트 장면입니다. 브라우저 비교실과 별개이며, 기존 일체형 거위의 외형을 사용합니다.

## 실행

**`Start-Playtest.cmd`를 더블클릭**하면 C# 빌드·모델 가져오기 후 게임 창이 열립니다. 현재 PC의 Godot 도구 경로와 .NET SDK를 사용합니다. 첫 C# 의존성 복원에는 인터넷이 필요할 수 있습니다.

에디터에서 편집하려면 Godot .NET 버전으로 `project.godot`를 가져와 F6/F5로 실행하세요. 일반 Godot 버전은 C#을 실행하지 못합니다.

다른 PC에서는 Godot 4.7.2 .NET과 .NET 8 이상 SDK/해당 런타임을 준비한 뒤:

```powershell
./Start-Playtest.ps1 -Godot 'C:/Tools/Godot/Godot_console.exe'
```

## 조작

| 입력 | 동작 |
|---|---|
| WASD | 이동, 대각선 속도 보정 |
| 마우스 | 바닥 위치를 향해 조준 |
| 좌클릭 유지 | 연사, 반동, 탄도 표시 |
| H | 피해 25 + 밀림 |
| K | 즉시 사망 + 공중 회전 |
| R | 시작점에 체력 100으로 복구 |
| Tab | 기본 55도 작은 크기 / 확대 전환 |

주황 표적은 세 발을 맞으면 쓰러졌다가 복구됩니다. 엄폐물은 이동과 사격을 모두 막습니다. `HIT ZONE` 바닥에서는 초당 피해를 받아 실제 월드 상호작용으로도 피격과 사망을 확인할 수 있습니다. 창이 초점을 잃으면 이동·연사를 멈춥니다. 탄약과 재장전은 이번 테스트 범위에 포함하지 않았습니다.

## 실제 게임으로 옮길 파일

- `assets/goose.glb`: 거위 단독 게임용 모델. 약 1.05MB, 삼각형 18,107개, 메시 10개, 머티리얼 슬롯 49개.
- `scenes/Goose.tscn` + `scripts/Goose.cs`: CharacterBody3D 기반 조작 캐릭터. 재사용 시 같은 GLB 경로와 `TargetDummy` 타입도 함께 가져오거나 게임의 피해 시스템으로 연결합니다.
- `scripts/Field.cs`, `TargetDummy.cs`, `scenes/Field.tscn`: 카메라·월드·표적·UI·탄도 테스트 장면.
- `../character-lab/output/goose-game.blend`: 정리한 모델의 Blender 원본.

모델은 미터 단위, Y 위쪽, **+Z 전방**입니다. Godot의 통상적인 -Z 전방과 다르므로 `Goose.cs`에서 +Z 기준 회전을 사용합니다. `Muzzle` 노드는 총구 기준점입니다. 캡슐 반지름 0.48m, 전체 높이 2.18m, 중심 높이 1.09m입니다.

## 현재 가능한 것과 한계

이 프로젝트에서는 이동·조준·사격·피격·사망·재시작까지 플레이할 수 있습니다. 생존 중에는 `CharacterBody3D`, 사망 시에는 시각 모델을 단일 `RigidBody3D`로 옮기고 기존 충돌체를 비활성화합니다. 모델의 `goose_deform` 기준점을 눌러 착지 변형을 표현합니다.

본 리깅 없이 발·몸통·무기 기준점을 코드로 움직입니다. 부분적인 살/천 구김, 다관절 래그돌, 장비별 물리 분리, 네트워크 동기화, 적 AI, 캠페인, 저장 게임은 포함하지 않습니다. 4인 플레이용 모델의 첫 적용 테스트이며, 호드 전체에 같은 모델을 대량 배치하는 성능 검증은 하지 않았습니다. 메시 수는 줄였지만 재질 슬롯 49개와 LOD 부재는 추가 최적화 여지가 있습니다.

실제 렌더는 Forward+ / Vulkan에서 확인했습니다. 현 PC는 RTX 5070 Ti이며 다른 GPU 성능을 보장하는 벤치마크가 아닙니다. 테스트 맵은 캐릭터 기능 검증용입니다.

## 재생성·검증

```powershell
# character-lab 폴더에서
node tools/export-goose.mjs
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python tools/optimize_goose.py
npm test

# goose-playtest 폴더에서
./Start-Playtest.ps1 -Test
./Start-Playtest.ps1 -Capture
```

`output/integration-report.json`에 엔진 통합 검사 결과가 저장됩니다. `output/gameplay.png`, `output/close.png`는 Godot 렌더 결과입니다. 캡처 옵션은 자동으로 창을 닫습니다.

공식 문서: [C# 설정](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html), [Jolt](https://docs.godotengine.org/en/stable/tutorials/physics/using_jolt_physics.html), [CharacterBody3D](https://docs.godotengine.org/en/stable/classes/class_characterbody3d.html), [Godot 다운로드](https://godotengine.org/download/windows/).
