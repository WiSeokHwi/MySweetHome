using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("�׸��� �� �ϳ��� ũ�� (��: 1�̸� 1x1 ����).")]
    [SerializeField] private float cellSize = 1.0f;
    [Tooltip("�׸����� ���� �� ����.")]
    [SerializeField] private int gridWidth = 10;
    [Tooltip("�׸����� ���� �� ����.")]
    [SerializeField] private int gridHeight = 10;

    [Header("Placement Settings")]
    [Tooltip("��ġ ������ �������� ���� �� ǥ�õ� �׸��� ����� Material.")]
    [SerializeField] private Material previewGridMaterial;
    [Tooltip("��ġ �Ұ����� ������ ǥ�õ� �׸��� ����� Material.")]
    [SerializeField] private Material invalidGridMaterial;
    [Tooltip("��ġ ��尡 �ƴ� �� �׸��� ��鿡 ǥ�õ� �⺻ Material.")]
    [SerializeField] private Material defaultGridVisualMaterial;

    private MeshRenderer gridMeshRenderer;
    private MeshFilter gridMeshFilter;
    private Material runtimeDefaultGridMaterial; // ��ũ��Ʈ�� ������ �⺻ Material

    // �ܺο��� runtimeDefaultGridMaterial�� ������ �� �ֵ��� public ������Ƽ �߰�
    public Material RuntimeDefaultGridMaterial => runtimeDefaultGridMaterial;
    // PlacableItem�� ���� �� ��� �߰�
    public List<Vector3Int> OccupiedCells { get; } = new List<Vector3Int>();


    private Dictionary<Vector3Int, PlacableItem> occupiedCells = new Dictionary<Vector3Int, PlacableItem>();

    private PlacableItem currentPlacingItem;

    void Awake()
    {
        gridMeshRenderer = GetComponent<MeshRenderer>();
        gridMeshFilter = GetComponent<MeshFilter>();

        if (gridMeshRenderer == null)
        {
            gridMeshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
        if (gridMeshFilter == null)
        {
            gridMeshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (defaultGridVisualMaterial == null)
        {
            Debug.LogWarning("GridManager: 'Default Grid Visual Material'�� �Ҵ���� �ʾҽ��ϴ�. �׸��尡 ����� ǥ�õ��� ���� �� �ֽ��ϴ�.", this);
        }
        runtimeDefaultGridMaterial = defaultGridVisualMaterial;

        GenerateGridMesh();

        // �ʱ� �׸��� Material�� runtimeDefaultGridMaterial�� ����
        SetGridMaterial(runtimeDefaultGridMaterial);
    }

    private void GenerateGridMesh()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(0, 0, 0);
        vertices[1] = new Vector3(gridWidth * cellSize, 0, 0);
        vertices[2] = new Vector3(0, 0, gridHeight * cellSize);
        vertices[3] = new Vector3(gridWidth * cellSize, 0, gridHeight * cellSize);

        int[] triangles = new int[]
        {
            0, 2, 1,
            1, 2, 3
        };

        Vector2[] uv = new Vector2[4];
        uv[0] = new Vector2(0, 0);
        uv[1] = new Vector2(gridWidth, 0);
        uv[2] = new Vector2(0, gridHeight);
        uv[3] = new Vector2(gridWidth, gridHeight);

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();

        gridMeshFilter.mesh = mesh;

        if (previewGridMaterial != null)
        {
            previewGridMaterial.mainTextureScale = new Vector2(gridWidth, gridHeight);
        }
        if (invalidGridMaterial != null)
        {
            invalidGridMaterial.mainTextureScale = new Vector2(gridWidth, gridHeight);
        }
        if (defaultGridVisualMaterial != null)
        {
            defaultGridVisualMaterial.mainTextureScale = new Vector2(gridWidth, gridHeight);
        }
    }

    public Vector3 SnapToGridForPlacement(Vector3 hitPoint, Vector3Int itemSize, float itemWorldHeight)
    {
        Vector3 relativeHitPos = hitPoint - transform.position;

        float snappedRelativeX = Mathf.Floor(relativeHitPos.x / cellSize) * cellSize;
        float snappedRelativeZ = Mathf.Floor(relativeHitPos.z / cellSize) * cellSize;

        Vector3 itemCenterOffset = new Vector3(itemSize.x * 0.5f * cellSize, 0, itemSize.z * 0.5f * cellSize);

        float snappedY = transform.position.y;

        Vector3 finalWorldPosition = transform.position + new Vector3(snappedRelativeX, snappedY, snappedRelativeZ) + itemCenterOffset;

        // Debug.Log���� Ȱ��ȭ�Ͽ� �� ���� (����� �� ����)
        // Debug.Log($"[GridManager.SnapToGridForPlacement] Input hitPoint Y: {hitPoint.y}");
        // Debug.Log($"[GridManager.SnapToGridForPlacement] GridManager Y: {transform.position.y}");
        // Debug.Log($"[GridManager.SnapToGridForPlacement] Calculated snappedY: {snappedY}");
        // Debug.Log($"[GridManager.SnapToGridForPlacement] Final Calculated World Position Y (Item Pivot): {finalWorldPosition.y}");

        return finalWorldPosition;
    }

    public Vector3Int WorldToGridCoordinates(Vector3 worldPosition)
    {
        Vector3 relativePos = worldPosition - transform.position;

        int x = Mathf.FloorToInt(relativePos.x / cellSize);
        int y = Mathf.FloorToInt(relativePos.y / cellSize);
        int z = Mathf.FloorToInt(relativePos.z / cellSize);

        return new Vector3Int(x, y, z);
    }

    public Vector3 GridToWorldCoordinates(Vector3Int gridCoordinates)
    {
        float worldX = gridCoordinates.x * cellSize + transform.position.x;
        float worldY = gridCoordinates.y * cellSize + transform.position.y;
        float worldZ = gridCoordinates.z * cellSize + transform.position.z;

        worldX += cellSize * 0.5f;
        worldZ += cellSize * 0.5f;

        return new Vector3(worldX, worldY, worldZ);
    }

    public bool IsInGridBounds(Vector3Int gridPos)
    {
        return gridPos.x >= 0 && gridPos.x < gridWidth &&
               gridPos.z >= 0 && gridPos.z < gridHeight;
    }

    public bool CanPlaceItem(Vector3Int itemGridOrigin, Vector3Int itemSize)
    {
        for (int x = 0; x < itemSize.x; x++)
        {
            for (int z = 0; z < itemSize.z; z++)
            {
                Vector3Int currentCell = new Vector3Int(itemGridOrigin.x + x, itemGridOrigin.y, itemGridOrigin.z + z);
                if (!IsInGridBounds(currentCell) || occupiedCells.ContainsKey(currentCell))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void OccupyCells(PlacableItem item)
    {
        Vector3Int itemGridOrigin = WorldToGridCoordinates(item.transform.position);
        Vector3Int itemSize = item.itemGridSize;

        for (int x = 0; x < itemSize.x; x++)
        {
            for (int z = 0; z < itemSize.z; z++)
            {
                Vector3Int cellToOccupy = new Vector3Int(itemGridOrigin.x + x, itemGridOrigin.y, itemGridOrigin.z + z);
                if (IsInGridBounds(cellToOccupy))
                {
                    occupiedCells[cellToOccupy] = item;
                }
                else
                {
                    Debug.LogWarning($"[GridManager] Attempted to occupy cell {cellToOccupy} outside grid bounds for item {item.name}.");
                }
            }
        }
    }

    public void ReleaseCells(PlacableItem item)
    {
        List<Vector3Int> cellsToRemove = new List<Vector3Int>();
        foreach (var entry in occupiedCells)
        {
            if (entry.Value == item)
            {
                cellsToRemove.Add(entry.Key);
            }
        }

        foreach (Vector3Int cell in cellsToRemove)
        {
            occupiedCells.Remove(cell);
        }
    }

    public void SetCurrentPlacingItem(PlacableItem item)
    {
        currentPlacingItem = item;
        SetGridMaterial(previewGridMaterial);
    }

    public void ClearCurrentPlacingItem()
    {
        currentPlacingItem = null;
        SetGridMaterial(runtimeDefaultGridMaterial); // <--- runtimeDefaultGridMaterial ���
    }

    public void SetGridMaterial(Material material)
    {
        if (gridMeshRenderer != null && material != null)
        {
            gridMeshRenderer.material = material;
        }
        else if (gridMeshRenderer != null && material == null)
        {
            // Material�� null�� ��� Debug.LogWarning�� ���ų� Unity Default-Material�� ��ü
            Debug.LogWarning("GridManager: Material to set is null. Grid visibility might be affected.");
            gridMeshRenderer.material = new Material(Shader.Find("Standard")); // Unity �⺻ Material
        }
    }

    public void UpdateGridMaterialForPlacement(bool isValidPlacement)
    {
        if (currentPlacingItem != null)
        {
            SetGridMaterial(isValidPlacement ? previewGridMaterial : invalidGridMaterial);
        }
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Vector3 origin = transform.position;
            Vector3 size = new Vector3(gridWidth * cellSize, 0.1f, gridHeight * cellSize);
            Gizmos.DrawWireCube(origin + size / 2f, size);

            Gizmos.color = Color.red;
            foreach (var entry in occupiedCells)
            {
                Vector3 cellWorldPos = GridToWorldCoordinates(entry.Key);
                Gizmos.DrawWireCube(cellWorldPos, new Vector3(cellSize, 0.1f, cellSize));
            }
        }
    }
}