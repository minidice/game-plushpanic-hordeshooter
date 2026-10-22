# 그림 지시문: 난전 카드 아이콘 (`card_*`) — 카드 창 · 투표 창의 큰 아이콘

> **상태**: 2026-10-22 작성. Codex CLI(`codex exec -s workspace-write`)에 맡기는 UI 그림 작업(decisions.md M-29 4단계 1묶음, [카드 계획](../onslaught-cards-plan.md) 16.6). 사용자 규칙: 이펙트 · 모델 · 아이콘 같은 리소스는 Codex가 만든다.
> **왜**: 카드 창의 아이콘 칸(150 × 92 px)에 지금은 무기 카드 말고는 ✦ · 숫자 글자만 있다. 효과 · 특별 · 반복 카드마다 한눈에 구분되는 아이콘이 필요하다. 이번에 4단계 1묶음의 새 카드(탭댄스 · 튕기는 탄 · 꽥 충전 · 운명의 총 · 특수무기 2발)도 들어온다.
> **검토자**: Claude가 스크립트를 실행해 PNG를 만들고 카드 창에 붙여 실제 화면으로 확인한다. Codex는 그림을 만드는 스크립트까지만 만든다.

## 1. 공통 규칙

- 분위기: 봉제 인형 · 장난감 톤의 **납작한 아이콘**(두툼한 외곽선 4~6 px `1c2230`, 둥근 모양, 2~4색 면 + 하이라이트 한 줄). 그림자 · 그라데이션 · 글자 없음. 어두운 카드 배경(`0d0f15` 근처) 위에서 읽혀야 한다.
- 크기 **256 × 256**, 배경 **투명**(RGBA). 그림은 가운데, 가장자리 여백 약 16 px.
- Python 3 + **Pillow만**(이미 설치됨). 결정적으로(무작위를 쓰면 시드 고정). 4배로 그린 뒤 줄여 안티에일리어싱.
- 색 팔레트(이 안에서): 빨강 `e5484d` · 주황 `ff9b4a` · 노랑 `ffd23f` · 금 `ffcf5a` · 연두 `7dff9a` · 초록 `4caf6a` · 하늘 `6bb8ff` · 파랑 `3f6fd8` · 보라 `c08bff` · 분홍 `ff9ccc` · 흰 `f3f4f1` · 회색 `8c96a8` · 갈색 `9a6a3f`.

## 2. 만들 그림 (`game/assets/icons/cards/<이름>.png`)

| 파일 | 카드 | 그림 |
|---|---|---|
| `card_balloon.png` | 풍선탄 | 분홍 풍선 하나(매듭 · 짧은 끈) 옆에 작은 총알 |
| `card_velcro.png` | 찍찍이탄 | 연두색 찍찍이 패치 두 장이 맞붙은 모양(한쪽 고리 무늬 점들) |
| `card_stuffing_burst.png` | 솜 폭발 | 흰 솜 뭉치가 사방으로 터지는 모양(가운데 솜 + 둘레 작은 솜 조각 6~8) |
| `card_billiards.png` | 관통 당구 · 산탄 볼링 | 당구공 두 개(노랑 1 · 빨강 3)와 둘 사이 부딪힘 선 |
| `card_tap_dance.png` | 탭댄스 | 탭 슈즈 한 짝(검은 구두 + 은색 바닥 쇠) + 음표 둘 |
| `card_bouncing_rounds.png` | 튕기는 탄 | 총알이 지그재그(두 번 꺾인 점선)로 튕기는 궤적, 꺾이는 곳마다 작은 별 |
| `card_honk_charge.png` | 꽥 충전 | 빨간 고무공 + 금색 나팔 경적(꽥) 옆에 번개 모양 하나 |
| `card_destiny_gun.png` | 운명의 총 | 주사위(흰, 점 5) 위에 작은 로켓 하나 |
| `card_helium_party.png` | 헬륨 파티 | 하늘색 · 분홍 · 노랑 풍선 세 개 다발 |
| `card_last_shot.png` | 마지막 한 발 | 빈 탄창 옆 마지막 총알 하나가 빛나는 모양(총알 끝에 주황 불꽃) |
| `card_glass_cannon.png` | 유리 대포 | 하늘색 반투명 느낌(밝은 하늘 + 흰 하이라이트)의 작은 대포, 몸통에 금 간 선 |
| `card_double_special.png` | 특수무기 2발 | 로켓 두 개가 나란히 위로 |
| `card_mystery.png` | "?" 카드 | 보라 선물 상자, 뚜껑 위 큰 물음표 모양 리본(글자 대신 리본 곡선으로) |
| `card_r_damage.png` | 피해 +10% | 위로 향한 주황 화살표 셋이 겹친 모양 |
| `card_r_heal.png` | 체력 회복 | 빨간 하트 + 흰 십자 |
| `card_r_medkit.png` | 구급상자 +1 | 흰 구급상자(빨간 십자) |

## 3. 만들 것 (Codex 작업 범위)

1. **새 파일** `prototypes/ui-icons/tools/card_icons.py` 하나. 실행하면 위 16장을 쓰고, 파일마다 `CARD_ICON_READY <파일>`, 끝에 `CARD_ICONS_BUILT 16`.
2. **assert**: 크기 256², 모드 RGBA, 네 모서리 픽셀 완전 투명, 불투명 픽셀이 전체의 8~60%, 16장이 서로 다름(픽셀 해시).
3. 실행은 하지 않는다. **Claude가 실행**: `python prototypes/ui-icons/tools/card_icons.py`

## 4. 하지 말 것

- `game/` 안의 C# · 데이터 · 장면 · 다른 에셋, `doc/`, 다른 도구 파일(`vote_icons.py` 포함)을 고치지 않는다. 새 파일 하나만.
- 커밋 · 푸시하지 않는다. 새 패키지를 설치하지 않는다.

## 5. 끝났다고 볼 기준

- `python -m py_compile prototypes/ui-icons/tools/card_icons.py` 통과.
- 마지막 메시지에: 만든 파일, 그림 설명, 확인하지 못한 것.

## 6. 결과 (2026-10-22)

- **Codex**(`codex exec -s workspace-write`, 지시문 그대로): 새 파일 `prototypes/ui-icons/tools/card_icons.py` 하나. `py_compile`만 실행. `git status`로 다른 파일 변경 없음 확인.
- **Claude가 실행**: `python prototypes/ui-icons/tools/card_icons.py` → 단언 통과, `game/assets/icons/cards/` 16장 생성, Godot 가져오기. 한 장에 모아 보니 16장 모두 구분되고 어두운 배경에서 읽힘.
- **게임 연결**: `CardCatalog.Icon` — 효과 카드 · 강화(기본 카드 그림) · 특별 · 반복 · "?" 카드가 카드 창 · 투표 창 아이콘 칸에 표시. 무기 · 소환수 카드는 기존 그대로.
