# 무기 계열 조준점 · 탄창 잔량 · 발사 방식

관련: C-30 · C-32 · M-33 · M-36. 사용자 수락한 [추천 설계·계획](../weapon-reticle-plan.md)을 구현했다. 디비전1 영상10개의 계열 형태와 사용자가 설명한 주변 게이지의 탄창 잔량 의미를 참고했다. 사용자 후속 요청인 기존 충전 UI 수준의 반투명 바탕·색 가장자리·경계 처리를 일반 조준점까지 적용했다.

## 변경

- `HudReticle.cs`: 권총/SMG/소총/LMG/산탄/저격(DMR 길이 차이) 형태, 현재 퍼짐 우선·옅은 조준 기준, 중앙점과 반투명 받침·청회색 경계·흰 중심선. 총 성능이나 집중 판정은 바꾸지 않는다.
- 산탄 표시는 실제 발사 경로의 `PelletSpread`와 `AimProgress`를 읽어 더블배럴의 넓은 산탄도 반영한다. 도형은 수평 각도 안내이며 정확한 화면 탄착 원을 주장하지 않는다.
- 오른쪽 아래 원호 = `Mag/MagSize`. 단발 한 획/n점사 n획/자동 두 꺾쇠는 폰트 없는 도형이며 `BurstCount`가 `Automatic`보다 우선한다. 예비탄 무한과 관계없이 탄창은 유한하게 표시한다. 근접·꽥·고무오리는 총기 원호/심볼을 표시하지 않는다.
- 원호 반경/시작각/길이가 기존 진행 링으로 바뀐다. 잔량과 진행 타이머는 보간해 섞지 않고 전환 구간에서 교차 페이드한다. 장전 완료/취소/한 발 삽입/무기 교체/특수무기 충전은 기존 상태를 따른다. 하단 탄수·부족탄 경고·진행 퍼센트는 유지하고 커서 아래 부족탄 문구는 제거했다.
- 화면 오른쪽/아래 끝에서는 보조 표시만 화면 안쪽 사분면으로 바뀌고 조준 중심은 유지한다. 집중 호는 주 눈금 바깥·탄창 원호 안쪽에 둔다. 공격 불가·투척 조준·메뉴 우선순위 유지.
- 한/영 Esc 조준 설명과 `weapon-spec.md` 6.4/8장, C-30/M-33 현재 내용·변경 이력, 결정 색인·체크리스트 갱신. 새 리소스의 `.uid` 포함.

## 검사

아래 로그/렌더는 `game/output/` 로컬 산출물이다. 수정 전 `ReticleCheck`에서 탄창 절반 소모 후 주변 표시가 변하지 않아 **실패**(`reticle-red.log`)함을 확인했다. 검토 중 가장자리에서 표시가 사라짐을 **실패**(`reticle-edge-red.log`)로 확인하고 보정했다.

| 실행 | 결과 | 로그 |
|---|---|---|
| 시작 Debug 빌드 / ReloadUiCheck 헤드리스 | 오류·경고0 / 38 통과 | `reticle-baseline.log` |
| 최종 Debug / ExportRelease 빌드 | 각각 오류·경고0 | 명령 출력 |
| Godot 에셋 가져오기 | 종료0, 오류·경고 없음 | `reticle-final-import.log` |
| ReticleCheck 1600×900 한국어 | 52 통과, 오류·종료 경고 없음 | `reticle-edge-green.log` |
| ReticleCheck 1280×720 영어·창고 본 구역 | 52 통과, 오류·종료 경고 없음 | `reticle-final-720.log` |
| ReloadUiCheck 실제 렌더 | 42 통과, 오류·종료 경고 없음 | `reticle-final-reload.log`, `reticle-transition.log` |
| WeaponAimCheck 실제 렌더 한국어/영어 | 각32 통과 | `reticle-WeaponAimCheck.log`, `reticle-aim-english.log` |
| WeaponAimCheck / WeaponHandlingCheck 최종 헤드리스 | 32 / 36 통과, 오류·종료 경고 없음 | `reticle-verified-WeaponAimCheck.log`, `reticle-verified-WeaponHandlingCheck.log` |
| Integration 최종 단독 헤드리스 | 149 통과, 오류·종료 경고 없음 | `reticle-integration-serial.log` |

