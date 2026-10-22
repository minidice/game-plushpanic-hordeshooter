# 그림 지시문: 특별 카드 투표 도장 (`vote_stamp`) — 무기명 투표 화면의 도장 · 승리 테두리

> **상태**: 2026-10-22 작성. Codex CLI(`codex exec -s workspace-write`)에 맡기는 작은 UI 그림 작업(decisions.md M-29 2단계, [카드 계획](../onslaught-cards-plan.md) 16.5). 사용자 규칙: 이펙트 · 모델 · 아이콘 같은 리소스는 Codex가 만든다.
> **왜**: 보스를 잡으면 모두 멈추고 특별 카드 3장에 **무기명 투표**를 한다. 카드 위에는 누가 찍었는지 알 수 없게 **모두 똑같은 모양의 도장**이 찍힌다. 그 도장과, 이긴 카드에 두를 빛나는 테두리 그림이 필요하다.
> **검토자**: Claude가 스크립트를 실행해 PNG를 만들고, 투표 화면에 붙여 실제 화면으로 확인한다. Codex는 그림을 만드는 스크립트까지만 만든다.

## 1. 공통 규칙

- 분위기: 게임의 봉제 인형 · 장난감 톤. 둥글고 두툼하게, 텍스처 같은 잔무늬 없이 단순하게. 작은 크기(화면에서 48~64 px)에서도 읽혀야 한다.
- 배경 **투명**(RGBA PNG). 색은 아래 값만.
- Python 3 + **Pillow만** 쓴다(이미 설치됨). 다른 패키지를 설치하지 않는다.
- 결과는 결정적이어야 한다(무작위를 쓰면 시드 고정).

## 2. 만들 그림

| 파일 | 크기 | 내용 |
|---|---|---|
| `game/assets/icons/ui/vote_stamp.png` | 256 × 256 | 둥근 **잉크 도장**. 바깥 둥근 테두리(두께 약 14 px) 안에 **거위 발자국**(물갈퀴 세 발가락). 색 `d9473b`(빨간 인주), 잉크가 덜 묻은 듯한 작은 빈 자국 몇 군데(가장자리 위주, 전체 면적의 10% 이하). 약간(−12°) 기울임. 가장자리는 안티에일리어싱 |
| `game/assets/icons/ui/vote_winner_frame.png` | 512 × 160 | 이긴 카드에 겹칠 **9-slice용 테두리**. 둥근 모서리 사각형(반지름 약 28 px) 테두리만, 두께 약 10 px, 금색 `ffcf5a` → 바깥으로 갈수록 투명해지는 부드러운 빛 번짐(약 14 px). 안쪽은 완전 투명. 네 모서리가 같아야 9-slice로 늘릴 수 있다(가운데 띠는 균일) |

## 3. 만들 것 (Codex 작업 범위)

1. **새 파일** `prototypes/ui-icons/tools/vote_icons.py` 하나. 실행하면 위 두 PNG를 쓰고, 끝에 파일마다 `VOTE_ICON_READY <파일> <가로>x<세로>`, 마지막에 `VOTE_ICONS_BUILT 2`를 출력한다.
2. 스크립트 끝 **assert**: 크기가 표와 같음, 모드 RGBA, 네 모서리 픽셀이 완전 투명, 도장은 불투명 픽셀이 전체의 15~45%, 테두리는 가운데(안쪽) 픽셀이 완전 투명.
3. 실행은 하지 않는다. **Claude가 실행**한다: `python prototypes/ui-icons/tools/vote_icons.py`

## 4. 하지 말 것

- `game/` 안의 C# · 데이터 · 장면 · 다른 에셋, `doc/`, 다른 도구 파일을 고치지 않는다. 새 파일 하나만.
- 커밋 · 푸시하지 않는다. 새 패키지를 설치하지 않는다.

## 5. 끝났다고 볼 기준

- `python -m py_compile prototypes/ui-icons/tools/vote_icons.py` 통과.
- 마지막 메시지에: 만든 파일, 그림 설명, 확인하지 못한 것.

## 6. 결과 (2026-10-22)

- **Codex**(`codex exec -s workspace-write`, 지시문 그대로): 새 파일 `prototypes/ui-icons/tools/vote_icons.py` 하나. `py_compile`만 실행. `git status`로 다른 파일 변경 없음 확인.
- **Claude가 실행**: `python prototypes/ui-icons/tools/vote_icons.py` → 단언 통과, `game/assets/icons/ui/vote_stamp.png`(256², 기울어진 빨간 거위 발 도장) · `vote_winner_frame.png`(512 × 160, 금색 빛 테두리) 생성, Godot 가져오기.
- **게임 연결**: `VoteWindow`가 카드 위 도장(92 px)과 이긴 카드의 9-slice 테두리로 씀. 렌더 `output/mode-survivor-vote.png` · `mode-survivor-vote-countdown.png`로 확인. 처음 렌더에서 도장이 작은 대체 문자로 보였던 것은 PNG를 가져오기 전이라서였고, 가져온 뒤 다시 확인.
