using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// ResourceGathering - VR에서 자원을 수집하는 시스템
/// 
/// == 주요 기능 ==
/// 1. VR 컨트롤러로 나무/돌 등 자원 채취
/// 2. 타격 시 파티클 효과 및 사운드 재생
/// 3. 채취된 자원을 인벤토리에 자동 추가
/// 4. 자원 고갈 시스템 (내구도 기반)
/// 
/// == 사용 방법 ==
/// 1. 채취 가능한 오브젝트에 이 스크립트 부착
/// 2. 드롭할 자원과 개수 설정
/// 3. VR 컨트롤러로 오브젝트를 때리면 자원 획득
/// </summary>
public class ResourceGathering : MonoBehaviour
{
    [Header("자원 설정")]
    [Tooltip("채취 시 획득할 자원")]
    [SerializeField] private CraftingMaterial resourceToDrop;
    [Tooltip("한 번에 드롭할 자원 개수")]
    [SerializeField] private int dropQuantity = 1;
    [Tooltip("자원의 총 내구도 (채취 가능 횟수)")]
    [SerializeField] private int maxDurability = 5;
    
    [Header("시각적 효과")]
    [Tooltip("타격 시 생성할 파티클 효과")]
    [SerializeField] private ParticleSystem hitEffect;
    [Tooltip("자원 고갈 시 생성할 파티클 효과")]
    [SerializeField] private ParticleSystem depletedEffect;
    
    [Header("사운드")]
    [Tooltip("타격 사운드")]
    [SerializeField] private AudioClip hitSound;
    [Tooltip("자원 고갈 사운드")]
    [SerializeField] private AudioClip depletedSound;
    
    [Header("디버그")]
    [Tooltip("디버그 로그 출력 여부")]
    [SerializeField] private bool enableDebugLogs = true;
    
    // 현재 상태
    private int currentDurability;
    private AudioSource audioSource;
    private Renderer objectRenderer;
    private Material originalMaterial;
    private Material damagedMaterial;
    
    // 타격 감지용
    private float lastHitTime = 0f;
    private const float HIT_COOLDOWN = 0.5f; // 연속 타격 방지
    
    void Awake()
    {
        // 컴포넌트 초기화
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
            
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
            originalMaterial = objectRenderer.material;
            
        // 내구도 초기화
        currentDurability = maxDurability;
        
        // 데미지 머티리얼 생성 (원본을 어둡게)
        if (originalMaterial != null)
        {
            damagedMaterial = new Material(originalMaterial);
            damagedMaterial.color = originalMaterial.color * 0.7f; // 어둡게
        }
    }
    
