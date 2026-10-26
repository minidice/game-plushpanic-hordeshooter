# 사운드 품질 시제품 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. 사용자 승인한 추천안을 현재 세션에서 직접 실행한다.

**Goal:** 무료 녹음 소재로 피격·신음·사망 A/B 후보, 동등 음량 비교, 현재 게임 믹스 녹음을 전달한다.

**Architecture:** 게임 런타임은 유지하고 독립적인 audio-lab 제작 도구와 테스트 전용 캡처 장면을 추가한다. 편집된 후보와 원본을 보존하고, 재생용 페이지·측정·캡처는 무시되는 output에 생성한다.

**Tech Stack:** Python 표준 라이브러리, FFmpeg(imageio-ffmpeg 0.6.0), Godot 4.7.2 .NET.

**Spec:** [설계](sound-quality-pilot.md)

## Global Constraints

- CC0 출처 확인. 기준 게임 음원 추출 안 함. 자동 생성 output/과 의존성 실행 파일은 커밋 제외.
- 별도 git worktree. .uid는 새 장면/스크립트와 함께 보존.
- 창 모드 실제 오디오 캡처가 필요. headless 무음은 성공으로 간주하지 않는다.
- 비교 재생은 음량 일치본, 게임 녹음은 원래 설정. 청음 승인과 측정 결과 구분.

## Review Focus

- 스테레오·다른 sample rate 입력을 mono PCM16으로 명확히 변환한다.
- 무음이나 이미 찢어진 원본을 정규화로 양질이라고 주장하지 않는다.
- 파일 다운로드와 ZIP 선택 추출은 출처·해시를 보존하고 지정 폴더 안에서만 쓴다.
- 구형 생성 WAV를 최신 캡처로 표시하지 않는다. 기준 파일은 현재 빌드에서 다시 추출한다.
- 비교 페이지의 경로·실제 파일과 볼륨 일치가 맞고, 소리는 사용자 재생 조작으로만 시작한다.

## Task 1: 녹음 소재와 재현 가능한 편집

Files: `prototypes/audio-lab/{sources.json,recipes.json,requirements.txt,README.md,source/,candidates/}`, `prototypes/audio-lab/tools/build.py`.

- [x] CC0 거위·좀비·충돌 소재 다운로드, 필요한 원본만 보존하고 SHA-256 기록.
- [x] `build.py`가 `recipes.json`의 시작/길이/필터/레이어로 후보 6개를 생성하고 WAV 측정 JSON을 출력.
- [x] 기존 출력과 후보를 같은 50 ms RMS로 낮춰 정렬한 비교본 생성. 무음은 오류, 과도한 피크는 출력 전 감쇠.
- [x] 검증: 실제 생성 파일 6개의 형식·길이·피크·무음·끝점·소스 해시 검사, 동일 명령 재생성 해시 비교.

## Task 2: 현재 엔진 출력 캡처와 청취 자료

Files: `game/tests/AudioPilotCapture.cs`, `.tscn`, `.uid`, `prototypes/audio-lab/tools/build.py`.

- [x] 테스트 전용 장면에서 현재 `Sfx`의 원음 3개와 소총을 저장.
- [x] 실제 Stage 리스너로 단독/군중/사격 겹침을 Master 버스에서 캡처해 WAV와 pre-PCM 피크·오버샘플 수 보고.
- [x] 기존/A/B 원음과 시나리오 캡처를 여는 비교 HTML 생성, 사용자 조작 없는 자동 재생 금지.
- [x] 검증: `dotnet build`, 창 모드 `AudioPilotCapture.tscn`, `AudioLevelCheck.tscn`, `AudioMixCheck.tscn`, `Integration.tscn` 별도 실행. 신호 없음/오류 시 실패.

## Task 3: 기록과 인계

- [x] 사운드 규격과 기능 체크리스트에 시제품 링크·실제 적용 전 상태 기록.
- [x] `doc/features/effects-audio/log/001-art-sound-quality-pilot.md`에 검사 결과, 출처, 청음 미승인과 남은 작업 기록.
- [x] 코드·음원·라이선스·문서만 검사 후 같은 커밋. 생성 출력 제외.
- [x] 비교 자료를 사용자에게 열고 A/B 선택 가능하게 전달.

## 실행 메모

- Task 1: 완료. 소스7 해시·후보6·비교9·형식/길이/피크/DC 검사 통과. 동일 재생성 후보6 해시 일치.
- Task 2: 완료. 캡처11·AudioLevel4·AudioMix6·Integration149 통과. 브라우저 렌더와 피격A 재생 확인.
- Task 2 Ruling: 고정 -12 dB 청취 감쇠는 랜덤 피치 중첩 피크를 항상 수용하지 못했다. 게임은 유지하고 float 원본을 보존한 뒤, 전체 배치에 같은 감쇠(최소12 dB·최대피크 여유3 dB)를 계산한다.
- Task 3: 완료. A-26, I-64 추가 측정, 규격·체크리스트·작업 기록. 독립 읽기 전용 리뷰에서 중요한 결함 없음.
- Windows에서 별도 shell 실행 ledger 도구 대신 이 문서와 기능 log에 명령·결과를 함께 기록. 시제품/아트 작업이므로 게임 제품 코드를 바꾸지 않았고, 생성 결과에 대한 실제 검증을 수행했다.