새 픽셀 검사는 실제 HUD를 고정된 월드에 렌더한다. 검은 받침으로 배경 애니메이션을 분리하고 같은 준비 상태의 full/half/empty, 권총 예비탄 무한, 계열 형태, 같은 소총의 자동/점사, 장전 중간 전환·취소·완료·한 발 삽입·교체, 근접·특수 비총기 제외, 공격 불가, 우측·하단·우하단 경계의 잔량 가독성을 비교한다. UI 전체를 숨겨서는 통과할 수 없다. 밝은 회색 받침은 추가 시각 확인용이다.

### 검사 도구·검토에서 수정한 것

- 초기 캡처는 `Stage.ProcessMode=Disabled`만으로 Hud도 멈춘다고 가정했다. Hud는 `Always`여서 수동0.04초 뒤 대기 중 다시 진행했다. `Hud.SetProcess(false)`를 추가하고 수동 시간으로만 진행하며 중간 모습이 준비/완료 모습과 다른지 검사한다.
- 초기 캡처에서 비활성 물리 공간에 `Player._PhysicsProcess`를 직접 호출해 Jolt `Parameter space is null` 오류가2회 나왔다. `SetControls`가 이미 조준 위치를 갱신하므로 불필요한 물리 호출을 제거했다. 이전52개 미만 통과 로그의 오류를 무경고 통과로 취급하지 않는다. 최종 두 해상도 실행에는 이 오류가 없다.
- 읽기 전용 독립 리뷰의 P2 두 건(화면 가장자리 클리핑, 캡처 시간 비결정성)을 위처럼 수정했다. C-30 현재 설명의 옛 대각 열기 문구를 갱신하고 변경 이력을 해당 ID 아래로 옮겼다.
- 최종 검사 묶음 중 통합 종료 확인 전에 전환 렌더를 시작해 같은 worktree의 실행이 잠시 겹쳤다. 빌드/가져오기와는 겹치지 않았지만 저장소의 단독 실행 규칙에 맞지 않으므로 두 종료 뒤 Integration을 별도 단독 재실행한 로그를 최종 근거로 사용한다.

## 렌더

- `reticle-family-sheet.jpg`: 실제 게임 렌더를 자른 무기8종 비교. 새 콘셉트 그림이 아니다.
- `reticle-{m1911,mp5,m4,m16,m249,r870,benelli_m4,double_barrel,awm,svd,m82}.png`: 실제 크기 확인. `-outdoor` 파일은1280×720 영어 창고 본 구역.
- `reticle-bright-background.png`: 밝은 배경에서 받침·경계의 대비 확인.
- `reticle-reload-enter-scene.png`, `reticle-reload-half.png`, `reticle-reload-cancel.png`, `reticle-reload-returned.png`: 준비/장전 중간/완료·취소 전환.
- `reticle-reload-transition.mp4`: ReloadUiCheck 실제 프레임70장에 시작/끝 유지36프레임을 붙인1600×900·30fps·106프레임 영상. 전투 플레이 녹화가 아닌 고정 검사 장면이다.
- `aim-focus-ready.png`, `aim-hit.png`, `reload-ui-recharge-hit.png`: 집중/명중/재충전 중첩. `aim-controls-open.png`:720p 영어 설명 전체가 화면 안에 보임.

재실행: Godot `--path game --resolution 1600x900 res://tests/ReticleCheck.tscn`, 영어/본 구역은 `--resolution 1280x720`와 `-- --outdoor --english`. ReloadUiCheck는 `-- --still-only`를 빼면 전환 프레임을 저장한다. `Start-Game.ps1 -Test`는 Integration만 실행하므로 전용 검사는 따로 실행한다.

## 기록 검토·제약

기존 C-30/M-33의 표시 계약을 갱신한 작업이며 새 게임 규칙·새 확정 제품 버그 ID는 만들지 않았다. 기존 이슈 상태·옛 기록은 유지한다. 검사 중 수정한 초기 구현/캡처 문제는 이 기록에 보존한다.

최종 외형 수락 전 제작값이다. 모든 거리·해상도·배경 조합, 실제2PC, 저사양·6인 호드 장시간 성능, 전체 패키지 내보내기는 미검증이다. 기존 수평 퍼짐 투영·명중 보조 규칙은 유지한다. 조준점 자체는 커서가 화면 끝에 있으면 일부 잘릴 수 있으며, 이번 보정은 신규 탄창/방식 보조 표시를 대상으로 한다.

최종 문서 검사: 새로 추가/변경한 로컬 링크12개 존재, 한/영 CSV 키 중복 없음·조준 설명 각1개, `git diff --check` 통과. 최종 단독 Integration 종료0·149 통과를 확인한 뒤 코드와 기록을 함께 커밋했다.
