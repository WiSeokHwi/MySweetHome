# 🏠 MySweetHome VR 시스템 아키텍처 개요

## 📋 전체 시스템 구성

### 🎯 핵심 아키텍처 클래스
```
🎮 VR 상호작용 계층
├── GrabbableItem (추상) - 모든 잡을 수 있는 오브젝트의 기본 클래스
│   ├── ToolItem - 도구류 (괭이, 물뿌리개, 도끼, 곡괭이)
│   ├── PickableItem - 수집 아이템 (씨앗, 재료)
│   └── PlacableItem - 배치 아이템 (가구, 건물)
├── EquipState - 중앙 입력 관리 시스템 (어떤 손에 뭐가 들려있는지 추적)
└── InteractionObject (추상) - 도구와 상호작용 가능한 오브젝트들
    └── LandObject - 땅/농지 (괭이로 갈 수 있음)

📦 인벤토리 계층
├── InventoryManager (싱글톤) - 인벤토리 데이터 및 UI 통합 관리
├── InventoryUIManager - 인벤토리 창 열기/닫기 및 아이템 정보 표시
├── VRUIItemIcon - 각 아이템의 UI 아이콘 (드래그 앤 드롭 포함)
├── VRUISlot - 인벤토리 슬롯 UI 컨테이너
└── InventoryDropZone - 아이템을 인벤토리에 넣을 수 있는 구역

🏗️ 배치 시스템 계층
├── VRPlacementController - 그리드 기반 아이템 배치 시스템
├── GridManager - 그리드 데이터 관리 및 셀 점유 추적
└── PreviewCollisionDetector - 배치 미리보기 충돌 검사

🔧 도구 상호작용 계층
├── ToolItem - 모든 도구의 기본 클래스
├── ToolTriggerHandler - 도구 충돌 감지 헬퍼
└── InteractionObject와 하위 클래스들 - 상호작용 가능한 오브젝트들

🎨 UI 시스템 계층
├── InventoryUIManager - 인벤토리 UI 총괄 관리
└── UIRaycastController - 인벤토리가 열렸을 때만 활성화되는 레이캐스트 UI
```

## 🔄 주요 데이터 흐름

### 1. 아이템 잡기 → 입력 처리 흐름
```
1. 플레이어가 VR 컨트롤러로 아이템 잡기
   ↓
2. XRGrabInteractable.selectEntered 이벤트 발생
   ↓
3. GrabbableItem.OnGrabStarted() 호출
   ↓
4. EquipState에서 equippedItems Dictionary 업데이트
   ↓
5. 플레이어가 입력 버튼 누름 (예: 트리거)
   ↓
6. EquipState.OnToolUsePerformed() 호출
   ↓
7. 현재 손에 들린 아이템의 OnToolUseInput() 호출
```

### 2. 인벤토리 추가 흐름
```
1. 플레이어가 아이템을 들고 InventoryDropZone에 진입
   ↓
2. InventoryDropZone.OnTriggerEnter() → GrabbableItem.OnEnteredDropZone()
   ↓
3. 플레이어가 인벤토리 추가 버튼 누름
   ↓
4. EquipState.OnInventoryAddPerformed() → GrabbableItem.OnInventoryAddInput()
   ↓
5. GrabbableItem.TryAddToInventory() → InventoryManager.AddItemToInventory()
   ↓
6. 성공 시 GameObject.Destroy() + InventoryManager.UpdateSlotUI()
```

### 3. 도구 사용 흐름
```
1. 플레이어가 ToolItem을 잡고 있음
   ↓
2. 도구가 InteractionObject에 닿음
   ↓
3. ToolTriggerHandler.OnTriggerEnter() → InteractionObject.Interact(true)
   ↓ (시각적 피드백 시작)
4. 플레이어가 도구 사용 버튼 누름
   ↓
5. ToolItem.OnToolUseInput() → ToolItem.UseTool()
   ↓
6. InteractionObject.InteractWithTool(toolType) 호출
   ↓
7. 각 InteractionObject 하위 클래스에서 고유한 상호작용 수행
```

