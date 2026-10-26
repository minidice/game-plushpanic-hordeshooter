# 재질 접촉음·오브젝트 프리셋 데이터

관련 A-26, I-64 부분 해결 유지, C-29 엄폐/내구도 보존. 사용자 승인: 추천 분류 실행 및 오브젝트의 추가 데이터/값 지정. [계획](../surface-audio-plan.md).

## 변경

- 오디오 전용 `surface_profiles.json`: 7계열, 15명명 프리셋, manifest76+runtime17=93매핑. 장식5개 null, 예비 fabric/sand 포함. 기존 맵 지면 구간 및 큰맵3개 region50 매핑. 물리·체력·차단·관통은 기존 `object_interactions.json`과 분리하며 해당 파일은 불변.
- 실제 발밑 충돌의 위치/높이로 발소리 선택. 엄폐 사격막은 본체 재질. hitscan 환경 명중과 ObjectBody 일회 파손에 소리 연결, 폭발 배럴 기존 폭발 경로 유지. 작은 숲맵 합성 컨테이너/창고/목교 난간과 큰맵 초소/크레인·도시 코드 컨테이너도 명시 지정.
- Kenney CC0 foley 편집으로 발6/명중7/파손4=17종×2변형34 WAV 추가. 기존21 WAV 해시 불변, 총55 WAV. 출처·원본 해시·레시피 보존. 흙/자갈 등은 제작 조합음이며 실제 재질별 현장 녹음이라고 주장하지 않음.
- 프리셋 추가 게인 기본0dB, 금속통 명중 -2dB. 이벤트 기본 발 -10 / 명중 -3 / 파손0dB, priority0·NearCap·최대3/3/2·초과 생략. 좀비 새 발소리는 추가 -5dB, 봇 기존 감쇠 유지. 파일 누락 시 작은 합성 fallback.
- Studio 오브젝트 속성에 프리셋·계열·이벤트/추가 게인·적용 범위·JSON 원본 조회. 별도 저장 폼은 추가하지 않음.
- `AudioSurfaceCapture`와 `tools/surfaces.py`: 변형34+7계열 발소리+중첩1=42조건 실제 Master 녹음/청음 페이지. 최신 표면 데이터/구현 지문 검사. 기존14종 페이지와 연결.

## 실패와 보정

- 최초 RED: 표면 데이터 파일 누락으로 전용 검사 실패. 구현 후 실제 걷기/사격/파손/높이 구분까지225검사 통과.
- 테스트 코드 작성 중 Floor의 존재하지 않는 y인자·확장 메서드 using 누락으로 빌드 실패, 파괴 후 해제된 오브젝트를 테스트가 열거해 예외. 실제 API/생존 객체 확인으로 수정, 게임 규칙이나 기준 완화 없음.
- 첫 Godot import가 새 WAV에 기본 compress/mode=2를 적용해 `release.py` 계약 검사가 실패. 실제 로드 형식 검사를 추가해 PCM16 RED를 확인한 뒤 무압축 설정으로 맞췄다.
- 데이터 설명을 다듬은 후 오래된 표면 캡처로 청음 페이지 생성 시 지문 불일치를 실제 거부했다. 최종 데이터/가져오기 설정으로 재녹음한다. 설명 변경만으로도 오래된 자료를 거부하는 보수적 지문 정책이다.

## 검증

- 최종 빌드 오류0·경고0, Godot import 완료. SurfaceAudioCheck 최종242통과(17개 새 기본음의 실제 PCM16 로드 포함).
- ObjectInteractionCheck30 / CoverCheck27 / DestructionCheck42 / DestructionCompatibilityCheck14 / AudioQualityCheck50 / AudioLevelCheck4 / Integration149 통과. 기능 검사 로그 오류·종료 경고 없음.
- AudioSurfaceCapture 최종42조건: 피크 최대 -0.999dBFS, 0dBFS 초과0. 7계열 10회 발소리 피크 -21.8~-18.6dBFS. 신호·프레임 수·상한 검증. 직접 Sfx/발밑 호출 통제 녹음이며 긴 실전 플레이와 구분.
- AudioQualityCapture27조건: 최대 -1.0dBFS, 초과0. AudioMixCheck15통과: 걷기/휘두르기/소총/피격/사이렌 상대 음량 유지.
- `release.py --page`: 총55 WAV·소스41개 해시·PCM/피크/DC/끝점/import·크레딧 확인. `surfaces.py`:42녹음 지문/상한·93매핑·페이지 링크 통과. 기존21 WAV SHA불변, 상호작용 JSON diff없음.
- Studio `surfaces.mjs`:93통과(환경 오브젝트90항목+3모델 실제 렌더), 픽셀 검사3통과. 첫 검사는 같은 UI에 있는 소환수11종까지 환경 표면으로 잘못 요구해 실패했으며, drone/sidekick/pet은 명시 제외했다. Node 종료 assertion은 이 실패 실행에서만 발생했고 최종 실행은 정상 종료.
- 실제 브라우저에서 모래주머니 모델과 천·충전재/음원ID/게인·기존HP50 표시 확인. 재질 청음 페이지의 금속 발소리 재생→일시중지 상태/시간 스크러버 활성 및 렌더 확인. 인간 음색 판정 아님.
- 최종 Godot 검사/녹음/import 로그 ERROR/WARNING 없음. 문서7개 상대 링크 및 git diff 공백 검사 통과(CRLF→LF 안내는 Git 형식 안내로 구분).

## 결정·이슈 검토와 남은 범위

A-26에 재질 분류/오디오 데이터 분리를 추가, I-64는 부분 해결 유지. 새 이슈 발급 없이 기존 믹스 후속으로 기록한다. 최종 사용자 청음·장시간 실제 플레이·실제2PC, 적 투사체/로켓/근접 접촉·동적 물체 충돌·면 단위 복합 재질은 미검증/미적용이다.

원격 Shot에는 충돌체/재질 정보가 없어 타 플레이어의 환경 명중음을 이번에 재현하지 않는다. 파손은 기존 ObjectBreak 동기화 경로에서 재생한다. GUI 저장 폼은 후속이며 현재 JSON을 편집한다. 모든 이름별 고유 음원을 주장하지 않고 계열별 공유 음원임을 표시한다.

소스/캡처/검사 로그는 `game/output/surface-*`, `game/output/audio-surfaces/`, 비교는 `prototypes/audio-lab/output/surfaces.html`(로컬 재생성, 커밋 제외).


## 독립 리뷰

읽기 전용 리뷰 Critical0 / Important0 / Minor1. 실제 길안내 표지판 모델은 장식이고 별도 기둥 Solid가 기본 콘크리트였던 누락을 확인했다. 이번 사용자의 오브젝트 값 적용 범위에 속하는 단일 매핑이므로 `surfaceObject: "route_sign"`를 연결해 철판 소리로 맞췄다. 물리 크기·위치·차단은 불변. 최종 빌드0/0과 SurfaceAudio242 회귀 통과. 리뷰 후 재리뷰는 하지 않았다.

리뷰에서 유보한 인간 청음·장시간/실제2PC·원격 사격 지원은 위 남은 범위로 수용한다. Studio/최신 캡처는 리뷰어가 실행하지 않았으나 제작자가 실제 검증했다. BaseCore 등 미등록 런타임 충돌체는 문서에 명시한 concrete fallback을 유지한다.
