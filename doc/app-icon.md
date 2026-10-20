# 실행파일 아이콘

기준일: 2026-10-21. 현재 선택은 **놀라서 부리를 벌린 거위**다. 사용자가 향후 주인공을 라쿤으로 바꿀 수 있다고 했으므로, 거위를 최종 주인공이나 영구 브랜드로 확정하지 않는다.

## 원본과 현재 적용

- [시안 폴더](../design/icon-concepts/2026-10-21/): `01-raccoon.png`, `02-goose.png`, `03-duo.png` 세 원본 보존. `index.html`을 브라우저로 열면 16·24·32·48·64·128 CSS 픽셀 비교를 볼 수 있다.
- 시안은 내장 ImageGen으로 생성했다. 공통 방향은 귀엽고 우스꽝스러운 놀란 표정, 크게 뜬 눈과 벌어진 입, 단순한 색 영역, 어두운 파란 배경, 글자 없음이다. 실제 게임 모델의 캡처가 아니다.
- 현재 원본은 `02-goose.png`. 구도·배경을 그대로 두고 크기와 파일 형식만 변환한다.
- 게임 리소스: `game/assets/branding/app-icon.png`(256×256), `app-icon.ico`(16·24·32·48·64·128·256 각 크기, 32비트).
- `game/project.godot`: `application/config/icon`은 PNG, `application/config/windows_native_icon`은 ICO.
- `game/export_presets.cfg`: `application/icon`과 `application/console_wrapper_icon` 모두 같은 ICO.
- 원본 시안은 Godot 프로젝트(`game/`) 밖에 보관하므로 게임 패키지에 포함하지 않는다. 플레이어 선택·매칭·네트워크 동작과 연결하지 않는다.

## 재생성과 교체

Windows 저장소 루트에서 실행한다. PowerShell과 Windows의 System.Drawing을 사용하며 별도 이미지 라이브러리는 필요 없다.

```powershell
.\tools\Build-AppIcon.ps1
# 향후 라쿤을 선택했을 때:
.\tools\Build-AppIcon.ps1 -Source .\design\icon-concepts\2026-10-21\01-raccoon.png
.\game\Export-Game.ps1
```

출력 이름을 유지하므로 종을 교체해도 설정 경로는 그대로다. 교체 후 이 문서와 decisions A-16, issues의 아이콘 검토 기록을 갱신한다.

검증은 ICO 내부의 7개 크기 확인, Godot 가져오기, Windows 내보내기 및 실행파일에 내장된 아이콘 확인을 포함한다. 축소본 실제 렌더에서 눈·부리의 구분도 확인한다. 설정 변경만으로 기존 배포된 EXE가 바뀌지는 않으므로 다시 내보내야 한다.

## 검증 기록

- 격리된 작업 공간에서 `dotnet build game/HordeShooter.csproj --nologo -v q`: 경고 0, 오류 0.
- `game/Export-Game.ps1 -NoZip`: Godot 가져오기·Windows 릴리스 내보내기 성공, 로그의 오류/경고 없음, .NET 데이터 폴더 존재 확인.
- 독립적인 ICO 디코더(Pillow)로 입력 ICO 7개 크기와 PNG 256×256을 확인했다.
- PE 리소스 검사(pefile)로 `HordeShooter.exe` 내장 아이콘을 추출해 각 크기의 픽셀이 입력 ICO와 일치함을 확인했다. 추출한 256px PNG 실제 렌더도 확인했다(`game/output/exported-app-icon.png`, 로컬 검사 산출물).
- **엔진 제약**: 입력 ICO에는 24px도 있지만 이번 Godot EXE에는 16·32·48·64·128·256px 6개만 포함된다. 최초에 7개를 기대한 검사는 실패했으며, 리소스 검사와 [Godot 크기 필터링 이슈](https://github.com/godotengine/godot/issues/84654)를 대조해 입력/출력 기대값을 구분했다. 추가 해상도를 넣기 위한 EXE 후처리는 하지 않는다.
- 콘솔 래퍼는 기존 `debug/export_console_wrapper=1` 설정상 릴리스에 생성되지 않는다. 경로는 연결했지만 디버그 래퍼 내보내기는 미검증이다.
- 원본은 배경이 포함된 사각형이다. OS 배율별 작업표시줄·탐색기 캐시와 다른 PC 표시, 실제 게임 실행은 이번에 검사하지 않았다. 이미 배포된 실행파일·압축은 자동 변경하지 않는다.

설정 참고: [Godot 4.7 Windows 아이콘 문서](https://docs.godotengine.org/en/4.7/tutorials/export/changing_application_icon_for_windows.html).
