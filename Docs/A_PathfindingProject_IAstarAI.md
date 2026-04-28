[System: A* Pathfinding Project `IAstarAI` Interface Reference & Bug Fix]

현재 `PlayerGrabber.cs`에서 `IAstarAI` 인터페이스에 대해 존재하지 않는 `.enabled` 프로퍼티를 호출하여 CS1061 에러가 발생했다. `IAstarAI`는 MonoBehaviour가 아니므로 `enabled`를 사용할 수 없다.

앞으로 A* Pathfinding 에셋의 AI를 제어할 때는 아래의 `IAstarAI` 공식 속성(Property) 및 메서드(Method)만 사용해야 한다. 이 마크다운을 숙지하고 오류가 발생한 스크립트를 즉시 수정해라.

### 📌 1. AI 이동 정지 및 제어 (`.enabled` 대체제)
AI의 이동 계산을 멈추거나 그랩(Grab), 랙돌(Ragdoll) 상태로 전환할 때 아래 속성을 사용한다.

* **`ai.canSearch = false;`**
    * 자동 경로 탐색(주기적 길찾기)을 중지한다.
* **`ai.simulateMovement = false;` (구 `canMove`)**
    * **핵심:** AI의 모든 이동 계산(물리, 로컬 회피 등)을 완전히 중지한다. 캐릭터가 물리 엔진(Rigidbody)이나 외부 힘(Grab)에 의해 완전히 통제되어야 할 때 사용한다.
* **`ai.isStopped = true;`**
    * AI가 현재 경로를 유지한 채 그 자리에 부드럽게 정지한다. 이동만 멈추고 물리/회피 연산은 유지하고 싶을 때 사용한다.

**[에러 수정 지시]**
`PlayerGrabber.cs` 등에서 `ai.enabled = false`로 작성된 부분을 모두 `ai.canSearch = false;` 와 `ai.simulateMovement = false;` 로 변경해라. 다시 풀어줄 때는 `true`로 돌려놓는다.

### 📌 2. 목적지 설정 및 도달 판정
* **`ai.destination = targetVector3;`**
    * AI의 목표 좌표를 설정한다. 즉각적인 이동 시작을 원하면 설정 직후 `ai.SearchPath();`를 호출한다.
* **`ai.reachedDestination` (bool)**
    * AI가 목표 지점에 도달했는지 확인하는 가장 권장되는 속성이다. (`Vector3.Distance` 하드코딩 대신 이거 하나면 끝난다.)
* **`ai.pathPending` (bool)**
    * 경로를 계산 중인지 확인한다.

### 📌 3. 강제 이동 및 초기화
* **`ai.Teleport(Vector3 newPosition, bool clearPath = true);`**
    * AI를 특정 위치로 즉시 이동시킨다. (먼 거리를 억지로 `position`으로 옮기면 길찾기가 꼬일 수 있으므로 반드시 이 메서드를 사용한다.)
* **`ai.SetPath(null);`**
    * 현재 계산된 경로를 즉시 날려버린다.

### 💡 (참고) RVO / Raycast 충돌 방지 요약
* AI가 서로의 대가리를 밟고 올라가지 않게 하려면 목적지(`ai.destination`) 할당 시 완벽히 동일한 좌표 대신 `Random.insideUnitSphere` 오프셋을 더하고 `y`축을 바닥으로 고정해야 한다.

위 내용을 바탕으로 CS1061 에러가 발생한 `PlayerGrabber.cs`의 코드를 즉시 수정해서 출력해.