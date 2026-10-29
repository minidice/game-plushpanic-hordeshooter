# 다국어 (언어 테이블) 규격

> **구현**: `game/scripts/Data/Loc.cs`
> **테이블**: `game/content/lan/<언어>/*.csv`
> **현재 언어**: `kor`(기본), `eng`

## 구조

데이터 파일에는 **문구 대신 키**를 넣고, 실제 문구는 언어별 폴더의 테이블에 둡니다.

```
content/data/weapons.csv          ak47,assault_rifle,name_weapon_ak47,7,…
content/lan/kor/weapon.csv        name_weapon_ak47,AK47
content/lan/eng/weapon.csv        name_weapon_ak47,AK-47
content/lan/jpn/weapon.csv        name_weapon_ak47,エーケー47      ← 언어를 추가하면 이렇게
```

- 테이블 형식은 `key,text` 두 열이고 UTF-8입니다. `#`으로 시작하는 줄은 주석입니다.
- 한 언어 폴더 안의 CSV는 **모두 합쳐서** 읽습니다. 그래서 주제별로 파일을 나눠도 됩니다.

| 파일 | 내용 | 키 예 |
|---|---|---|
| `weapon.csv` | 무기, 무기 분류, 쌍권총 이름 형식 | `name_weapon_m4`, `name_weapontype_shotgun`, `fmt_weapon_dual` = `쌍 {0}` |
| `throwable.csv` | 투척물 | `name_throwable_lure` |
| `monster.csv` | 몬스터, 좀비 이름 형식 | `name_monster_bull`, `fmt_zombie` = `좀비 {0}` |
| `character.csv` | 플레이어 캐릭터 이름과 설명 | `name_character_goose`, `desc_character_goose` |
| `ui.csv` | HUD, 메인 메뉴, 일시정지·결과 화면, 목표, 스테이지 알림, `[E]` 안내, 줍기·보충 메시지, 바닥 글씨 | `ui_hud_reload`, `msg_pickup_full`, `fmt_hud_timer` = `{0:00}:{1:00}    처치 {2}` |

## 키 이름 규칙

`<용도>_<분류>_<id>` 형식입니다. 용도는 `name`(이름), `desc`(설명), `fmt`(`{0}`이 들어가는 형식)입니다.
UI 문구는 용도 자리에 `ui`(고정 문구), `msg`(알림·떠오르는 글씨)를 씁니다. 값이 들어가는 문구는 모두 `fmt`입니다.

- `fmt` 문구는 C#의 `string.Format` 형식이라 `{0:0.0}`, `{0:00}`처럼 숫자 형식을 붙일 수 있습니다. 번역할 때 `{0}`, `{1}`의 순서는 바꿔도 됩니다(예: `fmt_pickup_throwable_max` 영어는 `{1} {0}` 순서).
- 셀 앞뒤 공백은 지워집니다. 공백으로 시작하거나 끝나는 조각을 따로 두지 말고 문장 전체를 한 키로 둡니다(예: `ui_pause_keys`와 `ui_pause_keys_host`).
- 쉼표나 줄바꿈이 들어가는 문구는 큰따옴표로 감쌉니다(예: `ui_controls`는 두 줄).
- `[E]`, `[Q]` 같은 키 표시와 `[1] 무기 이름  ∞`처럼 언어와 상관없는 조합은 코드에 그대로 둡니다.

## 없는 문구

1. 현재 언어에 키가 없으면 **한국어(kor)** 문구를 씁니다.
2. 한국어에도 없으면 **키 자체**가 화면에 나옵니다. 게임은 멈추지 않고, 빠진 곳이 눈에 띕니다.
3. 데이터 로더는 무기, 투척물, 몬스터의 이름 키가 kor 테이블에 없으면 오류로 보고합니다.
4. `./Start-Game.ps1 -Test`의 "every Korean text key has an English line" 검사가 영어 누락을 잡습니다.

## 언어 바꾸기·추가하기

