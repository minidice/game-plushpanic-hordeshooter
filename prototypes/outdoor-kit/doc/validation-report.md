# 야외 모델 팩 검증 기록 — 2차 조형

2026-10-22. 아래는 최신 32종 팩의 검사 결과이며 현재 게임에 적용한 검증이 아니다.

| 검사 | 결과 | 재현/근거 |
|---|---|---|
| GLB 구조·역할·예산 | 32종, 총 121,090삼각형. 각 16,000 이하. 외부 파일 의존 없음 | `python tools/validate_pack.py`, `output/validate-pack-r2.log` |
| Godot 가져오기 | 32종의 실제 정점 크기·바닥 피벗·역할 계층 통과 | `Start-Preview.ps1 -Validate`, `output/validate-r2.log` |
| 하천 연결 | 양 모듈의 연결점 4개 위치, 수면/흙/잔디 실제 끝단 정점 단면 일치, 정점색 가져오기 확인 | `preview/validate_import.gd` |
| 가림 계약 | 나무 5종+열린 컨테이너, 총 172표면. 선택 부위·인스턴스 격리·원상 복원 통과 | `-- --validate-preview` |
| 실제 렌더 | Godot Forward+ 58장: 모델32, 가림13, 비교2, 조합/날씨6, 자연 조형2, 물가3. capture-errors.log 비어 있음 | `output/capture.log`, `gallery/images` |
| 이미지·문서 | 32카드, 154 HTML 링크/이미지 참조, Markdown 상대 링크, Python 구문 확인. 13종의 가림 이미지가 각각 원본과 다름 | `python tools/validate_delivery.py` |
| 복사 도구 | 임시 대상에 35파일 해시 일치, 기본 실행 무변경, 덮어쓰기/잘못된 루트 거부 | `python tools/validate_delivery.py` |

최종 읽기 전용 검토에서 차단 문제 없음. 변경된 10종의 실제 GLB에서 인덱스/정점/퇴화 면/노멀을 확인했고, 별도 배포 검사에서 ZIP 132항목의 CRC와 모든 파일 SHA256을 확인했다.

겹친 잎 침엽수, 둥근 참나무, 바위, 자연 조형 전체/부분 가림, 물가 확대 실제 렌더를 직접 확인했다. 비교용 이전 이미지 4장은 도감에 별도로 포함한다. 수면 경계 샘플은 경사 둑·젖은 흙·정점색·부분 포말 방식이며 물리 수심이나 굴절이 아니다.

브라우저 UI 재검수는 `file:` URL 접근이 브라우저 보안 정책으로 차단되어 실행하지 못했다. 1차 때의 브라우저 결과를 2차 결과로 간주하지 않는다. 이번에는 HTML 파일/이미지/링크와 실제 Godot 렌더를 검사했다. 자동 승인 요청이나 다른 브라우저 우회는 하지 않았다.

실패/수정: 기존 28종에서 신규 32종 요구 검사가 예상대로 실패했고, 새 하천 연결점 이름 검사는 굽이 모델에서 실패한 뒤 수정/재생성해 통과했다. 생성 헬퍼 변수 충돌, 이끼의 가는 틈과 규칙적인 물 반짝임도 수정했다. Blender의 Material.use_nodes 6.0 제거 예정 경고는 남아 있다. 실제 엔진 가져오기/캡처 오류는 없다.

현재 게임 Integration/MapCheck/VisionTierCheck, 자동 가림, 실제 물리/길찾기, 멀티/호드 성능은 미검증이다. 아트 최종 승인과 대규모 식생 LOD/인스턴싱은 후속이다. 변경 이유/잔여 제약은 [decisions.md](decisions.md), [issues.md](issues.md), [보완 규격](nature-revision-spec.md)에 기록했다.

## 1차 검증 — 과거 이력 (현재 팩 수치가 아님)

### 1차 기록

2026-10-22. 현재 게임 통합 검사는 이 독립 팩 검사에 포함되지 않는다.

