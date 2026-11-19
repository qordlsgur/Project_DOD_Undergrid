# Project Architecture Overview

## 1. 프로젝트 아키텍처 목표

본 프로젝트는 DOD(Data-Oriented Design)와 MonoBehaviour를 결합한 Hybrid Architecture를 사용합니다.

목표는 대량의 게임 데이터를 효율적으로 관리하면서 Unity 엔진의 기능과 자연스럽게 연결하는 것입니다.

핵심 설계 원칙은 다음과 같습니다.

- 데이터와 처리 로직을 가능한 한 분리합니다.
- 대량의 데이터는 struct와 배열을 우선 고려합니다.
- 데이터 처리 로직은 Unity API에 대한 의존성을 최소화합니다.
- MonoBehaviour는 Unity 생명주기와 GameObject를 연결하는 역할을 담당합니다.
- 불필요한 메모리 할당과 GC Alloc을 줄이는 방향을 고려합니다.
- 실제 성능은 구현 후 측정하여 판단합니다.

## 2. Hybrid Architecture 구성

프로젝트는 크게 두 영역으로 구분합니다.

### 2.1 DOD 영역

DOD 영역은 게임 데이터를 저장하고 처리하는 역할을 담당합니다.

주요 책임:

- 게임 데이터 정의
- 대량 데이터 저장 및 조회
- 데이터의 생성, 변경, 삭제
- 반복적인 데이터 처리 및 계산
- 게임 규칙과 데이터 처리 로직

설계 방향:

- 데이터 구조는 struct를 우선 고려합니다.
- 데이터 저장은 배열을 우선 고려합니다.
- 처리 로직은 static 메서드로 분리하는 방식을 고려합니다.
- UnityEngine 객체에 대한 의존성을 최소화합니다.

### 2.2 MonoBehaviour 영역

MonoBehaviour 영역은 Unity 엔진과 게임 데이터를 연결합니다.

주요 책임:

- Unity 생명주기 관리
- GameObject 및 Component 연결
- 입력과 물리 시스템 처리
- Transform 및 화면 표현 갱신
- DOD 데이터와 Unity 오브젝트 사이의 연결

설계 방향:

- Unity API가 필요한 작업을 담당합니다.
- DOD 영역의 데이터 처리 로직을 직접 중복 구현하지 않습니다.
- 데이터 처리와 화면 표현의 책임을 구분합니다.

## 3. 데이터 처리 흐름

기본적인 데이터 처리 흐름은 다음과 같습니다.

1. MonoBehaviour 영역에서 입력이나 Unity 이벤트를 전달합니다.
2. DOD 영역에서 전달받은 데이터를 기반으로 게임 로직을 처리합니다.
3. 처리 결과를 데이터에 반영합니다.
4. MonoBehaviour 영역에서 변경된 데이터를 확인합니다.
5. Unity GameObject 또는 화면 표현에 결과를 반영합니다.

위 흐름은 기본 설계 방향이며, 실제 시스템별 데이터 전달 방식은 구현 단계에서 구체화합니다.

## 4. 설계 및 구현 원칙

- 새로운 시스템을 추가하기 전에 기존 시스템과의 책임 중복을 확인합니다.
- 하나의 클래스에 과도한 책임을 부여하지 않습니다.
- 필요 이상의 클래스 분리와 추상화를 피합니다.
- 모든 데이터를 무조건 struct와 배열로 만들지 않습니다.
- 데이터 크기, 접근 패턴, 복사 비용을 고려합니다.
- 실제 구현과 문서의 내용이 일치하도록 유지합니다.

### 4.1 클래스 책임 및 스크립트 분리 판단 기준

새로운 기능을 구현할 때 기존 스크립트의 책임을 확인하고, 다음 기준에 따라 수정 또는 분리 여부를 판단합니다.

