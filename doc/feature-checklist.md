# 게임 기능 체크리스트

기준일: 2026-10-20. `[x]`는 코드에 구현된 기능, `[ ]`는 미구현 또는 후속 작업이다. 최종 품질·밸런스가 확정됐다는 뜻은 아니다. 새 기능은 이 목록과 해당 상세 규격을 함께 갱신한다.

## 메뉴와 설정

- [x] 현재 거위 앱·Windows 실행파일 아이콘, 3종 원본 시안 보존 및 교체 도구 ([아이콘 규격](app-icon.md), A-16). 최종 주인공·브랜드 확정은 아님.
- [x] 메인 메뉴: 커스터마이징 · 싱글 · 멀티 · 설정 · 종료, 모든 페이지 왼쪽에 캐릭터 미리보기
- [x] 싱글: 모드 3종 출격(거점·난전은 2026-10-21부터 1차 버전, 화면 이름 M-23), 스테이지는 자동(1개)
- [x] 거점(구 생존) 1차: 창고 발전기(거점) 방어, 준비 + 웨이브 3개(약 2분 43초), 일반 적이 가까운 발전기를 때림, 뒤 웨이브에 무장 랫서판다·정예병, 거점 내구도 막대, 발전기 파괴 패배·마지막 웨이브 클리어 ([모드 3.1](menu-multiplayer-plan.md), M-22)
- [x] 난전(구 서바이버) 1차: 선반 없는 창고 홀, 사방 화면 밖에서 약한 무리(점점 커짐), 2분 30초에 스킬 보스(돌진·빨간 원 내려찍기·소환·격노), 보스 막대, 보스 처치 클리어 (M-22)
- [x] 개편 2단계(2026-10-21, T-21·M-24): 몬스터 등급 boss, 모드별 몬스터 테이블(`modes/<mode>_spawns.csv`), 보스 페이즈·스킬 데이터(`bosses/<id>.json`, 3페이즈·부채꼴 탄막 추가), 난전 named 처치 후 정비 시간 ([규격](mode-monster-tables.md))
- [x] 개편 3단계(2026-10-21, A-17): 난전 보스 전용 모델 `boss.glb`(Codex 제작, 삼각형 4,466) 적용, 지시문 → Codex → Blender 최적화 → 게임 연결 흐름 ([지시문](model-briefs/boss.md))
- [x] 개편 4단계(2026-10-21, L-13): 맵 인터페이스 `StageMap`(Stage 1은 그 하위 클래스, 모드별 맵 선택 자리, 검사용 맵 교체) ([레벨 규칙](level-design-rules.md))
- [x] 개편 5단계(2026-10-21, M-26): 난전 전용 들판(나무·돌기둥 무작위 산포, 방에서는 같은 씨앗), 바닥 진열대로 무기 2개 고르기, 예비탄 무제한, 3번 칸 종류 25초 충전, 네임드 처치 시 강화 진열대, 화면 밖 팀원·보스 화살표
- [x] 개편 6단계(2026-10-21, M-25): 거점 배낭 4×5 = 20칸(I로 열기, 드래그 장착·옮기기·바닥 버리기, 마우스 오버 정보), 탄 종류 8개와 배낭 탄으로 장전, 바닥 무기·탄약 더미·구급상자가 배낭으로, 네임드 처치 시 전리품 3~5개 흩뿌림, 멀티 동기화(방장 판정)
- [x] 카드 실험실 1차(2026-10-21, T-22): `Start-Game.ps1 -CardLab`, 난전 들판에서 카드 6장(풍선탄 · 찍찍이탄 · 솜 폭발 · 당구 · 꽥 장전 · 고무오리)을 켜고 끄며 시험, 몬스터 카드 상태(부풂 · 둥실 · 찍찍이 · 날아감) ([아이디어 풀](onslaught-card-ideas.md) 6장)
- [x] 카드 창 1차(2026-10-21, M-27 · T-22, 실험실에서): 하데스식 세로 3장, 받은 순서 · 받을 때 정해진 3장 유지, 무장 1 · 2 · 3번 칸 카드(닫기 없음), 레벨업 카드 ✕ · F로 미루기 + 배지 ×N, 열려 있는 동안 걷기 · 도약만
- [x] 정식 난전 카드 흐름 1차(2026-10-21, M-27): 진열대 · 강화 진열대 제거, 시작 때 1 · 2 · 3번 칸 무장 카드, 팀 공유 경험치 → 레벨업 일반 카드(실험실 카드 + 반복 카드 피해 · 회복 · 구급상자), 보스 기준 4구간(3 · 4 · 5 · 6분, 구간 보스 체력 35~100%), 중간 보스 = 특별 카드, 최종 보스 = 클리어, Lv · 경험치 막대, 멀티 동기화(방장 판정, 카드 번호) ([규격](mode-monster-tables.md) 5장)
- [x] 20분 판 내용 1차(2026-10-21, M-27 · Q-24): 모드 표를 구간 시계로(보스전 동안 멈춤), 4구간 표(일반 체력 0.6 → 1.25, 구간마다 named 증가), 구간 보스 무작위(`mid_bosses` · `final_bosses`, 겹치지 않게), 임시 중간 보스 3종(거대 황소 · 풍선 대왕 · 토사물 어미), 보스 소환 몬스터 지정 ([규격](mode-monster-tables.md) 2 · 3 · 5장)
- [x] 난전 중간 보스 전용 모델(2026-10-21, A-17): 풍선 대왕(금관 · 망토 · 풍선 다발) · 토사물 어미(뒤쪽 후드 · 입 깔때기 · 등 주머니), Codex 2회 + Claude 마무리, 보스 크기 정리(중간 약 3 m < 최종 약 3.2 m), `MonsterLineupCapture`에 보스 줄 · 근접 렌더 ([지시문](model-briefs/midbosses-balloon-spitter.md))
- [x] 실험실 피드백 반영 1차(2026-10-21, M-27 계획 13.2): 카드는 고른 사람의 것(명중에 쏜 사람 · 무기 분류), 풍선탄 4방 펑 · 네임드 면역, 카드 트리(풍선탄 · 찍찍이 강화 Lv), 당구 저격 · 샷건 전용(관통 당구 · 산탄 볼링), 꽥 · 고무오리는 레벨업에서 제외, 실험실 L = 난전 레벨업 제안
- [x] 난전 3번 칸 꽥 · 고무오리 1차(2026-10-21, M-27 계획 13.1): 발사 방식 `shout`(주변 밀쳐내기) · `throw`(투척물 던지기), 3번 칸 무장 카드 후보 5종, 난전 전용 무기별 쿨타임 `special_cooldowns`, 실험실 오리 카드도 같은 오리
- [x] 꽥 경적 · 고무오리 전용 모델 · 아이콘(2026-10-21, A-17 · I-87): Codex Blender 스크립트(`gadget-models.py`) + Claude 수정(축 · 색 · 부리 · 크기), 던진 오리도 같은 모델, 아이콘은 `render-weapon-icons.py --only`
- [x] 소환수 게임 적용 1차(2026-10-22, M-28): 난전 무장 네 번째 소환수 카드 · 실험실 C, 따라가기 · 기본 공격 · 12 m 벗어나면 복귀(시전은 끝내고), 펫 · 드론 무적 · 몸 없음, 부사수 쓰러짐 → 8초 뒤 부활, 게임 중 Esc 메뉴에서 소환수 모델 품질 전환, 멀티 목록 동기화 ([규격](companion-model-spec.md))
- [x] 갈 수 없는 곳 채우기 1차(2026-10-22, A-23): 외곽 전용 모델 6종(Codex, 충돌 · 뒷면 · 가림 부위 없음), 인스턴싱 줄 배치(덤불 띠/바위벽 → 숲 → 실루엣), 가장자리 줄만 그림자, 카메라 쪽 · 서쪽 끝은 낮게, 먼 바닥 판, 카메라 경계 — Stage 2 적용
- [x] 길 경계 섞기 1차(2026-10-22, A-24): 바닥 타일 대신 섞임 지도 + 셰이더(흙 · 아스팔트 · 자갈 갓길이 노이즈로 풀밭에 섞임, 가운데 점선), 바닥 조각 5종(Codex: 풀 다발 · 큰 풀 다발 · 돌 3개 · 돌 하나 · 자갈, 판 없음)을 자연 바닥 전체에 풀 위주로 인스턴싱, 자연 · 인공 바닥 분리 — Stage 2 적용 ([지시문](model-briefs/ground-border-bits.md))
- [x] 난전 카드 규칙 3차(M-29) 1단계(2026-10-22): 피해 출처(로켓 · 폭발 · 불 · 드럼통 · 카드 연쇄가 시작한 사람 몫, 처치 몫 `LastHitBy`), 마지막 한 발 = 쏜 사람 자기 탄창, 유리 대포 = 팀 전원(대미지 2배 · 최대 체력 절반), 솜 폭발 · 찍찍이 덩어리는 그 사람 카드 · 레벨로 ([계획](onslaught-cards-plan.md) 16.8)
- [x] 난전 카드 규칙 3차(M-29) 2단계(2026-10-22): 중간 보스 처치 = 모두 멈춤(온라인도) → 특별 카드 무기명 투표(같은 도장 · 투표 수 · 60초 게이지 · 조기 확정 · 동률 룰렛) → 이긴 카드 팀 전체 → 3 · 2 · 1 재개, 도장 · 테두리 그림 Codex ([계획](onslaught-cards-plan.md) 16.5, [지시문](model-briefs/vote-stamp.md))
- [ ] 난전 카드 규칙 3차(M-29) 나머지: 각자 리롤 5회, 등급 7단계 · "?" 카드, 새 카드(단추 미사일 · 튕기는 탄 · 깜짝 상자 · 모여라! 등)
- [ ] 난전 개편(M-27) 나머지: 소환수 카드(기본 공격 강화 · 새 능력) · 각성, 총 분류별 카드 풀, 버프 줄 ([계획](onslaught-cards-plan.md))
- [x] 사족 펫 모델 라이브러리(A-20): 거북이·강아지·토끼 기본형, 공용 등 무기 2종 교체/해제, 시각용 걷기. 세 종 형태 개선·고품질/저품질 6개 GLB·설정 저장/전환 포함. 거북이·토끼는 귀여움 기준으로 큰 머리/볼·짧은 몸/발 재조정, 게임 크기 전후 비교 확인. 모델 185개·메뉴 44개 검사와 6마리 렌더 확인. 모델 단계이며 추종·소환 카드·공격은 후속 ([규격](pet-model-spec.md)).
- [x] 드론·부사수 모델 라이브러리(A-21): 드론4종×고/저, 공용 하부장비4종 교체, 기존 캐릭터60% 프리셋4종·기존 걷기/무기 잡기. 소환수 공용 품질 설정, 부사수는 자동LOD 조절. 모델253·펫185·메뉴44 검사 및 실제 렌더 확인. 추종·공격·소환카드는 후속 ([규격](companion-model-spec.md)).
- [ ] 거점·난전 다음: 팀원 직접 교환·큰 배낭·탈출, 전용 맵·전용 보스 모델, 멀티에서 보스 연출 동기화(I-77), 수치 플레이 조정
- [x] 멀티 메뉴: 방 만들기, 주소로 참가(2인 테스트). 방 코드·공개방은 준비 중
- [x] 멀티 테스트 실행: 한 PC 두 창(`Start-Multi-Test.cmd`), 방장 LAN 주소 안내, 다른 PC용 Windows 실행 파일(`Export-Game.cmd` → `build/HordeShooter-windows.zip`)
- [x] AI 분대원 선택(없음 / 1명 기본 / 3명) 저장
- [x] AI 분대원 등장(싱글): 역할 3종·무기 풀, 따라가기·교전·구출·치료·줍기, 지나온 길에서 따라잡기 ([설계](ai-squad-plan.md))
- [x] 쓰러짐·일으키기(4초, 맞으면 끊김), 한 생명 2회, 출혈 30초, NPC 재등장 60초·2회 — 설정의 "부활" 옵션(기본 켬)
- [x] 멀티 페이지 껍데기(방 만들기·코드 참가·공개방 모두 "준비 중")
- [x] 방(로비) 1차: 참가자 목록·준비·방장 모드 선택(3종 모두)·전원 준비 시 [시작], 게임 중 재시작은 모두 방으로, 게임 중 참가는 바로 합류 (2인, M-21)
- [x] 공개 방 목록(lobby.minidice.net): 방장이 방을 올리고 목록에서 눌러 참가, 다른 버전은 잠김, 같은 공유기는 LAN 주소, 방장 UPnP 포트 열기 시도 (M-6; 서버 2026-10-21 설치, 실제 방장 등록 확인. 다른 집 접속은 미확인)
- [ ] 로비 남은 것: 방 옵션(아군 오사·부활), 3~6인, 결과 화면, 캠페인·스테이지 선택, 포트를 열 수 없는 집을 위한 중계
- [x] 설정: 언어(한국어/English), 전체·효과음·배경음 볼륨, 사격 반동 밀림 %; `user://settings.cfg` 저장
- [ ] 키 재설정

