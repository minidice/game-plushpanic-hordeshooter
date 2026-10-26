# 사운드 품질 1차 비교 시제품

2026-10-27. 사용자가 추천한 단계적 제작을 승인했고, 외부 생성 계정 대신 **무료 녹음 소재부터** 진행하도록 선택했다. 분위기 기준은 Escape from Duckov, 좀비는 Left 4 Dead다. 기준 게임의 파일을 추출하는 작업이 아니라 별도 라이선스 소재를 편집하는 작업이다.

## 목적과 범위

- 첫 납품: 피격 `honk`, 신음 `groan`, 사망 `zombie_die` 각각 A/B 후보와 기존 소리의 비교 청취 자료.
- 동물 발성은 거위 시제품부터. 모든 플레이어 종의 최종 발성을 정한 것으로 해석하지 않는다.
- 피격은 짧은 반응, 신음은 호흡과 목 울림, 사망은 짧은 발성과 둔탁한 낙하. 장난감 경적·뿅·삑은 줄인다.
- 원본·출처·라이선스·편집 수치와 측정 결과를 보존한다. 후보는 `prototypes/audio-lab/`에 두고 실제 게임의 `content/sounds`에는 아직 설치하지 않는다.
- 소리의 최종 선택은 비교 청취 후 정한다. 자동 피크 검사 통과를 청음 승인으로 기록하지 않는다.

## 접근 방식

1. 합성 코드만 개선: 재현성은 높지만 발성 질감의 한계가 있다.
2. 녹음 소재만 편집: 무료 CC0 소재로 이번 시제품 제작. 사용자가 선택한 첫 단계.
3. 녹음+AI 생성+편집: 전체 작업의 추천 방식. ElevenLabs 연결·비용을 준비한 뒤 필요할 때 확장. 이번에는 API 호출·과금·MCP 설치 없음.

## 제작·비교 계약

- 후보 각 2개, PCM16 mono 48 kHz. 48 kHz 변환은 원본에 없는 정보를 복원하지 않는다.
- 청취 비교는 소리 종류별로 기존/A/B의 가장 큰 50 ms RMS를 맞춘 별도 WAV. 파일 전체 RMS/LUFS 하나로 긴 무음과 짧은 충격을 동일화하지 않는다.
- 실제 믹스 녹음은 현재 Godot의 Sfx·리스너·버스를 사용한다. 단독/군중/사격과 겹침을 구분하고 원음 청취와 구분한다.
- 시제품 길이: 피격 0.15~0.35초, 신음 0.65~1.5초, 사망 0.25~0.65초. 측정 가능한 편집 시작값이지 사용자 확정 밸런스가 아니다.
- 피크 여유·DC·처음/끝 불연속·길이·무음·파일 해시를 점검한다. 원본 손실 압축과 현장 배경음의 한계는 표시한다.

## 후속 게임 적용

선택 후 `honk`의 피격/무기/카드 사용처 분리, 발성 변형 선택, 근거리 상한(I-64), 군중 동시 재생과 결과음 버스/볼륨을 작업한다. `honk_long`, `balloon_inflate`, `balloon_pop`, `bull_bonk`, `game_over`, `stage_clear`는 같은 기준으로 이어서 만든다. 이번 비교 시제품이 9종 전체 교체나 I-64 해결을 뜻하지 않는다.

## 근거

- [사운드 규격](../../sound-spec.md), [캐릭터 방향](../../character-art-direction.md), [기존 이슈 I-64·I-74](../../issues.md)
- [레시피](../../../game/scripts/Audio/SoundRecipes.cs), [재생](../../../game/scripts/Audio/Sfx.cs), [기존 믹스 검사](../../../game/tests/AudioMixCheck.cs)
- [ElevenLabs API](https://elevenlabs.io/docs/eleven-api/guides/cookbooks/sound-effects), [현행 MCP](https://elevenlabs.io/mcp), [FFmpeg 필터](https://ffmpeg.org/ffmpeg-filters.html)