- 기능이 기존 스크립트의 책임에 속하면 기존 스크립트를 수정합니다.
- 독립적인 책임이 필요하거나 기존 스크립트의 책임이 과도해지는 경우 새로운 스크립트 생성을 고려합니다.
- 기능의 이름이나 코드 줄 수만으로 스크립트를 분리하지 않습니다.
- 필요 이상의 클래스 분리와 추상화를 피합니다.
- 분리 여부가 불명확하면 AI가 임의로 결정하지 않고 판단 근거와 변경 계획을 제안한 후 승인받습니다.
- 사용자가 특정 스크립트의 수정 또는 생성을 명확하게 지정한 경우 해당 지시를 우선합니다.
- 확정된 아키텍처 결정과 충돌하는 경우 임의로 변경하지 않고 충돌 내용을 알립니다.

### 4.2 클래스 간 참조 및 통신 방식

서로 다른 클래스가 데이터를 주고받거나 기능을 요청할 때는 각 클래스의 책임과 데이터 흐름에 적합한 통신 방식을 선택합니다.

- **직접 참조:** 다른 컴포넌트의 기능이나 데이터에 접근해야 하는 경우 사용합니다.
- **GetComponent<T>():** 같은 GameObject에 부착된 컴포넌트를 참조할 때 사용합니다.
- **SerializeField 참조:** Inspector에서 연결 대상을 명시적으로 지정해야 하는 경우 사용합니다.
- **이벤트(Action 등):** 특정 상태 변화나 사건을 다른 객체에 알릴 때 사용합니다.
- **Singleton:** 전역 접근이 반드시 필요한 경우에만 사용하며, 단순한 컴포넌트 연결 목적으로 사용하지 않습니다.
- **상속:** 공통 기능이나 규약을 공유하는 명확한 관계가 있을 때 사용합니다. 단순히 클래스 간 데이터를 전달하거나 기능을 연결하기 위한 목적으로 사용하지 않습니다.

추가 원칙:

- 클래스 간 통신을 위해 불필요한 상속이나 Singleton을 만들지 않습니다.
- 매 프레임 참조가 필요한 경우와 특정 사건 발생 시 알림이 필요한 경우를 구분합니다.
- 기존 참조 구조와 데이터 흐름을 먼저 확인한 후 통신 방식을 결정합니다.
- 새로운 통신 구조가 필요한 경우 도입 이유와 대안을 설명하고 승인받습니다.

## 5. 성능 고려 사항

다음 항목을 중심으로 성능을 고려합니다.

- 반복적인 메모리 할당
- GC Alloc 발생 여부
- 배열의 접근 패턴
- 데이터 복사 비용
- 불필요한 Unity API 호출
- Update 및 FixedUpdate에서 수행하는 작업량

성능 개선 여부는 실제 프로파일링과 테스트 결과를 기준으로 판단합니다.

## 6. 실제 구현 구조 기록

이 문서는 프로젝트의 기본 아키텍처 원칙을 정의합니다.

다음 항목은 실제 시스템 구현이 진행된 후 확인된 구조를 기준으로 추가합니다.

- 실제 데이터 구조와 필드
- 시스템별 클래스 및 메서드 구성
- 데이터 저장 및 접근 방식
- 시스템 간 호출 관계
- 데이터의 생성부터 삭제까지의 흐름
- 주요 의존 관계

아직 구현되지 않은 구조를 확정된 사실처럼 기록하지 않습니다.

## 7. 아키텍처 변경 관리

다음과 같은 변경은 아키텍처 변경으로 취급합니다.

- DOD와 MonoBehaviour의 책임 변경
- 핵심 데이터 저장 방식 변경
- 시스템 간 의존 관계 변경
- 기존 시스템의 통합 또는 분리

아키텍처 변경은 `Docs/Workflow/Approval_Process.md`에 정의된 승인 절차를 따릅니다.

변경이 완료되면 실제 코드와 일치하도록 이 문서를 갱신합니다.

## 8. 무기 데이터 및 장착 구현 구조

현재 구현된 무기 시스템 1단계는 플레이어의 10개 무기 슬롯과 장착 상태를 관리합니다.