| 검사 | 결과 | 근거/재현 |
|---|---|---|
| GLB 독립 구조/명명/재질/삼각형 예산 | 28종 통과. 합계 49,104삼각형(전체 재고 합계, 한 화면 부하 아님). 외부 버퍼/이미지 의존 없음 | `python tools/validate_pack.py`, 로컬 `output/validate-pack.log` |
| Godot 실제 가져오기 | 28종 통과. 부위 계층·실제 정점 기준 크기·최하단 y=0 확인 | `Start-Preview.ps1 -Validate`, `output/validate-import.log` |
| 가림 재질 계약 | 48개 표면 검사. 작은 나무/열린 컨테이너의 선택 부위 변경·다른 인스턴스 격리·복원 통과 | `-- --validate-preview`, `output/validate-preview.log` |
| Forward+ 실제 렌더 | 47장: 모델 28 + 가림 11 + 비교 2 + 배치/날씨 6. 최종 capture-errors.log 비어 있음 | `Start-Preview.ps1 -Capture`, `gallery/images` |
| 가림 이미지 구별 | 대상 11종 모두 원본/가림 WebP의 픽셀 차이 확인 | `python tools/validate_delivery.py` |
| HTML/문서 링크 | HTML 카드 28개, 링크/이미지 참조 125개와 Markdown 상대 링크 확인 | `output/validate-delivery.log` |
| 브라우저 동작 | 식생 8개 → 침엽수 검색 2개 → 없는 검색어 0개, 초기화 28개. 확대/닫기, 원본/가림 토글, 카드/목록 전환, 비교 슬라이더 100%, 적설/낮 전환 확인 | Codex 내장 브라우저, 로컬 `gallery/index.html` |
| 안전한 복사 | 임시 `output/copy-test-*`에서 기본 실행 무변경, 30파일 복사·해시 일치, 기존 폴더/잘못된 루트 거부, 대상 프로젝트 파일 보존 | `python tools/validate_delivery.py` |
| 독립 검토 | OUT-I1 중복 메시 이름 문제 발견 → 수정 후 재검토에서 해당 문제 해결, 추가 차단 사항 없음 | requesting-code-review 읽기 전용 검토 |
| 배포 ZIP | 109개 항목, 28 GLB 포함. ZIP CRC와 모든 파일 SHA256 확인. 캐시·로그·참고 원본 제외 | `python tools/package_delivery.py`, `delivery/plushpanic-outdoor-kit.zip.sha256` |

실제 렌더는 Godot 4.7.2 Forward+에서 수행했다. 작은 나무 가림 상태, 열린 컨테이너, 전체 조합, 정상/가림 비교, 석양·적설 이미지를 직접 확인했다. 전체 모델 접촉시트도 확인했다. 브라우저는 약 700px 폭의 앱 패널에서 확인했으며 별도 모바일 기기 검증은 아니다.

실패/수정 이력은 [issues.md](issues.md)의 OUT-I1과 제작 준비 문제에 보존했다. 최종 Godot 가져오기·계약 검사·캡처에서는 ERROR/종료 ObjectDB 경고가 없었다. Blender에는 6.0에서 `Material.use_nodes` 제거 예정이라는 DeprecationWarning 1종이 남아 있으며 생성 결과는 성공했다. 외부 브러시 원본 경고는 제거했다.

게임의 Integration/MapCheck/VisionTierCheck, 실제 캐릭터 이동/가림 자동 감지, 멀티 2PC/6인·저사양·장시간 부하는 미검증이다. 적설은 재질 샘플이며 지붕 차폐를 계산하지 않는다. 모델 조형·가림 농도는 1차 제작안으로 최종 사용자 아트 평가는 남아 있다.

참고 자료: 사용자 PNG 및 `지형참고.mp4`(50.83초), `전투및지형참고.mp4`(68.03초)에서 각 9개 시간 표본 프레임을 추출해 공간/오브젝트를 살폈다. 원본 영상의 내부 기술 구현을 확인한 것은 아니다. 원본 참고 영상/이미지는 배포 ZIP에 포함하지 않는다.
