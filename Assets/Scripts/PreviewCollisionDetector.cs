using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// PreviewCollisionDetector - 미리보기 오브젝트의 충돌 감지 컴포넌트
/// 
/// == 주요 기능 ==
/// 1. 미리보기 오브젝트와 다른 콜라이더간의 충돌 감지
/// 2. 충돌 상태를 VRPlacementController에 실시간 전달
/// 3. 자기 자신(원본 오브젝트)과의 충돌은 무시
/// 4. 트리거 이벤트 기반 충돌 감지
/// 
/// == 동작 원리 ==
/// - OnTriggerEnter: 충돌 시작 시 충돌 오브젝트 추가
/// - OnTriggerExit: 충돌 종료 시 충돌 오브젝트 제거
/// - 충돌 오브젝트 리스트 관리로 정확한 충돌 상태 추적
/// </summary>
public class PreviewCollisionDetector : MonoBehaviour
{
    private VRPlacementController placementController;
    private PlacableItem originalItem;
    private HashSet<Collider> collidingObjects = new HashSet<Collider>();

    /// <summary>
    /// 컴포넌트 초기화
    /// </summary>
    /// <param name="controller">충돌 상태를 전달받을 VRPlacementController</param>
    public void Initialize(VRPlacementController controller)
    {
        placementController = controller;
        originalItem = controller.GetCurrentGrabbedItem();
    }

    /// <summary>
    /// 트리거 충돌 시작 감지
    /// 원본 아이템과의 충돌은 무시하고, 다른 오브젝트와의 충돌만 처리
    /// </summary>
    /// <param name="other">충돌한 콜라이더</param>
    void OnTriggerEnter(Collider other)
    {
        // 원본 아이템과의 충돌은 무시
        if (originalItem != null && other.transform.IsChildOf(originalItem.transform))
            return;

        // 같은 오브젝트 내의 다른 콜라이더와의 충돌도 무시
        if (originalItem != null && other.gameObject == originalItem.gameObject)
            return;

        // 충돌 오브젝트 추가
        collidingObjects.Add(other);
        
        // 충돌 상태 업데이트
        UpdateCollisionState();
        
        Debug.Log($"PreviewCollision: '{other.gameObject.name}'와 충돌 시작");
    }

    /// <summary>
    /// 트리거 충돌 종료 감지
    /// 충돌 오브젝트 리스트에서 제거하고 상태 업데이트
    /// </summary>
    /// <param name="other">충돌이 끝난 콜라이더</param>
    void OnTriggerExit(Collider other)
    {
        // 충돌 오브젝트 제거
        collidingObjects.Remove(other);
        
        // 충돌 상태 업데이트
        UpdateCollisionState();
        
        Debug.Log($"PreviewCollision: '{other.gameObject.name}'와 충돌 종료");
    }

    /// <summary>
    /// 충돌 상태를 VRPlacementController에 전달
    /// 충돌하는 오브젝트가 하나라도 있으면 충돌 상태로 판단
    /// </summary>
    private void UpdateCollisionState()
    {
        bool hasCollision = collidingObjects.Count > 0;
        
        if (placementController != null)
        {
            placementController.SetPreviewCollisionState(hasCollision);
        }
    }

    /// <summary>
    /// 컴포넌트 파괴 시 충돌 상태 초기화
    /// </summary>
    void OnDestroy()
    {
        if (placementController != null)
        {
            placementController.SetPreviewCollisionState(false);
        }
        
        collidingObjects.Clear();
    }
}