- `WeaponData`는 Unity `ScriptableObject`이며 무기 이름, 아이콘, 공격력, 공격 간격, 무기 종류를 보관합니다.
- `WeaponSlot`은 한 슬롯의 `WeaponData` 참조를 보관합니다.
- `WeaponManager`는 슬롯, 선택 슬롯, 현재 무기만 관리합니다. 실제 무기 참조가 달라질 때 `OnWeaponChanged`를 한 번 발생시킵니다.
- `PlayerInputHandler`는 숫자키 1~0과 마우스 휠을 감지해 `WeaponManager`에 선택 요청을 전달합니다.
- `WeaponView`는 `SpriteRenderer`를 갱신해 현재 무기 아이콘을 표시하며, 무기가 없거나 아이콘이 없으면 렌더러를 숨깁니다.
- `WeaponAttack`은 입력된 공격 시작 시점과 반복 주기를 관리하고 `OnAttackRequested`로 무기 데이터를 전달합니다.
- `WeaponData`는 근접 공격 형태와 Arc 범위·각도, Thrust 길이·폭을 보관합니다. `WeaponView.AimDirection`은 화면의 조준 방향을 읽기 전용으로 제공합니다.
- `MeleeAttackResolver`는 공격 요청에 맞춰 최신 조준을 반영하고 Enemy Layer의 Collider를 조회합니다. 한 공격에서 `CombatReceiver`마다 한 번만 `ReceiveDamage`를 호출합니다.
- Enemy Layer와 Player Resolver 연결은 추가했지만, 실제 Enemy 프리팹과 `CombatReceiver.Initialize()` 호출·CombatManager 주입은 아직 연결되지 않았습니다. 투사체 생성과 유도 기능도 미구현입니다.

`WeaponData.asset` 무기 데이터 에셋은 존재합니다. Main 씬 Player 슬롯에 무기 데이터를 할당하는 연결은 아직 통합 단계에서 진행할 예정입니다.

## 9. 월드 그리드 Tilemap 연동

- `Prohect_DOD_Undergird.DOD.World` 어셈블리는 Unity 의존성이 없는 `WorldGrid` 데이터 계층을 유지합니다.
- `Prohect_DOD_Undergird.DOD.World`의 순수 C# `WorldTerrainGenerator`는 Seed와 절대 월드 셀 좌표로 벽·동굴 Noise를 계산합니다. 청크 모드는 공유 경계의 Seed 기반 입구를 만들고 청크 범위에 제한한 4방향 BFS로 활성 입구와 주요 동굴을 연결합니다. 기존 전체 월드 모드는 `Queue<int>` 기반 전체 그리드 BFS로 유지됩니다. 벽 변경은 `WorldGrid.TryPlaceWall()`과 `TryDamageWall()`을 사용하므로 바닥 데이터는 보존됩니다.
- `WorldCellCoordinate`는 절대 셀 좌표, `WorldCellBounds`는 저장·로딩 상태와 독립된 유효 월드 범위, `WorldChunkCoordinate`는 32×32 청크 주소, `WorldGridCoordinate`는 단일 `WorldGrid` 배열 좌표로 구분합니다. 청크 입구는 공유 경계와 유효 월드 범위로 활성화하며 이웃 청크의 바닥 데이터 유무에는 의존하지 않습니다. 청크 주소 변환은 음수 좌표에서 바닥 나눗셈과 양수 로컬 나머지를 사용합니다.
- `WorldGridBootstrap`은 기존 전체 월드 생성 모드와 스트리밍 모드의 데이터 경로를 구분합니다. 스트리밍 모드에서는 유한 월드 bounds와 Seed 설정을 갖는 `WorldChunkStore`, `WorldGridWorld`, `WorldChunkGenerationPipeline`을 초기화합니다. 청크 생성은 절대 셀 좌표의 Noise와 청크 내부 BFS 연결을 사용하며 전체 월드 BFS를 추가 적용하지 않습니다. Main 씬은 512×512, 원점 `(-32, -32)`, Seed 9999, 청크 크기 32의 스트리밍 모드를 사용합니다.
- 생성이 끝난 뒤 `WorldGridTilemapView`를 초기화하므로 화면 표시는 현재처럼 단일 Floor/Wall Tilemap의 전체 동기화 및 셀 변경 이벤트를 사용합니다.
- `WorldGridTilemapView`는 Floor와 Wall Tilemap을 초기 블록 단위로 동기화한 후 `CellChanged`를 구독해 변경된 좌표만 반영합니다. 표시 Tile 참조가 동일하면 Tilemap을 다시 설정하지 않습니다.
- Main 씬의 Grid 원점은 `(-32, -32)`, 셀 크기는 `(1, 1)`이며 64×64 셀을 사용합니다. Floor Tilemap에는 충돌을 두지 않고 Wall Tilemap에만 `TilemapCollider2D`를 연결합니다.
- 임시 타일 에셋은 `Assets/Art/WorldTiles/`에 저장되며, 최종 그래픽은 Tile 참조를 교체해 적용할 수 있습니다.
- Main 씬은 Player 시작 셀 반경 5와 Inspector에 지정한 테스트 적 시작 위치 주변을 벽 생성 안전 영역으로 보호합니다. `PlayerMiningController`는 좌클릭 공통 행동을 장비 데이터와 WorldGrid 벽 피해로 연결하며, 플레이 중 벽 파괴·충돌 해제·통행·바닥 유지를 사용자 확인했습니다.
- `PlayerFollowCamera`는 Main 씬의 기존 Orthographic Camera에서 플레이어를 부드럽게 추적하고 유한 월드 경계를 따릅니다. Player Rigidbody2D Interpolation은 `Interpolate`이며, 사용자가 추적·경계와 달리기·대시 중 Jitter 해소를 확인했습니다.

