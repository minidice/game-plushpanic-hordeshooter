# 모델 지시문: 네임드 "박치기 황소" (`bull`) · "풍선 기생충" (`balloon`)

> 2026-10-25 아트 기준 정정: 이 브리프의 봉제 인형 표현은 과거 제작 이력이다. 재제작 시 [현재 캐릭터 아트 기준](../character-art-direction.md)을 우선하며 몸의 천 결·스티치·솜 같은 부피를 새 목표로 사용하지 않는다. 기존 보스/네임드 모델과 능력은 이번에 변경하지 않았다.

> **상태**: 2026-10-21 작성. Codex CLI(`codex exec`)에 맡기는 두 번째 몬스터 모델 작업(decisions.md A-17, 첫 작업은 [boss.md](boss.md)). 한 번의 Codex 실행에서 두 모델을 **차례로** 만든다.
> **왜**: 두 네임드는 모델 파일이 없어 코드로 공을 붙인 임시 도형(`game/scripts/Characters/PlaceholderModel.cs`)으로 그려진다. 황소 얼굴은 앞뒤로 75%, 코는 40%로 눌려 있어 쿼터뷰에서 **종이 인형처럼 납작**해 보인다(사용자 지적 2026-10-21).
> **검토자**: Claude가 결과를 게임에 붙이고 실제 렌더·검사로 확인한다. Codex는 모델 생성 코드와 원본 GLB까지만 만든다.

## 1. 공통 규칙 (두 모델 모두)

- 분위기: 플레이어(거위·오리·라쿤·랫서판다)·감염체·보스처럼 **귀엽고 우스꽝스러운 봉제 인형**(`doc/character-model-spec.md` 6장, `doc/monster-design-guide.md`). 무섭거나 사실적이지 않게. 피·상처 없음.
- **두께 규칙 (이번 작업의 핵심)**: 카메라는 55° 위에서 비스듬히 내려다본다(직교 크기 17, 정면 +Z가 화면 아래쪽을 향하는 일이 많음). 앞뒤 두께가 얇으면 납작해 보인다.
  - **몸통 앞뒤 깊이 = 몸통 폭(팔 제외)의 80~100%**. 머리 앞뒤 깊이 = 머리 폭의 85% 이상.
  - 큰 덩어리(몸통·머리·주둥이)를 Z축으로 0.9배 미만으로 누르지 않는다. 둥글고 통통하게.
  - 얼굴 부품(눈·코·주둥이)은 표면에서 **앞으로 튀어나오게** 붙여 옆·위에서 봐도 입체가 읽히게.
- **노드 구조** (런타임이 이 이름을 찾아 움직임 — 리깅·본 없음, 보스와 같은 방식):

```
<id>_root
└─ Body                     (발바닥 높이, 몸 중심)
   └─ Deform                (발바닥 높이, 납작해지는 스쿼시 기준)
      ├─ Head               (목 위치 — 보블헤드 스프링, 돌진 때 숙임)
      │  └─ Muzzle          (입 앞)
      ├─ <id>_leftArm / <id>_rightArm   (어깨 피벗 — 팔 메시를 이 아래에)
      ├─ <id>_leftLeg / <id>_rightLeg   (엉덩이 관절 피벗 — 다리 메시를 이 아래에)
      │  └─ LeftFoot / RightFoot        (각 발 중심 피벗, 발 메시를 이 아래에)
      ├─ <id>_tail          (황소만: 엉덩이 뒤 관절 피벗, 꼬리 메시를 이 아래에)
      └─ Weapon             (가슴 앞, 비어 있어도 됨)
```

  - **정면 +Z**, 위 +Y, 단위 미터, 원점 = 발바닥. glTF 그대로(`ModelFacesPositiveZ = true`).
  - 관절 피벗(팔·다리·꼬리)은 **관절 위치에** 둔다(어깨·엉덩이·꼬리뿌리). 지금은 런타임이 팔(`*_leftArm`·`*_rightArm`)과 발(`LeftFoot`·`RightFoot`)만 움직이지만, 다리·꼬리 피벗은 나중의 축 늘어지는 래그돌(issues.md I-83)과 보스 동작(decisions.md A-18)에 쓴다.
  - 메시 이름은 자유. 최적화 단계가 피벗별로 메시를 합친다(팔·다리·꼬리·발 메시는 각 피벗 아래에 보존).
- 생성 코드는 감염체·보스처럼 `SphereGeometry` 타원체, `CapsuleGeometry`, `TorusGeometry`, `ConeGeometry` 정도만 쓴다. 스타일·도우미 함수는 `prototypes/character-lab/tools/boss-models.mjs`·`infected-models.mjs`를 참고(복사해도 됨).
- 충돌은 모델이 아니라 `.tres`의 캡슐이 맡는다. 모델 크기는 아래 각 항목의 키·폭에 맞춘다.

