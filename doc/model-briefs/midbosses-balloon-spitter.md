# 모델 지시문: 난전 중간 보스 "풍선 대왕" (`boss_balloon_king`) · "토사물 어미" (`boss_spitter_mother`)

> **상태**: 2026-10-21 작성. Codex CLI(`codex exec`)에 맡기는 세 번째 몬스터 모델 작업(decisions.md A-17, 앞선 작업은 [boss.md](boss.md) · [named-bull-balloon.md](named-bull-balloon.md)). 한 번의 Codex 실행에서 두 모델을 **차례로** 만든다.
> **왜**: 난전 중간 보스(decisions.md M-27 · Q-24)는 지금 네임드 모델(풍선 `balloon.glb`, 토사물 감염체 `spitter.glb`)을 2.6배 · 2.2배로 키운 임시 외형이라 "보스"로 읽히지 않는다. 거대 황소(`boss_bull_giant`)는 네임드 황소를 키운 그대로 둔다.
> **검토자**: Claude가 결과를 게임에 붙이고 실제 렌더 · 검사로 확인한다. Codex는 모델 생성 코드와 원본 GLB까지만 만든다.

## 1. 공통 규칙 (두 모델 모두)

- 분위기: 플레이어(거위 · 오리 · 라쿤 · 랫서판다) · 감염체 · 보스처럼 **귀엽고 우스꽝스러운 봉제 인형**(`doc/character-model-spec.md` 6장, `doc/monster-design-guide.md`). 무섭거나 사실적이지 않게. 피 · 상처 · 역겨운 표현 없음(토사물 어미도 "볼이 빵빵한 심술쟁이" 정도).
- **보스 표시**: 두 모델 모두 네임드 원본과 한눈에 구분되는 **보스 장식**(왕관 · 망토 · 큰 덩어리 등)을 단다. 쿼터뷰 55°, 직교 크기 17에서 "보스"로 읽혀야 한다.
- **두께 규칙**: 카메라는 55° 위에서 비스듬히 내려다본다. 앞뒤가 얇으면 종이처럼 보인다(I-84).
  - **몸통 앞뒤 깊이 = 몸통 폭(팔 제외)의 80~100%**. 머리(있다면) 앞뒤 깊이 = 머리 폭의 85% 이상.
  - 큰 덩어리를 Z축으로 0.9배 미만으로 누르지 않는다. 얼굴 부품은 표면에서 **앞으로 튀어나오게**.
- **노드 구조** (런타임이 이 이름을 찾아 움직임 — 리깅 · 본 없음, 네임드 황소 · 풍선과 같은 방식):

```
<id>_root
└─ Body                     (발바닥 높이, 몸 중심)
   └─ Deform                (발바닥 높이, 납작해지는 스쿼시 기준)
      ├─ Head               (목 위치 — 보블헤드 스프링)
      │  └─ Muzzle          (입 앞 — 부채꼴 탄막이 나오는 곳)
      ├─ boss_leftArm / boss_rightArm   (어깨 피벗 — 팔 메시를 이 아래에. 이름은 보스 공용, id를 붙이지 않음)
      ├─ <id>_leftLeg / <id>_rightLeg   (엉덩이 관절 피벗 — 다리 메시를 이 아래에)
      │  └─ LeftFoot / RightFoot        (각 발 중심 피벗, 발 메시를 이 아래에)
      └─ Weapon             (가슴 앞, 비어 있어도 됨)
```

  - **팔 피벗 이름은 `boss_leftArm` / `boss_rightArm`** (두 모델 모두). 런타임 보스 몸동작(`game/scripts/Characters/CharacterBossPose.cs`)이 이 이름으로 팔을 올리고 벌린다(내려찍기 · 포효 · 조준). 팔은 어깨에서 위로 들 수 있는 길이와 모양으로.
  - **정면 +Z**, 위 +Y, 단위 미터, 원점 = 발바닥. glTF 그대로(`ModelFacesPositiveZ = true`).
  - 메시 이름은 자유. 최적화 단계가 피벗별로 메시를 합친다(팔 · 다리 · 발 메시는 각 피벗 아래에 보존).