### 9.1 청크 데이터 저장소 및 월드 절대 좌표 API 3-A

- `WorldChunkStore`는 유한 `WorldCellBounds`와 청크 크기를 기준으로 청크별 `WorldGrid`를 보유합니다. 청크 데이터 상태는 `Unloaded`, `Queued`, `Generating`, `Ready`, `Failed`로 관리하며, 월드 밖 청크는 `OutOfWorldBounds`로 구분합니다.
- 청크 생성 시 월드 bounds와 겹치는 셀만 크기로 갖는 `WorldGrid`를 만듭니다. 따라서 월드 외곽의 부분 청크는 잘린 배열 크기와 해당 절대 셀 원점을 사용합니다.
- `WorldChunkStore`의 표시 상태(`NotDisplayed`, `Displayed`)는 데이터 준비 상태와 별도로 보관합니다. 표시 중인 청크 데이터 해제는 거부됩니다.
- `WorldGridWorld`는 절대 `WorldCellCoordinate`를 청크 주소 및 해당 데이터 배열의 로컬 `WorldGridCoordinate`로 변환해 조회, 바닥·벽 배치, 벽 피해 및 통행 판정을 기존 `WorldGrid`에 전달합니다. 미준비 상태는 `WorldCellAccessStatus` 및 `WorldCellWalkability`로 빈 셀과 구분합니다.
- 청크 데이터 요청을 실제 생성·복원으로 처리하는 내용은 3-B에, Tilemap 표시·해제는 3-C에 정리되어 있습니다. Player 중심 자동 생성·표시·해제는 3-D `WorldChunkStreamer`가 담당하며 기존 전체 월드 초기화 경로도 유지됩니다.

### 9.2 청크 지형 생성 및 변경 데이터 복원 3-B

- `WorldChunkGenerationPipeline`은 대기 청크를 `Generating`으로 전환한 뒤 해당 청크 크기의 `WorldGrid`에 Ground를 채우고, 기존 Seed·절대 셀 좌표 기반 벽 Noise, 동굴 제거, 청크 내부 입구·주요 영역 BFS 연결을 순서대로 적용합니다. 청크 생성은 이웃 청크의 데이터 상태에 의존하지 않습니다.
- 생성 알고리즘이 `WorldGrid`에 직접 적용한 변경은 청크가 `Ready`가 되기 전에 수행되며, `WorldChunkStore`의 플레이어 변경 기록 대상이 아닙니다. 이전 플레이어 변경 델타 복원을 마친 뒤에만 청크를 `Ready`로 전환합니다.
- `WorldGridWorld`를 통한 성공한 벽 배치·피해만 절대 셀 좌표 기준의 희소 메모리 델타로 보존합니다. 최초 상태와 현재 벽 유무·종류·내구도를 비교해 원래 상태로 되돌아온 셀은 델타를 제거합니다. 청크를 해제해도 델타는 유지되며 재생성 시 기본 지형 위에 복원됩니다. 바닥은 청크마다 다시 Ground로 채우며 변경 델타에 포함하지 않습니다.
- `WorldTerrainGenerator`의 청크 범위와 교차, 경계, 월드-배열 로컬 좌표 중간 계산은 `long`을 사용하고 실제 셀 좌표를 `int`로 만들기 전 범위를 확인합니다. 공개 좌표 타입은 기존 `int`를 유지하며, int 좌표 공간에 실제로 겹치는 부분 청크를 허용합니다. 기존 전체 월드 생성 경로는 별도 유지됩니다.
- 청크 생성 요청 대기열을 자동 실행하는 로더, 디스크 저장, 카메라 스트리밍 및 비동기 처리는 아직 구현하지 않았습니다. 청크 Tilemap 표시·해제는 3-C의 별도 API로 연결했습니다.

