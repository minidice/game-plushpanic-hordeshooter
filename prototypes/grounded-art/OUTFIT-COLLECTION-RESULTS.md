# 추가 투구·조끼 후보 05–07

후속: 모자가 너무 둥글고 사각 고글이 판처럼 보인다는 피드백을 받아 [모자 후보 08–14](HEADWEAR-COLLECTION-RESULTS.md)를 추가했다. 챙과 장식 종류를 늘리고 좌우 곡면 고글을 새로 만들었으며 이 문서의 후보 05–07 모델은 그대로 보존했다.

2026-10-23. 사용자의 “나머지 복장도 추가 작업해줘 투구 조끼들” 요청에 따라, 앞서 제안한 경량형·정찰형·중장비형을 독립 모델로 제작했다. 기존 거위 몸과 표정은 유지했다. 게임에 적용하거나 복장 선택 기능을 구현한 것은 아니다.

## 세 가지 형태

| 후보 | 투구 | 조끼 |
|---|---|---|
| 경량형 05 | 어두운 천 모자, 납작한 봉제선, 고글과 버클 | 황토색 가슴 하네스, 세 개의 탄창 파우치, 몸을 따르는 어깨끈과 허리띠. 옆구리가 열린 경량 장비 |
| 정찰형 06 | 둥근 황갈색 투구, 넓은 검은 테두리와 고정쇠 | 청회색 앞·뒤·옆판, 낮은 앞 목둘레, 큰 지도 주머니 한 개 |
| 중장비형 07 | 청회색 전술 투구, 고글·소형 광학 장치 외형·측면 레일 | 황토색 보호 면, 높은 뒤 칼라, 어두운 가슴판·옆판, 두 개의 파우치 |

기본 방탄형 04의 큰 면·얇은 테두리·거친 천·도장 표면 방향을 이어간다. 세트 차이는 색상 외에 투구 윤곽, 덮는 면적, 목둘레, 파우치 수와 부착물로 만든다. 실제 방어력·정찰 능력·광학 장치 기능을 부여한 것은 아니다. 옆구리를 감싸는 방탄형과 열린 하네스형을 구분하며, 방탄형 06·07은 날개 구멍을 남겼다.

경량형 어깨끈은 방탄조끼의 공통 외곽을 사용하면 몸에서 뜨므로, 몸통 표면을 따라가도록 별도로 맞췄다. 고글 밴드는 각 투구 곡면에 맞춰 반지름을 계산해 투구에 파묻히거나 들뜨는 구간을 정리했다.

## 파일

투구와 조끼를 각각 교체할 수 있도록 세트당 세 개의 GLB로 저장했다. 단독 GLB는 부착점 조상 노드와 장비 메시만 포함하며 몸 메시가 없다.

| 후보 | 캐릭터 포함 | 투구만 | 조끼만 | 방향별 렌더 |
|---|---|---|---|---|
| 05 | [경량형](assets/goose-light-outfit.glb) | [투구](assets/goose-light-helmet.glb) | [조끼](assets/goose-light-vest.glb) | [모음](output/outfit-collection/light-overview.png) |
| 06 | [정찰형](assets/goose-recon-outfit.glb) | [투구](assets/goose-recon-helmet.glb) | [조끼](assets/goose-recon-vest.glb) | [모음](output/outfit-collection/recon-overview.png) |
| 07 | [중장비형](assets/goose-heavy-outfit.glb) | [투구](assets/goose-heavy-helmet.glb) | [조끼](assets/goose-heavy-vest.glb) | [모음](output/outfit-collection/heavy-overview.png) |

- [편집용 Blender 원본](source/goose-outfit-collection.blend): 기본형과 새 세 벌을 모두 포함. 처음에는 경량형이 보이며, 객체 접두사 `base_`, `light_`, `recon_`, `heavy_`로 구분한다. 모델은 같은 위치에 있으므로 원하는 접두사만 표시한다.
- [세 벌 중립 조명 비교](output/outfit-collection/new-neutral-quarter.png), [분위기 조명 비교](output/outfit-collection/new-mood-quarter.png).
- [기본형 04 포함 비교](output/outfit-collection/lineup-neutral.png), [분위기 조명](output/outfit-collection/lineup-mood.png), [작은 화면 크기](output/outfit-collection/lineup-small.png).
- [위쪽 중립 시점](output/outfit-collection/new-neutral-top.png), [위쪽 분위기 시점](output/outfit-collection/new-mood-top.png).
- [자산 명세와 보호 해시](outfit-collection-manifest.json), [검사 결과](output/outfit-collection/validation.json).

이번 범위는 투구·조끼다. 새 세트에는 배낭이 없으며, 비교 렌더의 기본형도 같은 조건으로 배낭을 제외했다. 원래 후보 04 파일의 배낭은 보존했다. 내보낼 때는 접두사를 제거하여 `goose_helmet` → `goose_head`, `goose_vest` → `goose_torso` 부모 연결과 기존 몸·Muzzle 노드를 유지했다. Blender는 +Z 위/-Y 정면, GLB는 +Y 위/+Z 정면, 단위는 미터다.