    void Start()
    {
        // XR 인터랙션 이벤트 등록
        var interactable = GetComponent<XRBaseInteractable>();
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnHit);
        }
        else
        {
            // XRBaseInteractable이 없으면 자동 추가
            var grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
            grabInteractable.selectEntered.AddListener(OnHit);
            
            if (enableDebugLogs)
                Debug.Log($"[ResourceGathering] XRGrabInteractable이 {gameObject.name}에 자동 추가되었습니다.");
        }
        
        if (enableDebugLogs)
            Debug.Log($"[ResourceGathering] {gameObject.name} 자원 준비 완료. 내구도: {currentDurability}/{maxDurability}");
    }
    
    /// <summary>
    /// VR 컨트롤러로 오브젝트를 잡거나 때릴 때 호출
    /// </summary>
    /// <param name="args">인터랙션 이벤트 인자</param>
    private void OnHit(SelectEnterEventArgs args)
    {
        // 쿨다운 체크
        if (Time.time - lastHitTime < HIT_COOLDOWN)
            return;
            
        lastHitTime = Time.time;
        
        // 이미 고갈된 자원이면 무시
        if (currentDurability <= 0)
        {
            if (enableDebugLogs)
                Debug.Log($"[ResourceGathering] {gameObject.name}은 이미 고갈된 자원입니다.");
            return;
        }
        
        // 자원 채취 실행
        HarvestResource();
        
        if (enableDebugLogs)
            Debug.Log($"[ResourceGathering] {gameObject.name}을 채취했습니다. 남은 내구도: {currentDurability}");
    }
    
    /// <summary>
    /// 실제 자원 채취 로직
    /// </summary>
    private void HarvestResource()
    {
        // 자원을 인벤토리에 추가
        if (resourceToDrop != null)
        {
            var inventory = PlayerInventory.Instance;
            if (inventory != null)
            {
                bool added = inventory.AddItem(resourceToDrop, dropQuantity);
                if (added && enableDebugLogs)
                {
                    Debug.Log($"[ResourceGathering] {resourceToDrop.materialName} x{dropQuantity}을 인벤토리에 추가했습니다.");
                }
                else if (!added)
                {
                    Debug.LogWarning($"[ResourceGathering] 인벤토리가 가득 차서 {resourceToDrop.materialName}을 추가할 수 없습니다.");
                }
            }
            else
            {
                Debug.LogError("[ResourceGathering] PlayerInventory.Instance를 찾을 수 없습니다!");
            }
        }
        
        // 내구도 감소
        currentDurability--;
        
        // 시각적/청각적 효과
        PlayHitEffects();
        
        // 시각적 데미지 표현
        UpdateVisualDamage();
        
        // 자원 고갈 체크
        if (currentDurability <= 0)
        {
            OnResourceDepleted();
        }
    }
    
    /// <summary>
    /// 타격 효과 재생
    /// </summary>
    private void PlayHitEffects()
    {
        // 파티클 효과
        if (hitEffect != null)
        {
            hitEffect.Play();
        }
        
        // 사운드 효과
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }
    
    /// <summary>
    /// 내구도에 따른 시각적 변화
    /// </summary>
    private void UpdateVisualDamage()
    {
        if (objectRenderer == null || originalMaterial == null) return;
        
        float damageRatio = 1f - ((float)currentDurability / maxDurability);
        
        if (damageRatio > 0.5f && damagedMaterial != null)
        {
            // 50% 이상 손상되면 어두운 머티리얼 사용
            objectRenderer.material = damagedMaterial;
        }
    }
    
    /// <summary>
    /// 자원 고갈 시 처리
    /// </summary>
    private void OnResourceDepleted()
    {
        if (enableDebugLogs)
            Debug.Log($"[ResourceGathering] {gameObject.name}의 자원이 고갈되었습니다.");
        
        // 고갈 효과 재생
        if (depletedEffect != null)
        {
            depletedEffect.Play();
        }
        
        if (depletedSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(depletedSound);
        }
        
        // 오브젝트를 비활성화하거나 제거
        StartCoroutine(DestroyAfterEffect());
    }
    
    /// <summary>
    /// 효과 재생 후 오브젝트 제거
    /// </summary>
    private System.Collections.IEnumerator DestroyAfterEffect()
    {
        // 효과가 끝날 때까지 대기
        float effectDuration = depletedEffect != null ? depletedEffect.main.duration : 1f;
        yield return new WaitForSeconds(effectDuration);
        
        // 오브젝트 제거
        if (enableDebugLogs)
            Debug.Log($"[ResourceGathering] {gameObject.name}이 제거됩니다.");
            
        Destroy(gameObject);
    }
    
    /// <summary>
    /// 에디터에서 자원 정보 표시
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (resourceToDrop != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            // 내구도 표시
            Gizmos.color = Color.yellow;
            Vector3 pos = transform.position + Vector3.up * 2f;
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(pos, $"{resourceToDrop.materialName}\n내구도: {currentDurability}/{maxDurability}");
            #endif
        }
    }
    
    /// <summary>
    /// 자원 재생성 (테스트용)
    /// </summary>
    [ContextMenu("자원 재생성")]
    public void RegenerateResource()
    {
        currentDurability = maxDurability;
        
        if (objectRenderer != null && originalMaterial != null)
        {
            objectRenderer.material = originalMaterial;
        }
        
        gameObject.SetActive(true);
        
        if (enableDebugLogs)
            Debug.Log($"[ResourceGathering] {gameObject.name}의 자원이 재생성되었습니다.");
    }
    
    void OnDestroy()
    {
        // 메모리 정리
        if (damagedMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(damagedMaterial);
            else
                DestroyImmediate(damagedMaterial);
        }
    }
}