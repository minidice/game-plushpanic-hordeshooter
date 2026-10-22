# 모자 후보 08–14와 곡면 고글

후속: 후보 10의 렌즈가 과하게 볼록하고 모자가 눌려 보인다는 피드백을 받아 [얼굴 액세서리 후보 15–19](FACE-ACCESSORIES-RESULTS.md)를 제작했다. 고글을 작고 얕게 만들고 정수리를 높였으며, 모자 없는 안경·선글라스·헤드셋도 추가했다. 이 문서의 후보 08–14 파일은 보존했다.

2026-10-23. 사용자는 이전 모자들이 너무 둥글고 챙이 없다고 지적했다. 중앙 줄무늬, 챙 있는 캡, 고글, 오토바이 헬멧, 야시경, 토끼귀, 꽃 장식 모자를 요청했으며, 이전 고글의 네모난 부분이 렌즈인지 질문했다.

이전 후보 05의 네모난 부품은 렌즈를 표현한 불투명 사각 판이었다. 렌즈와 프레임의 곡률·윤곽이 부족해 고글보다 판처럼 읽혔다. 새 후보 10은 **좌우가 분리된 볼록 타원 렌즈, 둥근 프레임, 코 연결부, 옆 고정쇠와 머리끈**으로 제작했다. 이전 모델은 비교용으로 보존했다.

## 이번에 만든 일곱 가지

| 후보 | 형태와 구별점 | 장비 단독 GLB | 캐릭터 포함 GLB |
|---|---|---|---|
| 08 중앙 줄무늬 투구 | 짧은 둘레 챙, 녹색 쉘, 앞뒤로 이어지는 밝은 중앙 줄무늬 | [투구](assets/goose-headwear-stripe.glb) | [캐릭터](assets/goose-headwear-stripe-character.glb) |
| 09 챙 있는 캡 | 긴 앞챙, 청색 천, 앞 패치와 정수리 단추 | [캡](assets/goose-headwear-cap.glb) | [캐릭터](assets/goose-headwear-cap-character.glb) |
| 10 곡면 고글 모자 | 갈색 가죽 모자, 좌우 볼록 렌즈와 둥근 프레임 | [고글 모자](assets/goose-headwear-goggles.glb) | [캐릭터](assets/goose-headwear-goggles-character.glb) |
| 11 오토바이 헬멧 | 노란 쉘·검은 중앙 줄무늬, 옆·뒷머리 덮개, 올려 쓴 곡면 바이저 | [헬멧](assets/goose-headwear-motorcycle.glb) | [캐릭터](assets/goose-headwear-motorcycle-character.glb) |
| 12 쌍안 야시경 투구 | 두 개의 원통과 녹색 볼록 렌즈, 연결 암·힌지, 측면 레일·뒤 배터리 외형 | [투구](assets/goose-headwear-nvg.glb) | [캐릭터](assets/goose-headwear-nvg-character.glb) |
| 13 토끼귀 모자 | 분홍 캡, 긴 귀 두 개, 한쪽 귀가 옆으로 접힌 비대칭 형태 | [모자](assets/goose-headwear-bunny.glb) | [캐릭터](assets/goose-headwear-bunny-character.glb) |
| 14 꽃 장식 모자 | 넓은 둘레 챙, 연한 녹색 천, 분홍 띠, 큰 꽃 한 송이와 잎 | [모자](assets/goose-headwear-flower.glb) | [캐릭터](assets/goose-headwear-flower-character.glb) |

오토바이 헬멧은 부리가 드러나는 오픈페이스형이다. 바이저는 올려 쓴 고정 외형이며 눈을 가리지 않는다. 야시경도 이마 위에 올린 장식 상태로, 실제 야간 시야 기능이나 접는 동작은 없다. 토끼귀는 고정된 천 형태이며 물리 시뮬레이션이나 별도 본을 추가하지 않았다.

