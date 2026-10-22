# Plush Panic 야외 모델 팩

**2026-10-22 게임에 통합됨**(decisions.md A-22): 모델은 `game/assets/outdoor-kit`(이 폴더의 `pack`과 같은 파일, `Copy-OutdoorKit.ps1 -Apply`로 복사), 배치는 시나리오 Stage 2(`game/scripts/World/StageTwoMap.cs`, L-14). 이 폴더는 원본 도구·Blender 원본·검수 장면·도감·문서의 보존본입니다. 아래 원래 안내의 "미적용" 표현과 `doc/`의 OUT-* 기록은 패키지 당시 상태입니다.

같은 계약의 전환 소품 4종(바닥 해치·벙커 해치·콘크리트 방벽·경고 표지)은 `tools/transition_props.py` → `game/assets/stage-props`, 원본 `source/transition-props.blend`(지시문 `doc/model-briefs/stage-transition-props.md`).

`tools/build_models.py`는 자기 폴더의 `pack/`에 씁니다. 다시 만든 뒤에는 `pack/models`와 `manifest.json`을 `game/assets/outdoor-kit`에 옮기고 `StageTransitionCheck`로 확인합니다.

- [이미지 도감](gallery/index.html): 더블클릭으로 오프라인 열기. 32종 검색·분류·목록·이미지 확대, 가림 상태, 시간대/날씨 샘플.
- [모델 목록 데이터](pack/manifest.json): GLB 경로, 실제 크기, 삼각형, 독립 가림 부위, 충돌 추천값.
- [Blender 조형 원본](source/outdoor-library.blend) / [재생성 도구](tools/build_models.py).
- [적용 안내](doc/integration-guide.md) / [결정 기록](doc/decisions.md) / [검증과 제약](doc/issues.md).

## 미리보기

Godot 4.7.2 Forward+에서 검수했습니다. C# 빌드나 원 게임 폴더가 필요 없습니다.

```powershell
pwsh -File ./Start-Preview.ps1
pwsh -File ./Start-Preview.ps1 -Validate
pwsh -File ./Start-Preview.ps1 -Capture
```

미리보기의 버튼으로 낮/석양/밤/젖음/적설을 선택하고, 체크박스로 지정한 나무와 컨테이너의 일부를 옅게 표시합니다. 수동 검수 기능이며 캐릭터 이동/자동 가림 판정은 없습니다.

## 제작과 검사

Blender 5.2.2 LTS, Python 3.14, Pillow 12.1.1에서 생성했습니다. 구조 검사에는 Python 표준 라이브러리만 필요합니다. 참고 영상 추출 도구만 별도 OpenCV가 필요하며 모델 재생성과 무관합니다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python tools/build_models.py
python tools/validate_pack.py
pwsh -File ./Start-Preview.ps1 -Capture
python tools/build_catalog.py
python tools/validate_delivery.py
```

패키지는 원본 모델/PBR 재질/제작 도구를 포함합니다. `.godot`, `output`, `.blend1`, `.import`는 로컬 재생성 산출물입니다. 갤러리의 WebP 이미지는 오프라인 열람용 배포 콘텐츠이며, PNG 원본은 캡처를 재실행하면 생성됩니다.

## 2차 조형 보완

겹잎 침엽수·활엽수를 개선하고 둥근 참나무/피나무, 직선/90도 굽이 하천을 추가했습니다. 도감에 이전/수정 비교와 물가 확대 이미지를 포함합니다. Godot의 Outdoor/Nature/Shore 버튼으로 검수 장면을 바꿀 수 있습니다. [보완 규격](doc/nature-revision-spec.md)과 [상세 생성 도구](tools/nature_shapes.py) 참조.
