using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 수확 가능한 작물 - 완전히 자란 작물에 부착되어 수확 상호작용을 처리
/// 
/// == 주요 기능 ==
/// 1. VR 상호작용을 통한 작물 수확
/// 2. 수확 시 아이템 드롭 및 인벤토리 추가
/// 3. 시각적 피드백 (하이라이트, 애니메이션)
/// 4. FarmTile과 연동된 수확 처리
/// </summary>
public class HarvestableItem : MonoBehaviour
{
    [Header("Harvest Settings")]
    [Tooltip("연결된 농장 타일")]
    public FarmTile parentFarmTile;
    
    [Tooltip("수확 시 아이템 드롭 위치 오프셋")]
    public Vector3 dropOffset = Vector3.up * 0.5f;
    
    [Tooltip("수확 시 시각적 효과 프리팹")]
    public GameObject harvestEffectPrefab;
    
    [Tooltip("하이라이트 머티리얼")]
    public Material highlightMaterial;
    
    [Header("Audio")]
    [Tooltip("수확 사운드")]
    public AudioClip harvestSound;
    
    // XR 상호작용 관련
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    private Renderer[] renderers;
    private Material[] originalMaterials;
    private AudioSource audioSource;
    
    // 상호작용 이벤트들
    private bool isHighlighted = false;
    
    void Start()
    {
        SetupInteractable();
        SetupRenderers();
        SetupAudio();
        
        // 부모 FarmTile 자동 감지
        if (parentFarmTile == null)
        {
            parentFarmTile = GetComponentInParent<FarmTile>();
        }
        
        if (parentFarmTile == null)
        {
            Debug.LogError($"[HarvestableItem] {gameObject.name}: 부모 FarmTile을 찾을 수 없습니다!");
        }
    }
    
    /// <summary>
    /// XR 상호작용 설정
    /// </summary>
    private void SetupInteractable()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (interactable == null)
        {
            interactable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        }
        
        // 이벤트 연결
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
    }
    
    /// <summary>
    /// 렌더러 및 머티리얼 설정
    /// </summary>
    private void SetupRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i].material;
            }
        }
    }
    
    /// <summary>
    /// 오디오 설정
    /// </summary>
    private void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && harvestSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // 3D 사운드
        }
    }
    
    /// <summary>
    /// 호버 시작 (마우스 오버 또는 VR 컨트롤러가 가까이 왔을 때)
    /// </summary>
    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        SetHighlight(true);
        Debug.Log($"[HarvestableItem] {gameObject.name}: 수확 가능 - 클릭하여 수확하세요!");
    }
    
    /// <summary>
    /// 호버 종료
    /// </summary>
    private void OnHoverExited(HoverExitEventArgs args)
    {
        SetHighlight(false);
    }
    
    /// <summary>
    /// 선택 시작 (클릭 또는 VR 트리거)
    /// </summary>
    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        HarvestCrop();
    }
    
    /// <summary>
    /// 선택 종료
    /// </summary>
    private void OnSelectExited(SelectExitEventArgs args)
    {
        // 빈 구현
    }
    
    /// <summary>
    /// 하이라이트 설정/해제
    /// </summary>
    /// <param name="highlight">하이라이트 여부</param>
    private void SetHighlight(bool highlight)
    {
        if (renderers == null || originalMaterials == null) return;
        
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                if (highlight && highlightMaterial != null)
                {
                    renderers[i].material = highlightMaterial;
                }
                else if (originalMaterials[i] != null)
                {
                    renderers[i].material = originalMaterials[i];
                }
            }
        }
    }
    
    /// <summary>
    /// 작물 수확 실행
    /// </summary>
    private void HarvestCrop()
    {
        if (parentFarmTile == null)
        {
            Debug.LogError("[HarvestableItem] 부모 FarmTile이 없습니다!");
            return;
        }
        
        // FarmTile에서 수확 처리
        HarvestReward reward = parentFarmTile.HarvestCrop();
        
        if (reward.mainItem != null)
        {
            // 수확 보상 아이템 생성
            CreateHarvestDrops(reward);
            
            // 시각적 효과 재생
            PlayHarvestEffect();
            
            // 사운드 재생
            PlayHarvestSound();
            
            Debug.Log($"[HarvestableItem] 수확 완료! {reward.mainItem.itemName} x{reward.mainItemAmount}개 획득");
            
            // 수확 가능한 오브젝트 제거
            Destroy(gameObject, 0.1f); // 약간의 딜레이 후 제거
        }
        else
        {
            Debug.LogWarning("[HarvestableItem] 수확할 수 있는 아이템이 없습니다.");
        }
    }
    
    /// <summary>
    /// 수확 보상 아이템들을 월드에 드롭
    /// </summary>
    /// <param name="reward">수확 보상</param>
    private void CreateHarvestDrops(HarvestReward reward)
    {
        Vector3 dropPosition = transform.position + dropOffset;
        
        // 주요 수확물 드롭
        if (reward.mainItem != null && reward.mainItemAmount > 0)
        {
            for (int i = 0; i < reward.mainItemAmount; i++)
            {
                CreateDropItem(reward.mainItem, dropPosition + Random.insideUnitSphere * 0.3f);
            }
        }
        
        // 보너스 씨앗 드롭
        if (reward.bonusSeeds > 0 && parentFarmTile.plantedSeed != null)
        {
            for (int i = 0; i < reward.bonusSeeds; i++)
            {
                CreateDropItem(parentFarmTile.plantedSeed, dropPosition + Random.insideUnitSphere * 0.3f);
            }
        }
    }
    
    /// <summary>
    /// 개별 아이템 드롭 생성
    /// </summary>
    /// <param name="itemData">드롭할 아이템 데이터</param>
    /// <param name="position">드롭 위치</param>
    private void CreateDropItem(ItemData itemData, Vector3 position)
    {
        if (itemData?.gameModel == null) return;
        
        GameObject droppedItem = Instantiate(itemData.gameModel, position, Quaternion.identity);
        
        // 물리 효과 추가 (약간 튀어오르는 효과)
        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = droppedItem.AddComponent<Rigidbody>();
        }
        
        // 랜덤한 방향으로 약간의 힘 가하기
        Vector3 randomForce = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(2f, 4f),
            Random.Range(-1f, 1f)
        );
        rb.AddForce(randomForce, ForceMode.Impulse);
        
        // 드롭된 아이템이 PickableItem 컴포넌트를 가지고 있다면 활성화
        PickableItem pickable = droppedItem.GetComponent<PickableItem>();
        if (pickable != null)
        {
            pickable.enabled = true;
        }
    }
    
    /// <summary>
    /// 수확 시각적 효과 재생
    /// </summary>
    private void PlayHarvestEffect()
    {
        if (harvestEffectPrefab != null)
        {
            GameObject effect = Instantiate(harvestEffectPrefab, transform.position, Quaternion.identity);
            
            // 파티클 시스템이 있다면 자동 제거 설정
            ParticleSystem particles = effect.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                var main = particles.main;
                Destroy(effect, main.duration + main.startLifetime.constantMax);
            }
            else
            {
                Destroy(effect, 2f); // 기본 2초 후 제거
            }
        }
    }
    
    /// <summary>
    /// 수확 사운드 재생
    /// </summary>
    private void PlayHarvestSound()
    {
        if (audioSource != null && harvestSound != null)
        {
            audioSource.PlayOneShot(harvestSound);
        }
    }
    
    /// <summary>
    /// 컴포넌트 정리
    /// </summary>
    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
    }
    
}