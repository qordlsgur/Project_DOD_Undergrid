---
name: Architecture Decisions
alwaysApply: false
description: EntityManager, EntityId, Generation, free list 등 DOD 엔티티 설계 결정이 필요한 설계안 작성이나 구조 변경 작업에 적용
---

# AI 설계 및 응답 규칙

이 문서는 DOD 엔티티 설계를 다루는 작업에만 적용합니다.
이동, 입력 등 확정 수정 요청과 단순 코드 수정에는 적용하지 않습니다.

## 1. 응답 필수 규칙

1. 사용자가 "설계"를 요청하면 코드를 작성하지 않습니다.
2. 설계 단계에서는 파일 구성, 클래스 책임, 필드 목록, API 시그니처(이름과 반환 타입)까지만 제시합니다.
3. 작업 응답(설계 제안, 구현, 보고)의 첫 줄은 다음 중 하나로 시작합니다. 단순 질문과 개념 설명에는 적용하지 않습니다.
   - [설계 제안]
   - [구현]
   - [보고]
4. 설계 응답(새 시스템, 구조 변경, 설계안 요청)의 마지막에만 확정된 결정 1~11번에 대한 대조표를 작성합니다. 확정 수정과 단순 코드 수정에는 작성하지 않습니다.
5. 미결정 항목은 임의로 결정하지 않고 "미결정"이라고 표시합니다.
6. 미결정 항목에 임의의 숫자나 예시 값을 넣지 않습니다.
7. 설계 제안 단계에서는 사용자의 승인 없이 프로젝트 파일을 생성하거나 수정하지 않습니다. 사용자가 수정 대상과 내용을 구체적으로 지정한 확정 수정은 지시 자체를 승인으로 봅니다.

## 2. 확정된 설계 결정

설계의 기준 문서는 `Docs/Architecture/Decisions.md`입니다.

아래 내용은 해당 문서의 결정을 AI가 확인하기 쉽도록 정리한 것입니다.
결정이 바뀌면 `Decisions.md`와 이 문서를 함께 수정합니다.

### Entity

1. EntityId는 `readonly struct { int Index; int Generation; }`이다.

2. Generation은 1부터 시작하며 `default(EntityId)`는 무효 ID이다.

3. 슬롯 데이터는 EntityId와 분리한다.
   - `int[] generations`
   - `EntityState[] states`
   - 배열 위치가 Index이다.

4. 빈 슬롯은 free list(int 스택)로 관리한다.

5. Generation은 `Release(id)` 안에서만 1 증가한다.
   - `Create()` 안에는 `generations` 배열에 대한 쓰기(`=`, `++`)가 없어야 한다.
   - 초기값 1은 배열 생성 또는 확장 시점에 설정한다.

6. EntityState는 Alive / Dead / Released이다.
   - Released는 슬롯이 비어 있고 free list에 있는 상태를 뜻한다.

7. `Release(id)`는 state가 Dead일 때만 동작한다.
   - Alive 또는 Released 상태이면 아무것도 하지 않고 false를 반환한다.
   - 자동 해제는 없다.

8. EntityManager는 인스턴스 클래스이며 UnityEngine에 의존하지 않는다.
   - Destroy와 사망 원인을 모른다.

9. API는 다음과 같다.
   - `Create()`
   - `IsAlive(id)`
   - `GetState(id)`
   - `MarkDead(id)`
   - `Release(id)`
   - 잘못된 ID나 잘못된 전이는 예외 없이 false를 반환한다.
   - `GetState(id)`에 유효하지 않은 ID가 들어오면 Released를 반환한다.

10. `IsAlive(id)`는 다음 조건을 모두 확인한다.
    - Index 범위
    - Generation 일치
    - state == Alive

11. `MarkDead(id)`와 `Release(id)`의 반환 타입은 bool이다.
    - 성공하면 true
    - 실패하면 false

12. 초기 슬롯 용량은 128이며, 부족 시 2배로 확장(Grow)한다.

13. `EntityId`는 외부에서 임의 생성을 방지하도록 생성자를 내부(internal)로 제한한다.

14. Entity 스크립트 경로는 `Assets/Scripts/DOD/Entity/`이다.

## 3. 미결정 사항

다음 항목은 설계안을 작성할 때 사용자가 결정하기 전까지 임의로 정하지 않습니다.

- 이동 방식, 입력 방식, 물리 처리 방식 (새 설계를 제안할 때만 해당하며, 사용자가 수정 내용을 직접 지정한 확정 수정에는 적용하지 않음)

## 4. 설계 검토 규칙

1. `Docs/Architecture/Decisions.md`를 최우선 기준으로 사용합니다.
2. 확정된 결정과 충돌하는 설계를 제안하지 않습니다.
3. 충돌이 발견되면 해당 부분을 먼저 보고합니다.
4. 확정된 결정은 사용자가 변경을 요청하기 전까지 다시 논의하지 않습니다.
5. 요청 범위 밖의 구조 변경을 임의로 추가하지 않습니다.
6. 설계 검토 중에는 코드를 작성하거나 문서를 수정하지 않습니다.
7. 대조표는 실제 설계 내용과 각 결정의 조건을 직접 비교하여 작성합니다. 결정 문장을 복사하는 것만으로 일치 판정을 내리지 않습니다.