# 모델 지시문: 난전 보스 "거대 두목 황소" (`boss`)

> **상태**: 2026-10-21 작성. Codex CLI(`codex exec`)에 맡기는 첫 몬스터 모델(decisions.md A-17). 같은 형식으로 다음 보스·네임드 지시문을 만든다.
> **검토자**: Claude가 결과를 게임에 붙이고 실제 렌더·검사로 확인한다. Codex는 모델 생성 코드와 원본 GLB까지만 만든다.

## 1. 무엇을 만드나

난전(코드 `Survivor`) 모드의 보스. 지금은 황소 도형 대체 모델(`content/enemies/bull.tres`, `Shape = 3`)을 1.7배로 키워 쓴다. 이것을 **전용 모델**로 바꾼다. 게임 규칙·수치는 바꾸지 않는다.

- 이름: 거대 두목 황소 / Boss Bull. 몬스터 id·모델 id: `boss`
- 스킬(모델이 읽혀야 할 것): 돌진(머리를 숙이고 뿔로 들이받음), **내려찍기**(두 주먹으로 땅을 침), 소환(포효), 부채꼴 탄막(입에서 솜뭉치를 뱉음). 스킬 규격은 `doc/mode-monster-tables.md` 3장.
- 분위기: 플레이어(거위·오리·라쿤·랫서판다)와 감염체처럼 **귀엽고 우스꽝스러운 봉제 인형** 느낌(`doc/character-model-spec.md` 6장, `doc/monster-design-guide.md`). 무섭거나 사실적이지 않게. 피·상처 없음.

## 2. 외형 (쿼터뷰 55°, 직교 크기 17에서 한눈에 "보스"로 읽힐 것)

- **두 발로 선 황소 레슬러/두목**: 크고 둥근 상체(가슴이 앞으로 나온 콩/물방울 모양), 짧고 굵은 다리, 아주 큰 주먹 두 개(내려찍기가 읽히게). 목 경계 없이 몸과 머리가 이어지는 단순 체형(D-5).
- **머리**: 넓은 주둥이(밝은 크림색), 큰 콧구멍 두 개, **금색 코뚜레**(토러스), 짧고 굵게 휜 **뿔 두 개**(크림색, 끝이 약간 위로), 작고 화난 눈썹, 작은 귀 두 개.
- **보스 표시**: 머리 위 작은 **금관**(뿔 사이), 허리에 **챔피언 벨트**(금색 버클), 어깨에 둥근 **어깨 보호대** 두 개. 이 세 가지로 일반 황소(`bull`)와 구분한다.
- **색**(재질 3~4개 목표): 몸 진한 갈색 `#6b4632`, 주둥이·뿔 크림 `#f3e3c4`, 금장 `#e2b34a`, 눈·콧구멍·주먹 마디선 잉크 `#2d2622`. 필요하면 벨트·어깨 보호대 진한 적갈색 `#7a2f2a`.
- **크기**: 원점 = 발바닥, 키 약 **1.9m**(뿔 끝 포함 약 2.1m), 몸 폭 약 1.3m. 게임이 1.7배로 키운다(`content/data/bosses/boss.json`의 `scale`). 충돌은 모델이 아니라 `.tres`의 캡슐(반지름 0.7, 높이 1.9)이 맡는다.

## 3. 노드 구조 (런타임이 이 이름을 찾아 움직임 — 리깅·본 없음)

`doc/character-model-spec.md` 2장과 같은 규칙. 감염체 생성기 `prototypes/character-lab/tools/infected-models.mjs`의 구조를 그대로 따른다.

```
boss_root
└─ Body                (발바닥 높이, 몸 중심)
   └─ Deform           (발바닥 높이, 납작해지는 스쿼시 기준)
      ├─ Head          (목 위치 — 보블헤드 스프링)
      │  └─ Muzzle     (입 앞, 탄막이 나오는 곳)
      ├─ LeftFoot / RightFoot   (각 발 중심)
      ├─ boss_leftArm / boss_rightArm   (어깨 피벗 — 소매·팔뚝·주먹을 이 아래에)
      └─ Weapon        (가슴 앞, 비어 있어도 됨)
```

