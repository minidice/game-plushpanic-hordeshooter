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
- [ ] 난전 개편(M-27, 계획만): 진열대 → 카드 선택, 소환수(드론 · 부사수 · 펫), 레벨업 일반 카드 · 보스 특별 카드, 총 분류별 풀, 하데스식 카드 창 · + 아이콘 · 버프 줄 ([계획](onslaught-cards-plan.md))
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

빌드·가져오기 후 Godot 테스트 장면: `CombatPacingCheck.tscn`, `EnemyAttackCheck.tscn`, `TailMeleeCheck.tscn`, `HitFlashCheck.tscn`, 생존·서바이버는 `ModeCheck.tscn`, 거점 배낭은 `InventoryCheck.tscn`, 카드 실험실은 `CardLabCheck.tscn`. 실제 화면 검사는 별도 캡처 장면과 플레이로 보완한다. 수치나 동작을 바꿀 때 해당 검사와 문서도 같이 갱신한다.

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

