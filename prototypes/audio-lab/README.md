# 사운드 비교 실험실

2026-10-27. 3종 A/B 시제품에 이어 **9종+발성 변형3개를 현재 작업 브랜치의 게임에 적용**했다. 사용자의 계속 진행 지시에 따라 A를 기본/B를 변형으로 선택했다. 최종 사용자 청음 취향 확정과 거위 이외 종별 발성은 후속이다.

## 열기

생성 후 `output/index.html`을 브라우저에서 연다. 위쪽은 9종 이전 합성음/적용본/변형을 같은 50 ms RMS로 맞춘 비교, 아래쪽은 적용 후 실제 게임 믹스20조건이다. 자동 재생하지 않으며 새 항목 재생 시 다른 항목은 멈춘다. 첫 시제품과 변경 전 캡처가 남아 있으면 `output/pilot.html`로 보존한다.

## 재현 (저장소 루트, Windows PowerShell)

```powershell
python -m pip install --target prototypes/audio-lab/output/vendor -r prototypes/audio-lab/requirements.txt
dotnet build game/HordeShooter.csproj --nologo -v q
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path game --quit
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --path game res://tests/Integration.tscn
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --path game --resolution 800x450 res://tests/AudioQualityCapture.tscn
python prototypes/audio-lab/tools/release.py --page
```

`release.py`는 매번 원본에서 배포12파일을 재생성하고 설치본/credits 해시·포맷·피크·DC·끝점·import 설정을 검증한다. 레시피 수정 후 실제 파일을 갱신할 때만 `--install`을 붙인다. 배포 `.import`도 버전 관리한다. 비교 기준은 `output/before`에 보존하며, 없으면 Integration이 내보낸 유지 중인 구형 합성 레시피 WAV를 복사한다. 이것을 변경 전 실제 믹스 녹음으로 표현하지 않는다.

`AudioQualityCheck.tscn`, `AudioLevelCheck.tscn`, 창 모드 `AudioMixCheck.tscn`도 별도 실행한다. Godot 실행은 한 번에 하나씩 끝낸다.

5분 반복 전투 확인은 창 모드 `res://tests/AudioCombatSoak.tscn`을 `--max-fps 60`으로 실행한다. `game/output/audio-combat-soak/`에 최종 출력 `combat.wav`, 발생 횟수/피크/프레임 `report.json`, 60초·240초 렌더를 저장한다. 이동/사격을 자동 조작하고 무적·체력 회복·탄약 보충·적 교체를 쓰므로 사람의 한 판 플레이나 최종 청음 평가가 아니다. 앰비언스를 포함하며 실제 플레이어 상태이상으로 헬륨·먹먹함을 적용한다.

Godot 경로는 PC별 설치 위치로 바꾼다. 캡처는 실제 오디오 장치가 필요하며 headless 무음은 실패다. 실제 Stage/Sfx/리스너를 사용한 자동 통제 장면이고, 일반 플레이 한 판 녹화는 아니다. 배경음은 끄고 Master 캡처 **뒤**에 -80 dB 모니터 감쇠를 붙인다. 출력 폴더 `game/output/audio-quality`의 WAV는 최종 limiter를 지난 실제 출력이며 추가 정규화 없이 비교 페이지의 `output/release-mix`에 복사한다. 설정은 저장하지 않는다.

### 이전 시제품 기록

첫 시제품 커밋 `520e55a`의 아래 결과는 변경 전 자료다. 현재 코드에서 `AudioPilotCapture`를 재실행하면 현재 적용본이 녹음된다. 이전 상태를 다시 얻으려면 해당 커밋의 별도 체크아웃에서 캡처해야 한다. `build.py --baseline <해당 캡처 폴더>`와 `check.py`는 시제품 제작·검증용으로 유지한다.