## 레벨과 화면

- [x] Stage 1: 세이프룸 → 거리 → 창고 셔터 이벤트 → 야적장 → 세이프하우스
- [x] 시나리오 Stage 1 → Stage 2 전환(2026-10-22, L-14): 세이프하우스를 닫으면 바닥 해치, E 길게 → 원 닫힘 → STAGE 2 카드 → 벙커 해치에서 원 열림, 실내 직교 시점 → 야외 원근 시점으로 이어 바뀜, 장비 이월(혼자 할 때만, I-90)
- [x] Stage 2 숲속 벙커길(2026-10-22, L-14): 벙커 공터 → 숲길 → 개울 다리 → 컨테이너 야적장 → 세이프하우스, 야외 키트 모델, 거리 안개·석양 조명, 조우·보급 데이터 ([레벨 규칙](level-design-rules.md) Stage 2)
- [x] 야외 모델 팩 32종 통합 + 전환 소품 4종(Codex 모델, 2026-10-22, A-22): manifest 기반 배치·추천 충돌, 바닥 타일·배경 숲 MultiMesh
- [ ] 키트의 가림 부위(`Occlude_*`) 옅게 하기, 비·젖음·낮밤, Stage 2 성능 측정과 손 플레이 검증(I-91), 멀티 다음 구역(I-90)
- [x] 화면 우측 상단을 주 진행 방향으로 사용; 기본/확대 카메라와 화면 기준 WASD
- [x] 주 경로 70% 이상 우측 상단이라는 [레벨 디자인 규칙](level-design-rules.md)
- [x] 타일 바닥, 벽 패널, 선반·보관함·작업대·단말기, 구역 조명
- [x] 하단 1/2/G/Q 장비 칸, 무기 아이콘, 체력·탄약·도약 대기시간·상호작용 안내
- [ ] 후속 스테이지별 실제 경로 거리 비율 검수, 최종 소품 아트·LOD·대규모 전투 최적화

## 캐릭터와 커스터마이징

- [x] 거위·오리·라쿤·랫서판다 플레이 모델, 종별 저장
- [x] 부위 색상·체형·얼굴·부리/귀·날개·꼬리 크기, 모자·조끼·배낭·고글·헤드셋
- [x] 몸 투톤(주색·보조색, 라쿤·랫서판다는 얼굴 무늬색), 팔·손이 몸 색을 따라감
- [x] 라쿤 가로 눈띠 / 랫서판다 흰 볼·적갈 세로 무늬·둥근 눈썹 구분, 얼굴 무늬 번짐 0~100%와 종별 저장 (I-54)
- [x] 귀 모양 4종·기울기(라쿤·랫서판다), 꼬리 모양 5종
- [x] 네 종 몸 배색·얼굴 무늬를 연속 마스크로 표시; 둥근 귀 테두리와 확대용 눈·주둥이·새 부리 보강 (`CharacterSurfaceCheck`, I-53)
- [x] 프리셋: 공용 3 + 종별 색 2, 고르면 바로 적용 (`appearance_presets.json`)
- [x] 총기별 손 위치와 양손/한손 파지, 달리기·조준 자세
- [x] 꼬리의 뒤쪽 부착과 종별 휴식 자세, 이동에 따른 작은 흔들림
- [x] 꼬리의 뒤집힌 면 보정과 연속 줄무늬 메시; 기존 색·크기 편집 유지
- [x] 낮은 전방 도약, 피격 흔들림, 물리 시체
- [x] [일반 시체 정리 목표 100개](corpse-retention.md): 모든 등록 관찰자의 화면/근거리 보호, 먼 후보를 생성 순으로 분할 정리
- [x] 시체와 게임플레이 소품의 충돌 분리, 다중 관찰자 등록 API
- [x] CSV 일반/네임드 등급, JSON 유지 정책, 네임드 기본 5분 수명·고정 사망 기록
- [x] 일반 시체 걷기·구르기 밀기/회전, 자연 휴면·재접촉 활성화, 폭발 거리/차폐 충격 및 같은 폭발의 사망 시체 포함. 네임드는 지면에 눕혀 외형까지 고정 (`CorpseReactionCheck`, I-69)
- [ ] 네임드 시체 루팅·모드별 정책 선택·저장·호스트 상태 동기화
- [ ] 멀티 참가/퇴장·관전과 실제 원격 관찰자 보호 연동
- [ ] 본 기반 최종 애니메이션·다관절 래그돌·소프트바디

