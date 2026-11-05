# Unity Development Rules

## 1. 기본 원칙

- Unity 6과 URP를 기준으로 개발합니다.
- 이 프로젝트는 2D 탑다운입니다. `Rigidbody2D`, `Collider2D`, `Physics2D`, `SpriteRenderer` 등 2D 컴포넌트를 사용하고, 3D 컴포넌트(`Rigidbody`, `Collider` 등)는 사용하지 않습니다.
- Unity 6 기준의 최신 API를 사용합니다. 구버전 API(deprecated)는 사용하지 않습니다.
- 기존 프로젝트의 Unity 설정과 구조를 먼저 확인합니다.
- Unity 기능을 사용하기 전에 해당 기능의 역할과 사용 이유를 고려합니다.
- Unity API에 불필요하게 의존하는 구조를 피합니다.
- 기존 시스템과 책임이 중복되는 MonoBehaviour를 만들지 않습니다.
- 지시나 `Docs/Architecture/Decisions.md`에 없는 사항(입력 방식, 물리 방식, 폴더 경로 등)은 임의로 정하지 않고 "미결정"으로 표시합니다.

## 2. MonoBehaviour의 역할

MonoBehaviour는 Unity 엔진과 게임 데이터 시스템을 연결하는 역할을 담당합니다.

주요 책임은 다음과 같습니다.

- Unity 생명주기 관리
- GameObject 및 Component 연결
- Transform과 물리 시스템 연동
- Unity Input 및 이벤트 수신
- DOD 데이터와 Unity 객체 간 연결
- 화면 및 게임 오브젝트 갱신

대량의 게임 데이터를 MonoBehaviour 내부에 직접 저장하고 관리하는 방식은 지양합니다.

구체적인 데이터 관리 원칙은 `Docs/Rules/DOD_Rules.md`를 따릅니다.

## 3. Unity 생명주기

- `Awake()`, `OnEnable()`, `Start()`, `Update()`, `FixedUpdate()`, `LateUpdate()`의 역할을 구분하여 사용합니다.
- 초기화 시점과 실행 순서를 고려합니다.
- 물리 연산은 적절한 경우 `FixedUpdate()`에서 처리합니다.
- 매 프레임 실행되는 메서드에는 불필요한 연산을 넣지 않습니다.
- Unity 생명주기를 사용하지 않는 클래스에 불필요하게 MonoBehaviour를 상속하지 않습니다.

## 4. GameObject 및 Component 관리

- GameObject와 Component의 책임을 명확하게 구분합니다.
- 필요한 Component 참조는 적절한 초기화 시점에 확보합니다.
- 반복적인 `GetComponent()` 호출을 주의합니다.
- Inspector에서 설정할 값은 필요한 경우 `[SerializeField]`를 활용합니다.
- 다른 객체를 찾기 위한 과도한 사용을 피합니다. (`FindFirstObjectByType`, `FindAnyObjectByType`, `GameObject.Find`)
- Unity 6에서 deprecated된 `FindObjectOfType`, `FindObjectsOfType`은 사용하지 않습니다.
- 객체 간 의존 관계를 명확하게 관리합니다.

## 5. Unity API와 DOD의 연결

- 순수 데이터 처리 로직은 가능한 한 Unity API와 분리합니다.
- Transform, Rigidbody2D 등 Unity 객체 조작은 적절한 MonoBehaviour 또는 Unity 연동 계층에서 담당합니다.
- DOD 데이터와 GameObject의 동기화 책임을 명확히 합니다.
- Unity 객체 참조를 대량의 데이터 구조에 무분별하게 저장하지 않습니다.
- Unity API가 필요한 작업과 일반 C# 데이터 처리를 구분합니다.

## 6. 성능 및 메모리 관리

- 매 프레임 불필요한 객체를 생성하지 않습니다.
- `Update()`와 `FixedUpdate()` 내부의 반복적인 메모리 할당을 주의합니다.
- 불필요한 Component 검색을 피합니다.
- Instantiate와 Destroy가 빈번하게 발생하는 경우 객체 풀링을 검토합니다.
- 코루틴과 이벤트 사용 시 수명 및 해제 시점을 고려합니다.
- 최적화 여부는 실제 프로파일링 결과를 바탕으로 판단합니다.

### 6.1 Update 계열 메서드 금지 패턴

`Update`, `FixedUpdate`, `LateUpdate` 안에서는 다음을 사용하지 않습니다.

- `new`로 클래스, 배열, `List` 생성
- LINQ 사용
- 문자열 연결(`+`)과 `$""` 보간
- `GetComponent`, `Find` 계열 호출 (초기화 시점에 캐싱)
- 캐싱하지 않은 `Camera.main` 반복 호출
- 매 프레임 새 델리게이트를 만드는 람다 캡처

## 7. 에디터 및 Inspector 관리

- Inspector에 노출되는 변수는 목적에 맞게 관리합니다.
- 개발자가 Inspector에서 설정해야 하는 값과 내부에서만 사용하는 값을 구분합니다.
- 필요한 경우 `[Header]`, `[Tooltip]`, `[Range]` 등의 Attribute를 사용합니다.
- Inspector 설정값의 유효성을 확인합니다.

### 7.1 Editor Script 작성 규칙

- Unity Editor 자동화 도구는 기본적으로 일반 C# `static class`로 작성합니다.
- `[MenuItem]`을 사용하여 Unity Editor 메뉴에서 실행할 수 있도록 합니다.
- 씬 생성, GameObject 생성, Component 추가, 참조 연결, 에셋 관리 등 단순 Editor 자동화 작업에는 `ScriptableObject`를 사용하지 않습니다.
- `ScriptableObject`는 에셋으로 저장하고 재사용해야 하는 데이터나 설정을 관리할 필요가 있을 때만 사용합니다.
- Editor 전용 코드는 반드시 `Editor` 폴더 또는 Editor 전용 Assembly에 배치합니다.
- Editor 자동화 작업은 기존 씬과 에셋을 임의로 덮어쓰거나 수정하지 않도록 보호합니다.

## 8. 코드 작성 및 학습 원칙

코드 작성 후의 설명 형식은 `Docs/Workflow/Approval_Process.md` 6번을 따릅니다.

Unity 코드 작업에서는 다음 항목을 추가로 설명합니다.

1. 어떤 Unity 기능이나 API를 사용했는가?
2. 어느 생명주기(`Awake`, `Update` 등)에서 실행되는가?
3. 성능이나 메모리 측면에서 주의할 부분은 무엇인가?

Unity API의 동작 원리를 이해할 수 있도록 필요한 경우 작은 예제를 함께 제공합니다.

## 9. 설계 주의 사항

- Unity 기능을 사용하기 위해 불필요하게 구조를 복잡하게 만들지 않습니다.
- MonoBehaviour 하나에 서로 다른 책임이 둘 이상 섞이면 분리합니다.
- 책임이 하나뿐인 작은 기능까지 무조건 별도 클래스로 분리하지는 않습니다.
- 현재 프로젝트의 규모와 요구 사항에 적합한 구조를 선택합니다.
- 아키텍처 변경은 사용자에게 영향과 대안을 설명하고 승인받은 뒤 진행합니다.