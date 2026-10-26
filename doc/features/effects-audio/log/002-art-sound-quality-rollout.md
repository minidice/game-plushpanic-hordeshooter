# 사운드 9종 게임 적용과 출력 제한

2026-10-27. 관련 A-26, I-64, 기존 피격 음량 계약 I-74. 사용자의 “일단 현재기준으로 나머지 작업”에 따라 시제품 이후 작업을 진행했다. 기준 커밋 520e55a, 작업 브랜치 codex/sound-quality-pilot.

## 변경

- 무료 CC0 원본9개를 바탕으로 7종 효과음 편집, 결과음2종은 직접 만든 감쇠 배음 악구. 9종+발성 변형3개를 `game/content/sounds`에 PCM16 mono48kHz로 배포했다. 모든 배포본 sample peak≤-6 dBFS. `.import` 무압축/정규화 없음과 UID를 함께 관리한다.
- 피격·신음·사망은 A/B 두 변형이며 단발 재생에서 연속 같은 파일을 선택하지 않는다. 카드4호출과 꽥 무기 타입의 소리만 `honk_ability`로 분리했다. 무기/카드 식별자·소음 반경은 바꾸지 않았다. Studio/Stream 조회는 대표 파일이다.
- honk_long/groan/zombie_hit/zombie_die/balloon_inflate/balloon_pop/bull_bonk에도 NearCap을 적용했다. 신음/사망은 최대3개이며 초과를 생략한다. 피격 피치 흔들림을 줄이고 새 음원 레벨에 맞춰 게인을 조정했다.
- 결과음·UI·귀울림은 `Cues`에서 SfxVolume/음소거를 따르며 헬륨/섬광 상태효과를 우회한다. Master HardLimiter는 ceiling -1 dB, pre-gain0, release0.1초이며 초기화를 반복해도 하나만 설치한다.
- 제작 레시피/원본 해시/배포 해시/CC0 출처를 보존했다. `release.py`는 오프라인 재생성·설치본 일치·출처·형식·끝점·피크·DC·import·비교 RMS·페이지 링크를 확인한다. 유료 API·MCP·앱 설치 없음.

## 실행 검사

저장소 루트에서 실행. Godot 경로는 `D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe`, Python은 `D:/Util/Python314/python.exe`.

| 검사 | 결과 |
|---|---|
| `dotnet build game/HordeShooter.csproj --nologo -v q` | 통과, 오류0·경고0 |
| Godot `--headless --editor --import --path game --quit` | 통과 |
| `AudioQualityCheck.tscn` headless | 30/30 통과. 소스 로딩/변형/상한/군중 제한/무기 ID/버스·볼륨/limiter 중복 |
| `AudioLevelCheck.tscn` headless | 4/4 통과. rifle -4.6, groan -16.1, step -20.9, zombie_step -24.1 dB(최대50ms RMS+레시피) |
| `AudioMixCheck.tscn` 창 모드 | 6/6 통과. SFX limiter 이전 peak: 걷기 -19.5, 휘두르기 -4.8, 소총 +0.3, 피격 -12.5, 사이렌 -24.3 dBFS |
| `AudioQualityCapture.tscn` 창 모드 | 20/20 실신호·프레임·유한값·상한 통과, 0 dBFS 초과 샘플0 |
| `Integration.tscn` headless, fixed-fps 미사용 | 149/149 통과 |
| `python prototypes/audio-lab/tools/release.py --install --page`, 이후 `--page` | 12배포본·설치 해시 일치, 반복 생성 검증 통과 |
| `python prototypes/audio-lab/tools/check.py` | 원본9·기존 후보6·시제품 비교9·이전 캡처 청취본11·페이지 링크 통과 |
| 비교 페이지 브라우저 | 9종·이후 캡처20조건 렌더, game_over 적용본 재생 readyState4·오류 없음·1.65초 확인. 청음 품질 평가와는 구분 |

출력: `game/output/quality-*.log`, `game/output/audio-quality/capture.json`, `prototypes/audio-lab/output/release-report.json`, `output/index.html`, `output/rollout-preview.png`. 출력 폴더는 재생성 자료이며 커밋하지 않는다. 검증 장면 출력 이름은 공용 캡처 코드 때문에 `AUDIO_PILOT_CAPTURE_PASS 20`이다.

## 적용 후 Master 측정