`game/output/audio-pilot/`:
- `honk.wav`, `groan.wav`, `zombie_die.wav`, `shot_rifle.wav`: 해당 실행의 게임 로더가 선택한 원음.
- 거리/군중 WAV: 현재 믹스를 PCM16으로 내보낸 진단 파일. 0 dBFS를 초과한 부분은 PCM 변환에서 잘린다.
- 같은 이름 `.f32le`: 잘리기 전 스테레오 float32 little-endian 원본. sample rate/frames는 `capture.json`.
- 실험실 `output/capture-listen/*.wav`: 같은 float 믹스에서 전부 **같은 게인으로 최소 12 dB 감쇠**한 청취본. 배치 최고 피크에 3 dB 여유가 생기도록 공통 감쇠를 계산하고 `report.json`에 기록한다. 비교 페이지에는 이것만 사용. 원음 비교와 믹스 청취는 음량 기준이 다르다.
- `capture.json`: 기록 시각, 피크, RMS, 0 dBFS 이상 샘플 수. 캡처 성공은 믹스의 클리핑 검사를 통과했다는 뜻이 아니다.

## 제작과 라이선스

- [sources.json](sources.json)에 출처 페이지·다운로드 URL·제작자·CC0 링크·각 원본 SHA-256을 보존했다. `source/` 원본은 수정하지 않는다.
- Navadaux의 **Geese Honking**: 실제 거위 현장 녹음. 로그인 없이 제공되는 **저품질 공개 MP3 미리듣기**를 사용했다. 원본 WAV나 고품질 MP3를 받았다고 표현하지 않는다. 배경 개체와 압축의 한계가 있어 향후 더 깨끗한 녹음으로 대체할 수 있다.
- artisticdude의 **Zombies Sound Pack**: 선택한 원본은 44.1 kHz stereo PCM24. 제작 페이지가 녹음·가공 과정을 구체적으로 밝히지 않으므로 무가공 인간 녹음이라고 단정하지 않는다.
- Kenney **Impact Sounds**: soft impact 2개·wood impact1개와 배포본 License.txt 보존.
- davidlay1 **Air release**: 쉐이빙 크림 캔에서 녹음하고 공기 압력음으로 편집한 소재의 공개 HQ MP3 미리듣기. 실제 풍선 녹음이나 원본 WAV라고 표현하지 않는다. 풍선 팽창/파열에 재편집했다.
- `game_over`와 `stage_clear`: 외부 음악 샘플 없이 직접 만든 악구. 감쇠하는 사인파 배음 악기로 구성하며 톱니/사각파·드라이브를 사용하지 않는다.
- `recipes.json`은 절취 위치·피치/길이 비율·필터·레이어 상대 음량을 기록한다. 후보는 mono PCM16 48 kHz. 변환으로 손실 원본의 품질이 복원되지 않는다.
- 후보 최종 게인은 50 ms RMS -18 dBFS와 sample peak -6 dBFS 중 더 낮은 쪽으로 맞춘 시제품 값. 같은 종류의 기존/A/B는 공통 RMS로 추가 감쇠하며, 음색 비교를 위해 개별 리미터를 사용하지 않는다.
- 소스 SHA-256이 달라지면 빌드 실패. 후보·비교 WAV·보고서 생성은 오프라인이며 API 호출·과금이 없다. `output/vendor`의 FFmpeg는 개발용이고 게임에 배포하지 않는다.

## 적용 내용과 남은 작업

`honk`/`honk_ability`를 분리하고, 3종은 연속 같은 변형 없이 재생한다. 대상 3D 효과의 근거리 상한·군중 제한·최종 limiter와 결과음 `Cues`의 효과음 볼륨 제어를 적용했다. I-64 중 나머지 3D 상대 밸런스, 사용자 최종 청음, 종별 발성과 장시간 멀티플레이 확인은 남았다. 무료 공개 MP3 소재의 압축/배경음 한계도 남는다.

[적용 계획·진행](../../doc/features/effects-audio/sound-quality-rollout-plan.md) · [변경·검증 기록](../../doc/features/effects-audio/log/002-art-sound-quality-rollout.md)