## 측정과 검사

| 후보 | 투구 삼각형 | 조끼 삼각형 | 캐릭터 전체 삼각형 | 전체 메시 / 재질 수 |
|---|---:|---:|---:|---:|
| 경량형 05 | 3,940 | 3,776 | 22,914 | 13 / 18 |
| 정찰형 06 | 3,384 | 5,884 | 24,466 | 13 / 18 |
| 중장비형 07 | 5,040 | 7,880 | 28,118 | 13 / 19 |

전체 재질 수는 몸의 기존 재질도 포함한다. 메시 하나에 재질 슬롯이 여러 개 있으므로 메시 수와 드로콜은 같지 않다. LOD·재질 통합·실제 게임 성능 최적화는 이번 범위가 아니다.

실제 내보낸 9개 GLB를 빈 Blender 장면으로 다시 가져오는 검사 **182개 통과**:

- 기존 게임 거위 GLB와 이전 후보의 GLB·Blender 원본 SHA-256 보존.
- 생성 단계에서 각 후보의 몸·얼굴·날개·발·꼬리 메시 11개 정점·면 완전 일치, 세계 변환 오차 0.000001 미만 확인.
- 내보낸 메시·삼각형 수, 축·경계, 면적 0인 면 없음, 장비 분리·부모 연결·Muzzle 보존, 스튜디오 객체 제외 확인.
- 천과 투구의 roughness/metallic 재가져오기 값 확인.
- 각 조끼와 기본 크기 몸통 사이의 삼각형 표면 교차 없음.
- 세 벌 × 원래 날개 양쪽 × 기본/앞 20°/뒤 15°/바깥 20°/앞 20°+바깥 15° = **30개 자세 검사에서 조끼와 표면 교차 없음**. 연속 동작 전체의 보증은 아니다.

중립 5시점×4벌과 분위기 3시점×4벌로 원본 PNG 32장, 비교판 10장을 생성한다. Blender 5.2.2 LTS / Cycles 24 samples / denoise / 일반 720×840, 작은 화면 720×480. 비교판은 실제 렌더를 배열하고 표제를 붙인 것이며, 모델 이미지를 재생성하거나 보정하지 않았다. 모든 복장은 같은 몸·포즈·카메라·조명 조건으로 비교한다. 분위기 조명은 차가운 주변광과 따뜻한 주광이며 실제 게임 화면이 아니다.

최종 생성·검사·비교판 명령은 모두 종료 코드 0이었다. PNG 42개를 확인했고, 세 벌의 방향별 모음·분위기 비교·기본형 포함 비교·작은 화면 비교를 직접 열어 확인했다. 생성 로그에는 Blender 6.0에서 제거 예정인 `Material.use_nodes`의 `DeprecationWarning`이 있다. 현재 5.2.2에서 생성과 검사는 성공했지만 6.0 호환 검증은 하지 않았다.

## 재현

저장소 루트에서 실행한다. 입력은 기본 방탄형 04 Blender 원본과 명세다. 출력은 이 독립 폴더 안으로 제한된다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/build_outfit_collection.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/validate_outfit_collection.py
python prototypes/grounded-art/tools/compare_outfit_collection.py
```

생성 명령에 `-- --preview`를 붙이면 새 세 벌의 중립 사선 렌더만 만든다. 전체 비교판에는 전체 실행이 필요하다. 비교 도구는 기존 Pillow를 사용한다. `output/`은 로컬 재생성 산출물이며 버전 관리에서 제외한다.

## 남은 범위와 기록 검토

Godot 적용, 실제 무기용 팔·장전·달리기·사망, 머리 스프링, 체형 슬라이더, 다른 동물, 다른 상의와 겹쳐 입기, 투구·조끼 교차 조합, 멀티와 성능은 검증하지 않았다. 게임은 무기를 들 때 원래 날개 대신 별도 팔을 사용하므로 위 날개 검사를 게임 조준 자세 검사로 해석하면 안 된다. 장비 노드 분리는 준비했지만 게임 복장 메뉴·저장 슬롯·색상 채널 연결은 별도 작업이다.

2026-10-23 `doc/decisions.md`, `doc/issues.md`, `doc/character-model-spec.md`를 검토했다. 게임 기능·동작·제품 계약을 변경하지 않은 독립 아트 후보이므로 기존 제품 결정·이슈 ID, 기능 현황을 변경할 사항은 없다. 사용자가 기존 게임 파일 보존을 요청해 기록은 이 폴더에 남겼다. 기존 게임 파일·이전 모델과 생성기·Git 브랜치·인덱스를 수정하지 않았다.