| 조건 | peak dBFS | 초과 샘플 |
|---|---:|---:|
| honk 0.5 / 3 / 8m | -12.7 / -11.5 / -16.1 | 0 |
| groan 0.5 / 3 / 8m | -5.6 / -6.1 / -9.7 | 0 |
| zombie_die 0.5 / 3 / 8m | -14.6 / -14.8 / -18.9 | 0 |
| 군중 사망(요청6, 허용3) | -11.6 | 0 |
| 신음·사망·소총·피격 | -1.0 | 0 |
| 쓰러짐 / 풍선 팽창 / 파열 / 황소 충돌 | -12.7 / -9.0 / -11.7 / -12.0 | 0 |
| 게임 오버 / 클리어 | -13.6 / -14.8 | 0 |
| 헬륨 / 먹먹함 / 총·폭발·다종 군중 스트레스 | -1.0 / -1.0 / -1.0 | 0 |

변경 전 군중 사망 +10.3, 신음·사망·사격 +9.5 dBFS는 [001 기록](001-art-sound-quality-pilot.md)에 보존했다. 변형/피치가 무작위이므로 전후 수치는 파형 동일 조건의 음색 선호도 비교가 아니다. 이후 캡처는 다른 출력 폴더에 저장해 이전 녹음을 유지한다.

## 실패·경고와 남은 제약

- RED: 신규 AudioQualityCheck가 기존 코드에서 28개 중27개 실패했다. 구현 후 처음에는 Godot 기본 WAV 손실 압축으로 포맷9개가 실패했고, import를 무압축으로 수정한 뒤30개 통과했다.
- 제작 도구 첫 실행은 Windows cp949 기본 읽기로 JSON 오류. UTF-8을 명시한 후 재실행 통과했다. 실패를 최종 성공으로 숨기지 않는다.
- 최종 Godot/빌드 로그의 오류·종료 경고 없음. Git CRLF→LF 안내는 줄끝 변환 안내이며 검사 실패가 아니다.
- 최종 사용자 청음 수락, 실제 2PC/장시간 플레이, 모든 종의 별도 발성은 미검증/후속이다. 음색이 기준 게임과 동등한 품질이라는 주장은 하지 않는다.
- 거위 저품질 공개 MP3와 공기 HQ MP3 미리듣기의 압축/배경음 한계가 남는다. 공기 소재는 캔 녹음을 편집한 것으로 실제 풍선 녹음이 아니다. 결과음은 원본 녹음이 아닌 직접 합성 악구다.
- I-64는 대상 소리/최종 합산 초과를 부분 해결했다. 그 밖의 분대원 사격·폭발 등의 근거리 상대 밸런스와 true-peak/외부 장치 왜곡은 별도이며 전체 이슈는 열림을 유지한다.
- A-26 현재 상태와 바뀐 결정, I-64 본문/색인, 사운드 규격, 기능 체크리스트를 함께 갱신했다. 새로운 이슈·결정 ID를 발급할 필요는 없었다.

## 최종 리뷰

독립 읽기 전용 리뷰 완료. Critical/Important 없음, 미해결 Minor 없음. 리뷰어가 12개 설치 WAV의 해시/PCM/여유/끝점, 원본9개 해시·출처 연결, raw float 캡처20개를 별도 확인했다. 규격 첫머리의 과거 “음원 파일 없음” 설명과 합성 내보내기 안내를 현재 구현에 맞게 정정했고 리뷰어가 확인했다.

리뷰 범위 판단: Duckov/L4D 음색 동등성·최종 선호도는 자동 검사로 승인할 수 없어 사용자 청음으로 남긴다. 전체 3D 상대 밸런스는 I-64 부분 해결 범위를 유지한다. 종별 발성은 공통 거위 소리 한계를 공개한 후속 항목이다. true-peak/외부 장치와 장시간·2PC는 이번 신호/샘플피크 검증으로 확정하지 않는다. 잘못 판단하면 최종 음색·종별 일관성·특정 환경의 밸런스를 추가 수정해야 한다.

문서 경로·설치 import12개·새 스크립트 UID·최종 로그 검사, raw 캡처20개 프레임/유한값/피크 독립 재계산, `git diff --check` 통과. 사용자 기본 작업 폴더와 무관한 작업은 섞지 않고 현재 브랜치에 보존한다.
