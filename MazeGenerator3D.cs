using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

// 关键改动 1: 添加此属性，使脚本的方法可以在编辑模式下运行
[ExecuteInEditMode]
public class MazeGenerator3D : MonoBehaviour
{
    // 迷宫尺寸
    public int xSize = 10;
    public int ySize = 1;
    public int zSize = 10;

    // 预制件和材质 (迷宫结构必需)
    public GameObject WallPrefab;
    public Material WallMaterial;
    public Material FloorMaterial;

    // 起始和终点预制件 (必需)
    public GameObject StartPrefab;
    public GameObject EndGatePrefab;

    // *** 尺寸控制参数 (包含墙高和地板厚度) ***
    public float WallScale = 1.0f;          // 迷宫单元格的宽度/深度
    public float WallHeight = 3.0f;         // <--- 墙壁的实际高度
    public float FloorThickness = 0.2f;     // <--- 地板的厚度

    // *** 物品高度修正参数 (保留，用于定位起点/终点) ***
    [Tooltip("起始物品的近似高度。")]
    public float ItemHeight = 1.0f;
    [Tooltip("对起始物体的Y轴进行微调。")]
    public float ItemYOffset = 0.0f;

    // 移除不再使用的公共字段
    [HideInInspector] public GameObject TreasureChestPrefab;
    [HideInInspector] public GameObject MonsterPrefab;
    [HideInInspector] public float MonsterHeight = 1.0f;
    [HideInInspector] public float SafetyCheckRadius = 0.2f;

    // 内部结构：用于描述迷宫中的每一个单元格
    private class Cell
    {
        public int x, y, z;
        public bool visited = false;
        public GameObject[] walls = new GameObject[6];
    }

    private Cell[,,] mazeCells;
    private Stack<Cell> stack = new Stack<Cell>();

    [ContextMenu("Generate Maze Now")]
    public void GenerateMaze()
    {
        Debug.Log("--- Maze Generation Started ---");

        DestroyExistingMaze();

        // 仅检查迷宫结构和起点终点所需的预制件
        if (WallPrefab == null || StartPrefab == null || EndGatePrefab == null)
        {
            Debug.LogError("One or more required prefabs (Wall, Start, EndGate) are not assigned.");
            return;
        }
        if (WallMaterial == null || FloorMaterial == null)
        {
            Debug.LogError("WallMaterial or FloorMaterial is not assigned in the Inspector.");
            return;
        }

        Debug.Log($"Starting generation with size: X={xSize}, Y={ySize}, Z={zSize}. Wall Scale={WallScale}.");

        mazeCells = new Cell[xSize, ySize, zSize];
        InitializeCells(); // 创建墙壁和地板，并分配标签

        Cell current = mazeCells[0, 0, 0];
        current.visited = true;
        stack.Push(current);

        // 深度优先搜索 (DFS) 生成迷宫路径
        while (stack.Count > 0)
        {
            Cell next = GetUnvisitedNeighbour(current);

            if (next != null)
            {
                RemoveWall(current, next);
                next.visited = true;
                stack.Push(next);
                current = next;
            }
            else
            {
                current = stack.Pop();
            }
        }

        PlaceStartAndEnd(); // 保留起点和终点门的生成

        Debug.Log("3D Maze Generation Complete in Editor Mode.");
    }

    [ContextMenu("Clear Existing Maze")]
    public void ClearMaze()
    {
        DestroyExistingMaze();
    }

