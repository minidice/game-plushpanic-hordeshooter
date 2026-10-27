# 보스 부채꼴 산탄의 근거리 게인 회귀 보정

- 관련 A-26·I-64. 전체 NearCap 후속(008) 최종 리뷰에서 실제 보스 호출부의 추가 게인을 확인했다. 새로운 기능이나 독립 이슈 ID가 아닌 같은 근거리 상한 계약의 누락 보완이다.
- 원인: `BossEnemy.FanShot`은 `shot_shotgun`(+2 dB)에 +3 dB를 더했다. 이전 재생기 기본 MaxDb는 +3이었으나 NearCap 기본 적용 후 +5가 되어 보스 발사가 의도치 않게 커졌다.
- 수정: 해당 호출부만 +3→+1 dB. 산탄총 공통 레시피 +2, 보스 pitch .7와 jitter, 피해·투사체·발사 시점은 유지한다. 일반 플레이어 산탄총은 바뀌지 않는다. 기존 69조건 `AudioBalanceCapture.cs`도 수정하지 않았다.

## 검증

- 신규 `AudioBossGainCheck.tscn`은 실제 `BossEnemy.StartSkill(FanShot)`으로 준비 동작과 실행을 진행한 뒤 `Uses`와 실제 재생 중인 `shot_shotgun` voice를 확인한다. 테스트용 직접 사운드 재생으로 보스 호출을 대체하지 않는다.
- RED: 변경 전 실제 voice `gain=5.0 cap=5.0`, 상한 계약 실패(exit1). 예상한 실패이며 종료 경고는 없었다.
- GREEN: 호출 보정 후 실제 voice `gain=3.0 cap=3.0`, `AUDIO_BOSS_GAIN_PASS`(exit0). 한 번 실행, +3상한, 기존 낮은 피치 범위, 일반 산탄 +2를 확인했다.
- `dotnet build game/HordeShooter.csproj --nologo -v q`: 통과, 오류0/경고0. headless editor import 통과. `AudioBalanceCheck` 84/84 회귀 통과. 정상 검사 로그에 ERROR/WARNING/종료 경고 없음.
- 실제 출력 검증은 상위 작업에서 직렬 실행 예정이며 이 기록 시점에 실행하지 않았다. 헤드리스 재생기 값 검사는 실제 믹스/청음 통과가 아니다.

## 실제 장치 짧은 재현

`Godot_v4.7.2-stable_mono_win64_console.exe --path game res://tests/AudioBossGainCapture.tscn` (`--headless` 없음). 같은 실제 보스 스킬을 약4m에서 실행하고 2.4초의 준비음+발사를 녹음한다. 초기화 포함 약4~5초. 다른 오디오 검사와 직렬 실행한다.

- Dummy 드라이버를 거부한다. Master limiter 뒤/-80dB 모니터 감쇠 앞에서 실제 신호·프레임 완전성80% 이상·버퍼 유실0·유한값·최종 피크≤-0.8dBFS·0dBFS 초과0을 검사한다.
- `game/output/audio-boss-gain/capture.json`에 통과 여부·샘플수/피크/RMS·실제 voice 게인/상한/피치·credits/보스/오디오/검사 코드와 보스 데이터 SHA-256, `boss-fan.wav`에 정규화 없는 stereo 출력을 저장한다. output은 재생성 산출물이라 커밋하지 않는다.
- 이번은 사운드 호출 게인만 바꾸므로 별도 화면 렌더는 해당하지 않는다. 보스 밸런스·새 음원 제작·사람의 최종 음색/피로도 승인을 주장하지 않는다.
