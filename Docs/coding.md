📜 [Vertigo Hound] 코딩 가이드라인 및 컨벤션 (The Law)
스파게티 코드를 막고 유지보수를 극대화하기 위해 프로젝트 내내 지켜야 할 철칙입니다.

1. 의존성과 아키텍처 (Architecture)
중앙집권 체제 (GameInstaller): 게임이 시작될 때 맵 매니저, 플레이어 컨트롤러, UI 매니저를 한곳에서 생성하고 연결해 주는 GameInstaller (또는 Bootstrapper) 클래스를 단 하나만 둡니다.

느슨한 결합 (Loose Coupling): 플레이어가 천장에 붙었다고 해서 플레이어 스크립트가 카메라 스크립트의 Camera.Rotate()를 직접 호출하면 안 됩니다. C#의 **event (또는 Action)**를 사용하여 "나 천장에 붙었어!"라고 방송(Invoke)만 하면, 카메라 컨트롤러가 그걸 듣고(Subscribe) 알아서 도는 구조를 짜야 합니다.

2. 스크립트 작성 규칙 (Scripting Rules)
sealed 키워드 강제: FSM의 모든 상태 클래스(RunState, WallRunState 등)는 무조건 sealed로 선언하여 더 이상의 상속을 막고 성능을 약간이라도 끌어올립니다.

안전장치 컴포넌트 ([RequireComponent]): * 예: [RequireComponent(typeof(Rigidbody))]를 스크립트 맨 위에 적습니다. 이 스크립트를 객체에 넣으면 유니티가 알아서 Rigidbody를 붙여주므로 "NullReferenceException" 에러를 원천 차단합니다.

인스펙터 정리 ([Tooltip], [Header]): * 기획자(본인)가 나중에 유니티 에디터에서 수치를 조절할 때 헷갈리지 않도록 변수 위에 [Tooltip("천장에 붙었을 때의 중력 배율")]을 반드시 적습니다.