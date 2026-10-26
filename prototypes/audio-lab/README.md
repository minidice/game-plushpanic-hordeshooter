# 사운드 비교 실험실

2026-10-27. 무료 CC0 소재로 만든 피격·좀비 신음·좀비 사망 각 A/B **비교 시제품**. 현재 게임에 자동 적용되지 않는다. 음색의 최종 청음 승인은 아직이며 거위 이외 플레이어 종의 발성은 후속이다.

## 열기

생성 후 `output/index.html`을 브라우저에서 연다. 위쪽은 기존/A/B 원음을 종류별 같은 50 ms RMS로 맞춘 비교, 아래쪽은 현재 게임 믹스 녹음이다. 기기 볼륨을 낮게 시작한다. 자동 재생하지 않으며 새 항목을 재생하면 다른 항목은 멈춘다.

## 재현 (저장소 루트, Windows PowerShell)

```powershell
python -m pip install --target prototypes/audio-lab/output/vendor -r prototypes/audio-lab/requirements.txt
dotnet build game/HordeShooter.csproj --nologo -v q
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --headless --editor --import --path game --quit
& 'D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe' --path game --resolution 800x450 res://tests/AudioPilotCapture.tscn
python prototypes/audio-lab/tools/build.py
python prototypes/audio-lab/tools/check.py
```

Godot 경로는 PC별 설치 위치로 바꾼다. 캡처는 실제 오디오 장치가 필요하며 headless로 실행하면 무음 검사가 실패한다. `AudioPilotCapture`는 실제 Stage/Sfx/리스너를 쓰지만, 자동 통제 장면이다. 게임 한 판의 일반 플레이 녹화로 표현하지 않는다. 배경음은 끄고, Master 캡처 **뒤**에 -80 dB 모니터 감쇠를 붙여 캡처 중 스피커 소리를 줄인다. 이 효과는 저장하지 않고 프로세스 종료와 함께 사라진다.

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
- Kenney **Impact Sounds**: soft impact 2개와 배포본 License.txt 보존.
- `recipes.json`은 절취 위치·피치/길이 비율·필터·레이어 상대 음량을 기록한다. 후보는 mono PCM16 48 kHz. 변환으로 손실 원본의 품질이 복원되지 않는다.
- 후보 최종 게인은 50 ms RMS -18 dBFS와 sample peak -6 dBFS 중 더 낮은 쪽으로 맞춘 시제품 값. 같은 종류의 기존/A/B는 공통 RMS로 추가 감쇠하며, 음색 비교를 위해 개별 리미터를 사용하지 않는다.
- 소스 SHA-256이 달라지면 빌드 실패. 후보·비교 WAV·보고서 생성은 오프라인이며 API 호출·과금이 없다. `output/vendor`의 FFmpeg는 개발용이고 게임에 배포하지 않는다.

## 게임 적용 전 남은 작업

사용자 청취 선택 → `honk` 피격/무기/카드 공유 분리 → 변형 재생 → 근거리 상한·군중 믹스(I-64) → 나머지 6종 제작 → 결과음의 볼륨 제어. 이 폴더의 후보를 기존 ID 파일에 무작정 복사하면 재사용처도 함께 바뀌므로 직접 설치하지 않는다.

[설계·작업 범위](../../doc/features/effects-audio/sound-quality-pilot.md)
