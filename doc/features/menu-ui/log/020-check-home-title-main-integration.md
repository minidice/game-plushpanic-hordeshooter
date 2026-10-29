# 020 · 메인 화면·타이틀 main 통합 검증

작업일 2026-10-29. 관련 M-0/M-38, [메인 화면018](018-code-minimal-home.md), [타이틀019](019-code-escape-fur-dead-title.md).

## 사용자 요청과 통합 범위

- 사용자 ‘일단 커밋 머지 푸쉬 반영해줘’에 따라 메인 화면/ESCAPE FUR DEAD 제목/검사/기록 14파일을 `f6dc31a`로 커밋했다. 018의 미병합·미푸시 문구는 당시 요청 범위의 이력이며 이번 요청이 그 후속이다.
- 최신 `origin/main`의 `9f94529`(홍보 영상 도구)를 `afd6574`로 통합했다. 체크리스트 양쪽 추가 항목을 보존했고 충돌은 없었다. 다음 반영 대상은 로컬 main fast-forward와 origin/main 일반 push이며 강제 푸시를 사용하지 않는다. 이 기록 커밋을 포함한 원격 최종 SHA는 푸시 후 별도로 확인한다.
- 원래 main 폴더의 미추적 `.codex-remote-attachments/`, ignored output/캐시/빌드 산출물은 커밋 대상이 아니다. 기존 저장 경로·게임 규칙·자산은 그대로다.

## 실행한 검증

Windows / Godot 4.7.2 .NET. 빌드/검사는 같은 작업 폴더에서 순차 실행했다.

| 상태 | 검사 | 결과 | 로그 |
|---|---|---|---|
| UI 커밋 전 | dotnet Debug build | 오류0·경고0 | 명령 출력 |
| UI 커밋 전 | MenuCheck 실제1600×900 | 54 통과·종료0 | `game/output/ui-merge-menu.log` |
| UI 커밋 전 | Integration headless | 149 통과·종료0 | `game/output/ui-merge-integration.log` |
| 최신 main 통합 후 | dotnet Debug build | 오류0·경고0 | 명령 출력 |
| 최신 main 통합 후 | MenuCheck 실제1600×900 | 54 통과·종료0 | `game/output/ui-merged-menu.log` |
| 최신 main 통합 후 | Integration headless | 149 통과·종료0 | `game/output/ui-merged-integration.log` |

위 게임 로그에서 실패/엔진 오류/종료 경고 없음. 앞선018/019의 홈·영문·상세 화면 렌더 검토를 유지하며 통합 후 MenuCheck에서도 같은 캡처를 다시 생성했다. 새 UI 문서의 상대 파일 링크 존재와 `git diff --check`를 확인했다. Git의 CRLF→LF 정규화 안내는 게임/빌드 경고와 구분한다.

별도 읽기 전용 코드 리뷰에서 미리보기 재배치/언어 재생성/입력/로비 복귀/저장 경로/메타데이터에 병합을 막는 회귀는 발견하지 않았다. 검토자가 빌드/Godot 검사를 실행한 것은 아니며 위 결과는 구현 작업에서 실행한 결과다.

## 기록 검토와 남은 제약

이번은 승인된 기존 변경의 통합이며 새 제품 결정·버그 ID를 만들지 않는다. 캐릭터/배경 품질 개선, Tripo 도입, 로고 미술은 아직 수행하지 않았다. 새 이름의 배포 ZIP/실행 파일 재내보내기·실제2PC/6인·장시간 플레이를 검증한 것으로 확대하지 않는다.