- 생성 코드는 앞선 모델처럼 `SphereGeometry` 타원체, `CapsuleGeometry`, `TorusGeometry`, `ConeGeometry`, `CylinderGeometry` 정도만 쓴다. 스타일 · 도우미 함수는 `prototypes/character-lab/tools/named-models.mjs` · `boss-models.mjs` · `infected-models.mjs`를 참고(복사해도 됨).
- **크기**: 두 모델 모두 키 약 **2.0 m**(장식 끝 포함 약 2.2 m). 게임이 1.5배로 키운다(`content/data/bosses/<id>.json`의 `scale`). 충돌은 모델이 아니라 `.tres`의 캡슐이 맡는다(반지름 0.7, 높이 1.9).
- **예산**: 최적화 후 모델마다 삼각형 **4000개 미만**, 재질 3~5개.

## 2. 풍선 대왕 (`boss_balloon_king`)

- **하는 일**: 돌진하지 않고 느리게 다가오며 입에서 **넓은 부채꼴 탄막**(9발), 두 팔을 벌려 **풍선 부하 셋을 소환**, 66%부터 **통통 튀어 올라 내려찍기**. 스킬 규격 `doc/mode-monster-tables.md` 3장, 데이터 `game/content/data/bosses/boss_balloon_king.json`.
- **네임드 풍선 기생충(`balloon`)과의 관계**: 그 풍선 생물들의 왕. 같은 계열 색이지만 훨씬 크고 위엄 있게.
- **외형**: 작고 통통한 봉제 몸통 위에 **아주 커다란 풍선 머리**(지름 약 1.3 m, 앞뒤도 같은 지름, 위쪽 꼭지에 묶은 매듭)가 달린 왕.
  - 풍선 머리 앞면에 크고 동그란 만화 눈 두 개(살짝 거만하게 반쯤 감은 눈꺼풀), 작고 동그란 **오므린 입**(탄막이 나오는 곳, `Muzzle`는 이 입 바로 앞), 옆에 반짝이는 하이라이트 점.
  - **금 왕관**: 풍선 머리 꼭대기 매듭 옆에 비스듬히 얹힌 작은 금관(뾰족한 끝 5개 + 끝마다 작은 공).
  - **빨간 망토**: 어깨에서 등 뒤로 늘어진 짧은 망토(가장자리에 흰 털 테두리 — 토러스나 납작한 캡슐 띠).
  - **팔**: 짧고 통통한 팔 두 개. 오른손에는 **작은 풍선 다발**(지름 0.25 m 풍선 3개, 서로 다른 색: 노랑 · 하늘 · 연두, 짧은 끈)을 쥔다 — 풍선 다발 메시는 `boss_rightArm` 아래에.
  - **다리**: 짧고 굵은 다리, 둥근 발.
- **색**(재질 4~5개): 몸 분홍 `#e86e9e`, 풍선 머리 밝은 분홍 `#f7a9c8`(약간 반짝이는 재질), 금장 `#e2b34a`, 망토 빨강 `#c23b3b`(털 테두리는 눈 흰자와 같은 크림 재질을 써도 됨), 눈 흰자 · 털 `#fff2e4`, 눈동자 · 입 잉크 `#4d1a2e`. 작은 풍선 다발 색은 정점 색이 아니라 기존 재질을 나눠 쓰거나 재질 하나를 더 써도 됨(재질 5개 이내).

## 3. 토사물 어미 (`boss_spitter_mother`)

- **하는 일**: 볼을 부풀렸다가 입에서 **굵은 탄 다섯 발 부채꼴**, 두 팔을 들어 **제자리 내려찍기**, 66%부터 포효하며 **일반 몹 소환**. 데이터 `game/content/data/bosses/boss_spitter_mother.json`.
- **네임드 토사물 감염체(`spitter`)와의 관계**: 그들의 어미. 같은 감염체 계열 색 · 둥근 후드 느낌을 이어받되 훨씬 크고 뚱뚱하게.
- **외형**: **물방울 / 배 모양의 아주 뚱뚱한 감염체 봉제 인형**. 몸통이 크고 둥글며(폭 약 1.4 m, 앞뒤 깊이 1.2 m 이상), 머리는 몸 위쪽에 반쯤 묻혀 이어진다(목 경계 없음, D-5).
  - **얼굴**: 빵빵하게 부푼 두 볼(앞 · 옆으로 튀어나온 공), 앞으로 내민 **동그란 입 깔때기**(짧은 원통 + 테두리 토러스, `Muzzle`는 그 끝), 작은 졸린 눈 두 개와 심술궂은 짧은 눈썹.
  - **보스 장식**: 머리에 감염체 계열의 **둥근 후드**(머리를 덮는 반구 + 테두리)와 그 위에 꿰맨 자국처럼 보이는 **천 조각 패치 두세 개**(살짝 튀어나온 납작한 타원). 등에 **부하 주머니 세 개**(연두색 둥근 주머니, 몸에 반쯤 붙음 — 소환한 부하들이 나오는 곳으로 읽히게). 배에 크림색 원형 패치.
  - **팔**: 짧지만 굵은 팔 두 개와 둥근 큰 손(내려찍기가 읽히게).
  - **다리**: 아주 짧고 굵은 다리, 넓은 발.
