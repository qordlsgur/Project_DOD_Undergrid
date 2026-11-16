# Project Development Status

프로젝트의 개발 진행 상황을 기록하는 기준 문서입니다.

## 1. 진행 상태 요약

| 시스템 | 하위 기능 | 상태 | 비고 |
|---|---|---|---|
| Player | WASD 이동 | 구현 완료 | 사용자 확인 완료 |
| Player | Shift 스프린트 | 구현 완료 | 사용자 확인 완료 |
| Player | Space 대시 (일반) | 구현 완료 | dashSpeed 상향 (10.0 → 15.0); 사용자 확인 완료 |
| Player | 스프린트 중 대시 연계 | 구현 완료 | 스프린트 중 Space 대시 및 복귀 처리; 사용자 확인 완료 |
| Player | 대시 안전장치 (OnDisable) | 구현 완료 | 비활성화 시 대시 잠김 방지; 사용자 확인 완료 |
| Entity (DOD) | EntityManager | 구현 완료 | Decisions.md 확정 명세 및 확장 로직 구현 완료 |
| Entity (DOD) | EntityId | 구현 완료 | readonly struct 및 생성자 제한 구현 완료 |
| Entity (DOD) | Entity EditMode Tests | 구현 완료 | 사용자 EditMode 테스트 15개 통과 확인 |
| World (DOD) | 월드 그리드 및 바닥·벽 데이터 1단계 | 구현 완료 | Unity 비의존 연속 배열, 좌표 변환, 벽 내구도·파괴, 변경 셀 이벤트 구현; Unity EditMode 테스트 16개 전부 통과 사용자 확인 |
| World (Unity) | Tilemap 렌더링 및 벽 충돌 2단계 | 사용자 확인 대기 | 64×64 Grid와 Floor/Wall Tilemap, 초기 블록 동기화 및 CellChanged 변경 셀 갱신 구현; 기존 Tile 색상·Collider를 보존하는 1×1 임시 Sprite 생성 및 연결 Editor 도구 추가, 실행과 Main 씬 확인 대기 |
| World (Unity) | 플레이어 벽 채굴 및 장비 능력치 3단계 | 구현 완료 | 좌클릭 공통 행동 라우팅, 커서 셀·장비 사거리 기반 벽 선택, MiningToolData 능력치와 WorldGrid 피해 연결; 돌벽 파괴·충돌 해제·이동 가능·바닥 유지까지 실제 플레이 사용자 확인 완료 |
| World (Unity) | 플레이어 추적 카메라 | 구현 완료 | 기존 Main Camera의 Player 부드러운 추적과 WorldGrid 경계 보정; 카메라 시작 정렬 및 실제 추적·경계 동작 사용자 확인 완료 |
| Combat | CombatManager & HealthData | 구현 완료 | DOD HealthData 단일 원본 유지; 사망 상태에서도 등록된 세대의 최대 체력을 조회하도록 보강; 관련 PlayMode 확인 완료 |
| Combat | CombatReceiver | 구현 완료 | DOD 체력 데이터를 복제하지 않고 Custom Inspector에서 현재/최대 체력을 읽기 전용 표시; 피해와 체력 갱신 PlayMode 확인 완료 |
| Combat | CombatBootstrap | 구현 완료 | Main 씬 공유 매니저 초기화 및 EntityManager 공유 PlayMode 확인 완료 |
| Combat | 테스트 적 초기화 및 피해 연결 | 구현 완료 | EnemyCombatInitializer가 사망 이벤트에서 적 제거를 요청하고 OnDestroy에서 Dead Entity Release; PlayMode 확인 완료 |
| Combat | EnemyData | 구현 완료 | 종류별 ScriptableObject 설정 및 적 프리팹 연결; PlayMode 확인 완료 |
| Combat | 일반 몬스터 대기·추적·조사 | 구현 완료 | EnemyData별 감지·추적 종료 범위와 조사 시간, EnemyChaseAI 상태 전환 및 마지막 발견 위치 조사; PlayMode 확인 완료 |
| Combat | 일반 몬스터 다중 생성 및 종류별 데이터 | 구현 완료 | Slime/Goblin EnemyData와 프리팹, 종류별 생성 수 및 공유 매니저 초기화; 체력·이동·공격 독립 적용과 사망 정리 PlayMode 확인 완료 |
| Combat | 일반 몬스터 공격 AI | 구현 완료 | EnemyData 공격력·사거리·간격 사용 및 Player CombatReceiver 피해; 사거리와 반복 공격 PlayMode 확인 완료 |
| Combat | Slime 돌진·복귀 공격 | 구현 완료 | 인스턴스별 고정 복귀 기준점, 적중·빗나감 복귀, 경로 차단, 도착 후 1초 대기와 재공격; Slime/Goblin 동시 공격 독립성 PlayMode 테스트 13개 전부 통과 및 사용자 확인 완료 |
| Combat | 플레이어 체력 및 사망 처리 | 구현 완료 | 공유 매니저에 최대 체력 100 등록; 사망 시 입력·이동·무기 조준·공격 중단 및 추가 피해 차단; 사용자 확인 완료 |
| Combat | CombatReceiver 런타임 어셈블리 분리 | 사용자 확인 대기 | Combat asmdef 추가 및 Player.Weapons 참조 연결; 전투 PlayMode 확인 완료, 전체 Unity 컴파일 및 EditMode 결과 미확인 |
| Combat | Combat EditMode Tests | 사용자 확인 대기 | 기존 테스트에 사망 후 최대 체력 유지 검증 추가; 이번 변경 테스트 미실행 |
| Weapon | WeaponData 및 WeaponSlot | 사용자 확인 대기 | ScriptableObject 데이터와 10개 단축키 슬롯 구현 |
| Weapon | 장착·교체 및 입력 | 사용자 확인 대기 | 숫자키 1~0, 빈 슬롯 비무장, 등록 무기 휠 순환, 변경 이벤트 구현 |
| Weapon | WeaponView | 사용자 확인 대기 | 별도 SpriteRenderer 자식으로 아이콘 표시 및 비무장 표시 구현 |
| Weapon | 공격 입력 및 반복 주기 | 구현 완료 | PlayerInputHandler 입력 전달, WeaponAttack의 즉시·반복 요청 및 무기 교체 취소; 사용자 확인 완료 |
| Weapon | 근접 공격 판정 | 구현 완료 | Arc/Thrust Physics2D 판정, Enemy Layer 필터 및 CombatReceiver 피해 전달; 사용자 확인 완료 |
| Weapon | 무기 시스템 EditMode Tests | 사용자 확인 대기 | 22개 작성; LayerMask Reflection 타입 불일치 수정 및 SetField 타입 검사 추가; standalone Roslyn 컴파일 성공, Unity Test Runner 미실행 |

