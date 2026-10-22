# 야외 모델 팩 구현 계획

Goal: 다른 AI의 저장소 변경 없이 옮겨 쓸 수 있는 야외 모델과 시각 카탈로그 제작.
Architecture: Blender 생성 → 독립 GLB/조형 원본 → manifest → Godot 검수/PNG → HTML 목록.
Spec: [outdoor-kit-spec.md](outdoor-kit-spec.md)

- [x] 1. 참고 2편/이미지, 기존 A-7·C-23·L-13·I-80 계약 읽기. 구조 검증 도구를 먼저 실행해 모델 부재 확인.
- [x] 2. 28종 모델 제작, Keep/Occlude 역할 보존, GLB 내장 PBR. 외부 텍스처 의존 없음.
- [x] 3. 독립 Godot 검수로 로딩·부위 분리와 렌더 확인. 원본·가림·조명/날씨 샘플 생성.
- [x] 4. 검색/분류/이미지/모델 링크를 포함한 HTML 생성 및 브라우저 확인.
- [x] 5. doc/decisions.md·issues.md·feature-checklist.md와 통합 인계 설명을 이 폴더 안에 기록. 원 저장소 문서는 교체하지 않고 병합하도록 안내.
- [x] 6. 최종 검증/검토, 패키지 해시와 안전한 복사 도구 준비. 원 게임 미적용을 명시.

실행: 현재 세션에서 직접 진행. 소품 제작/카탈로그는 사용자 요청으로 승인된 가역적 작업이다. 별도 Git 커밋·원 저장소 스테이징/빌드·공유 파일 수정 없이 완성한다.

구현 판단: 이번 저밀도 장식은 별도 Detail 그룹 없이 Keep에 합쳐 유지한다. 부속은 재질 표면으로 구분하며 실제 가림 대상만 Occlude로 분리했다. 자동 판정/날씨/차량 기능은 추가하지 않았다. requesting-code-review 절차에 따라 읽기 전용 검토를 수행했고 OUT-I1을 발견·수정 후 재검토에서 차단 사항 없음을 확인했다.

## 2차 조형 보완

사용자 피드백에 따른 32종 확장과 상세 검사는 [nature-revision-spec.md](nature-revision-spec.md), 최신 결과는 [validation-report.md](validation-report.md)를 기준으로 한다. 위 28종/브라우저 확인은 1차 완료 이력이다.