### 9.3 청크별 Tilemap 표시·해제 및 Collider 연동 3-C

- `WorldGridTilemapView.InitializeChunks()`는 기존 Floor/Wall Tilemap을 청크 저장소에 연결합니다. `TryDisplayChunk()`는 Ready 상태 청크의 배열을 `SetTilesBlock()`으로 표시하고, 성공한 뒤에만 `WorldChunkStore`의 표시 상태를 `Displayed`로 전환합니다.
- 청크 데이터의 물리 월드 원점과 크기를 Unity `Grid`에 대응시켜 Tilemap 좌표를 계산하므로 음수 셀 좌표와 월드 외곽 부분 청크도 같은 변환을 사용합니다. 기존 `Initialize(WorldGrid)` 전체 월드 표시 경로는 유지합니다.
- `TryRemoveChunkDisplay()`는 해당 청크의 정확한 Tilemap 범위만 비우고 벽 Collider 변경을 처리한 뒤 Store 표시 상태를 해제합니다. 데이터 해제는 별도 `WorldChunkStore.TryUnloadChunk()` 호출로 처리하며, 표시 중인 청크의 데이터 해제는 계속 거부됩니다.
- 청크 모드에서 Store의 절대 셀 `CellChanged` 이벤트를 구독합니다. 표시 상태인 청크의 실제 Tile이 달라질 때만 해당 셀을 갱신하고 벽 Collider를 처리합니다. 비표시 청크 변경은 렌더하지 않으며 다시 표시할 때 최신 배열 데이터로 동기화합니다.
- 표시 상태는 Store가 단일 원본으로 관리합니다. Tilemap 뷰가 비활성화된 동안에는 구독을 해제하고, 다시 활성화될 때 Store의 표시 청크 목록을 이용해 표현을 재동기화합니다.
- 자동 청크 수명 관리는 3-D의 `WorldChunkStreamer`가 맡습니다. 이 뷰는 카메라나 플레이어 범위를 계산하지 않고 Store 상태에 따라 표시와 제거를 실행합니다.

### 9.4 플레이어 중심 자동 청크 스트리밍 3-D