- **정면 +Z**, 위 +Y, 단위 미터. glTF 그대로(`ModelFacesPositiveZ = true`).
- 팔은 반드시 `boss_leftArm` / `boss_rightArm` 피벗 아래에 둔다. 런타임이 이 이름을 찾으면 보조 팔을 만들지 않고 이 팔로 공격 자세를 만든다(A-6, `CharacterEnemyPose`).
- 메시 이름은 자유. 최적화 단계가 피벗별로 메시를 합친다.

## 4. 예산

- 최적화 후 **삼각형 4500개 미만**, 메시는 피벗당 하나(최적화가 합침), 재질 3~5개. 보스는 한 마리지만 소환된 잡몹 70마리와 같이 그려진다.
- 생성 코드는 감염체처럼 `SphereGeometry(1,16,10)` 타원체와 `CapsuleGeometry`, 금장·코뚜레는 `TorusGeometry` 정도만 쓴다.

## 5. 만들 것 (Codex 작업 범위)

1. `prototypes/character-lab/tools/boss-models.mjs` — `createBoss()`가 위 구조의 three.js `Group`(`boss_root`)을 돌려준다. 스타일·도우미 함수는 `infected-models.mjs`를 참고(복사해도 됨).
2. `prototypes/character-lab/tools/export-game-roster.mjs` — **`--boss-only`** 옵션을 추가해 `output/roster/boss-source.glb`만 내보낸다. 기존 플레이어·감염체 내보내기 동작은 바꾸지 않는다.
3. `prototypes/character-lab/tools/optimize_game_roster.py` — **`--boss-only`** 옵션을 추가해 `boss`만 처리하고 `game/assets/enemies/boss/boss.glb`와 `boss-report.json`을 쓴다. 감염체 분기처럼 팔 메시를 `boss_leftArm`/`boss_rightArm` 아래에 보존하고, 삼각형 4500 미만·두 팔 피벗과 메시 존재를 assert로 확인한다. 다른 모델 처리와 기존 assert는 그대로.
4. `node tools/export-game-roster.mjs --boss-only`를 `prototypes/character-lab`에서 실행해 원본 GLB를 실제로 만든다. **Blender는 실행하지 않는다**(Claude가 실행).

## 6. 하지 말 것

- `game/` 안의 C#·데이터·`.tres`·다른 GLB를 고치지 않는다(Claude가 연결).
- 기존 플레이어·감염체 모델 코드와 출력(`game/assets/characters`, `game/assets/enemies/<기존>`)을 바꾸지 않는다.
- 커밋·푸시하지 않는다. 새 npm 패키지를 설치하지 않는다(`three` 0.180.0 사용).

## 7. 끝났다고 볼 기준

- `prototypes/character-lab/output/roster/boss-source.glb`가 생기고, 노드 `boss_root`, `Body`, `Deform`, `Head`, `Muzzle`, `LeftFoot`, `RightFoot`, `boss_leftArm`, `boss_rightArm`, `Weapon`이 있다.
- 마지막 메시지에: 바꾼 파일, 원본 삼각형 수(대략), 노드 목록, 외형에서 고민한 점, 확인하지 못한 것.

## 8. 결과 (2026-10-21)

- Codex(`codex exec -s workspace-write`): `tools/boss-models.mjs`(`createBoss()`) 추가, `export-game-roster.mjs`·`optimize_game_roster.py`에 `--boss-only` 추가. `doc/`·`game/`은 건드리지 않음. 원본 `output/roster/boss-source.glb` 삼각형 11,608개.
- Claude: `Blender --background --python tools/optimize_game_roster.py -- --boss-only --no-preview` → `game/assets/enemies/boss/boss.glb` **삼각형 4,466개, 메시 6, 재질 5**(cream·fur·gold·ink·leather), 두 팔 피벗 보존. `content/enemies/boss.tres` 추가, `monsters.csv`의 `boss` 외형을 `bull` → `boss`로.
- 확인: 창 모드 `ModeCheck`의 `output/mode-survivor-boss-close.png`·`mode-survivor-slam.png`에서 금관·코뚜레·어깨 보호대·큰 주먹이 쿼터뷰로 읽힘. 4,466개는 예산(4,500) 바로 아래라 장식을 더하려면 다른 곳을 줄여야 함.
- 다음 지시문은 이 파일을 복사해 1~7장만 바꾼다. Codex에는 "모델 코드·원본 GLB까지, `doc/`·`game/`·Blender·커밋 금지"를 그대로 둔다.
