using UnityEngine;


public enum LandType
{
    Grass,
    Dirt,
    Water,
}


public class Land : MonoBehaviour
{
    
    public LandType landType;

    public Material grass, dirt, water;

    private Renderer landRenderer;

    public GameObject selectObject;
    void Start()
    {
        landRenderer = GetComponent<Renderer>();

        // 기본값을 Grass로 설정
        SwitchLandStatus(LandType.Grass);
    }

    public void SwitchLandStatus(LandType newLandType)
    {
        landType = newLandType;
        Material newMaterial = grass;

        switch (landType)
        {
            case LandType.Grass:
                newMaterial = grass;
                break;
            case LandType.Dirt:
                newMaterial = dirt;
                break;
            case LandType.Water:
                newMaterial = water;
                break;
        }

        
        if (GetComponent<Renderer>() != null)
        {
            GetComponent<Renderer>().material = newMaterial;
        }
        else
        {
            Debug.LogWarning("Renderer is not assigned on " + gameObject.name);
        }
    }

    public void GizmosSelected(bool select)
    {
        selectObject.SetActive(select);
    }
}