- **색**(재질 4~5개, 감염체 토사물 계열 `infected-models.mjs`의 spitter 색을 이어받음): 몸 `#c1ca79`, 후드 · 손 · 발 `#688c77`, 짙은 부분(눈 · 눈썹 · 입 안 · 꿰맨 선) `#476654` 또는 잉크 `#2d2622`, 부하 주머니 연두 `#a8d46f`, 배 패치 · 입 테두리 크림 `#efe6c4`.

## 4. 만들 것 (Codex 작업 범위)

1. `prototypes/character-lab/tools/midboss-models.mjs` — `createBalloonKing()`과 `createSpitterMother()`가 위 구조의 three.js `Group`(`boss_balloon_king_root`, `boss_spitter_mother_root`)을 돌려준다.
2. `prototypes/character-lab/tools/export-game-roster.mjs` — **`--midboss-only`** 옵션을 추가해 `output/roster/boss_balloon_king-source.glb`와 `output/roster/boss_spitter_mother-source.glb`만 내보낸다. 기존 플레이어 · 감염체 · `--boss-only` · `--named-only` 동작은 바꾸지 않는다.
3. `prototypes/character-lab/tools/optimize_game_roster.py` — **`--midboss-only`** 옵션을 추가해 두 모델만 처리하고 `game/assets/enemies/boss_balloon_king/boss_balloon_king.glb` · `boss_balloon_king-report.json`, `game/assets/enemies/boss_spitter_mother/boss_spitter_mother.glb` · `boss_spitter_mother-report.json`을 쓴다. 네임드 분기처럼 팔 · 다리 · 발 메시를 각 피벗 아래에 보존한다(풍선 다발은 `boss_rightArm` 아래). 감축 비율은 표면이 각지거나 큰 구에 주름이 생기지 않게 넉넉히(네임드는 0.25 · 0.32를 썼다) 하되 예산 안. assert로 확인: 삼각형 4000 미만, 1장의 노드가 모두 있음, 몸통 앞뒤 깊이 ≥ 폭의 0.8, 머리 앞뒤 깊이 ≥ 폭의 0.85. 다른 모델 처리와 기존 assert는 그대로. **보고 줄(report)의 코드를 고칠 때 문법 · 이름 오류가 없게** — 지난번 이 줄이 깨져 Blender가 중간에 멈췄다.
4. `node tools/export-game-roster.mjs --midboss-only`를 `prototypes/character-lab`에서 실행해 원본 GLB 두 개를 실제로 만든다. **Blender는 실행하지 않는다**(Claude가 실행).

## 5. 하지 말 것

- `game/` 안의 C# · 데이터 · `.tres` · 다른 GLB를 고치지 않는다(Claude가 연결). `doc/`도 고치지 않는다.
- 기존 플레이어 · 감염체 · 보스 · 네임드 모델 코드와 출력을 바꾸지 않는다.
- 커밋 · 푸시하지 않는다. 새 npm 패키지를 설치하지 않는다(`three` 0.180.0 사용).

## 6. 끝났다고 볼 기준

- `prototypes/character-lab/output/roster/boss_balloon_king-source.glb`와 `boss_spitter_mother-source.glb`가 생기고, 1장의 노드가 모두 있다.
- 마지막 메시지에: 바꾼 파일, 모델별 원본 삼각형 수(대략), 노드 목록, 몸통 · 머리의 폭/앞뒤 깊이(미터), 외형에서 고민한 점, 확인하지 못한 것.