- Main 씬의 `WorldChunkStreamer`는 Player가 속한 절대 셀 청크를 기준으로 기본 5×5 데이터 범위를 유지합니다. 중앙 3×3은 우선 데이터 생성과 Tilemap 표시 대상으로 삼고, 나머지 범위는 데이터만 생성합니다. 실제 카메라 화면에 포함되는 청크는 중앙 3×3 바깥이어도 생성하고 표시합니다. 유한 월드 경계와 겹치지 않는 주소는 요청하지 않습니다.
- 각 스트리밍 갱신에서 필요한 주소 집합을 다시 계산하고 `WorldChunkStore`의 상태를 확인하므로 이미 Ready 또는 Queued인 청크를 중복 생성하지 않습니다. 플레이어 중심, 화면 표시, 외곽 사전 생성 순으로 요청을 정렬합니다. 생성, 표시, 표시 제거, 데이터 해제의 프레임별 최대 개수는 Inspector 설정으로 제한합니다.
- `WorldGridTilemapView`는 Ready 청크만 표시하고 벽 `TilemapCollider2D` 변경을 처리한 뒤 Store의 표시 상태를 갱신합니다. 카메라 화면 밖이면서 보존 범위 밖인 청크는 표시를 제거한 후 데이터를 해제합니다. `WorldChunkStore`에 보존된 벽 변경 기록은 해제 후에도 메모리에 유지되어 재생성 시 복원됩니다.
- `PlayerMovement`는 물리 이동의 Collider 발자국을 검사합니다. 이동 경로가 미준비 또는 비표시 청크에 닿으면 그 주소를 임시 우선 준비 대상으로 요청하고 해당 지형과 충돌 표시가 준비될 때까지 Rigidbody 이동을 막습니다. 시작 시 중앙·화면 청크가 준비될 때까지 이동과 대시를 잠급니다.
- 대시는 시작 전에 전체 이동 경로의 청크 데이터와 충돌 표시 상태를 확인합니다. 준비되지 않은 경로는 생성·표시를 우선 요청하고 시작을 보류합니다. 실제 대시 중에는 해당 주소를 고정해 범위 갱신으로 해제되지 않게 하며, 대시 종료 후 고정을 해제합니다.
- `WorldChunkStreamer`는 `WorldChunk.GenerationAndRestore`, `WorldChunk.TilemapWrite`, `WorldChunk.TilemapClear`, `WorldChunk.ColliderRefresh` Profiler 표식을 통해 생성·복원, 배치, 제거, 충돌 갱신 구간을 구분합니다. 이 표식은 측정을 제공하며 성능 개선을 보장하지 않습니다. 카메라 기반 스트리밍은 메인 스레드에서 동작하고, 멀티스레딩·Job System은 사용하지 않습니다.

### 9.5 광물 생성 및 자원 수집 4-A

- `WorldOreDefinition`은 광맥 벽 종류, 바탕 벽 종류, 자원 종류, 노이즈 채널·주파수·임계값, 내구도 및 파괴 보상 수량을 Unity 비의존 값으로 보관합니다. `WorldGridBootstrap`은 Inspector의 광물 설정 배열을 이 정의 배열로 변환해 전체 월드와 청크 생성 경로에 전달합니다.
- `WorldTerrainGenerator`는 기존 벽 배치와 별개의 정의별 노이즈 채널을 절대 셀 좌표와 Seed로 샘플링합니다. 지정된 바탕 벽 셀에서 임계값을 만족하는 경우에만 구리 또는 철 광맥 벽을 배치하므로 생성 순서와 무관한 결과를 만듭니다. 광맥은 벽 데이터만 대체하며 바닥, 동굴 연결 및 청크 경계 알고리즘을 변경하지 않습니다.
- `WorldWallData`는 일반 벽·광맥의 종류와 내구도 외에 파괴 보상 종류·수량을 함께 보유합니다. `WorldChunkStore`의 희소 플레이어 변경 델타도 보상 메타데이터를 포함해 저장하므로 내구도 피해는 복원되고 파괴한 광맥은 재생성 후에도 비어 있습니다. 생성 중 만들어지거나 제거된 셀은 Ready 전 변경 델타 구독 이전에 처리되어 플레이어 변경으로 기록되지 않습니다.
- `WorldResourceInventory`는 자원별 수량을 Unity 비의존 배열로 보관하고 `PlayerMiningController`가 플레이어별 인스턴스를 소유합니다. 일반 채굴 경로가 벽 데이터와 파괴 결과를 확인한 뒤 광맥이 실제로 파괴된 경우에만 보상 수량을 더합니다. UI, 제작 및 디스크 저장은 포함하지 않습니다.
- `WorldGridTilemapView`는 별도 광맥 Tile이 지정되면 해당 Tile을 원본으로 사용하고, 비어 있으면 돌벽 Tile을 복제해 Sprite와 Collider 설정을 유지하면서 구리는 `#E87532`, 철은 `#C0C0C0` 색을 적용합니다. 복제본의 색만 변경하므로 일반 돌벽 Tile 에셋은 기존 회색을 유지하며, 광맥은 기존 벽 TilemapCollider2D를 공유합니다. 광맥 전용 이미지 에셋은 추가하지 않았습니다.
