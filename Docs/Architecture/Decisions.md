# Architecture Decisions

확정된 결정을 기록하는 문서입니다.

- 이 문서의 결정은 사용자가 변경을 요청하기 전에는 다시 논의하지 않습니다.
- 이 문서와 어긋나는 설계는 작성하지 않고, 어긋나는 부분을 먼저 보고합니다.
- 새 결정은 사용자가 "확정"이라고 명시한 뒤에만 추가합니다.
- 이 문서에 없는 사항은 확정되지 않은 것이며, 임의로 정하지 않고 질문합니다.

## Entity

1. EntityId는 `readonly struct { int Index; int Generation; }`이다.
2. Generation은 1부터 시작하며 `default(EntityId)`는 무효 ID이다.
3. 슬롯 데이터는 EntityId와 분리한다: `int[] generations`, `EntityState[] states` (배열 위치가 Index).
4. 빈 슬롯은 free list(int 스택)로 관리한다.
5. Generation은 `Release(id)` 안에서만 1 증가한다. `Create()`는 Generation을 변경하지 않는다.
6. EntityState는 Alive / Dead / Released이다. Released는 "슬롯이 비어 있고 free list에 있음"을 뜻한다.
7. Dead → Released는 `Release(id)`를 명시적으로 호출할 때만 가능하다. 타이머 등 자동 해제는 없다.
8. EntityManager는 인스턴스 클래스이며 UnityEngine에 의존하지 않는다. Destroy와 사망 원인을 모른다.
9. API는 Create(), IsAlive(id), GetState(id), MarkDead(id), Release(id)이다. 잘못된 ID나 잘못된 전이는 예외 없이 false를 반환한다.
10. IsAlive(id)는 Index 범위, Generation 일치, state == Alive를 모두 확인한다.

## 미결정 (임의로 정하지 않음)

- 배열 초기 용량과 확장 정책
- 스크립트 폴더 실제 경로 (`Assets/Scripts`인지 확인 필요)
- 이동, 입력, 물리 방식 (Rigidbody2D 여부, 입력 시스템)