## 전투와 피드백

- [x] 주무기·보조무기 교체/드롭, 동일 권총으로 쌍권총, 탄약 보충
- [x] [L4D2 참고 탄창·재장전 조정](combat-pacing-research.md)
- [x] R870·Benelli 한 발씩 장전, 중간 사격, 교체 시 이미 넣은 탄 보존
- [x] 네 동물의 공통 탄창식·한 발씩 넣는 샷건 손/총 장전 모션, 실제 장전 주기 연결·중간 사격 전 총구 복원 ([범위·제약](weapon-spec.md#61-손총-장전-모션-2026-10-20), I-55). 실제 탄창/탄 메시 교체와 쌍권총·더블배럴·중화기 전용 모션은 미구현.
- [x] 근접 무기 준비 → 휘두르기 → 회복; 타격 시점에 부채꼴 판정
- [x] 일반 적 접촉만으로 즉시 피해를 주지 않음; 공격 준비와 손/몸 동작 뒤 1회 타격
- [x] 감염체 원래 팔을 어깨 피벗으로 분리해 공격 동작 적용
- [x] 타격 순간 거리·방향·차폐 확인; 피하거나 기절시키면 공격 무효화
- [x] 돌격 적의 예고·돌격 자세와 돌격 중 충돌 피해
- [x] 실제 피해를 받은 플레이어/적의 약 0.1초 흰색 반짝임; 원래 색/좀비 재질 복원
- [x] 조준 집중 치명타, 반동·넉백, 탄착·솜 파편·사망 효과·효과음
- [x] 무기 발사·근접 시각 효과(2026-10-22, A-19): 분류별 흰/노란 총구 섬광·짧은 탄 잔상, 레일건 청백색 빔, 근접 초승달, 알라봉·박격포 발사 섬광. 꽥·고무오리 고유 연출 유지. 피격·사망은 아래 항목, UI 개편은 후속 ([무기 규격 6.3](weapon-spec.md#weapon-effects)).
- [x] 피격·사망 섬광(2026-10-22, A-19·C-19): 일반 흰색/치명타 노란색 몸·타격선·사망 원, 선·원 투명도 10%. 수신 색 동기화(프로토콜 6), 반복 피격 재사용, 숨김 적의 섬광 차단, 기존 솜 파편·시체·다운/사망 구분 유지 ([캐릭터 규격 8.1](character-model-spec.md#damage-effects)).
- [x] 수류탄·화염병·섬광탄·유인 폭탄, 투척 궤적·착탄 범위
- [x] 구급상자 회복, 일시정지·재시작·스테이지 클리어

- [x] 무기 개편 1단계(2026-10-21, C-24~C-26): 무기별 이동속도(기관단총 110%, 경기관총·저격 85%), 우클릭/Ctrl 조준(사거리·퍼짐·집중·낮은 자세·카메라, 걸음 느려짐), 투척·박격포 조준 중 이동 -50%, 3번 특수무기 칸(로켓런처·박격포·레일건), 박격포 거치·회수, 레일건 충전·관통 ([무기 규격](weapon-spec.md) 8장)
- [ ] 무기 개편 다음: 개별 총 개성 다듬기, 레일건 전용 모델, 파티원 화면의 충전·거치 표시(I-78)

## 적과 협동

- [x] 감염체/야생 동물/무장 동물 테마별 적 구성
- [x] 인간형·기어오는 적·끌개·덮치는 적·토사물 적·돌격 적·풍선 적
- [x] 일반 시체 더미가 결국 잠들어 물리 비용이 돌아옴, 레벨 밖으로 날아간 시체 정리 (I-70·I-71)
- [x] 감염체 5종의 앞뒤 볼륨 보강과 얼굴/의복 부품 정렬, 라쿤과 정면·측면·후면·사선 비교 렌더 (`CaptureInfectedVolume`, I-68)
- [x] 두꺼워진 인간형/끌개/덮치는 적의 이동·피격·시체 반경 보정, 토사물·끌개 연결의 입 앵커 및 차폐 확인 (`InfectedAlignmentCheck`, I-68). 캡슐은 몸통 근사이며 부위별 정밀 판정은 아님.
- [x] 시야/소음 반응, 길찾기·군중 분리, 웨이브 디렉터
- [x] 적 보임 단계: 보임 / 실루엣 / 숨김, 아군 손전등 빛 공유, 나를 공격하는 적 표시, 맞은 방향 빨간 호, 나타날 때 모양·색이 빠르게 채워짐 (C-23)
- [x] 반응 유형 3종(대기형·경계형·웨이브형), 맞으면 근처 동료 연쇄, 무기별 소음 반경 ([설계](spawn-design.md))
- [x] 구역 단위 미리 배치, 화면 밖 우선 등장, 지나온 길·벽 위 뛰어내리기 등장, 출입구 보호 구역
- [x] 차 경보기: 쏘거나 폭발에 울리고 20초 동안 웨이브
- [ ] 스테이지 1 재구성: 넓은 거리·병목·옆 공간, 꺾인 계단, 하수도, 벽 뚫기·환풍구 연출. 시간 목표 12~15분은 [시나리오 레벨 디자인](scenario-level-design.md) 9.2 참고
- [x] 보급 지점 단위 아이템 배치와 실험 예산(시작·주요 2·선택 1·이벤트 보상) — 충분한지는 플레이 확인 전
- [ ] 픽업 강조 표현(링·부유·회전) 정리 — 전후 렌더 비교 후 결정
- [ ] 개발자 조정 패널(F9 등, 슬라이더로 탄속·준비 시간·음량·디렉터 수치를 실시간 조절하고 JSON 저장) — 사용자 요청으로 나중에
- [x] 붙잡힘과 동료 구조 전제; 싱글 테스트용 F8 탈출 치트
- [x] 멀티 2인 동기화 MVP: 방장 권한, 내 이동은 내 PC, 꼭두각시 적, 줍기·문·목표·피해·쓰러짐/부활 공유(남은 한계 I-72), 메뉴의 방 만들기·주소로 참가 / 명령줄 실행 ([설계·결과](multiplayer-sync-design.md))
- [ ] 멀티 다음 단계: 다른 집 접속 확인·포트를 못 여는 집 대책(③), 3~6인(④), 인원 보정(⑤), 중간 합류(⑥), Steam(⑧)
- [ ] 동료 구조가 준비된 뒤 테스트 치트를 기본 비활성화
- [ ] 세 테마에 대응하는 별도 완성 스테이지

## 확인 방법

실행: `game/Start-Game.cmd`. 기본 통합 검사: `game/Start-Game.ps1 -Test`.

빌드·가져오기 후 Godot 테스트 장면: 스테이지 전환·Stage 2·야외 키트는 `StageTransitionCheck.tscn`, `CombatPacingCheck.tscn`, `EnemyAttackCheck.tscn`, `TailMeleeCheck.tscn`, `HitFlashCheck.tscn`, 생존·서바이버는 `ModeCheck.tscn`, 거점 배낭은 `InventoryCheck.tscn`, 카드 실험실은 `CardLabCheck.tscn`. 실제 화면 검사는 별도 캡처 장면과 플레이로 보완한다. 수치나 동작을 바꿀 때 해당 검사와 문서도 같이 갱신한다.

상세 규격: [무기](weapon-spec.md), [캐릭터](character-model-spec.md), [적·투척물](throwable-monster-spec.md), [사운드](sound-spec.md), [다국어](localization.md).

결정과 이유는 [결정 기록](decisions.md), 알려진 버그와 제약은 [이슈 기록](issues.md)에 있다.

### 이번 변경 확인 기록 — 2026-10-20

- C# 빌드 성공. 탄약/재장전/화면 방향 검사 44개, 적 공격 검사 79개 통과.
- 꼬리 면 방향·부착·좀비 복제·근접 타이밍 검사 통과. 삼각형별 반복 검사가 포함되어 출력 검사 수는 기능 수와 다르다.
- 피격 표시, 파지, 종별 커스터마이징 검사 통과. 통합 검사 145개 통과.
- 실제 Godot 렌더에서 새 카메라와 HUD, 꼬리, 근접/적 공격 단계, 피격 표시 확인. 화면/UI 검사 47개 통과.
- 일부 단독 헤드리스 검사 종료 때 `ObjectDB instances were leaked at exit` 정리 경고가 남는다. 실행 중 기능 실패는 관찰되지 않았지만 종료 자원 정리는 후속 점검 대상이다.
- 이 검사는 동작과 표시의 회귀 확인이다. 호드 밀도에 따른 체감 난이도와 최종 애니메이션 품질은 실제 플레이로 계속 조정한다.

### 이번 변경 확인 기록 — 2026-10-21 (생존·서바이버 1차)

- C# 빌드 경고·오류 0. `ModeCheck` 43개 통과(헤드리스·창 모드 모두), `Integration` 147, `MenuCheck` 39, `LobbyCheck` 31, `NetSyncCheck` 28, `SpawnCheck` 41, `NetCodecCheck` 11 통과.
- 창 모드 `ModeCheck` 렌더 `output/mode-*.png`로 거점·웨이브 문구·막대, 서바이버 무리, 보스 내려찍기 원·소환·격노·결과 화면 확인.
- 일부 헤드리스 실행 종료 때 I-17의 오디오 스트림 잔존 경고가 남음. 2인 멀티로 두 모드를 실제로 해 보지는 않았고(I-77), 난이도·시간은 직접 플레이로 조정해야 함(I-26).

### 이번 변경 확인 기록 — 2026-10-21 (무기 개편 1단계)

- C# 빌드 경고·오류 0. `WeaponHandlingCheck` 30개 통과(헤드리스·창 모드), `Start-Game.ps1 -Test`(Integration) 148, `CombatPacingCheck` 44, `NetCodecCheck` 11, `NetSyncCheck` 28, `SquadCheck` 50, `ReloadPoseCheck` 279, `WeaponGripCheck` 187, `SupplyCheck` 12, `MenuCheck` 39, `LobbyCheck` 31, `ModeCheck` 43, `EnemyAttackCheck` 79, `SpawnCheck` 41, `TailMeleeCheck`, `VisionTierCheck` 23, `HitFlashCheck` 통과.
- 창 모드 렌더 `output/weapons-*.png`로 조준 중 카메라·조준선, 3번 칸 HUD, 박격포 착탄 원·궤적, 레일건 충전선·진행 막대 확인.
- 실제 손으로 해 본 조준 손맛·이동속도 차이 체감은 확인하지 않음(I-26). 2인 멀티로 3번 칸을 써 보지는 않음(I-78).

### 이번 변경 확인 기록 — 2026-10-21 (개편 2단계)

- C# 빌드 경고·오류 0. `ModeCheck` 67개 통과(헤드리스·창 모드), `Integration` 148, `CorpseTierCheck` 36, `CorpseRetentionCheck` 16, `CorpseReactionCheck` 26, `EnemyExpansion` 60, `SpawnCheck` 41, `NetSyncCheck` 28, `NetCodecCheck` 11, `MenuCheck` 39, `LobbyCheck` 31, `WeaponHandlingCheck` 30, `SquadCheck` 50, `EnemyAttackCheck` 79, `CombatPacingCheck` 44 통과.
- 창 모드 렌더 `output/mode-survivor-fan.png`·`mode-survivor-boss.png`로 부채꼴 탄막 바닥 선, 페이즈 알림, 보스 막대 30%를 확인.
- 보스 2~3분 전투 체감, 테이블 가중치의 난이도는 직접 플레이로 확인하지 않음(I-26).

### 이번 변경 확인 기록 — 2026-10-21 (개편 3단계, Codex 보스 모델)

- Blender 최적화 assert 통과(삼각형 4,466 < 4,500, 두 팔 피벗·메시). C# 빌드 경고·오류 0. `ModeCheck` 67개 통과(헤드리스·창 모드, 보스가 `boss.glb`의 팔 피벗을 씀), `Start-Game.ps1 -Test`(Integration) 148(적 정의 목록 검사에 `boss` 추가), `EnemyExpansion` 60, `HitFlashCheck`, `EnemyAttackCheck` 79, `CorpseTierCheck` 36, `NetCodecCheck` 11 통과. `CharacterSurfaceCheck`는 창 모드 전용이라 헤드리스 실행이 "렌더된 화면 필요"로 멈췄고, 플레이어 표면 검사라 이번 변경과 무관해 다시 돌리지 않음.
- 렌더: `output/mode-survivor-boss-close.png`(확대), `mode-survivor-slam.png`(기본 크기)에서 보스 표시(금관·코뚜레·어깨 보호대·큰 주먹)가 읽힘. 손으로 해 본 전투 체감은 미확인(I-26).


### 이번 변경 확인 기록 — 2026-10-21 (개편 4단계, 맵 인터페이스)

- C# 빌드 경고·오류 0. `MapCheck` 17개 통과(헤드리스·창 모드), `Integration` 148, `ModeCheck` 67, `MenuCheck` 39, `NetSyncCheck` 28, `NetCodecCheck` 11, `SpawnCheck` 41, `SquadCheck` 50, `SupplyCheck` 12, `AudioLevelCheck` 4, `LobbyCheck` 31, `WeaponHandlingCheck` 30, `CombatPacingCheck` 44, `VisionTierCheck` 23, `CorpseRetentionCheck` 16, `EnemyAttackCheck` 79 통과.
- 창 모드 렌더 `output/map-flat-onslaught.png`: 부품 없는 평지 검사 맵에서 난전이 돌고 HUD 이름이 모드 이름만 보임. Stage 1은 이전과 같은 화면(ModeCheck·MenuCheck 렌더 경로 그대로).

### 이번 변경 확인 기록 — 2026-10-21 (개편 5단계, 난전 들판)

- C# 빌드 경고·오류 0. `ModeCheck` 81개(창 모드, 화면 밖 화살표 포함)·79개(헤드리스), `MapCheck` 25, `WeaponHandlingCheck` 30, `Integration` 148, `NetSyncCheck` 28, `NetCodecCheck` 11, `LobbyCheck` 31, `MenuCheck` 39, `SquadCheck` 50, `SpawnCheck` 41, `CombatPacingCheck` 44, `ReloadPoseCheck` 279, `EnemyAttackCheck` 79, `VisionTierCheck` 23 통과.
- 렌더: `output/map-open-field.png`·`mode-survivor-rack.png`(들판·진열대·바닥 글씨·무장 목표), `mode-survivor-arrow.png`(화면 밖 팀원 화살표, `∞` 탄약, "난전 · 들판"). 실제 손으로 하는 난이도·들판 성능은 미확인(I-26·I-80). 2인 멀티로 들판·진열대를 해 보지는 않음.

### 이번 변경 확인 기록 — 2026-10-22 (야외 키트 통합 · 시나리오 Stage 1 → 2 전환, A-22 · L-14)

- C# 빌드 경고 · 오류 0. 새 `StageTransitionCheck` 143(헤드리스 · 창 모드 모두), `Start-Game.ps1 -Test` → `Integration` 148, `MapCheck` 24, `VisionTierCheck` 23, `SupplyCheck` 12, `SquadCheck` 50, `ModeCheck` 123, `SpawnCheck` 41, `AudioMixCheck` 6, `CombatPacingCheck` 44, `WeaponHandlingCheck` 36, `NetSyncCheck` 32, `MenuCheck` 44, `LobbyCheck` 31 통과. 전환 소품은 Blender 스크립트 단언 통과(삼각형 · 부위 · 뚜껑 방향).
- 렌더 `output/transition-0-stage1-street.png`(비교용 Stage 1 실내 직교) → `transition-1-safehouse-hatch`(바닥 해치 · 길게 누르기 안내) → `-2-closing`(원 닫힘) → `-3-title`(STAGE 2 · 숲속 벙커길) → `-4-opening`(벙커 해치에서 원 열림) → `-5-arrival` · `-6-arrival-close`(야외 원근 기본 · 확대) → `-7-forest` · `-bridge` · `-yard` · `-safehouse`. 참고 영상 프레임과 비교해 전환 순서 · 주변 오브젝트 · 시점 변화가 대응함을 확인. 영상의 비 · 어두운 석양 색감은 미적용(I-91). 손으로 해 본 Stage 2 플레이 · 성능 · 멀티 다음 구역은 미확인(I-26 · I-90 · I-91).

### 이번 변경 확인 기록 — 2026-10-22 (모두 멈춤 · 특별 카드 투표, M-29 2단계)

- C# 빌드 경고 · 오류 0. Codex `vote_icons.py` 단언 통과(도장 256² · 승리 테두리 512 × 160).
- `ModeCheck` 123 → 137(헤드리스) · 141(창 모드): 조기 확정 규칙 표(3·1·1 +1, 4·0 +2, 3·0 +1/+2 확정, 3·2 +1 대기, 2·2·2 무작위, 시간 끝), `special_vote_s` 60, 중간 보스 처치 → 모두 멈춤 · 투표 창(카드 줄에 특별 카드 없음), 멈춘 동안 구간 시계 · 몹 정지 · 투표 시계만 감, 혼자면 찍자마자 확정 · 두 번째 도장 무시, 이긴 카드 적용 → 3 · 2 · 1 → 재개, 다음 중간 보스들도 투표.
- `OnslaughtNetCheck` 19 → 27(실제 ENet 2인): 파티원도 멈추고 같은 3장 · 투표자 2명, 방장 도장은 파티원 화면에 카드의 도장 수로만(누구 것인지 없음), 1표로는 확정 안 됨, 파티원 도장이 방장에 닿아 확정, 둘 다 승자 · 재개, 다음 구간.
- 헤드리스 전부 통과: `CardLabCheck` 100, `NetSyncCheck` 32, `NetCodecCheck` 12, `CompanionCheck` 39, `SquadCheck` 51, `SpawnCheck` 41, `SupplyCheck` 12, `MenuCheck` 44, `LobbyCheck` 31, `StageTransitionCheck` 164, `Integration` 148(`Start-Game.ps1 -Test`). 실패 0, 종료 경고(I-17)만 2~13개.
- 렌더 `output/mode-survivor-vote.png`(투표 중: 카드 3장 · 투표 0/1 · 게이지 · 안내) · `mode-survivor-vote-countdown.png`(도장 · 금색 테두리 · 팀 카드 이름 · 3). 처음에는 투표 창이 창 모드에서 늦게 떠 검사가 실패(물리 프레임이 그리기 프레임보다 먼저 여러 번 돎 → 창이 뜰 때까지 기다리게 고침), 도장이 대체 문자로 보임(PNG 가져오기 전) → 가져온 뒤 다시 확인, 게이지가 사라질 때 창이 위로 뜀 → 투명하게만.
- 서로 다른 두 PC에서의 실제 멈춤 · 재개와 3인 이상은 미확인(I-95).

### 이번 변경 확인 기록 — 2026-10-22 (보급 종류 판별, I-94 ②)

- C# 빌드 경고 · 오류 0. 헤드리스: `SquadCheck` 50 → 51(문구 키가 아니라 `Interactable.Pickup` 종류로 구급상자 · 탄약을 알아봄), `SupplyCheck` 12, `NetSyncCheck` 32, `MapCheck` 24, `StageTransitionCheck` 164, `ModeCheck` 123 통과.
- `Integration`: `Start-Game.ps1 -Test`에서 1회 "shotgun pellets hit several enemies" 실패(73번째, 알려진 무작위 실패 I-65), 바로 재실행 2회 148 통과. 종료 경고(I-17) 2~20개는 실행마다 다름.

### 이번 변경 확인 기록 — 2026-10-22 (난전 카드 규칙 3차 1단계, M-29)

- C# 빌드 경고 · 오류 0. `CardLabCheck` 85 → 100: 유리 대포가 팀 전원 대미지 2배 · 최대 체력 절반 · 나중에 들어온 사람도 · 끄면 원래대로, 카드 동기화의 특별 카드 비트, 마지막 한 발(아무도 안 쏜 명중 · 소환수 물기는 아님, 쏜 사람 몫, 파티원 자기 빈 탄창에서 터짐 · 탄이 남으면 안 터짐), 피해 출처(소환수 물기 = 주인, 무기 폭발 = 주인 몫 + 배율, 주인 없는 폭발은 아무 몫 없음, 풍선 터짐 · 당구 · 솜 폭발이 시작한 사람 몫, 솜 폭발은 처치한 사람 카드일 때만, 찍찍이 덩어리는 주인 레벨).
- 헤드리스 전부 통과: `ModeCheck` 123, `CompanionCheck` 39, `NetSyncCheck` 32, `NetCodecCheck` 12, `OnslaughtNetCheck` 19, `SquadCheck` 50, `SpawnCheck` 41, `SupplyCheck` 12, `MenuCheck` 44, `LobbyCheck` 31, `EnemyExpansion` 60, `CorpseTierCheck` 36, `StageTransitionCheck` 164, `MapCheck` 24, `Integration` 148(`Start-Game.ps1 -Test`). 실패 0. 종료 경고(I-17)만 여러 검사에서 2~13개(실행마다 다름, 같은 날 main에서도 같은 경고).
- 2인 실제 플레이로 파티원 화면의 유리 대포 체력 막대는 보지 않음(카드 동기화 비트는 `CardLabCheck`, 송수신 틀은 `OnslaughtNetCheck`로만 확인).

### 이번 변경 확인 기록 — 2026-10-22 (길 경계 섞기 1차)

- C# 빌드 경고 · 오류 0. Blender `ground_bits.py` 단언 통과(2차 5종 15~182 삼각형). `StageTransitionCheck` 164(헤드리스 · 창 모드: 키트 47종 목록, 그린 바닥 · 키트 바닥 타일 없음, 해치 흙 · 야적장 아스팔트 · 자갈 갓길 · 점선 위치, 흙길 경계 흔들림, 바닥 조각 풀 다발 271 · 큰 풀 다발 35 · 돌 하나 64 · 돌 3개 9 · 자갈 17, 풀 > 돌 3배, 아스팔트 위 조각 없음, 풀밭 한가운데에도 있음 266/396, 그림자 없음. 헤드리스에서는 MultiMesh가 인스턴스 위치를 보관하지 않아 처음엔 이 검사가 헤드리스에서만 실패 → 맵이 놓은 자리(`GroundBitSpots`)를 따로 기록해 검사), `MapCheck` 24, `ModeCheck` 123, `SpawnCheck` 41, `SupplyCheck` 12, `NetSyncCheck` 32, `MenuCheck` 44, `LobbyCheck` 31, `CardLabCheck` 85, `CompanionCheck` 39, `SquadCheck` 50, `Integration` 148 통과.
- 렌더 `output/transition-5-arrival.png` · `transition-7-forest/bridge/yard/safehouse.png`(전후 비교, 이전은 로컬 `output/before-ground/`): 흙 공터 · 흙길 경계가 풀밭으로 들쭉날쭉 섞이고 풀 다발 · 돌이 선을 끊음, 아스팔트는 단단한 경계 + 흔들리는 자갈 갓길 + 점선. 첫 렌더의 네모 색 얼룩 · 풀밭에 놓인 자갈 조각은 고친 뒤 다시 확인. 사용자 피드백 2차(판 없는 돌, 풀밭 전체 흩뿌림) 뒤 다시 렌더해 돌 판 테두리가 없어지고 풀이 길 주변에만 몰리지 않는 것을 확인. 성능 · 섞임 지도 굽는 시간은 측정 전(I-93).

### 이번 변경 확인 기록 — 2026-10-22 (갈 수 없는 곳 채우기 1차)

- C# 빌드 경고 · 오류 0. Blender `edge_kit.py` 단언 통과(6종 274~998 삼각형). `StageTransitionCheck` 153(헤드리스 · 창 모드: 키트 42종 목록, 외곽 모델 충돌 · 가림 부위 없음, 외곽 개수 · 그림자 줄 · 물리 없음 · 먼 바닥 · 카메라 경계), `MapCheck` 24, `ModeCheck` 123, `SpawnCheck` 41, `SupplyCheck` 12, `NetSyncCheck` 32, `MenuCheck` 44, `LobbyCheck` 31, `CardLabCheck` 85, `CompanionCheck` 39, `SquadCheck` 50, `Integration` 148 통과.
- 렌더 `output/transition-5-arrival.png` · `transition-7-forest/bridge/yard/safehouse.png`(전후 비교, 이전은 로컬 `output/before-edge/`): 먼 쪽 빈틈 · 바닥 끝 사라짐, 개울 끝이 숲에 묻힘. 성능 측정 · 저사양은 미확인(I-92).

### 이번 변경 확인 기록 — 2026-10-22 (소환수 게임 적용 1차)

- C# 빌드 경고 · 오류 0. 새 `CompanionCheck` 39(헤드리스 · 창 모드), `CompanionModelCheck` 253 · `PetModelCheck` 185(드론 날갯짓 폭 변경 뒤), `CardLabCheck` 85(Home 무장 4장 · 소환수 고르기), `ModeCheck` 123(무장 네 번째 소환수 카드 · 합류), `MapCheck` 24, `OnslaughtNetCheck` 19(파티원의 소환수 고르기가 방장에 반영 · 파티원 화면에 두 소환수), `MenuCheck` 44, `LobbyCheck` 31, `NetSyncCheck` 32, `NetCodecCheck` 12, `HitFlashCheck`, `EnemyAttackCheck` 79, `SpawnCheck` 41, `SquadCheck` 50, `InventoryCheck` 41, `WeaponHandlingCheck` 36, `CombatPacingCheck` 44, `Integration` 148 통과.
- 렌더 `output/companion-follow.png`(꿀벌 드론 · 거위 부사수 · 쌍열 블래스터 강아지가 생존자 곁), `companion-fight.png`. 처음 크기(펫 0.5 · 드론 0.25)는 너무 작아 0.6 · 0.4로 키움. 모델 자체를 고칠 문제는 보이지 않아 이번에는 Codex 작업 없음. 손으로 해 본 체감 · 2인 플레이는 미확인(I-89).

### 이번 변경 확인 기록 — 2026-10-21 (꽥 · 고무오리 전용 모델 · 아이콘)

- Blender `gadget-models.py` 내보내기 검사 통과(경적 삼각형 1,568 · 메시 3 · 재질 4, 오리 2,444 · 4 · 4, `Muzzle` 앞쪽 거리 = `length_m`). `CardLabCheck` 84(헤드리스) · 85(창 모드, 근접 렌더 포함), `ModeCheck` 120, `OnslaughtNetCheck` 16, `MapCheck` 24, `HitFlashCheck`, `EnemyAttackCheck` 79, `SpawnCheck` 41, `NetSyncCheck` 28, `NetCodecCheck` 12, `LobbyCheck` 31, `MenuCheck` 39, `SquadCheck` 50, `InventoryCheck` 41, `WeaponHandlingCheck` 36, `CombatPacingCheck` 44, `SupplyCheck` 12, `ReloadPoseCheck` 279, `Integration` 148 통과.
- 렌더: `game/assets/icons/honk.png` · `rubber_duck.png`, `output/cardlab-arming-3.png` · `cardlab-slot3-honk-close.png` · `cardlab-slot3-duck-thrown.png` · `cardlab-slot3-duck-hand.png`. 이전 모습은 로컬 `output/before-gadget/`.

### 이번 변경 확인 기록 — 2026-10-21 (3번 칸 꽥 · 고무오리)

- C# 빌드 경고 · 오류 0. `CardLabCheck` 84(헤드리스, 2회) · 85(창 모드: 꽥이 옆 몹을 밀쳐내고 피해 · 8초 충전, 고무오리가 조준점에 떨어져 미끼 · 14초 충전 · 펑, 유리대포 검사는 레벨업 반복 카드를 고려해 상대 비교로 고침), `ModeCheck` 120(헤드리스) · 124(창 모드: 풀 · 무기별 쿨타임 데이터 · 나중에 든 꽥도 자기 쿨타임 · 잘못된 쿨타임 보고), `InventoryCheck` 41(처음엔 꽥 · 오리에 탄 종류가 없어 실패 → 예비탄 무제한으로), `OnslaughtNetCheck` 16, `MapCheck` 24, `HitFlashCheck`, `EnemyAttackCheck` 79, `EnemyExpansion` 60, `SpawnCheck` 41, `NetSyncCheck` 28, `NetCodecCheck` 12, `LobbyCheck` 31, `MenuCheck` 39, `SquadCheck` 50, `WeaponHandlingCheck` 36, `CombatPacingCheck` 44, `SupplyCheck` 12, `ReloadPoseCheck` 279, `Integration` 148 통과.
- 렌더: `output/cardlab-arming-3.png`(3번 칸 카드에 꽥 · 고무오리 · 레일건), `cardlab-slot3-honk.png`(꽥 고리 · "꽥!" · 충전 8초, 조준점 경고 없음), `cardlab-slot3-duck.png`(오리 미끼 · 충전 14초). 손으로 해 본 체감 · 2인 플레이는 미확인(I-87).

### 이번 변경 확인 기록 — 2026-10-21 (실험실 피드백 반영 1차)

- C# 빌드 경고 · 오류 0. `CardLabCheck` 79(헤드리스) · 80(창 모드, 2회: 풍선탄 3방 부풂 · 4방째 펑, 샷건 한 발은 한 번만 부풂, 네임드는 안 부풀고 펑 피해만, 폭발 범위 · 대미지 강화와 최대 레벨, 찍찍이 덩어리 강화, 소총 명중은 큐볼 아님 · 저격은 큐볼 · 산탄 볼링, 카드 없는 팀원 명중엔 안 걸림, 덩어리 같이 펑, 레벨업 제안: 샷건이면 산탄 볼링 · 고른 카드는 강화만 · 저격 · 샷건 없으면 당구 없음 · 꽥 · 오리 없음), `ModeCheck` 117, `OnslaughtNetCheck` 16, `MapCheck` 24, `HitFlashCheck`, `EnemyAttackCheck` 79, `EnemyExpansion` 60, `SpawnCheck` 41, `NetSyncCheck` 28, `NetCodecCheck` 12, `LobbyCheck` 31, `MenuCheck` 39, `SquadCheck` 50, `InventoryCheck` 41, `WeaponHandlingCheck` 36, `CombatPacingCheck` 44, `Integration` 148 통과.
- 렌더 `output/cardlab-levelup.png`(SVD를 들면 "관통 당구 · 나 · 저격 전용" 카드), `cardlab-clump-puff.png`. 실제 손으로 해 본 체감 · 2인 플레이의 카드 소유는 미확인(I-85).

### 이번 변경 확인 기록 — 2026-10-21 (중간 보스 전용 모델)

- 최적화(Blender `--midboss-only --no-preview`) 단언 통과: 풍선 대왕 삼각형 3,752 · 재질 6, 토사물 어미 3,352 · 재질 5, 노드 · 두께 조건. C# 빌드 경고 · 오류 0. `ModeCheck` 117(헤드리스) · 121(창 모드), `MapCheck` 24, `OnslaughtNetCheck` 16, `CardLabCheck` 68, `HitFlashCheck`, `EnemyAttackCheck` 79, `EnemyExpansion` 60, `CorpseTierCheck` 36, `CorpseReactionCheck` 29, `SpawnCheck` 41, `NetSyncCheck` 28, `NetCodecCheck` 12, `LobbyCheck` 31, `MenuCheck` 39, `SquadCheck` 50, `InventoryCheck` 41, `WeaponHandlingCheck` 36, `CombatPacingCheck` 44, `Integration` 148(적 외형 목록 검사를 11개로 갱신) 통과.
- 렌더: `output/lineup-bosses-front/side.png`, `lineup-boss_balloon_king-close.png` · `lineup-boss_spitter_mother-close.png` · `lineup-boss_bull_giant-close.png`(`MonsterLineupCapture`), 게임 화면 `mode-survivor-boss_balloon_king.png` · `mode-survivor-boss_spitter_mother.png`(`ModeCheck` 창 모드). 이전 모습은 로컬 `output/before-midboss/`. 손으로 싸워 본 보스 체감은 미확인(I-85).

### 이번 변경 확인 기록 — 2026-10-21 (20분 판 내용 1차)

- C# 빌드 경고·오류 0. `ModeCheck` 117개(헤드리스, 2회)·121개(창 모드, 2회: 중간 보스 후보 · 보스 목록 오류, 구간마다 일반 체력 증가 · named 증가 · 마지막 구간 표, 보스전 동안 구간 시계 멈춤 · 다음 구간에서 이어짐, 중간 보스가 겹치지 않음, 풍선 대왕의 풍선 소환, 보스 이름 막대 · 알림, 최종 보스 "최종 보스 …"), `OnslaughtNetCheck` 16(2회), `MapCheck` 24, `CardLabCheck` 68, `HitFlashCheck`, `EnemyAttackCheck` 79, `SpawnCheck` 41, `NetSyncCheck` 28, `LobbyCheck` 31, `MenuCheck` 39, `SquadCheck` 50, `InventoryCheck` 41, `WeaponHandlingCheck` 36, `CombatPacingCheck` 44, `NetCodecCheck` 12, `Integration`(`Start-Game.ps1 -Test`) 148 통과.
- 렌더: `output/mode-survivor-boss_balloon_king.png`(풍선 대왕 2.6배, 막대 · 알림 이름), `mode-survivor-boss_bull_giant.png`(거대 황소 2.3배, 앞 구간 풍선 대왕 시체 유지). 토사물 어미는 무작위 순서상 두 번의 창 모드 실행에서 나오지 않아 렌더 없음. 20분 판을 손으로 해 본 난이도 · 보스 체감은 미확인(I-85).

### 이번 변경 확인 기록 — 2026-10-21 (정식 난전 카드 흐름 1차)

- C# 빌드 오류 0. `ModeCheck` 101개(헤드리스)·105개(창 모드: 무장 카드 3번, 레벨업, 네임드 경험치, 구간 보스 4번, 최종 보스 클리어), 새 `OnslaughtNetCheck` 16개(한 PC ENet 방장 + 파티원: 무장 카드 전달 · 고른 카드로 방장이 무장, 레벨업 카드, 중간 보스 특별 카드 · 다음 구간, 2회 실행 모두 통과), `MapCheck` 24, `CardLabCheck` 68, `NetSyncCheck` 28, `NetCodecCheck` 12, `LobbyCheck` 31, `MenuCheck` 39, `SquadCheck` 50, `InventoryCheck` 41, `WeaponHandlingCheck` 36, `EnemyAttackCheck` 79, `SpawnCheck` 41, `CombatPacingCheck` 44, `Integration`(`Start-Game.ps1 -Test`) 148 통과.
- 렌더: `output/mode-survivor-arming.png`(1번 칸 무장 카드, "보스 1/4까지 3:00", Lv 막대, 3번 칸 타일), `mode-survivor-levelup.png`(레벨 업! 카드 3장: 반복 카드 + 실험실 카드), `mode-survivor-swarm.png`(무리 · 경험치 막대 · 박격포 3번 칸). 2인 실제 플레이 · 20분 판 전체 손으로 해 보기는 미확인(I-85).

### 이번 변경 확인 기록 — 2026-10-21 (개편 6단계, 거점 배낭)

- C# 빌드 경고·오류 0. 새 `InventoryCheck` 41개(헤드리스)·43개(창 모드, 실제 마우스 드래그로 장착·바닥 버리기 포함) 통과( 탄 종류 표, 배낭 쌓기·꺼내기·합치기·바꾸기, 바닥 무기 장착/배낭, 드래그 장착·넣기·바닥 버리기, 배낭 탄으로 장전, 탄약 더미, G·Q, 구급상자 자동 보충, 일부만 줍기, 네임드 전리품, 배낭 화면). `NetCodecCheck` 12(배낭 왕복 추가), `ModeCheck` 79(헤드리스)·81(창 모드), `Integration`(`Start-Game.ps1 -Test`) 148, `MapCheck` 25, `NetSyncCheck` 28, `LobbyCheck` 31, `MenuCheck` 39, `SquadCheck` 50, `SpawnCheck` 41, `CombatPacingCheck` 44, `WeaponHandlingCheck` 30, `SupplyCheck` 12, `InputCheck` 14(창 모드. 헤드리스로 잘못 돌렸을 때는 마우스 조준 검사가 실패 — 창 전용 검사라 정상) 통과.
- 렌더 `output/inventory-open.png`: 배낭 화면(장착 칸 1·2·3·G·Q, 4×5 칸, 무기 아이콘·탄 묶음 색·투척물 도형·구급상자, 안내 문구)과 네임드 전리품. 마우스 드래그는 창 모드 검사의 가상 입력으로 확인(헤드리스 Godot은 GUI 판정을 하지 않아 생략). 2인 멀티 실제 플레이 미확인(I-81).

### 이번 변경 확인 기록 — 2026-10-21 (카드 실험실 1차)

- C# 빌드 경고·오류 0. 새 `CardLabCheck` 37개 통과(헤드리스·창 모드: 실험실 규칙, F키 조작, 패널, 카드 6장 각각, 찍찍이 + 풍선 덩어리가 같이 뜨고 같이 터짐). 회귀: `Integration`(`Start-Game.ps1 -Test`) 148, `ModeCheck` 79, `MapCheck` 25, `InventoryCheck` 41, `NetSyncCheck` 28, `NetCodecCheck` 12, `CombatPacingCheck` 44, `EnemyAttackCheck` 79, `WeaponHandlingCheck` 30, `SpawnCheck` 41, `SquadCheck` 50, `CorpseRetentionCheck` 16, `HitFlashCheck`, `EnemyExpansion` 60, `SupplyCheck` 12 통과.
- 렌더 `output/cardlab-chaos-110.png`·`cardlab-chaos-200.png`(모든 카드를 켠 싸움: 부푼 몹 · 찍찍이 고리 · 삑! · 딱! · 실험실 패널), `cardlab-clump-float.png`(떠오른 덩어리, 화면 왼쪽 위). 손으로 해 본 재미 · 가독성은 미확인(I-82).

### 이번 변경 확인 기록 — 2026-10-21 (총 분류별 조준 시야)

- 새 열 `aim_view`(C-26): 샷건은 조준해도 화면이 거의 그대로(2.8 m), 소총은 그대로(11.8 m), 저격은 화면이 넓어지며(크기 17 → 26.4) 소총의 약 1.55배까지 보임. 특수무기는 들고 있을 때만 느려짐을 검사로 확인(이미 그렇게 동작).
- `WeaponHandlingCheck` 36개 통과(헤드리스 · 창 모드), 렌더 `output/weapons-aim-r870.png` · `weapons-aim-m4.png` · `weapons-aim-awm.png`.
- 회귀: `Integration` 148, `ModeCheck` 79, `MapCheck` 25, `MenuCheck` 39, `NetSyncCheck` 28, `CombatPacingCheck` 44, `SquadCheck` 50, `SupplyCheck` 12, `InventoryCheck` 41, `CardLabCheck` 37, `InputCheck` 14(창 모드) 통과.

### 이번 변경 확인 기록 — 2026-10-21 (카드 창 1차)

- C# 빌드 경고·오류 0. `CardLabCheck` 57개(헤드리스)·58개(창 모드, 카드 실제 클릭 포함) 통과: Home 치트 초기화 → 1 · 2 · 3번 칸 무장 카드(닫기 없음, 1 · 2번 칸 총이 겹치지 않음, 3번 칸 특수무기), 키 1 · 2 · 3과 클릭으로 고르기, 레벨업 카드 F로 미루기 · + 배지 ×2 · 같은 3장 유지 · 받은 순서 유지.
- 회귀: `Integration` 148, `ModeCheck` 79, `MenuCheck` 39, `NetSyncCheck` 28, `LobbyCheck` 31, `InventoryCheck` 41, `WeaponHandlingCheck` 36, `SquadCheck` 50, `InputCheck` 14(창 모드) 통과. 카드 고른 클릭이 사격으로 이어지지 않게 한 수정 뒤 `CardLabCheck` · `InventoryCheck` · `ModeCheck` · `InputCheck`를 다시 돌려 통과.
- 렌더 `output/cardlab-arming-1.png`(1번 칸 무기 카드 3장), `cardlab-levelup.png`(레벨업 카드와 ✕), `cardlab-plus-badge.png`(+ ×2 F 배지). 손으로 해 본 느낌은 미확인.

### 이번 변경 확인 기록 — 2026-10-21 (훈련용 통나무 · 특별 카드 치트)

- `CardLabCheck` 68개(헤드리스)·69개(창 모드) 통과: L 일반 · K 특별 카드가 받은 순서대로 쌓임, 특별 카드 3장(유리 대포 피해 2배, 마지막 한 발 폭발, 헬륨 파티는 일반 몹만 띄움), T 통나무가 커서 자리에 서고 어떤 피해 · 풍선 펑에도 죽지 않고 제자리, 몹 지우기에도 남음, Shift+T로 치움.
- 회귀: `Integration` 148, `ModeCheck` 79, `EnemyAttackCheck` 79 통과.
- 렌더 `output/cardlab-special.png`(금색 특별 카드 창), `cardlab-training-log.png`(통나무 피해 숫자 · DPS — 검사 중 9999 밀치기가 섞여 DPS가 크게 보임), 죽는 모습 확인용 `output/death-*.png`(I-83).

### 이번 변경 확인 기록 — 2026-10-21 (죽는 모습 1차, 코드만)

- 늘어진 시체 자세 · 날아가는 거리 줄임 · 착지 찌그러짐 · 네임드 넘어지기(I-83). `CorpseReactionCheck` 29개(새 3개: 네임드가 선 자세에서 넘어짐, 넘어지는 동안 바닥 아래로 안 들어감, 몸은 그대로 고정 자세로 끝남), `CorpseTierCheck` 36, `CorpseBoundsCheck` 1, `CorpsePileCheck` 2, `CorpseRetentionCheck` 16, `EnemyExpansion` 60, `CombatPacingCheck` 44, `ModeCheck` 79, `CardLabCheck` 68, `NetSyncCheck` 28, `EnemyAttackCheck` 79, `SpawnCheck` 41, `Integration` 148 통과.
- 렌더 `output/death-*.png`(`tests/DeathCapture.tscn`): 시체가 쓰러진 자리 근처(몇 m 안)에 발을 벌리고 누움. 손으로 해 본 느낌은 미확인.

### 이번 변경 확인 기록 — 2026-10-21 (보스 몸동작, A-18)

- 자세 8종(`crouch_leap` `land` `rear_roar` `sweep_aim` `recoil` `shake_off` `paw_ground` `gallop`)을 `boss.json`에서 이름으로 고름. 내려찍기 점프 착지(먼지 고리 · 히트스톱), 0.9초 포효 소환(땅에서 솟음), 돌진 앞발 긁기 · 출렁 · 먼지, 페이즈 포효 · 몸 털기, 마지막 페이즈 콧김.
- `ModeCheck` 86개(헤드리스)·90개(창 모드) 통과: 모르는 자세 · 음수 값 오류 보고, 보스 데이터의 동작 이름, 2.4 m 높이로 뛰어 대상이 서 있던 곳(몸이 겹칠 수 없어 0.8 m 옆)에 착지, 페이즈 포효, 포효 뒤 소환. 회귀: `Integration` 148, `EnemyAttackCheck` 79, `EnemyExpansion` 60, `NetSyncCheck` 28, `CardLabCheck` 68, `MapCheck` 25, `CombatPacingCheck` 44, `HitFlashCheck`, `CorpseReactionCheck` 29 통과.
- 렌더 `output/mode-survivor-slam.png`(웅크려 팔 올림) · `slam-air.png`(공중) · `slam-land.png` · `phase-roar.png` · `summon-roar.png` · `charge-paw.png` · `charge-gallop.png` · `fan.png`. 손으로 해 본 느낌 · 파티원 화면은 미확인(I-77).

### 이번 변경 확인 기록 — 2026-10-21 (네임드 황소 · 풍선 정식 모델, A-17 2차)

- Codex 모델 `bull.glb`(삼각형 2,644) · `balloon.glb`(2,264), 몸통 앞뒤 깊이 ≥ 폭 0.8 · 관절 피벗(팔 · 다리 · 발 · 꼬리). `bull.tres`·`balloon.tres`가 모델을 씀.
- `EnemyExpansion` 60, `EnemyAttackCheck` 79, `CorpseTierCheck` 36, `CorpseReactionCheck` 29, `CorpseRetentionCheck` 16, `HitFlashCheck`(모델 있는 황소는 생성 팔 없음 검사 추가), `ModeCheck` 86, `SpawnCheck` 41, `CombatPacingCheck` 44, `NetSyncCheck` 28, `CardLabCheck` 68, `VisionTierCheck` 23, `Integration` 148 통과.
- 렌더 `output/lineup-*.png`(`tests/MonsterLineupCapture.tscn`, 빈 바닥 · 시야 끔 · 조명): 앞 · 옆 · 뒤, 황소 돌진 준비, 풍선 부풂 · 펑.

### 이번 변경 확인 기록 — 2026-10-21 (M4 발사 시각 효과 1차, A-19)

- M4 전용 흰/노란 총구 섬광과 앞으로 이동하는 짧은 탄 잔상. 히트스캔·피해·탄약·소리·사거리 계약 유지, 로컬/수신 발사에 연결. 다른 무기·근접·피격·사망·UI는 아직 적용하지 않았다.
- C# 빌드 경고 0·오류 0, Godot 가져오기 성공. `RifleEffectsCheck` 헤드리스 17개·창 모드 17개, `WeaponHandlingCheck` 36개, `NetSyncCheck` 28개, `Integration` 148개 통과. 코드 리뷰에서 수정이 필요한 문제 없음.
- Forward+ 실제 렌더: `output/rifle-fx-muzzle.png`, `rifle-fx-travel.png`, `rifle-fx-end.png`. `RifleEffectsCheck` 창 모드가 효과의 표시 시간을 60 fps로 고정한 72프레임을 `output/rifle-fx-frames/`에 출력한다. 이번 로컬 미리보기 `output/rifle-fx-preview.gif`는 이 프레임을 잘라 2배 느리게 보여 준다(실제 플레이 녹화 아님).
- 종료 경고는 별도: 무기 조작 검사 8개(`--verbose`: AudioStreamWAV 4개 + AudioStreamPlaybackWAV 4개), 통합 4개, 전용 헤드리스 재실행 2개 ObjectDB 잔존. 전용 창 모드·멀티 검사는 종료 경고 없음. 기존 I-17을 해결했다고 보지 않는다. 6인 장시간 렌더 성능·다른 PC·사용자 체감은 미검증.

### 이번 변경 확인 기록 — 2026-10-22 (다른 무기로 효과 확장, A-19)

- M4의 방향을 다른 총기·근접으로 확장. 산탄은 여러 궤적과 섬광/조명 1개, 점사는 각 발마다, 쌍권총은 좌우 총구 교대, 레일건은 청백색 빔. 알라봉·박격포 실제 투사체는 유지하고 발사 섬광 추가. 꽥·고무오리 고유 연출 유지.
- C# 빌드 경고 0·오류 0, Godot 가져오기 성공. `WeaponEffectsCheck` 헤드리스 100개·창 모드 100개, `RifleEffectsCheck` 17개, `TailMeleeCheck` 13,976회(메시 반복 검사 포함), `CombatPacingCheck` 44개, `WeaponHandlingCheck` 36개, `NetSyncCheck` 28개, `Integration` 148개 통과. 코드 리뷰에서 수정이 필요한 문제 없음.
- Forward+ 실제 렌더 `output/weapon-fx-*.png`. 고정된 효과 시간·합성한 끝점으로 비교하는 검사 장면이며 플레이 녹화가 아니다. 비교 이미지 `output/weapon-fx-comparison.png`는 로컬 렌더 산출물을 묶은 미리보기다.
- 종료 ObjectDB 잔존: 새 전용 헤드리스 2개·M4 2개·무기 조작 8개·멀티 12개·통합 2개. 이번 실행에서는 객체별 식별을 하지 않았고 I-17은 미해결. 새 전용 창 모드·근접·전투 속도 검사는 종료 경고 없음. 기존 원격 근접 표시 시점·원격 치명타 정보 제약, 6인 장시간 성능·다른 PC 확인은 [무기 규격 6.3](weapon-spec.md#weapon-effects)에 기록했다.

### 이번 변경 확인 기록 — 2026-10-22 (피격·사망 이펙트, A-19·C-19)

- 흰 몸 점멸은 유지하고 6방향 피격 타격선(0.16초)·8방향 사망 섬광/확산 링(0.32초)을 추가했다. 피격은 캐릭터마다 하나 재사용, 사망은 작은 피격선을 교체하고 독립적으로 소멸. 일반·네임드·풍선·플레이어와 기존 수신 경로에 적용, 다운·시체 보존·관전·피해 판정 유지. 숨겨진 적의 신규 사망 섬광은 차단한다.
- 빌드 경고/오류 0·Godot 가져오기 성공. `DamageEffectsCheck` 헤드리스/창 모드 각 24개, `HitFlashCheck`, `CorpseReactionCheck` 29개, `CorpseTierCheck` 36개, `VisionTierCheck` 23개, `Integration` 148개 통과. `NetSyncCheck` 첫 병렬 실행의 위치 추적 실패(평균 2.07m, 최대 4.80m)는 원인 미확정이고, 코드 변경 없이 단독 재실행 28개 통과. 코드 리뷰의 숨김 사망 표시 지적은 재현 검사와 가시성 조건으로 수정 후 재확인했다.
- Forward+ `CaptureDamageEffects` 150프레임 확인. `output/damage-fx-comparison.png`·`damage-fx-preview.gif`는 통제된 실제 엔진 렌더에서 만든 로컬 미리보기(GIF 2배 느리게). `DamageEffectsCheck` 창 모드의 일반·네임드·풍선·플레이어 스틸도 생성한다.
- 종료 ObjectDB 경고: 최종 전용 헤드리스 4개(AudioStreamWAV 2 + AudioStreamPlaybackWAV 2), HitFlash 2개, 시체 등급 11개, 시야 4개, 통합 2개, 멀티 단독 12개. 전용 최종 창 모드·시체 반응·프레임 렌더는 경고 없음. 기존 I-17 미해결이며 실제 2PC·6인 장시간/저사양 성능·UI 이펙트는 이번에 검증/개편하지 않았다.

### 이번 변경 확인 기록 — 2026-10-22 (피격 색·투명도 피드백, A-19·C-19)

- 일반 흰색/치명타 노란색으로 몸·타격선·사망 원을 통일하고 선·원 최고 불투명도 90% 적용. 몸 점멸 최고 불투명도 72%·지속시간 유지. 치명타 뒤 일반색 복귀, 특수 사망·카드 콜백의 치명 처치 색 보존. 네트워크 적 피격/사망에 치명타 U8 전달, 프로토콜 6.
- 빌드 경고/오류 0. `DamageEffectsCheck` 31개, `HitFlashCheck`, `NetSyncCheck` 32개, `NetCodecCheck` 12개, `LobbyCheck` 31개, `Integration` 148개 통과. `CaptureDamageEffects --fixed-fps 60 --disable-vsync`의 Forward+ 150프레임과 `output/damage-color-comparison.png` 비교 렌더 확인.
- 전용 4개·멀티 14개·통합 2개 ObjectDB 종료 경고(I-17), 객체별 미식별. 나머지 위 검사·렌더는 종료 경고 없음. 초기 실패·카드 콜백 수정·멀티 검사 가시성 조건 수정은 `issues.md`에 기록했다. 실제 2PC·장시간 부하 미검증.