## 7. 2차 수정 (Claude 렌더 검토, 2026-10-21)

1차 결과를 게임에 붙여 쿼터뷰로 렌더했다(`game/output/lineup-boss_balloon_king-close.png` · `lineup-boss_spitter_mother-close.png`, `tests/MonsterLineupCapture.tscn`). 카메라는 **위에서 약 55° 내려다보므로 머리 꼭대기와 앞쪽 윗면이 가장 많이 보이고 얼굴 아래쪽 · 옆면은 거의 안 보인다.** 지금은 두 보스 모두 위에서 보면 "분홍 공 · 청록 돔"뿐이라 보스로 읽히지 않는다. 노드 구조 · 예산 · 크기 규칙(1 · 4 · 5장)은 그대로 두고 외형만 고친다.

**풍선 대왕**
- **왕관을 2.5~3배로 크게, 풍선 꼭대기 한가운데에 똑바로** 세운다(지름 약 0.55 m, 높이 0.3 m, 뾰족한 끝 5개 + 끝마다 공, 아래 띠). 위에서 금색 왕관이 제일 먼저 보여야 한다. 꼭지 매듭은 왕관 뒤로 옮겨도 된다.
- **얼굴을 풍선 앞면 윗부분으로**: 눈 중심이 풍선 중심보다 위(풍선 반지름의 +20~30% 높이), 앞쪽(+Z)으로 향하되 약간 위를 보게(약 25° 위로 기울임). 눈은 지금보다 1.4배 크게(흰자 지름 약 0.28 m), 눈썹처럼 반쯤 감은 눈꺼풀. 입은 눈 아래 풍선 앞면 중간쯤.
- **망토가 위에서 보이게**: 어깨를 덮는 둥근 칼라(흰 털 테두리, 위에서 보이는 고리)와 등 뒤로 넓게 퍼지는 빨간 망토(풍선 아래에서 뒤로 비스듬히 펼쳐져 위에서 빨간 면이 보이게).
- **풍선 다발을 크게, 머리 높이 옆으로**: 지름 0.35 m 풍선 3개를 오른팔 끝 위쪽(풍선 머리 옆, 머리 꼭대기 높이 근처)에 띄우고 끈은 손까지. 위에서 노랑 · 하늘 · 연두 세 점이 보이게. 정점 색이 게임에서 보이지 않을 수 있으니 **다발 풍선 색은 재질로**(필요하면 재질 하나 추가, 전체 5개 이내 — 금장 재질을 노랑 풍선에 같이 써도 됨).
- **큰 풍선 머리의 각짐**: 큰 구는 `SphereGeometry(1, 28, 18)` 정도로 촘촘하게 만들고, 최적화에서 **풍선 머리 메시는 덜 줄이도록**(다른 부위보다 높은 비율) 해 위에서 면이 각져 보이지 않게. 예산 4000 안.

**토사물 어미**
- **후드를 머리 뒤쪽 절반만** 덮게 줄인다(뒤통수 · 정수리 뒤쪽, 테두리가 정수리 앞쪽에서 끝남). 위에서 **얼굴(눈 · 볼 · 입 깔때기)이 보여야** 한다.
- **얼굴을 머리 앞쪽 윗면으로**: 눈은 머리 앞면 위쪽(정수리에서 앞으로 기울어진 면), 짙고 큰 눈썹(심술). 두 볼은 지금보다 1.3배 크고 앞 · 옆 · 위로 튀어나오게. 입 깔때기는 앞으로 크게(길이 0.3 m, 테두리 크림 토러스), 위에서 동그란 구멍이 보이게 약간 위로 들어 올림.
- **등 주머니를 크고 밝게**: 지름 0.35~0.45 m 주머니 세 개를 등 위쪽 · 어깨 뒤에 위로 솟게(위에서 연두 원 세 개가 보이게), 주머니마다 작은 매듭 꼭지.
- 배 패치 · 꿰맨 천 조각은 그대로 두되, 천 조각은 후드 대신 몸 위쪽 · 어깨에.

**공통**: 고친 뒤 `node tools/export-game-roster.mjs --midboss-only`로 원본 GLB를 다시 만든다. 1차 때 고친 최적화 스크립트(`optimize_game_roster.py`의 중간 보스 몸통 치수 필터 · 단언 분기 · 감축 비율 0.5)는 Claude가 이미 고쳤으니 **되돌리지 말 것**. 바꿔야 하면(풍선 머리 감축 비율 등) 그 부분만 고친다. Blender · `game/` · `doc/` · 커밋 금지는 그대로.

