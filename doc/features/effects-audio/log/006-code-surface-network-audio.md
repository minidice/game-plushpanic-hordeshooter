# 환경 명중음 네트워크 동기화

관련: A-26, I-64, [표면 적용 계획](../surface-audio-plan.md), [후속 계획](../surface-followup-plan.md). 기준 커밋 `4bacc0d`, 독립 작업 트리 `surface-net-followup`, 브랜치 `codex/surface-net-followup`에서 작업했다.

## 변경

- 기존 `Shot`은 탄도 끝점만 보내므로 원격 환경 명중음이 없었다. 호스트 hitscan의 실제 접촉 재질·위치를 수집하고, `Shot` 끝점 목록 뒤에 전달한다. 빗나간 탄도와 Enemy/Player/Corpse 접촉에서는 환경 재질을 만들지 않는다. 낮은 엄폐 사격막은 기존 본체 재질 해석을 쓴다.
- 전송: U8 개수 0..3, 각 접촉은 U16 프리셋 번호 + XYZ cm int16(6B). 프리셋 번호는 이름의 Ordinal 정렬 순서이며, 추가 크기는 발당 1..25B다. 잘못된 개수·잘린 레코드·알 수 없는 프리셋은 전체 접촉 목록의 재생 전에 거부한다. 기존 길이 기반 이벤트 처리에서 해당 사건만 실패 처리한다.
- 총성·섬광·탄도는 기존 클라이언트 즉시 예측을 유지한다. 환경 접촉음은 호스트 결과만 재생하므로 자기 발도 결과를 1회 듣고, 자기 총성을 다시 재생하지 않는다. 클라이언트의 예측 충돌체와 호스트 재질이 달라도 중복 소리가 생기지 않는다. 네트워크 지연만큼 자기 환경 명중 확인음이 늦어지는 선택이다.
- 산탄은 모든 펠릿의 기존 피해·관통·충격 처리를 유지하고 오디오 수집/재생만 한 발당 최대3회다. 추가 발사 카드의 hitscan echo에도 같은 계약을 적용한다. 오프라인 사격도 동일한 3회 오디오 예산을 사용한다.
- `NetLobby.Protocol` 16→17. `NetIds.DataHash`에 `surface_profiles.json` 전체를 포함하여 프리셋 번호·게인·오브젝트 매핑이 다른 데이터로 접속하지 못하게 한다. 기존처럼 CRLF/LF만 정규화하며 JSON 서식 차이도 다른 해시다. Studio 저장 후 실제 게임 프로세스/양쪽 데이터의 갱신은 별도 작업이다.
- 피해/체력/탄성/마찰/충돌 레이어/재질 파일/음원/레시피는 변경하지 않았다. 새로운 결정·이슈를 만들지 않고 승인된 A-26 후속 및 I-64 부분 해결 범위를 확장한다. 전체 I-64 해결이나 최종 인간 청음 승인으로 표시하지 않는다. 중앙 규격·색인은 통합 작업에서 갱신한다.

## 검사

Godot: `D:/Util/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe`.

```powershell
dotnet build game/HordeShooter.csproj --nologo -v q
& $godot --headless --editor --import --path game --quit
& $godot --headless --path game res://tests/SurfaceAudioNetCheck.tscn
& $godot --headless --path game res://tests/SurfaceAudioCheck.tscn
& $godot --headless --path game res://tests/NetCodecCheck.tscn
& $godot --headless --path game res://tests/DestructionCompatibilityCheck.tscn
& $godot --headless --path game res://tests/CoverCheck.tscn
& $godot --headless --path game res://tests/Integration.tscn
git diff --check
```

- RED: 제품 코드 변경 전에 실제 두 ENet 피어/독립 물리 월드에서 호스트 소총 명중음을 확인했지만 원격의 `remote rifle contact reaches client once`가 실패했다. 프로세스 exit1. `game/output/surface-net-red.log`.
- 제작 중 경고: 테스트 필드 `Wall`이 `StageMap.Wall`을 가렸다는 CS0108 경고1개를 `MetalWall`로 이름을 바꿔 제거했다. 최종 빌드 경고0/오류0.
- GREEN: `SURFACE_AUDIO_NET_PASS 36`, exit0. 호스트/클라이언트 독립 월드, 실제 loopback `127.0.0.1` 바인딩, 상대발 재생1회, 본인 총성 중복 없음, 클라이언트만 목재로 바꾼 예측과 무관하게 권위 금속음1회, 빗나감/적 명중의 환경음 없음, 실제 적 피해, 모래주머니 피해/엄폐·본체 재질, 3개 초과 펠릿의 발당3음, 잘못된 데이터 거부, 25B 왕복, 프리셋 게인/매핑 해시·줄바꿈 동일성. `game/output/surface-net-green.log`.
- 회귀: `SurfaceAudioCheck`242, `NetCodecCheck`12, `DestructionCompatibilityCheck`14, `CoverCheck`27 각각 exit0. 로그 `game/output/surface-net-<장면>.log`.
- 일반 전체 통합: `INTEGRATION_PASS 149`, exit0. `game/output/surface-net-Integration.log`. 최종 가져오기/전용/위 회귀/통합 로그에서 오류·종료 경고 없음. `git diff --check` 통과.

## 렌더와 남은 제약

외형/화면 변경은 없으며 새 렌더와 실제 오디오 장치 캡처는 이 작업에서 실행하지 않았다. 재생 호출 수·위치/재질 전송 계약을 헤드리스로 검사했다. 실제2PC/인터넷 지연·손실/장시간 네트워크·인간의 자연스러움 청음은 미검증이다. 다른 무기의 음량 합산·오디오 장치 캡처는 통합 작업의 별도 검증이다. 로켓·적 투사체·근접·물체끼리의 충돌 접촉음은 이번 전송 범위 밖이다.