### 4. 배치 시스템 흐름
```
1. 플레이어가 PlacableItem을 잡고 배치 모드 버튼 누름
   ↓
2. EquipState.OnPlacementModeTogglePerformed() → VRPlacementController.TogglePlacementMode()
   ↓
3. 미리보기 오브젝트 생성 + XRRayInteractor 활성화
   ↓
4. 실시간 레이캐스트로 배치 위치 결정
   ↓
5. GridManager로 그리드 좌표 변환 + 충돌 검사
   ↓
6. 배치 확정 버튼 누름 → VRPlacementController.ConfirmPlacement()
   ↓
7. GridManager에 셀 점유 등록 + 아이템 위치 확정
```

## 🎯 주요 설계 원칙

### 1. 단일 책임 원칙 (SRP)
- **EquipState**: 입력 관리만 담당
- **VRPlacementController**: 배치 시스템만 담당  
- **InventoryManager**: 인벤토리 데이터만 담당
- **UIRaycastController**: UI 레이캐스트만 담당

### 2. 의존성 역전 원칙 (DIP)
- **InteractionObject 시스템**: 도구가 구체적인 오브젝트 타입을 알 필요 없음
- **GrabbableItem 추상화**: 공통 인터페이스로 다양한 아이템 타입 처리

### 3. 개방-폐쇄 원칙 (OCP)
- **새로운 InteractionObject 추가**: 기존 ToolItem 코드 수정 불필요
- **새로운 GrabbableItem 타입**: 기존 EquipState 코드 수정 불필요

## 🔧 주요 확장 포인트

### 새로운 도구 추가
1. `EquipmentData`에 새로운 `ToolType` 추가
2. `ToolItem.UseTool()`에 케이스 추가 (또는 더 나은 방법으로 리팩토링)

### 새로운 상호작용 오브젝트 추가  
1. `InteractionObject` 상속받는 새 클래스 생성
2. `InteractWithTool(ToolType)` 메서드 구현
3. `compatibleToolTypes` 배열에 호환 도구 설정

### 새로운 아이템 타입 추가
1. `GrabbableItem` 상속받는 새 클래스 생성  
2. `GetItemData()`, `GetQuantity()`, `OnToolUseInput()` 구현
3. 필요한 경우 `ItemData` 하위 클래스 생성

## 💾 중요한 데이터 저장소

| 클래스 | 저장 데이터 | 용도 |
|--------|-------------|------|
| `InventoryManager.inventorySlots` | List<SlotData> | 모든 인벤토리 아이템 데이터 |
| `EquipState.equippedItems` | Dictionary<NearFarInteractor, GrabbableItem> | 각 손별 현재 장착 아이템 |
| `GridManager.occupiedCells` | Dictionary<Vector2Int, PlacableItem> | 그리드 셀 점유 정보 |
| `VRPlacementController.inPlacementMode` | bool | 현재 배치 모드 여부 |
| `GrabbableItem.currentDropZones` | HashSet<InventoryDropZone> | 현재 감지된 드롭존들 |

## 🚨 주의사항 및 개선 포인트

### 현재 알려진 문제점
1. **ToolItem.UseTool()**: switch문으로 도구별 처리 → Strategy 패턴으로 개선 가능
2. **싱글톤 의존성**: InventoryManager.Instance 과도한 사용 → DI 컨테이너 고려
3. **UI 업데이트**: 일부 수동 UI 업데이트 → Observer 패턴 도입 고려

### 성능 최적화 포인트
1. **UIRaycastController**: 인벤토리 닫힌 상태에서 완전 비활성화 ✅
2. **Grid 렌더링**: 배치 모드에서만 로컬 그리드 표시 ✅  
3. **Collision Detection**: 트리거 기반으로 물리 연산 최소화 ✅

## 📚 학습 리소스

- **Unity XR Interaction Toolkit**: VR 상호작용의 기반
- **Unity Input System**: 현대적인 입력 처리
- **Observer Pattern**: UI 업데이트 자동화
- **Strategy Pattern**: 도구별 동작 분리
- **Dependency Injection**: 싱글톤 의존성 해결