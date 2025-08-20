using UnityEngine;

/// <summary>
/// 일반적인 상호작용 가능한 오브젝트 인터페이스
/// 
/// == 사용 방법 ==
/// 1. MonoBehaviour에서 이 인터페이스를 구현
/// 2. EquipState에서 자동으로 감지되어 상호작용 처리
/// 3. NearFarInteractor의 Hover/Select 이벤트 활용
/// 
/// == 구현 예시 ==
/// - FarmTile: 수확 가능한 작물과 상호작용
/// - Door: 문 열기/닫기 상호작용
/// - Chest: 상자 열기 상호작용
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 현재 상호작용이 가능한 상태인지 확인
    /// </summary>
    /// <returns>상호작용 가능 여부</returns>
    bool CanInteract();
    
    /// <summary>
    /// VR 컨트롤러가 이 오브젝트에 호버할 때 호출
    /// 하이라이트 표시, UI 업데이트 등을 처리
    /// </summary>
    void OnHoverEnter();
    
    /// <summary>
    /// VR 컨트롤러가 이 오브젝트에서 호버를 벗어날 때 호출
    /// 하이라이트 제거, UI 정리 등을 처리
    /// </summary>
    void OnHoverExit();
    
    /// <summary>
    /// 실제 상호작용이 실행될 때 호출 (도구 사용 버튼 등)
    /// 각 오브젝트의 고유한 상호작용 로직을 처리
    /// </summary>
    void OnInteract();
    
    /// <summary>
    /// 상호작용 대상의 GameObject를 반환
    /// EquipState에서 컴포넌트 검색에 사용
    /// </summary>
    /// <returns>상호작용 가능한 GameObject</returns>
    GameObject GetGameObject();
}