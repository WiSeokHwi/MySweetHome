using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// VRCraftingTable - VR 환경에서 사용할 수 있는 제작대 인터페이스
/// 
/// == 주요 기능 ==
/// 1. 3x3 그리드 기반 제작 인터페이스
/// 2. VR 컨트롤러로 아이템 배치/제거
/// 3. 실시간 레시피 매칭 및 결과 미리보기
/// 4. 제작 실행 및 애니메이션 효과
/// 5. 인벤토리 시스템과 연동
/// 
/// == 사용 방법 ==
/// 1. VR 플레이어가 제작대에 접근
/// 2. 그리드 슬롯에 재료 배치
/// 3. 올바른 레시피가 감지되면 결과물 미리보기 표시
/// 4. 제작 버튼을 눌러 아이템 제작
/// </summary>
public class VRCraftingTable : MonoBehaviour
{
    [Header("제작대 설정")]
    [Tooltip("제작 가능 거리")]
    [SerializeField] private float interactionDistance = 2f;
    [Tooltip("그리드 슬롯 간격")]
    [SerializeField] private float slotSpacing = 0.1f;
    [Tooltip("디버그 로그 출력 여부")]
    [SerializeField] private bool enableDebugLogs = true;
    
    [Header("UI 오브젝트들")]
    [Tooltip("3x3 제작 그리드의 부모 오브젝트")]
    [SerializeField] private Transform craftingGridParent;
    [Tooltip("결과물 미리보기 슬롯")]
    [SerializeField] private Transform resultSlot;
    [Tooltip("제작 버튼")]
    [SerializeField] private XRBaseInteractable craftButton;
    [Tooltip("제작대 메인 패널")]
    [SerializeField] private GameObject craftingPanel;
    
    [Header("시각적 효과")]
    [Tooltip("제작 성공 시 파티클 효과")]
    [SerializeField] private ParticleSystem craftSuccessEffect;
    [Tooltip("제작 사운드")]
    [SerializeField] private AudioClip craftSound;
    
    // 내부 상태
    private CraftingSlot[,] craftingGrid = new CraftingSlot[3, 3]; // 3x3 제작 그리드
    private CraftingRecipe currentRecipe; // 현재 매칭된 레시피
    private GameObject resultPreview; // 결과물 미리보기 오브젝트
    private AudioSource audioSource;
    private Transform playerTransform; // 플레이어 위치 추적용
    
    // 그리드 슬롯 관리
    private List<GameObject> gridSlotObjects = new List<GameObject>();
    
    /// <summary>
    /// 제작 슬롯 클래스 - 각 그리드 위치의 상태 관리
    /// </summary>
    [System.Serializable]
    public class CraftingSlot
    {
        public CraftingMaterial material;
        public int quantity;
        public GameObject visualObject; // 시각적 표현용 오브젝트
        
        public bool IsEmpty => material == null || quantity <= 0;
        
        public void Clear()
        {
            material = null;
            quantity = 0;
            if (visualObject != null)
            {
                if (Application.isPlaying)
                    Destroy(visualObject);
                else
                    DestroyImmediate(visualObject);
                visualObject = null;
            }
        }
        
        public void SetItem(CraftingMaterial newMaterial, int newQuantity)
        {
            material = newMaterial;
            quantity = newQuantity;
        }
    }
    
    void Awake()
    {
        // 컴포넌트 초기화
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }
    
    void Start()
    {
        InitializeCraftingGrid();
        SetupInteractions();
        
        // 플레이어 찾기
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;
        
        // 제작대 패널 초기 비활성화
        if (craftingPanel != null)
            craftingPanel.SetActive(false);
            
        if (enableDebugLogs)
            Debug.Log("[VRCraftingTable] 제작대가 초기화되었습니다.");
    }
    