    private void DestroyExistingMaze()
    {
        GameObject oldMaze = GameObject.Find("Maze3D");
        if (oldMaze != null)
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                DestroyImmediate(oldMaze);
            }
            else
            {
                Destroy(oldMaze);
            }
            Debug.Log("Existing Maze3D destroyed.");
        }
    }


    // 初始化所有单元格及其墙壁 (Tagging logic included)
    private void InitializeCells()
    {
        GameObject mazeHolder = new GameObject("Maze3D");

        // 定义要使用的标签名称
        const string WallTag = "Wall";
        const string FloorTag = "Ground";

        if (mazeHolder == null)
        {
            Debug.LogError("Failed to create Maze3D parent object!");
            return;
        }

        float wallYCenter = WallHeight / 2f;
        float verticalWallThickness = WallScale / 10f;
        Vector3 verticalWallScaleX = new Vector3(verticalWallThickness, WallHeight, WallScale + verticalWallThickness);
        Vector3 verticalWallScaleZ = new Vector3(WallScale + verticalWallThickness, WallHeight, verticalWallThickness);
        float horizontalThickness = FloorThickness;


        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                for (int z = 0; z < zSize; z++)
                {
                    Cell newCell = new Cell { x = x, y = y, z = z };
                    mazeCells[x, y, z] = newCell;

                    Vector3 center = GetCellPosition(x, y, z);
                    Material currentMaterial = WallMaterial;

                    Vector3 verticalWallBase = center + new Vector3(0, wallYCenter, 0);


                    // 1. 生成右墙 (x + 1) - 内部墙 (WallTag)
                    if (x < xSize - 1)
                        newCell.walls[0] = CreateWall(verticalWallBase + new Vector3(WallScale / 2f, 0, 0), verticalWallScaleX, mazeHolder.transform, currentMaterial, WallTag);

                    // 2. 生成前墙 (z + 1) - 内部墙 (WallTag)
                    if (z < zSize - 1)
                        newCell.walls[4] = CreateWall(verticalWallBase + new Vector3(0, 0, WallScale / 2f), verticalWallScaleZ, mazeHolder.transform, currentMaterial, WallTag);

                    // 3. 生成上墙/天花板 (y + 1) - 内部墙 (WallTag)
                    if (y < ySize - 1)
                    {
                        Vector3 ceilingPos = center + new Vector3(0, WallScale - horizontalThickness / 2f, 0);
                        newCell.walls[2] = CreateWall(ceilingPos, new Vector3(WallScale, horizontalThickness, WallScale), mazeHolder.transform, currentMaterial, WallTag);
                    }

                    // --- 边界墙生成 (WallTag) ---

                    if (x == 0)
                        newCell.walls[1] = CreateWall(verticalWallBase + new Vector3(-WallScale / 2f, 0, 0), verticalWallScaleX, mazeHolder.transform, currentMaterial, WallTag);

                    if (z == 0)
                        newCell.walls[5] = CreateWall(verticalWallBase + new Vector3(0, 0, -WallScale / 2f), verticalWallScaleZ, mazeHolder.transform, currentMaterial, WallTag);

                    if (x == xSize - 1)
                        newCell.walls[0] = CreateWall(verticalWallBase + new Vector3(WallScale / 2f, 0, 0), verticalWallScaleX, mazeHolder.transform, currentMaterial, WallTag);

                    if (z == zSize - 1)
                        newCell.walls[4] = CreateWall(verticalWallBase + new Vector3(0, 0, WallScale / 2f), verticalWallScaleZ, mazeHolder.transform, currentMaterial, WallTag);

                    // 5. 底部边界墙/地板 (y = 0) - (GroundTag)
                    if (y == 0)
                    {
                        Vector3 floorPos = center + new Vector3(0, -FloorThickness / 2f, 0);
                        Vector3 floorScale = new Vector3(WallScale, FloorThickness, WallScale);
                        newCell.walls[3] = CreateWall(floorPos, floorScale, mazeHolder.transform, FloorMaterial, FloorTag); // 传入 FloorTag
                    }
                }
            }
        }
    }


    // *** 移除所有怪物和宝箱生成逻辑 ***
    private void PlaceItems()
    {
        // 留空，不生成怪物和宝箱
    }

    // 放置起点和终点（光门） (保留生成逻辑)
    private void PlaceStartAndEnd()
    {
        Cell startCell = mazeCells[0, 0, 0];
        Cell endCell = mazeCells[xSize - 1, 0, zSize - 1];

        Vector3 startPos = GetCellPosition(startCell.x, startCell.y, startCell.z);

        // 起点门生成 (使用 ItemHeight/ItemYOffset 定位)
        Vector3 spawnPosAboveGround = new Vector3(startPos.x, (ItemHeight / 2f) + ItemYOffset, startPos.z);
        GameObject startInstance = Instantiate(StartPrefab, spawnPosAboveGround, Quaternion.identity);

        // 终点门生成
        Vector3 endPos = GetCellPosition(endCell.x, endCell.y, endCell.z);
        GameObject endInstance = Instantiate(EndGatePrefab, endPos, Quaternion.identity);

        if (!Application.isPlaying)
        {
            Transform mazeParent = GameObject.Find("Maze3D").transform;
            if (mazeParent != null)
            {
                startInstance.transform.SetParent(mazeParent);
                endInstance.transform.SetParent(mazeParent);
            }
        }

        // *** 出口边界墙处理 ***
        if (endCell.walls[4] != null)
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                DestroyImmediate(endCell.walls[4]);
            }
            else
            {
                Destroy(endCell.walls[4]);
            }
        }
    }

    // *** MonsterAI 需要的公共接口 (保持为 PUBLIC) ***

    public Vector3 GetCellCenter(int x, int z)
    {
        return GetCellPosition(x, 0, z);
    }

    public bool IsWallBetween(int x1, int z1, int x2, int z2)
    {
        if (x1 < 0 || x1 >= xSize || z1 < 0 || z1 >= zSize ||
            x2 < 0 || x2 >= xSize || z2 < 0 || z2 >= zSize)
        {
            return true;
        }

        int dx = Mathf.Abs(x1 - x2);
        int dz = Mathf.Abs(z1 - z2);
        if (dx + dz != 1)
        {
            return true;
        }

        Cell cell1 = mazeCells[x1, 0, z1];
        Cell cell2 = mazeCells[x2, 0, z2];

        if (dx == 1)
        {
            Cell smallerXCell = (x1 < x2) ? cell1 : cell2;
            return smallerXCell.walls[0] != null;
        }

        if (dz == 1)
        {
            Cell smallerZCell = (z1 < z2) ? cell1 : cell2;
            return smallerZCell.walls[4] != null;
        }

        return true;
    }


    // *** 辅助方法 ***

    private Vector3 GetCellPosition(int x, int y, int z)
    {
        return new Vector3(x * WallScale, y * WallScale, z * WallScale);
    }

    private GameObject CreateWall(Vector3 position, Vector3 scale, Transform parent, Material material, string tag)
    {
        GameObject wall = Instantiate(WallPrefab, position, Quaternion.identity, parent);
        wall.transform.localScale = scale;

        if (!string.IsNullOrEmpty(tag))
        {
            wall.gameObject.tag = tag;
        }

        // 确保对象是静态的以便于物理和NavMesh工作
        if (Application.isEditor && !Application.isPlaying)
        {
            wall.isStatic = true;
        }

        Renderer renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = material;
        }

        return wall;
    }

    private void RemoveWall(Cell current, Cell neighbour)
    {
        int dx = current.x - neighbour.x;
        int dy = current.y - neighbour.y;
        int dz = current.z - neighbour.z;

        bool useImmediate = Application.isEditor && !Application.isPlaying;

        if (dx == 1)
        {
            if (neighbour.walls[0] != null)
            {
                if (useImmediate) DestroyImmediate(neighbour.walls[0]);
                else Destroy(neighbour.walls[0]);
                neighbour.walls[0] = null;
            }
        }
        else if (dx == -1)
        {
            if (current.walls[0] != null)
            {
                if (useImmediate) DestroyImmediate(current.walls[0]);
                else Destroy(current.walls[0]);
                current.walls[0] = null;
            }
        }

        if (dy == 1)
        {
            if (neighbour.walls[2] != null)
            {
                if (useImmediate) DestroyImmediate(neighbour.walls[2]);
                else Destroy(neighbour.walls[2]);
                neighbour.walls[2] = null;
            }
        }
        else if (dy == -1)
        {
            if (current.walls[2] != null)
            {
                if (useImmediate) DestroyImmediate(current.walls[2]);
                else Destroy(current.walls[2]);
                current.walls[2] = null;
            }
        }

        if (dz == 1)
        {
            if (neighbour.walls[4] != null)
            {
                if (useImmediate) DestroyImmediate(neighbour.walls[4]);
                else Destroy(neighbour.walls[4]);
                neighbour.walls[4] = null;
            }
        }
        else if (dz == -1)
        {
            if (current.walls[4] != null)
            {
                if (useImmediate) DestroyImmediate(current.walls[4]);
                else Destroy(current.walls[4]);
                current.walls[4] = null;
            }
        }
    }

    private Cell GetUnvisitedNeighbour(Cell c)
    {
        List<Cell> neighbours = new List<Cell>();
        int x = c.x, y = c.y, z = c.z;

        CheckNeighbour(x + 1, y, z, neighbours);
        CheckNeighbour(x - 1, y, z, neighbours);
        CheckNeighbour(x, y + 1, z, neighbours);
        CheckNeighbour(x, y - 1, z, neighbours);
        CheckNeighbour(x, y, z + 1, neighbours);
        CheckNeighbour(x, y, z - 1, neighbours);

        if (neighbours.Count > 0)
        {
            return neighbours[Random.Range(0, neighbours.Count)];
        }
        return null;
    }

    private void CheckNeighbour(int x, int y, int z, List<Cell> neighbours)
    {
        if (x >= 0 && x < xSize && y >= 0 && y < ySize && z >= 0 && z < zSize)
        {
            if (!mazeCells[x, y, z].visited)
            {
                neighbours.Add(mazeCells[x, y, z]);
            }
        }
    }
}