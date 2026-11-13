# Project Development Status

프로젝트의 개발 진행 상황을 기록하는 기준 문서입니다.

## 1. 진행 상태 요약

| 시스템 | 하위 기능 | 상태 | 비고 |
|---|---|---|---|
| Player | WASD 이동 | 구현 완료 | 기존 구현 완료 |
| Player | Shift 스프린트 | 구현 완료 | 기존 구현 완료 |
| Player | Space 대시 (일반) | 사용자 확인 대기 | dashSpeed 상향 (10.0 -> 15.0) |
| Player | 스프린트 중 대시 연계 | 사용자 확인 대기 | 스프린트 중 Space 대시 및 복귀 처리 |
| Player | 대시 안전장치 (OnDisable) | 사용자 확인 대기 | 비활성화 시 대시 잠김 방지 |
| Entity (DOD) | EntityManager | 사용자 확인 대기 | Decisions.md 확정 명세 및 확장 로직 구현 완료 |
| Entity (DOD) | EntityId | 사용자 확인 대기 | readonly struct 및 생성자 제한 구현 완료 |
| Entity (DOD) | Entity EditMode Tests | 사용자 확인 대기 | 15개 검증 항목 NUnit 단위 테스트 작성 완료 |
| Combat | CombatManager & HealthData | 사용자 확인 대기 | 체력 관리, 피해/회복, 사망 판정, EntityId Generation 연동 구현 완료 |
| Combat | CombatReceiver | 사용자 확인 대기 | MonoBehaviour 공통 수신 컴포넌트 구현 완료 |
| Combat | Combat EditMode Tests | 사용자 확인 대기 | 13개 검증 항목 NUnit 단위 테스트 작성 완료 |

## 2. 사용자 확인 대기 항목

- **스프린트 중 대시 동작 및 속도 체감 검증**
  - 스프린트(속도 10.0) 도중 Space 키 입력 시 순간 돌진 속도(15.0)로 가속되는지 확인
  - 대시 완료(0.2초) 후 스프린트 키 유지 시 멈칫거림 없이 정상 스프린트 속도로 복귀하는지 확인
  - 대시 도중 오브젝트 비활성화 후 재활성화 시 이동 불가 버그가 해결되었는지 확인
- **Entity System EditMode 단위 테스트 실행 검증**
  - Unity Editor의 Test Runner 창(Window > General > Test Runner)에서 `EntityManagerTests` 실행
  - 15개 테스트 케이스(생명주기, LIFO FreeList, 배열 2배 확장, 동등성) 통과 여부 및 컴파일 정상 확인
- **Combat System EditMode 단위 테스트 실행 검증**
  - Unity Editor의 Test Runner 창에서 `CombatManagerTests` 실행
  - 13개 테스트 케이스(피해 적용, 오버플로 방지 회복, 사망 판정, Generation 안전성, 배열 동반 확장) 통과 여부 확인

## 3. 다음 예정 작업

- 플레이어 Collider2D 추가 및 물리 충돌 환경 구성
- 사용자 확인 완료 후 `Entity` 및 `Combat` 시스템 "구현 완료" 상태 전환
- 투사체 및 무기 시스템(피격 판정 연결) 기획 및 설계

## 4. 확인된 이슈 및 주의 사항

- 네임스페이스 오타(`Prohect_DOD_Undergird`): 추후 사용자 승인 후 일괄 리팩토링 검토 필요
- Unity Editor 컴파일 및 플레이 테스트는 AI가 직접 확인할 수 없으므로 사용자 확인 필요
