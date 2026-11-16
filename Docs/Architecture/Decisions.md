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
11. 초기 슬롯 용량은 128이며, 슬롯 부족 시 2배로 확장(Grow)한다.
12. GetState(id)에 유효하지 않은 ID가 들어오면 예외 없이 Released를 반환한다.
13. EntityId는 외부에서 임의로 유효한 ID를 생성하지 못하도록 생성자를 내부(internal)로 제한한다.
14. Entity 스크립트 경로는 `Assets/Scripts/DOD/Entity/`이다.

## Weapon

1. 무기별 기본 데이터는 각각의 `WeaponData` ScriptableObject 에셋으로 저장하며, 이름, 아이콘, 공격력, 공격 간격, 무기 종류를 보관한다.
2. 무기 단축키 슬롯은 10개이며 1~9 키가 1~9번 슬롯, 0 키가 10번 슬롯에 대응한다.
3. 숫자키로 빈 슬롯을 선택하면 현재 무기를 해제해 비무장 상태가 된다. 마우스 휠 순환은 무기가 등록된 슬롯만 대상으로 하며 양 끝에서 순환한다.
4. `WeaponManager`는 슬롯과 장착 상태만 관리한다. 입력 감지는 `PlayerInputHandler`, 화면 표시는 `WeaponView`가 담당한다.
5. 현재 무기 데이터 참조가 실제로 달라질 때만 `OnWeaponChanged`를 한 번 발생시킨다. 향후 공격 실행기는 이 변경에 반응해 진행 중인 공격을 취소하며, 이미 발사한 투사체는 무기 교체와 무관한 수명을 가진다.

## 미결정 (임의로 정하지 않음)

- 이동, 입력, 물리 방식 (Rigidbody2D 여부, 입력 시스템)