- **바꾸기**: 코드에서 `Loc.Language = "eng";`로 바꿉니다. 이름과 HUD 문구, 목표, 고정 `[E]` 안내는 화면에 그릴 때마다 조회하므로 바로 바뀝니다. 메인 메뉴, 바닥 글씨, 떨어진 무기의 이름표·줍기 안내, 이미 떠 있는 알림은 만들 때 정해지므로 장면을 다시 열면 바뀝니다. 메인 메뉴 **설정 → 언어**에서도 바꿀 수 있고, 메뉴를 그 자리에서 다시 만들며 `user://settings.cfg`에 저장합니다.
- **다른 언어로 화면 확인**: `Godot --path . res://tests/Capture.tscn -- --lang=eng`로 실행하면 `output/*-eng.png`로 캡처합니다.
- **추가하기**: `content/lan/jpn/` 폴더를 만들고, kor 폴더의 CSV를 복사해 `text` 열만 번역합니다. 각 CSV 옆에 `.import` 파일(`importer="keep"`)도 같이 둡니다. 이 파일이 없으면 Godot이 CSV를 번역 리소스로 가져가 버립니다.

## 언어별 글꼴

- 설정: `game/content/data/ui_fonts.json`. 언어 ID(`kor`, `eng`, 이후 `jpn` 등) 아래에 `body`·`menu`·`title`의 `res://` 폰트 경로를 각각 둔다. 한·영은 현재 Pretendard Regular/Bold/ExtraBold 원본을 공유하지만 각 언어 항목은 독립적이다. **한 언어의 경로만 바꾸면 다른 언어는 바뀌지 않는다.** 수정 후 게임을 다시 실행한다.
- 새 언어를 추가할 때 폰트 설정은 선택 사항이다. 언어/역할이 없거나 값이 비어 있으면 해당 역할 굵기의 `SystemFont`를 쓴다. 다른 언어의 번들 글꼴을 강제로 상속하지 않는다. 잘못된 경로·다른 타입 리소스·손상된 JSON은 시스템 글꼴로 대체하고 진단 경고를 남긴다.
- 번들 글꼴에 없는 문자는 시스템 폰트 체인 및 Godot의 시스템 폴백을 사용한다. OS에 설치된 폰트에도 해당 글자가 없으면 표시를 보장할 수 없다. 일본어/중국어/아랍어 등 정식 지원 시 자형·연결·줄바꿈·폭을 별도 검수한다.
- `Loc.LanguageChanged`가 `UiFonts`의 공유 `FontVariation.BaseFont`를 바꾸므로 이미 만들어진 테마와 월드 글꼴 참조도 갱신된다. **문구**가 생성 때 정해지는 이름표/바닥/알림의 기존 재생성 규칙은 유지한다.
- 적용 굵기·역할: [UI 규격](features/menu-ui/ui-style-spec.md#글꼴). Pretendard 정적 파일과 `LICENSE.txt`는 `assets/fonts/pretendard/`에 보관한다. 내보내기 설정은 라이선스를 PCK에 포함하고, `Export-Game.ps1`은 `licenses/<폰트 폴더>/LICENSE.txt`를 실행 파일 옆에도 복사하여 ZIP에 넣는다. 에디터에서 직접 내보내면 읽을 수 있는 라이선스 파일도 별도로 함께 배포한다.
- 검사: `res://tests/FontCheck.tscn`은 역할·한/영 전환·기존 UI/월드 참조 갱신·미설정 언어·개별 언어 교체·오류 설정 대체를 확인한다. 실제 렌더는 `MenuCheck`와 `InteractionUiCheck`로 확인한다.

## 아직 옮기지 않은 것

화면에 나오는 문구는 모두 `ui.csv`로 옮겼습니다. 아래는 개발자용이라 일부러 코드에 한국어로 남겨 둔 것입니다.

- **데이터 오류 메시지**: `WeaponDatabase.cs`, `ThrowableDatabase.cs`, `MonsterDefinition.cs`, `DataReaders.cs`가 CSV·JSON을 읽다가 내는 오류(예: `id가 중복입니다`). 데이터를 고치는 사람이 보는 문구입니다.
- **데이터 입력 별칭**: `DataReaders.cs`가 받아 주는 `무제한`, `보조`, `주무기` 같은 값. 표시 문구가 아니라 데이터에 쓸 수 있는 단어입니다.
- **효과음 설명**: `SoundRecipes.cs`의 소리 이름(예: `권총 발사`).
- **테스트 문구**: `tests/` 안의 확인용 문자열.
