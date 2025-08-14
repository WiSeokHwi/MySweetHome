/// <summary>
/// ==================== VR 인벤토리 UI 슬롯 컴포넌트 ====================
/// 
/// 【 시스템 개요 】
/// 인벤토리 UI에서 각 슬롯의 위치와 인덱스를 관리하는 간단한 컴포넌트입니다.
/// 드래그 앤 드롭 시스템에서 타겟 슬롯을 찾는 데 사용됩니다.
/// 
/// 【 주요 기능 】
/// - 슬롯 인덱스 저장 및 식별
/// - VRUIItemIcon의 드래그 앤 드롭 대상 역할
/// 
/// 【 연동 시스템 】
/// - VRUIItemIcon.FindNearestSlot(): 드래그 시 가장 가까운 슬롯 찾기
/// - InventoryManager.slotUIList: 모든 슬롯들의 배열
/// 
/// 【 데이터 저장 위치 】
/// - slotIndex: 이 슬롯의 고유 인덱스 (이 클래스에서 관리)
/// 
/// 【 사용 플로우 】
/// 1. InventoryManager.Awake()에서 slotIndex 설정
/// 2. VRUIItemIcon에서 드래그 시 FindNearestSlot()로 이 컴포넌트 참조
/// 3. slotIndex를 통해 InventoryManager.MoveItem() 호출
/// </summary>
using UnityEngine;

public class VRUISlot : MonoBehaviour
{
    /// <summary>
    /// 【데이터 저장】이 슬롯의 고유 인덱스 번호
    /// 【설정 위치】InventoryManager.Awake()에서 슬롯 초기화 시 자동 설정
    /// 【사용 위치】VRUIItemIcon.FindNearestSlot()에서 드래그 대상 슬롯 결정 시
    /// 【범위】보통 0 ~ (InventoryManager.maxSlots - 1)
    /// </summary>
    public int slotIndex;
}