## 2. 박치기 황소 (`bull`, 네임드)

- **하는 일**: 예고(머리를 숙이고 앞발을 긁음) 후 **돌진**, 돌진 중 부딪힌 생존자를 날림, 벽에 박으면 어지러움(`@_@`). 시체는 위치 고정으로 남는다. 규격 `doc/throwable-monster-spec.md`, `game/content/data/monster_traits/bull_charge.json`.
- 테마 셋(감염체·야생 동물·무장 동물)에 모두 나오므로 **테마 중립적인 봉제 황소**로.
- **보스와 구분**: 보스(`boss.glb`)는 두 발로 선 레슬러 두목(금관·금 코뚜레·챔피언 벨트·어깨 보호대·큰 주먹). 네임드 황소는 **금장 장식 없음**, 주먹 작음, 대신 **크고 굵은 뿔**과 **앞으로 기운 자세**로 "들이받는 놈"이 읽히게.
- **외형**: 두 발로 서되 상체를 앞으로 15~20° 숙인 땅딸막한 황소 인형. 크고 둥근 머리(몸통의 70~80% 크기)가 몸 앞쪽에 붙음. 넓고 두툼한 크림색 주둥이(앞으로 튀어나옴)와 큰 콧구멍 두 개, 크게 휜 뿔 두 개(앞·옆으로 뻗음), 화난 짙은 눈썹과 둥근 눈, 작은 귀, 배에 크림색 원형 패치, 짧은 팔(주먹은 보스보다 작게), 짧고 굵은 다리와 둥근 발굽(진한 색), 엉덩이에 끝이 술처럼 뭉친 짧은 꼬리.
- **색**(재질 3~4개): 몸 갈색 `#8c6147`, 주둥이·뿔·배 크림 `#f1e6c8`, 발굽·눈·콧구멍·눈썹 잉크 `#2a1f1a`, 꼬리 술·코 안쪽 연분홍 `#dcb1a1`(선택).
- **크기**: 키 약 **1.9 m**(뿔 끝 포함 약 2.0 m), 몸통 폭 약 1.2 m, 몸통 앞뒤 깊이 **1.0~1.2 m**, 머리 앞뒤 깊이 0.8 m 이상. 충돌 캡슐 반지름 0.7, 높이 1.9.
- **예산**: 최적화 후 삼각형 **3000개 미만**, 재질 3~4개.

## 3. 풍선 기생충 (`balloon`, 네임드)

- **하는 일**: 느리게 뒤뚱거리며 다가와 가까우면 **부풀다가 펑** 터짐(헬륨 가스·부하 소환). 런타임이 부푸는 동안 모델 전체를 키우고(`Rig.Model.Scale`), **터지면 `Head` 노드를 숨겨** 풍선이 사라지고 몸만 시체로 남는다. 규격 `game/content/data/monster_traits/balloon_pop.json`.
- **외형**: 작고 통통한 봉제 몸통(짧은 팔·다리) 위에 **커다란 풍선 머리**가 달린 생물. 풍선 머리 = `Head` 아래 전부: 아주 둥근 풍선(앞뒤도 둥글게, 납작 금지), 위쪽 꼭지에 묶은 매듭, 옆에 반짝이는 하이라이트 점, 앞면에 크고 동그란 만화 눈 두 개와 작은 입. 목 부분에 리본처럼 묶인 매듭 끈(몸 쪽에 남는 부분은 `Head`가 아니라 몸에).
- **색**(재질 3~4개): 몸 분홍 `#f27fac`, 풍선 머리 밝은 분홍 `#f7a9c8`(약간 반짝이는 재질), 눈 흰자 `#fff2e4`, 눈동자·입 잉크 `#4d1a2e`.
- **크기**: 키 약 **1.9 m**(풍선 꼭지 포함), 풍선 지름 약 1.1~1.2 m(앞뒤도 같은 지름), 몸통 폭 약 0.7 m, 몸통 앞뒤 깊이 0.6~0.7 m. 충돌 캡슐 반지름 0.5, 높이 1.9.
- **예산**: 최적화 후 삼각형 **2500개 미만**, 재질 3~4개.

## 4. 만들 것 (Codex 작업 범위)

