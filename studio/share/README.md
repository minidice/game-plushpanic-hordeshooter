# 피드백 공유 꾸러미 (studio/share)

지인에게 **캐릭터 모델과 배경 오브젝트 모델만** 보여 주고 의견을 받기 위한 정적 페이지 묶음이다([decisions.md](../../doc/decisions.md) T-27). Studio 서버를 밖에 열지 않는다. 받은 사람은 zip을 풀고 `index.html`을 더블클릭하면 인터넷 없이 본다.

## 들어가는 것 · 안 들어가는 것

- 들어감: [selection.json](selection.json)에 고른 모델의 회전 사진(눈높이 · 게임 시점 55° 각 16장), 3D 모델(GLB, 숨김 부품 뺌), 게임 장면 사진 4장, 페이지 하나, three.js(페이지 안에 포함).
- **안 들어감**: md 문서, 게임 데이터(JSON · CSV), 코드, Studio. 꾸러미를 만든 뒤 `packs/NNN-models/` 목록을 한 번 보고 보낸다.
- 번호: 플레이어 P1~P8, 일반 몬스터 M1~M8, 오브젝트 O1~O15, 장면 S1~S4. 의견은 이 번호로 받는다.

## 만들기

1. Studio를 켠다(`studio/Start-Studio.ps1` 또는 `node server.mjs --port 4317`). 다른 작업이 같은 폴더에서 Studio · Godot를 쓰는 중이면 별도 worktree와 다른 포트에서 켠다.
2. 모델 사진 · GLB: `node tools/build-share.mjs export --studio http://127.0.0.1:4317` → `share/.build/<번호-id>/`. 일부만 다시: `--only P2,M4`.
3. 장면 사진(창 모드, GPU 필요):
   `Godot_v4.7.2-stable_mono_win64_console.exe --path game res://tests/ShareSceneCapture.tscn` → `share/.build/scenes/S1~S4.png`, 끝에 `SHARE_SCENES_PASS 4`.
4. 꾸러미: `node tools/build-share.mjs pack` → `share/packs/NNN-models/`와 `NNN-models.zip`. 번호는 자동으로 다음 번호. three.js는 `prototypes/character-lab/node_modules/three`(`npm ci`)에서 읽는다. 다른 곳이면 `--three <폴더>`.
5. 확인: 압축을 새 폴더에 풀고 `index.html?selftest`를 열면 모든 모델을 읽어 제목에 `selftest 31/31 webgl`이 뜬다. 헤드리스 Chrome으로도 같다(`--dump-dom "file:///…/index.html?selftest"`).

`.build/`와 `packs/`는 만들 때마다 생기는 산출물이라 커밋하지 않는다. 커밋하는 것은 `selection.json`, 이 문서, `tools/build-share.mjs` · `tools/share-page.html`, 게임 쪽 `tests/StudioShare.cs`(Studio `share_item`) · `tests/ShareSceneCapture.*`뿐이다.

## 페이지

- 카드: 사진을 좌우로 끌면 회전, 휠 확대, 두 번 클릭 초기화, `눈높이 / 게임 시점` 전환, `3D` 버튼.
- 3D 보기: 끌어서 회전 · 휠 확대 · 오른쪽 끌기 이동, `정면 · 옆 · 뒤 · 게임 시점(55°, -30°) · 게임 크기`(1080p 화면에서 게임 넓은 카메라 17 m로 볼 때의 크기). 바로 열기 링크: `index.html#3d=P2:game`.
- 의견: 카드마다 입력 칸(그 브라우저에만 저장), `의견 모아 보기` → 복사 또는 텍스트 파일 저장.

## 한계

- 3D 보기는 게임 셰이더 · 조명 없이 three.js 기본 조명으로 그린다. 색 · 질감은 **사진이 정확하다**(사진은 실제 Godot 렌더).
- 장면 사진은 몬스터 · 동료 AI와 시야 원뿔(C-23)을 멈추고 찍은 연출이다. 실제 플레이에서는 시야 밖 몬스터가 실루엣으로 보인다.
- 곤돌라 승강장(O15)처럼 바닥을 게임이 따로 깔아 주는 모델은 틀만 보인다.
