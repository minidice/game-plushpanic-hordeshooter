# 011 · code · NuGet 로컬 소스를 GODOT_DIR에 맞춤

- 작성일: 2026-10-29. 관련: I-46, [선행 작업 010](010-code-godot-dir-environment.md).
- 사용자 요청: `nuget.config`도 PC별 `GODOT_DIR`을 사용하고 현재 PC 빌드 확인, 문서 기록.
- 결정·이슈 검토: 기존 I-46의 해결로 기록한다. 새 결정·이슈 ID는 만들지 않음. 사용자가 선택한 환경변수 설정을 기존 NuGet 제약에도 적용하는 변경이다.

## 변경과 근거

- [game/nuget.config](../../../../game/nuget.config)의 `GodotLocal472`: D 경로 → `%GODOT_DIR%/GodotSharp/Tools/nupkgs`. NuGet의 [공식 환경변수 확장 문법](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file#using-environment-variables)을 사용한다. `nuget.org` 소스는 유지한다.
- [Start-Game.ps1](../../../../game/Start-Game.ps1): 경로 선택 순서는 유지한다. `dotnet build` 전에 선택한 `$GodotDir`을 프로세스의 `GODOT_DIR`에 넣어 NuGet과 엔진 경로가 일치하게 한다. `finally`에서 원래 환경변수와 작업 폴더를 복원한다. 따라서 `-GodotDir`으로 다른 설치를 고르거나 환경변수 없이 기존 기본값으로 실행해도 NuGet에 같은 폴더가 전달된다. Windows 사용자 환경변수 등록값은 수정하지 않는다.
- 루트·게임 README에 직접 빌드 및 에디터 빌드의 환경변수 조건을 안내했다. 기존 I-46 번호로 이슈 상태를 해결로 갱신하고 최초 제약 설명은 이력으로 보존했다. 선행 기록 010에도 현재 상태로 연결하는 후속 링크를 추가했다.

## 실행한 검사와 결과

1. 수정 전 소스 확인: C 경로의 `GODOT_DIR`이 있어도 `dotnet nuget list source --configfile game/nuget.config --format Detailed`는 D 경로를 출력했다. 환경변수를 사용하지 않는 원인 확인.
2. 수정 후 같은 명령: 소스가 `C:\Util\Godot\Godot_v4.7.2-stable_mono_win64\GodotSharp\Tools\nupkgs`로 확장됨. 통과.
3. 첫 샌드박스 빌드: 사용자 NuGet 캐시 `C:\Users\mini\.nuget\packages\godot.net.sdk\4.7.2` 생성 권한 부족으로 실패. 잘못된 패키지 경로·C# 컴파일 실패와 구분한다.
4. 실제 사용자 계정으로 재실행: 사용자 설정의 `GODOT_DIR`을 읽어 현재 빌드 프로세스에 적용한 뒤 저장소 루트에서 `dotnet build game/HordeShooter.csproj --nologo -v minimal` 실행. .NET SDK **10.0.400**, 프로젝트 대상 **net8.0**.
   - 복원: 완료(2.9초).
   - 빌드: **성공, 종료 코드 0, 경고 0개, 오류 0개**, 총 20.49초.
   - 산출물: `game/.godot/mono/temp/bin/Debug/HordeShooter.dll`(Git 제외).
5. 실행기 경로 전달·복원 검사: 실제 `Start-Game.ps1`을 C 설치 폴더의 명시적 `-GodotDir`로 호출하고, 빌드 명령 진입 시점에만 검사 함수로 중단했다. 사용자 입력값이 빌드 환경에 전달되고, 빌드 오류 경로에서도 기존 환경변수·작업 폴더가 복원됨을 확인. 원래 환경변수가 없던 경우의 복원도 통과. 실제 게임 창은 열지 않았다.
6. `git diff --check`: 통과.

## 남은 제약·미검증

- 사용자 환경변수 등록 후 이미 열린 터미널·Codex·에디터는 다시 열어야 한다. 직접 `dotnet build`에는 실행기의 D 경로 기본값이 적용되지 않으므로 `GODOT_DIR`이 필요하다. NuGet은 없는 환경변수를 기본 경로로 대체하지 않는다.
- D 드라이브 설치 PC의 재빌드는 이번에 실행하지 않았다. 직접 빌드를 사용하는 각 PC는 사용자 환경변수를 등록해야 한다.
- `Export-Game.ps1` 등 별도 실행기의 엔진 기본 경로는 수정하지 않았다.
- 게임 통합 검사·게임 창 실행·에셋 가져오기·렌더는 미실행. 이번 요청의 검증 범위는 패키지 소스 확장·복원·C# 빌드와 실행기의 빌드 환경 전달이다.
