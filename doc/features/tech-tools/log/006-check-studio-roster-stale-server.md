# Studio roster 페이지 404 복구

- 확인일: 2026-10-26. 관련 결정 T-25. 사용자 보고: `/roster`가 없는 페이지로 표시됨.
- 변경: 기존 Studio 프로세스를 종료하고 현재 작업 폴더의 실행기로 재시작했다. 코드·게임 데이터 변경 없음.

## 확인과 원인

- 재시작 전 `/roster`는 HTTP404 `Not found`, `/api/health`는 ready=true였다. 앞선 [005 준비 대기 진단](005-check-studio-ready-timeout.md) 이후 렌더러는 다시 실행 중이었다.
- Node PID47408은 2026-10-25 01:44:59 시작한 프로세스였고, 현재 `studio/server.mjs`에는 `/roster` 정적 경로와 `/api/roster` 경로가 있었다. 실행 중인 이전 서버 코드에 신규 라우트가 반영되지 않은 상태로 확인했다.
- 웹의 로컬 데이터 다시 읽기는 Godot만 다시 빌드/import/시작한다. Node의 메모리에 로드된 HTTP 경로는 갱신하지 않는다. 실행기 역시 같은 폴더/포트의 ready 서버를 재사용하므로 서버 코드 변경 여부는 검사하지 않는다.

## 복구와 검증

- 프로세스 명령행으로 해당 저장소의 Studio임을 확인한 뒤 PID47408과 그 자식 렌더러만 종료했다. 다른 Godot/build 프로세스가 없음을 확인하고 `Start-Studio.ps1`을 숨김 실행해 기존 방식대로 최신 빌드/import/렌더러 준비를 진행했다.
- 새 서버 PID7220: `/roster` HTTP200, `/api/roster` 종7개·검증 오류0, `/api/roster/render` 실제 독수리/MP5 미리보기 응답 확인. 반환 PNG도 열어 확인했다.
- 실행 로그: `studio/output/roster-restart.log`, `roster-restart-error.log`(비어 있음), 실제 렌더 `studio/output/roster-restart-preview.png`. 자동 산출물은 커밋 대상이 아니다.
- 이번은 실행 환경 복구이며 자동 버전 감지·서버 재시작 기능을 구현하지 않았다. Node 서버 코드가 바뀌면 전체 Studio 재시작이 필요하다는 제약은 남는다. Integration/전체 웹 회귀/종료 ObjectDB 검사는 미실행.
- 기존 결정의 목적·기능 현황은 변경하지 않는다. 새 결정이나 이슈 ID를 발급하지 않고 재현 상태·원인·복구·남은 제약을 이 진단 기록에 보존한다.
