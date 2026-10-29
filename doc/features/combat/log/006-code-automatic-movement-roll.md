# 걷기·자동 달리기·2회 구르기 구현

- 요청: 장비 슬롯은 뒤로 두고 확정한 이동·구르기를 먼저 플레이 가능하게 구현.
- 변경: 걷기3.6m/s·자동 달리기4.2m/s·대기2초, 기본 구르기2회·종료부터 순차6초, 두 번째 동작 중 회복 지속. 일반 피격은 피해와 달리기 대기만 갱신하고 구르기는 지속. Player와 적 근접 양쪽의 피해 면제 제거.
- 표시·멀티: HUD 충전 수/진행/초와 도움말, 프로토콜20의 충전 스냅샷·입력 확인 보호, 방향 없는 멀티 피격도 걷기 복귀.
- 구현 판단: 보정 후 일반 이동 상한4.2m/s(기존 SMG110%가4.62m/s가 되는 모순 방지), 특수 강제 이동/잡기 면역 유지, 새 개체2회·부활 시 기존 충전 유지. [상세 규격](../equipment-movement-plan.md) 10장.
- 결정·이슈 검토: C-1·C-35 구현 후속. C-34 장비는 미구현. 이번 변경에 필요한 확정 규칙 갱신이며 별도 가짜 이슈를 발급하지 않는다. 기존 종료 경고/비결정적 검사 이력은 삭제하지 않는다.
- 실패 재현→수정: 변경 전 Integration149 통과. 새 MovementCheck는 걷기3.6에서 실패, 멀티 추가 검사는 호스트 충전 수신에서 실패, SMG는 이동 상한에서 실패했다. 각 구현 후 통과했다. 지연 특수무기 추가 발사 직후 달리기 대기가1.40초였던 계약 누락도 MovementEchoCheck로 먼저 재현하고 실제 추가 발사에 대기를 갱신해 통과했다.
- 제작 중 실패: 새 검사 지역변수 이름 충돌로 C# 빌드1회 실패 후 이름 수정. 최초 소스 검색의 없는 파일 경로는 실제 목록으로 정정. 제품 실행 실패와 구분한다.
- 검사 중간 실패: 적이 살아 있는 NetSyncCheck 중간에 새 이동 검사들을 넣은 실행에서 기존 보간 오차(mean0.60/max3.08) 및 새 피격 확인 타이밍 검사 실패가 있었다. 테스트 상호 간섭 가능성이 있어 새 검사를 적 없는 독립 MovementNetCheck로 분리했고 기존 NetSyncCheck 소스는 원상 유지했다. 제품 네트워크 버그를 발견·해결했다고 단정하지 않는다. 실패 로그는 `game/output/movement-final-NetSyncCheck.log`, `movement-verify-NetSyncCheck.log`.
- 최종 통과: MovementCheck22(헤드리스·실제 창 각각), MovementEchoCheck1, MovementNetCheck12, Integration149, NetCodecCheck24, NetSyncCheck32, EnemyAttackCheck79, WeaponAimCheck32, TraversalQualityCheck55, CommonRosterPlayCheck7, ReloadUiCheck38. 최종 빌드 경고0·오류0, 가져오기 및 위 통과 로그에 ERROR/WARNING 없음. 종료 경고 없음. Start-Game의 Test는 Integration만 실행하므로 나머지 장면은 각각 직접 실행했다.
- 검사 환경: Windows, Godot4.7.2 .NET, 한 PC 실제 ENet 두 월드. 로그는 `game/output/movement-{final,verify,edge,release}-*.log`, 실제 창 검사는 `movement-window.log`. 빌드/가져오기는 `movement-release-build.log`·`movement-release-import.log`. output은 로컬 재생성 산출물로 커밋하지 않는다.
- 렌더:1600×900 실제 창의 `game/output/movement-full.png`·`movement-one.png`·`movement-zero.png`를 직접 확인.2/2·1/2·0/2, 충전 점·다음 회복 원호·초가 HUD에서 겹치거나 잘리지 않는다.
- 코드 리뷰: 새 리뷰어가 독립 검토했으며 Critical/Important 없음. Minor로 제기한 지연 추가 발사 대기 갱신 누락은 기능 계약에 해당해 이번 작업에서 수정·재현 검사했다.
- 플레이: 검증한 작업 폴더의 게임 메뉴를 `--large-stages`로 실제 창 실행했다. 게임 시작에서 캠페인을 선택해 플레이한다. 재실행은 해당 폴더의 `game/Start-Game.ps1 -GodotDir C:/Util/Godot/Godot_v4.7.2-stable_mono_win64 -LargeStages`.
- 남은 제약: 수치 체감·실제2PC/인터넷·장시간/저사양은 미검증. 무기/카드 양수 이동 보정은4.2 상한 아래에서만 유효하며 난전 밸런스는 후속. 장비 슬롯은 구현하지 않았다. 기본 구르기 모션·속도/거리와 특수 강제 이동/잡기 면역은 유지한다.
- 관련 ID: C-1 · C-35 · C-34(범위 제외).