1. `prototypes/character-lab/tools/named-models.mjs` — `createBull()`과 `createBalloon()`이 위 구조의 three.js `Group`(`bull_root`, `balloon_root`)을 돌려준다.
2. `prototypes/character-lab/tools/export-game-roster.mjs` — **`--named-only`** 옵션을 추가해 `output/roster/bull-source.glb`와 `output/roster/balloon-source.glb`만 내보낸다. 기존 플레이어·감염체·`--boss-only` 동작은 바꾸지 않는다.
3. `prototypes/character-lab/tools/optimize_game_roster.py` — **`--named-only`** 옵션을 추가해 `bull`·`balloon`만 처리하고 `game/assets/enemies/bull/bull.glb`·`bull-report.json`, `game/assets/enemies/balloon/balloon.glb`·`balloon-report.json`을 쓴다. 보스 분기처럼 팔·다리·발·꼬리 메시를 각 피벗 아래에 보존한다. assert로 확인: 삼각형 예산(황소 3000 · 풍선 2500 미만), 2장의 노드가 모두 있음, **몸통(`Deform` 아래 피벗에 속하지 않은 메시)의 앞뒤 깊이 ≥ 폭의 0.8**, 머리(`Head` 아래)의 앞뒤 깊이 ≥ 폭의 0.85. 다른 모델 처리와 기존 assert는 그대로.
4. `node tools/export-game-roster.mjs --named-only`를 `prototypes/character-lab`에서 실행해 원본 GLB 두 개를 실제로 만든다. **Blender는 실행하지 않는다**(Claude가 실행).

## 5. 하지 말 것

- `game/` 안의 C#·데이터·`.tres`·다른 GLB를 고치지 않는다(Claude가 연결). `doc/`도 고치지 않는다.
- 기존 플레이어·감염체·보스 모델 코드와 출력을 바꾸지 않는다.
- 커밋·푸시하지 않는다. 새 npm 패키지를 설치하지 않는다(`three` 0.180.0 사용).

## 6. 끝났다고 볼 기준

- `prototypes/character-lab/output/roster/bull-source.glb`와 `balloon-source.glb`가 생기고, 1장의 노드가 모두 있다(풍선은 `<id>_tail` 없음).
- 마지막 메시지에: 바꾼 파일, 모델별 원본 삼각형 수(대략), 노드 목록, 몸통·머리의 폭/앞뒤 깊이(미터), 외형에서 고민한 점, 확인하지 못한 것.

## 7. 결과 (2026-10-21)

- Codex(`codex exec -s workspace-write`): `tools/named-models.mjs`(`createBull()`·`createBalloon()`) 추가, `export-game-roster.mjs`·`optimize_game_roster.py`에 `--named-only` 추가(노드 · 예산 · 두께 assert 포함). `doc/`·`game/`은 건드리지 않음. 원본 삼각형 황소 10,608 · 풍선 7,080.
- Claude가 고친 것: Codex가 최적화 스크립트의 보고 줄 하나를 깨뜨려(`for m in meshes for m in o.data.materials` → 이름 오류) Blender가 황소만 쓰고 멈춤 → 원래 문장(`for o in meshes`)으로 되돌림. 감축 비율이 너무 커서(황소 14% · 풍선 20%) 표면이 각지고 풍선 머리에 주름이 생김 → 25% · 32%로 올려 예산을 씀(I-84).
- 최종: `Blender --background --python tools/optimize_game_roster.py -- --named-only --no-preview` → `game/assets/enemies/bull/bull.glb` **삼각형 2,644**(몸통 폭 1.14 × 깊이 1.18 m, 머리 1.47 × 1.35 m), `game/assets/enemies/balloon/balloon.glb` **삼각형 2,264**(몸통 0.72 × 0.66 m, 풍선 머리 1.12 × 1.15 m). 팔 · 다리 · 발 · 꼬리 피벗 보존.
- 게임 연결: `content/enemies/bull.tres`·`balloon.tres`에 `Model`·`ModelFacesPositiveZ`·노드 이름 추가(`Shape`는 모델이 없을 때의 대체로 남김). 능력치 · 충돌 크기는 그대로.
- 확인: `tests/MonsterLineupCapture.tscn` 렌더 `output/lineup-front/side/back.png`(빈 바닥에 몬스터 11종, 이전 모습은 로컬 `output/before/`), `lineup-bull-windup.png`(머리를 숙이고 돌진 준비), `lineup-balloon-inflate.png`·`lineup-balloon-popped.png`(부푼 뒤 펑, 머리가 사라지고 몸만 시체). `EnemyExpansion` 60 · `EnemyAttackCheck` 79 · `CorpseTierCheck` 36 · `CorpseReactionCheck` 29 · `HitFlashCheck`(황소 예시를 모델 없는 복사본으로 바꿈) · `ModeCheck` 86 · `Integration` 148 등 통과.
- 남은 것: 다리 · 꼬리 피벗은 아직 런타임이 움직이지 않음(래그돌 I-83 · 보스 동작 A-18 다음 단계). 풍선 머리에 감축 흔적(옅은 주름)이 조금 남음.