## 8. 결과 (2026-10-21)

- **Codex 1차**(`codex exec -s workspace-write`): `tools/midboss-models.mjs`(`createBalloonKing()` · `createSpitterMother()`), `export-game-roster.mjs` · `optimize_game_roster.py`에 `--midboss-only`. 원본 삼각형 풍선 대왕 7,376 · 토사물 어미 6,706.
- **Claude가 고친 최적화 스크립트 오류 두 개**(I-86): ① 몸통 치수 필터(`obj.name!=core_body`)가 중간 보스 함수가 아니라 **네임드 함수에 들어가** `--named-only`가 이름 오류로 멈추게 됨 → 두 줄을 제자리로. ② 중간 보스 단언 분기가 앞의 `elif kind!='boss'`(감염체 분기)에 가려 **실행되지 않고** 감염체 단언에서 실패 → 앞 분기에서 중간 보스를 뺌. 감축 비율 0.38 → 0.5(네임드 때처럼 면이 각지지 않게).
- **1차 렌더**(`lineup-boss_*-close.png`): 쿼터뷰에서 풍선 대왕은 분홍 공(왕관 · 얼굴이 옆 · 아래), 토사물 어미는 청록 돔(후드가 얼굴을 덮음)만 보여 보스로 안 읽힘 → 7장 2차 수정.
- **Codex 2차**: 왕관 · 얼굴 · 망토 · 다발과 후드 · 얼굴 · 주머니 재배치, 풍선 머리 28×18 분할 + 감축 0.88(최적화 스크립트의 Claude 수정은 유지됨).
- **Claude 마무리**: 풍선 대왕의 **눈 흰자와 왕관 띠가 풍선 구 안에 묻혀** 끝만 보이던 것을 표면 위로(왕관 y 0.84, 눈 z 0.6), 왕관을 굵게(띠 2개 · 굵은 뾰족 끝), 얼굴 · 털 · 반짝이를 정점 색 대신 재질(`king_cream` · `king_ink`, 재질 6개 — 단언도 풍선 대왕만 6), 다발 풍선은 노랑 · 하늘 · 빨강 재질. 예산 때문에 풍선 대왕 감축 비율 머리 0.8 · 나머지 0.44.
- **최종**: `Blender --background --python tools/optimize_game_roster.py -- --midboss-only --no-preview` → `game/assets/enemies/boss_balloon_king/boss_balloon_king.glb` **삼각형 3,752, 재질 6**(몸통 0.84 × 0.80 m, 풍선 머리 1.32 × 1.35 m), `boss_spitter_mother.glb` **삼각형 3,352, 재질 5**(몸통 1.44 × 1.32 m, 머리 1.28 × 1.73 m). 팔(`boss_leftArm` · `boss_rightArm`) · 다리 · 발 피벗 보존.
- **게임 연결**: `content/enemies/boss_balloon_king.tres` · `boss_spitter_mother.tres`(캡슐 0.7 × 1.9), `monsters.csv`의 외형 `balloon` · `spitter` → 전용 외형, `bosses/*.json`의 `scale` 2.6 · 2.2 → **1.5**, 거대 황소 2.3 → **1.6**(중간 보스 키 약 3 m, 최종 보스 두목 황소 약 3.2 m가 가장 크게).
- **확인**: `tests/MonsterLineupCapture.tscn`에 보스 줄 세우기 · 근접 렌더 추가 → `output/lineup-bosses-front/side.png`, `lineup-boss_balloon_king-close.png`(금관 · 흰 눈 · 입 · 빨간 망토 · 풍선 다발), `lineup-boss_spitter_mother-close.png`(뒤쪽 후드 · 얼굴 · 입 깔때기 · 등 주머니). 이전 모습은 로컬 `output/before-midboss/`.
- **남은 것**: 토사물 어미의 눈은 위에서 작게 보임. 다리 피벗은 아직 런타임이 움직이지 않음(I-83 · A-18). 두 보스 모두 황소 보스 코드(`BossEnemy : BullEnemy`)라 걸음 · 피격 반응은 같음(I-85).