    /// <summary>
    /// 제작 그리드 초기화
    /// </summary>
    private void InitializeCraftingGrid()
    {
        // 그리드 슬롯 오브젝트들 생성
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                craftingGrid[x, y] = new CraftingSlot();
                CreateSlotVisual(x, y);
            }
        }
    }
    
    /// <summary>
    /// 그리드 슬롯의 시각적 표현 생성
    /// </summary>
    /// <param name="x">그리드 X 좌표</param>
    /// <param name="y">그리드 Y 좌표</param>
    private void CreateSlotVisual(int x, int y)
    {
        GameObject slotObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        slotObj.name = $"CraftingSlot_{x}_{y}";
        slotObj.transform.SetParent(craftingGridParent);
        
        // 위치 설정
        Vector3 position = new Vector3(
            (x - 1) * slotSpacing, // 중앙 정렬
            0,
            (y - 1) * slotSpacing
        );
        slotObj.transform.localPosition = position;
        slotObj.transform.localScale = Vector3.one * 0.08f; // 작게 만들기
        
        // XR 인터랙션 설정
        var interactable = slotObj.AddComponent<XRGrabInteractable>();
        
        // 슬롯 클릭 이벤트 설정
        int capturedX = x, capturedY = y; // 클로저를 위한 지역 변수
        interactable.selectEntered.AddListener((args) => OnSlotInteracted(capturedX, capturedY, args));
        
        // 시각적 설정
        var renderer = slotObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            // 기본 빈 슬롯 머티리얼 (반투명 회색)
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            renderer.material = material;
        }
        
        gridSlotObjects.Add(slotObj);
    }
    
    /// <summary>
    /// VR 인터랙션 설정
    /// </summary>
    private void SetupInteractions()
    {
        // 제작 버튼 이벤트 연결
        if (craftButton != null)
        {
            craftButton.selectEntered.AddListener(OnCraftButtonPressed);
        }
    }
    
    /// <summary>
    /// 그리드 슬롯 상호작용 처리
    /// </summary>
    /// <param name="x">그리드 X 좌표</param>
    /// <param name="y">그리드 Y 좌표</param>
    /// <param name="args">인터랙션 이벤트</param>
    private void OnSlotInteracted(int x, int y, SelectEnterEventArgs args)
    {
        if (enableDebugLogs)
            Debug.Log($"[VRCraftingTable] 슬롯 ({x}, {y})이 클릭되었습니다.");
        
        var slot = craftingGrid[x, y];
        
        if (slot.IsEmpty)
        {
            // 빈 슬롯: 인벤토리에서 아이템 가져오기
            TryPlaceItemInSlot(x, y);
        }
        else
        {
            // 채워진 슬롯: 아이템을 인벤토리로 반환
            ReturnItemToInventory(x, y);
        }
        
        // 레시피 매칭 확인
        CheckForRecipeMatch();
    }
    
    /// <summary>
    /// 인벤토리에서 아이템을 슬롯에 배치 시도
    /// </summary>
    /// <param name="x">그리드 X 좌표</param>
    /// <param name="y">그리드 Y 좌표</param>
    private void TryPlaceItemInSlot(int x, int y)
    {
        var inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            Debug.LogError("[VRCraftingTable] PlayerInventory를 찾을 수 없습니다!");
            return;
        }
        
        // 간단한 구현: 인벤토리의 첫 번째 아이템 사용
        // 실제로는 UI를 통해 아이템 선택 구현 필요
        var firstItem = GetFirstAvailableItem(inventory);
        if (firstItem != null)
        {
            int removed = inventory.RemoveItem(firstItem, 1);
            if (removed > 0)
            {
                craftingGrid[x, y].SetItem(firstItem, 1);
                UpdateSlotVisual(x, y);
                
                if (enableDebugLogs)
                    Debug.Log($"[VRCraftingTable] {firstItem.materialName}을 슬롯 ({x}, {y})에 배치했습니다.");
            }
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log("[VRCraftingTable] 인벤토리에 배치할 아이템이 없습니다.");
        }
    }
    
    /// <summary>
    /// 인벤토리에서 사용 가능한 첫 번째 아이템 찾기
    /// </summary>
    /// <param name="inventory">플레이어 인벤토리</param>
    /// <returns>첫 번째 사용 가능한 아이템</returns>
    private CraftingMaterial GetFirstAvailableItem(PlayerInventory inventory)
    {
        var allSlots = inventory.GetAllSlots();
        
        foreach (var slot in allSlots)
        {
            if (slot != null && !slot.IsEmpty && slot.Quantity > 0)
            {
                return slot.material;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 슬롯의 아이템을 인벤토리로 반환
    /// </summary>
    /// <param name="x">그리드 X 좌표</param>
    /// <param name="y">그리드 Y 좌표</param>
    private void ReturnItemToInventory(int x, int y)
    {
        var slot = craftingGrid[x, y];
        if (slot.IsEmpty) return;
        
        var inventory = PlayerInventory.Instance;
        if (inventory != null)
        {
            bool added = inventory.AddItem(slot.material, slot.quantity);
            if (added)
            {
                if (enableDebugLogs)
                    Debug.Log($"[VRCraftingTable] {slot.material.materialName} x{slot.quantity}을 인벤토리에 반환했습니다.");
                
                slot.Clear();
                UpdateSlotVisual(x, y);
            }
        }
    }
    
    /// <summary>
    /// 슬롯의 시각적 표현 업데이트
    /// </summary>
    /// <param name="x">그리드 X 좌표</param>
    /// <param name="y">그리드 Y 좌표</param>
    private void UpdateSlotVisual(int x, int y)
    {
        var slot = craftingGrid[x, y];
        int slotIndex = x * 3 + y;
        
        if (slotIndex < gridSlotObjects.Count)
        {
            var slotObj = gridSlotObjects[slotIndex];
            var renderer = slotObj.GetComponent<Renderer>();
            
            if (slot.IsEmpty)
            {
                // 빈 슬롯 - 반투명 회색
                renderer.material.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            }
            else
            {
                // 채워진 슬롯 - 아이템 색상 (임시로 파란색)
                renderer.material.color = new Color(0.2f, 0.6f, 1f, 0.8f);
            }
        }
    }
    
    /// <summary>
    /// 현재 그리드 상태로 레시피 매칭 확인
    /// </summary>
    private void CheckForRecipeMatch()
    {
        var craftingManager = CraftingManager.Instance;
        if (craftingManager == null) return;
        
        // 현재 그리드를 ItemStack 배열로 변환
        ItemStack[,] currentPattern = new ItemStack[3, 3];
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                var slot = craftingGrid[x, y];
                if (!slot.IsEmpty)
                {
                    currentPattern[x, y] = new ItemStack(slot.material, slot.quantity);
                }
            }
        }
        
        // 레시피 매칭 시도
        currentRecipe = craftingManager.FindMatchingRecipe(currentPattern);
        
        // 결과 미리보기 업데이트
        UpdateResultPreview();
        
        if (enableDebugLogs)
        {
            string resultText = currentRecipe != null ? currentRecipe.recipeName : "매칭 없음";
            Debug.Log($"[VRCraftingTable] 레시피 매칭 결과: {resultText}");
        }
    }
    
    /// <summary>
    /// 결과물 미리보기 업데이트
    /// </summary>
    private void UpdateResultPreview()
    {
        // 기존 미리보기 제거
        if (resultPreview != null)
        {
            if (Application.isPlaying)
                Destroy(resultPreview);
            else
                DestroyImmediate(resultPreview);
        }
        
        // 새로운 미리보기 생성
        if (currentRecipe != null && resultSlot != null)
        {
            resultPreview = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            resultPreview.name = "ResultPreview";
            resultPreview.transform.SetParent(resultSlot);
            resultPreview.transform.localPosition = Vector3.zero;
            resultPreview.transform.localScale = Vector3.one * 0.05f;
            
            // 시각적 설정 (임시로 초록색)
            var renderer = resultPreview.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = Color.green;
                renderer.material = material;
            }
            
            // 콜라이더 제거 (시각적 목적만)
            var collider = resultPreview.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                    Destroy(collider);
                else
                    DestroyImmediate(collider);
            }
        }
    }
    
    /// <summary>
    /// 제작 버튼 클릭 처리
    /// </summary>
    /// <param name="args">인터랙션 이벤트</param>
    private void OnCraftButtonPressed(SelectEnterEventArgs args)
    {
        if (currentRecipe == null)
        {
            if (enableDebugLogs)
                Debug.Log("[VRCraftingTable] 제작할 레시피가 없습니다.");
            return;
        }
        
        // 제작 실행
        bool success = ExecuteCrafting();
        
        if (success)
        {
            PlayCraftingEffects();
            ClearCraftingGrid();
            
            if (enableDebugLogs)
                Debug.Log($"[VRCraftingTable] 제작 성공: {currentRecipe.recipeName}");
        }
        else
        {
            if (enableDebugLogs)
                Debug.Log("[VRCraftingTable] 제작 실패: 재료가 부족하거나 인벤토리가 가득 참");
        }
    }
    
    /// <summary>
    /// 실제 제작 실행
    /// </summary>
    /// <returns>제작 성공 여부</returns>
    private bool ExecuteCrafting()
    {
        var inventory = PlayerInventory.Instance;
        if (inventory == null) return false;
        
        // 결과물을 인벤토리에 추가 시도
        bool added = inventory.AddItem(currentRecipe.resultMaterial, currentRecipe.resultQuantity);
        return added;
    }
    
    /// <summary>
    /// 제작 그리드 초기화
    /// </summary>
    private void ClearCraftingGrid()
    {
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                craftingGrid[x, y].Clear();
                UpdateSlotVisual(x, y);
            }
        }
        
        currentRecipe = null;
        UpdateResultPreview();
    }
    
    /// <summary>
    /// 제작 효과 재생
    /// </summary>
    private void PlayCraftingEffects()
    {
        // 파티클 효과
        if (craftSuccessEffect != null)
        {
            craftSuccessEffect.Play();
        }
        
        // 사운드 효과
        if (craftSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(craftSound);
        }
    }
    
    void Update()
    {
        // 플레이어 거리에 따른 UI 활성화/비활성화
        if (playerTransform != null && craftingPanel != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);
            bool shouldShow = distance <= interactionDistance;
            
            if (craftingPanel.activeSelf != shouldShow)
            {
                craftingPanel.SetActive(shouldShow);
                
                if (enableDebugLogs)
                    Debug.Log($"[VRCraftingTable] 제작대 UI {(shouldShow ? "활성화" : "비활성화")}");
            }
        }
    }
    
    /// <summary>
    /// 에디터에서 상호작용 거리 표시
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, $"제작대\n상호작용 거리: {interactionDistance}m");
        #endif
    }
    
    void OnDestroy()
    {
        // 메모리 정리
        ClearCraftingGrid();
        
        if (resultPreview != null)
        {
            if (Application.isPlaying)
                Destroy(resultPreview);
            else
                DestroyImmediate(resultPreview);
        }
    }
}