일곱 모자 모두 같은 거위 몸·얼굴·기본 방탄형 04 조끼에 입혀 비교한다. 조끼의 색이나 체형 변화가 모자 차이로 보이지 않도록 고정했다. 배낭은 이번 비교에서 제외한다. 비교 기준인 “이전 고글 05”도 원래 머리 장비에 같은 몸·조끼를 조합한 것이며, 후보 05 파일 자체를 수정한 것은 아니다.

## 비교 이미지와 편집 원본

- [모자 확대 모음: 이전 사각 고글 + 새 일곱 가지](output/headwear-collection/headwear-details.png)
- [사각 고글과 곡면 고글의 같은 시점 확대 비교](output/headwear-collection/goggles-before-after.png)
- [캐릭터 전체 중립 조명](output/headwear-collection/headwear-neutral.png), [분위기 조명](output/headwear-collection/headwear-mood.png), [작은 화면 크기](output/headwear-collection/headwear-small.png)
- 방향별 모음: [줄무늬 투구](output/headwear-collection/stripe-overview.png), [캡](output/headwear-collection/cap-overview.png), [고글](output/headwear-collection/goggles-overview.png), [오토바이](output/headwear-collection/motorcycle-overview.png), [야시경](output/headwear-collection/nvg-overview.png), [토끼귀](output/headwear-collection/bunny-overview.png), [꽃](output/headwear-collection/flower-overview.png)
- [Blender 편집 원본](source/goose-headwear-collection.blend), [자산 측정값·보호 해시](headwear-collection-manifest.json), [검사 결과](output/headwear-collection/validation.json)

Blender 원본에는 여덟 비교 모델이 같은 위치에 있다. 처음에는 새 고글이 표시되며, 객체 이름의 `base_`, `stripe_`, `cap_`, `goggles_`, `motorcycle_`, `nvg_`, `bunny_`, `flower_` 접두사로 구분해 원하는 모델만 표시한다. GLB로 내보낼 때는 접두사를 제거했다. 모자 및 부착물은 한 개의 머리 장비 메시이며 `goose_helmet` → `goose_head` 연결을 유지한다. 단독 GLB에는 장비와 조상 노드만 있고 몸 메시가 없다. 고글·꽃·야시경 등을 별도로 켜고 끄는 런타임 슬롯은 이번 범위가 아니다.

## 측정과 검증

| 후보 | 머리 장비 삼각형 | 장비 재질 수 | 캐릭터 전체 삼각형 |
|---|---:|---:|---:|
| 08 줄무늬 | 8,880 | 4 | 31,146 |
| 09 캡 | 6,648 | 2 | 28,914 |
| 10 고글 | 10,788 | 5 | 33,054 |
| 11 오토바이 | 10,484 | 5 | 32,750 |
| 12 야시경 | 12,964 | 4 | 35,230 |
| 13 토끼귀 | 8,168 | 3 | 30,434 |
| 14 꽃 | 11,064 | 5 | 33,330 |

캐릭터 포함 GLB는 각 13개 메시, 머리 장비 단독은 1개 메시다. 장비 하나에 재질 슬롯이 여러 개 있으므로 메시 수와 드로콜은 같지 않다. 현재는 형상 검토 후보이며 실제 게임 성능·LOD 최적화를 마친 자산은 아니다.

내보낸 GLB **14개**를 빈 Blender 장면에 재가져와 **289개 검사 통과**:

- 이전 GLB·Blender 원본과 게임 거위 GLB의 SHA-256 유지.
- 생성 단계에서 각 후보의 기존 몸 11개와 조끼 1개의 정점·면 완전 일치 및 세계 변환 오차 0.000001 미만 확인.
- 내보낸 메시·삼각형 수, 경계·축, 면적 0인 면 없음, 부모 연결, 단독 파일에 몸 없음, 스튜디오·리그 제외, Muzzle 보존 확인.
- 일곱 모자의 기본 자세에서 몸통·부리·왼눈·오른눈·조끼와의 표면 교차 없음: **35개 정적 맞춤 검사**.
- 고글·바이저·야시경 렌즈 재질과 낮은 roughness가 GLB 재가져오기 후에도 유지됨을 확인.

