using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// VR UI 관리자 - VR 환경에서 인벤토리와 제작 UI를 통합 관리
/// </summary>
public class VRUIManager : MonoBehaviour
{
    // 싱글톤 인스턴스
    private static VRUIManager _instance;
    public static VRUIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<VRUIManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("VRUIManager");
                    _instance = go.AddComponent<VRUIManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("UI Panels")]
    [SerializeField] private VRInventoryUI inventoryUI;
    [SerializeField] private VRCraftingUI craftingUI;
    [SerializeField] private Transform playerHead;

    [Header("UI Manual Positioning")]
    [SerializeField] private Vector3 inventoryOffset = new Vector3(-0.75f, 0.5f, 2.0f);
    [SerializeField] private Vector3 inventoryRotation = new Vector3(0f, 15f, 0f);
    [SerializeField] private Vector3 craftingOffset = new Vector3(0.75f, 0.5f, 2.0f);
    [SerializeField] private Vector3 craftingRotation = new Vector3(0f, -15f, 0f);

    [Header("Input Settings")]
    [SerializeField] private KeyCode inventoryToggleKey = KeyCode.Tab;
    [SerializeField] private KeyCode craftingToggleKey = KeyCode.C;
    [SerializeField] private InputActionProperty inventoryToggleAction;
    [SerializeField] private InputActionProperty craftingToggleAction;

    public VRInventoryUI InventoryUI => inventoryUI;
    public VRCraftingUI CraftingUI => craftingUI;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (inventoryUI == null)
            inventoryUI = FindAnyObjectByType<VRInventoryUI>();

        if (craftingUI == null)
            craftingUI = FindAnyObjectByType<VRCraftingUI>();

        if (playerHead == null)
        {
            Camera vrCamera = Camera.main;
            if (vrCamera != null)
                playerHead = vrCamera.transform;
        }
    }

    void OnEnable()
    {
        if (inventoryToggleAction.action != null)
        {
            inventoryToggleAction.action.performed += OnInventoryTogglePerformed;
            inventoryToggleAction.action.Enable();
        }

        if (craftingToggleAction.action != null)
        {
            craftingToggleAction.action.performed += OnCraftingTogglePerformed;
            craftingToggleAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (inventoryToggleAction.action != null)
        {
            inventoryToggleAction.action.performed -= OnInventoryTogglePerformed;
            inventoryToggleAction.action.Disable();
        }

        if (craftingToggleAction.action != null)
        {
            craftingToggleAction.action.performed -= OnCraftingTogglePerformed;
            craftingToggleAction.action.Disable();
        }
    }

    void Start()
    {
        if (craftingUI != null)
            craftingUI.OnCraftingCompleted += OnCraftingCompleted;
    }

    void Update()
    {
        HandleKeyboardInput();
        UpdateUIPositions();
    }

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(inventoryToggleKey))
            ToggleInventory();

        if (Input.GetKeyDown(craftingToggleKey))
            ToggleCrafting();
    }

    private void UpdateUIPositions()
    {
        if (playerHead == null) return;

        Vector3 playerPos = playerHead.position;
        Quaternion playerYRotation = Quaternion.Euler(0f, playerHead.eulerAngles.y, 0f);

        if (inventoryUI != null && inventoryUI.IsOpen)
        {
            Vector3 inventoryPos = playerPos + playerYRotation * inventoryOffset;
            inventoryUI.transform.position = inventoryPos;
            inventoryUI.transform.rotation = playerYRotation * Quaternion.Euler(inventoryRotation);
        }

        if (craftingUI != null && craftingUI.IsOpen)
        {
            Vector3 craftingPos = playerPos + playerYRotation * craftingOffset;
            craftingUI.transform.position = craftingPos;
            craftingUI.transform.rotation = playerYRotation * Quaternion.Euler(craftingRotation);
        }
    }

    public void ToggleInventory()
    {
        if (inventoryUI == null) return;

        bool targetVisibility = !inventoryUI.IsOpen;
        inventoryUI.SetVisibility(targetVisibility);

        if (targetVisibility && craftingUI != null && craftingUI.IsOpen)
            craftingUI.SetVisibility(false);
    }

    public void ToggleCrafting()
    {
        if (craftingUI == null) return;

        bool targetVisibility = !craftingUI.IsOpen;
        craftingUI.SetVisibility(targetVisibility);

        if (targetVisibility && inventoryUI != null && !inventoryUI.IsOpen)
            inventoryUI.SetVisibility(true);
    }

    public void CloseAllUI()
    {
        if (inventoryUI != null && inventoryUI.IsOpen)
            inventoryUI.SetVisibility(false);

        if (craftingUI != null && craftingUI.IsOpen)
            craftingUI.SetVisibility(false);
    }

    public bool IsAnyUIOpen()
    {
        return (inventoryUI != null && inventoryUI.IsOpen) || (craftingUI != null && craftingUI.IsOpen);
    }

    private void OnCraftingCompleted()
    {
        Debug.Log("VRUIManager: 제작이 완료되었습니다!");
    }

    public void SetUIScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0.5f, 2.0f);

        if (inventoryUI != null)
            inventoryUI.transform.localScale = Vector3.one * scale;

        if (craftingUI != null)
            craftingUI.transform.localScale = Vector3.one * scale;
    }

    private void OnInventoryTogglePerformed(InputAction.CallbackContext context)
    {
        ToggleInventory();
    }

    private void OnCraftingTogglePerformed(InputAction.CallbackContext context)
    {
        ToggleCrafting();
    }
}