📄 1. 문제기술서 (Problem Statement) : 마인드맵 구조
게임의 핵심 목표와 해결 과제를 한눈에 파악할 수 있는 마인드맵입니다.

코드 스니펫
mindmap
  root((Vertigo Hound
  Core Concept))
    Problem
      밋밋한 3인칭 런닝뷰 한계
      안전 위주의 플랫포머
      속도감 저하 및 몰입도 부족
    Goal
      1인칭 극한의 속도감
      시각적 쾌감과 멀미의 경계
      쉴 틈 없는 조작 강요
    Key Mechanics
      수직 낙하 번지점프
      바닥 ↔ 천장 중력 반전
      사족보행 착지 회복
      나선형 벽 타기
📋 2. 요구사항 정의서 (SRS) 아키텍처 다이어그램
🧩 2-1. 시스템 파티셔닝 (System Architecture)
맵(환경)과 플레이어(주체)가 서로 간섭하지 않고 트리거를 통해서만 소통하는 구조입니다.

코드 스니펫
graph TD
    subgraph Map_System [Map System : 환경 제공 및 이벤트 발생]
        M1[Map Pattern Manager<br>난이도 및 패턴 제어] --> M2[Chunk Pooler<br>오브젝트 풀링/최적화]
        M2 --> M3[Event Triggers<br>수직 낙하 존, 중력 반전 존 스폰]
    end

    subgraph Player_System [Player System : 물리 반응 및 상태 제어]
        P1[Trigger Detection<br>충돌 및 이벤트 감지] --> P2[FSM Controller<br>상태 강제 전환]
        P2 --> P3[Physics & Movement<br>RigidBody 제어]
        P2 --> P4[Camera & Animation<br>1인칭 연출, FOV, 덤블링]
    end

    M3 == 1. Collider 충돌 / Trigger 진입 ==> P1
    P4 -. 2. 시각적 피드백 제공 .-> User
🏃‍♂️ 2-2. 플레이어 이동 상태 머신 (FSM Diagram)
클래스로 분리될 각 상태(IState) 간의 전이 조건과 흐름입니다.

코드 스니펫
stateDiagram-v2
    [*] --> RunState : 게임 시작
    
    state RunState {
        [*] --> Bipedal : 이족 보행 질주
    }
    
    RunState --> FreeFallState : 수직 낙하 트리거 감지
    FreeFallState --> QuadRecoveryState : 바닥 충돌 (속도 비례 연산)
    QuadRecoveryState --> RunState : 충격 흡수 후 1.5초 경과 (폼 회복)
    
    RunState --> CeilingState : 점프 + 천장 충돌
    CeilingState --> RunState : 스페이스바 (중력 원상복구 및 낙하)
    
    RunState --> WallRunState : 측면 벽 충돌
    WallRunState --> RunState : 나선형 하강 완료 또는 점프 이탈
    
    note right of FreeFallState
        - 카메라 180도 백덤블링 연출
        - Air Strafing (공중 좌우 회피) 가능
    end note
    
    note right of QuadRecoveryState
        - 카메라 높이 대폭 낮아짐 (사족보행 뷰)
        - 질주 속도(Momentum) 100% 유지
    end note
🏗️ 2-3. 맵 청크 라이프사이클 (Object Pooling Flow)
메모리(RAM) 부하를 막기 위한 맵 생성 및 소멸 사이클입니다.

코드 스니펫
graph LR
    A[(Chunk Pool<br>대기열 RAM)] -->|1. 패턴 요구| B(Spawn<br>플레이어 전방 시야 밖)
    B --> C{Player Interaction<br>틈새 통과 / 트리거 작동}
    C -->|2. 플레이어 통과| D(Despawn<br>카메라 후방 렌더링 종료)
    D -->|3. 비활성화 및 회수| A
    
    style A fill:#f9f,stroke:#333,stroke-width:2px
    style B fill:#bbf,stroke:#333,stroke-width:2px
    style D fill:#fbb,stroke:#333,stroke-width:2px