제작 중 줄무늬가 투구에 묻힌 원인은 띠의 폭 방향을 직선 두 점으로 연결해 곡면을 가로질렀기 때문이다. 폭 방향 곡면 샘플을 추가하고 쉘 법선 방향으로 얇게 띄워 정리했다. 띠 끝은 투구 테두리 위에서 끝나도록 했다. 원통의 방향이 잘못된 문제는 부모 지정 전 평가되지 않은 세계 변환을 읽었기 때문이며, 변환 갱신 후 부모를 연결하도록 수정했다. 오토바이 헬멧은 둥근 정수리와 옆·뒤 덮개를 따로 구성해 얼굴 입구가 과도한 V형이 되지 않게 했다.

중립 조명 6시점×8모델, 분위기 조명 2시점×8모델로 원본 PNG **64장**, 비교판 **12장**을 생성한다. Blender 5.2.2 LTS / Cycles 32 samples / denoise. 일반 720×840, 작은 화면 720×480. 전체 캐릭터 비교는 같은 카메라·몸·조끼·조명을 사용한다. 모자 확대판은 장식 높이에 맞춰 화면을 채우며, 이전/새 고글 비교만은 확대 카메라도 정확히 동일하다. 이미지는 실제 모델 렌더에 표제를 붙인 것으로, 모델 픽셀을 AI로 재생성하거나 보정하지 않았다.

최종 생성·검사·비교판 명령은 모두 종료 코드 0으로 끝났다. PNG 76개의 파일 무결성과 문서 링크 88개, 생성·검사·비교 스크립트 구문을 확인했다. 보호 대상 기존 파일 24개의 해시가 유지되었다. 모자 확대판·고글 전후·전체 분위기 비교와 오토바이·토끼귀·꽃 모자의 측면을 직접 열어 확인했다. 생성 로그에는 Blender 6.0에서 제거 예정인 `Material.use_nodes`의 `DeprecationWarning`이 있으며, 현재 5.2.2에서는 성공했다. Blender 6.0 호환은 미검증이다.

## 재현과 남은 범위

저장소 루트에서 실행한다. 기존 후보 04·05를 포함한 복장 원본을 입력으로 읽으며 출력은 이 독립 폴더 안으로 제한한다.

```powershell
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/build_headwear_collection.py
& 'C:/Util/Blender Foundation/Blender 5.2/blender.exe' --background --python-exit-code 1 --python prototypes/grounded-art/tools/validate_headwear_collection.py
python prototypes/grounded-art/tools/compare_headwear_collection.py
```

생성 명령 뒤에 `-- --preview`를 붙이면 여덟 모델의 중립 확대 렌더만 생성한다. 전체 비교판에는 전체 렌더가 필요하다. 비교판 도구는 기존 Pillow를 사용한다. `output/`은 로컬 재생성 산출물이다.

Godot 적용·게임 성능·체형 슬라이더·머리 스프링·다른 종·후드나 다른 조끼와의 조합은 검증하지 않았다. 렌즈는 곡면·광택·색으로 표현한 불투명 스타일 재질이며 실제 굴절·투과나 야시경 효과를 구현하지 않았다. 긴 토끼귀는 외형 크기만 늘리며 게임 충돌 판정은 변경하지 않았다.

2026-10-23 `doc/decisions.md`, `doc/issues.md`, `doc/character-model-spec.md`를 검토했다. 이번 작업은 독립 모델 후보 추가이며 기존 게임 기능·동작·확정 계약을 바꾸지 않았다. 기존 게임 파일 보존 요청에 따라 제품 결정·이슈 문서는 수정하지 않고 제작·수정 원인·검증을 이 폴더에 기록했다. 이전 모델·생성기, 게임 파일, Git 브랜치·인덱스를 변경하지 않았다.