## 2. 사용자 확인 대기 항목

- **월드 Tilemap 렌더링 및 벽 충돌 2단계 확인**
  - Main 씬 진입 시 64×64 Ground 바닥과 초기 Stone 테스트 벽이 표시되는지 확인
  - Grid Transform 원점 `(-32, -32)` 및 셀 크기 `(1, 1)`가 WorldGrid 좌표 변환과 일치하는지 확인
  - Player와 테스트 몬스터 시작 위치가 초기 벽과 겹치지 않는지 확인
  - Wall Tilemap만 TilemapCollider2D를 가지며 벽이 Rigidbody2D 이동을 막는지 확인
  - WorldGrid에서 벽을 파괴했을 때 Wall Tile과 Collider가 제거되고 Ground 바닥은 유지되는지 확인
  - Main 씬에서 WorldGrid 변경 후 Tilemap 벽 제거와 Floor 유지 동작 확인

- **무기 장착·교체 시스템 확인**
  - Main 씬 Player의 1번 슬롯에 연결된 WeaponData.asset으로 숫자키 1 장착과 WeaponView 표시 확인
  - 빈 슬롯(2~9, 0) 선택 시 비무장 처리 및 WeaponView 숨김 확인
  - 마우스 휠이 빈 슬롯을 건너뛰고 양 끝에서 순환하는지 확인
  - WeaponView 자식 위치와 무기 아이콘 크기·정렬 확인
  - Unity EditMode Test Runner에서 무기 시스템 테스트 22개 실행

## 3. 다음 예정 작업

- Player 이동·달리기·대시 중 미세 Jitter 원인 분석

## 4. 확인된 이슈 및 주의 사항

- 네임스페이스 오타(`Prohect_DOD_Undergird`): 추후 사용자 승인 후 일괄 리팩토링 검토 필요
- Player가 달리기·대시할 때 미세한 Jitter가 관찰되었으며 원인 분석 및 수정은 미완료
- Unity batchmode가 종료 코드 1로 끝나고 결과 XML을 생성하지 않아 Unity 컴파일 및 전체 EditMode 테스트 실행 결과를 확인하지 못함; standalone Roslyn 컴파일은 통과
- Main 씬 테스트 생성기는 Slime·Goblin 프리팹을 각각 1마리, 2.0 간격으로 생성하고 TestEnemy는 종류별 목록이 없을 때 사용하는 단일 프리팹 fallback으로 연결됨; 정식 콘텐츠용 Enemy 프리팹 및 웨이브 스폰 시스템은 미구현
- 테스트 적 생성 경로는 Main 씬 전용이며 정식 Enemy 프리팹 및 웨이브 생성 시스템은 미구현
- 투사체 생성 및 유도 기능은 미구현
