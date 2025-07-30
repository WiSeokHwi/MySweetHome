using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit; // XR Interaction Toolkit 사용을 위해 필요

/// <summary>
/// ResourceGathering - VR에서 자원을 수집하는 시스템
///
/// == 주요 기능 ==
/// 1. VR 컨트롤러 (또는 도구)로 나무/돌 등 자원 채취
/// 2. 타격 시 파티클 효과 및 사운드 재생
/// 3. 채취된 자원을 인벤토리에 자동 추가
/// 4. 자원 고갈 시스템 (내구도 기반)
///
/// == 사용 방법 ==
/// 1. 채취 가능한 오브젝트에 이 스크립트 부착
/// 2. 드롭할 자원과 개수 설정
/// 3. VR 컨트롤러 또는 'PlayerTool' 태그가 있는 오브젝트로 자원을 때리면 자원 획득
/// </summary>
[RequireComponent(typeof(Collider))] // 충돌 감지를 위해 Collider 필요
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
    [Tooltip("타격 시 생성할 파티클 효과 (이 오브젝트의 자식으로 두는 것이 좋음)")]
    [SerializeField] private ParticleSystem hitEffect;
    [Tooltip("자원 고갈 시 생성할 파티클 효과 (이 오브젝트의 자식으로 두는 것이 좋음)")]
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
    private Material damagedMaterial; // 손상 시 사용할 머티리얼

    // 타격 감지용 쿨다운
    private float lastHitTime = 0f;
    private const float HIT_COOLDOWN = 0.5f; // 연속 타격 방지 쿨다운 (초)

    void Awake()
    {
        // 컴포넌트 초기화 및 유효성 검사
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false; // 자동 재생 방지
        }

        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            originalMaterial = objectRenderer.material; // 원본 머티리얼 저장
            // 데미지 머티리얼 생성 (원본을 복사하여 색상 변경)
            damagedMaterial = new Material(originalMaterial);
            damagedMaterial.color = originalMaterial.color * 0.7f; // 어둡게
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[ResourceGathering] {gameObject.name}: Renderer를 찾을 수 없습니다. 시각적 손상 효과가 작동하지 않습니다.");
        }

        // 내구도 초기화
        currentDurability = maxDurability;
    }

    void Start()
    {
        if (resourceToDrop == null)
        {
            if (enableDebugLogs)
                Debug.LogError($"[ResourceGathering] {gameObject.name}: 'Resource To Drop'이 할당되지 않았습니다. 이 자원은 작동하지 않습니다.", this);
            enabled = false; // 스크립트 비활성화
            return;
        }

        // 초기 내구도 상태에 따라 시각적 업데이트 (선택 사항)
        UpdateVisualDamage();

        if (enableDebugLogs)
            Debug.Log($"[ResourceGathering] {gameObject.name} 자원 준비 완료. 내구도: {currentDurability}/{maxDurability}");
    }

    /// <summary>
    /// 다른 콜라이더와 충돌했을 때 호출됩니다.
    /// 이 메서드를 사용하여 VR 컨트롤러 또는 도구의 물리적 타격을 감지합니다.
    /// </summary>
    /// <param name="collision">충돌 정보</param>
    void OnCollisionEnter(Collision collision)
    {
        // "PlayerTool" 태그를 가진 오브젝트와 충돌했는지 확인
        // (예: VR 컨트롤러에 붙은 도구 또는 컨트롤러 자체)
        if (collision.gameObject.CompareTag("PlayerTool"))
        {
            // 쿨다운 체크
            if (Time.time - lastHitTime < HIT_COOLDOWN)
            {
                if (enableDebugLogs)
                    Debug.Log($"[ResourceGathering] {gameObject.name}: 쿨다운 중입니다.");
                return;
            }

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
    }

    /// <summary>
    /// 실제 자원 채취 로직
    /// </summary>
    private void HarvestResource()
    {
        // 내구도 감소
        currentDurability--;

        // 시각적/청각적 효과 재생
        PlayHitEffects();

        // 시각적 데미지 표현 업데이트
        UpdateVisualDamage();

        // 자원 고갈 체크
        if (currentDurability <= 0)
        {
            OnResourceDepleted();
        }
        else // 고갈되지 않았다면 인벤토리에 아이템 추가
        {
            AddItemToInventory();
        }
    }

    /// <summary>
    /// 자원을 인벤토리에 추가합니다.
    /// </summary>
    private void AddItemToInventory()
    {
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
                else if (!added && enableDebugLogs)
                {
                    Debug.LogWarning($"[ResourceGathering] 인벤토리가 가득 차서 {resourceToDrop.materialName}을 추가할 수 없습니다.");
                }
            }
            else
            {
                if (enableDebugLogs)
                    Debug.LogError("[ResourceGathering] PlayerInventory.Instance를 찾을 수 없습니다! 아이템을 인벤토리에 추가할 수 없습니다.");
            }
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
            // hitEffect가 월드 공간에 있다면 자원 오브젝트의 위치에서 재생
            hitEffect.transform.position = transform.position;
            hitEffect.Play();
        }

        // 사운드 효과
        if (hitSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(hitSound);
        }
    }

    /// <summary>
    /// 내구도에 따른 시각적 변화를 업데이트합니다.
    /// (예: 머티리얼 색상 변경)
    /// </summary>
    private void UpdateVisualDamage()
    {
        if (objectRenderer == null || originalMaterial == null || damagedMaterial == null) return;

        // 내구도 비율에 따라 머티리얼 변경
        // 50% 이하로 남았을 때 데미지 머티리얼 적용 (예시)
        if ((float)currentDurability / maxDurability <= 0.5f)
        {
            objectRenderer.material = damagedMaterial;
        }
        else
        {
            objectRenderer.material = originalMaterial;
        }
    }

    /// <summary>
    /// 자원 고갈 시 처리 (아이템 드롭 및 오브젝트 파괴)
    /// </summary>
    private void OnResourceDepleted()
    {
        if (enableDebugLogs)
            Debug.Log($"[ResourceGathering] {gameObject.name}의 자원이 고갈되었습니다.");

        // 고갈 효과 재생
        if (depletedEffect != null)
        {
            depletedEffect.transform.position = transform.position;
            depletedEffect.Play();
        }

        if (depletedSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(depletedSound);
        }

        // 오브젝트를 비활성화하거나 제거 (효과 재생 후)
        StartCoroutine(DestroyAfterEffect());
    }

    /// <summary>
    /// 효과 재생 후 오브젝트 제거
    /// </summary>
    private System.Collections.IEnumerator DestroyAfterEffect()
    {
        // 모든 렌더러와 콜라이더를 비활성화하여 즉시 상호작용 및 렌더링 중지
        if (objectRenderer != null) objectRenderer.enabled = false;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // 효과가 끝날 때까지 대기
        float effectDuration = 0f;
        if (depletedEffect != null)
        {
            effectDuration = depletedEffect.main.duration;
            // 파티클 시스템이 재생 중인 동안 기다립니다.
            while (depletedEffect.isPlaying)
            {
                yield return null;
            }
        }
        else
        {
            // 파티클 효과가 없으면 기본 대기 시간
            yield return new WaitForSeconds(1f);
        }

        // 오브젝트 제거
        if (enableDebugLogs)
            Debug.Log($"[ResourceGathering] {gameObject.name}이 제거됩니다.");

        Destroy(gameObject);
    }

    /// <summary>
    /// 에디터에서 자원 정보 표시 (Gizmos)
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (resourceToDrop != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.5f);

            // 내구도 표시 (에디터 전용)
            Gizmos.color = Color.yellow;
            Vector3 pos = transform.position + Vector3.up * 0.75f; // 오브젝트 위쪽에 표시

#if UNITY_EDITOR
            // UnityEditor.Handles.Label을 사용하기 위해 UnityEditor 네임스페이스 필요
            // 이 코드는 에디터에서만 컴파일됩니다.
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

        // 원본 머티리얼로 복원
        if (objectRenderer != null && originalMaterial != null)
        {
            objectRenderer.material = originalMaterial;
        }

        // 오브젝트 활성화 및 콜라이더/렌더러 재활성화
        gameObject.SetActive(true);
        if (objectRenderer != null) objectRenderer.enabled = true;
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        if (enableDebugLogs)
            Debug.Log($"[ResourceGathering] {gameObject.name}의 자원이 재생성되었습니다.");
    }

    void OnDestroy()
    {
        // 동적으로 생성된 머티리얼 메모리 정리
        if (damagedMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(damagedMaterial); // 런타임에 생성된 머티리얼은 Destroy
            else
                DestroyImmediate(damagedMaterial); // 에디터 모드에서는 DestroyImmediate
        }
    }
}
