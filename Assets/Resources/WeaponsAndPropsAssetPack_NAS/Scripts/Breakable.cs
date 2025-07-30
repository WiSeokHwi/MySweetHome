using System.Collections;
using UnityEngine;

namespace WeaponsAndPropsAssetPack_NAS.Scripts
{
    public class Breakable : MonoBehaviour
    {
        [Header("Object References")]
        [SerializeField] private Transform wholeObject;
        [SerializeField] private Transform fracturedObject;
        [SerializeField] private bool isCyclic;

        [Header("Cycle Timings")]
        private const float timeToCleanUp = 5f;
        private const float timeToStartDestruction = 2f;
        private const float timeToReconstructObject = 2f;
        private const float cycleTime = 0.2f;

        // State
        private Transform fracturedObjectInstance;
        private bool isBroken = false;
        private bool isClean = false;
        private bool objectReseted = true;
        private bool breakInProgress = false;

        private void Start()
        {
            if (isCyclic)
            {
                StartCoroutine(CycleDestruction());
            }
            else
            {
                StartCoroutine(DestroyOnce());
            }
        }

        private IEnumerator DestroyOnce()
        {
            yield return new WaitForSeconds(timeToStartDestruction);
            if (!breakInProgress)
            {
                StartCoroutine(BreakAndCleanSequence());
            }
        }

        private IEnumerator BreakAndCleanSequence()
        {
            breakInProgress = true;

            // Break
            BreakObject();

            // Wait for clean-up
            yield return new WaitForSeconds(timeToCleanUp);
            CleanUp();

            // Optionally reset if cyclic
            if (isCyclic)
            {
                yield return new WaitForSeconds(timeToReconstructObject);
                ResetObject();
            }

            breakInProgress = false;
        }

        private void BreakObject()
        {
            if (isBroken) return;

            wholeObject.gameObject.SetActive(false);
            fracturedObjectInstance = Instantiate(fracturedObject, wholeObject.position, wholeObject.rotation);
            fracturedObjectInstance.gameObject.SetActive(true);
            isBroken = true;
        }

        private void CleanUp()
        {
            if (fracturedObjectInstance != null)
            {
                Destroy(fracturedObjectInstance.gameObject);
                fracturedObjectInstance = null;
            }

            isClean = true;
        }

        private void ResetObject()
        {
            if (!isClean) return;

            wholeObject.gameObject.SetActive(true);
            isBroken = false;
            isClean = false;
            objectReseted = true;
        }

        private IEnumerator CycleDestruction()
        {
            while (true)
            {
                if (objectReseted && !breakInProgress)
                {
                    objectReseted = false;
                    yield return BreakAndCleanSequence();
                }
                yield return new WaitForSeconds(cycleTime);
            }
        }
    }
}
