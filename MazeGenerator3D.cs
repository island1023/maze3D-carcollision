using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;

public class MazeGenerator3D : MonoBehaviour
{
    // 迷宫尺寸
    public int xSize = 10;
    public int ySize = 1;
    public int zSize = 10;

    // 预制件和材质
    public GameObject WallPrefab;
    public Material WallMaterial;
    public Material FloorMaterial;

    // 起始和终点预制件
    public GameObject StartPrefab;
    public GameObject EndGatePrefab;

    // 怪物和宝箱预制件
    public GameObject TreasureChestPrefab;
    public GameObject MonsterPrefab;

    // *** 尺寸控制参数 (包含墙高和地板厚度) ***
    public float WallScale = 1.0f;          // 迷宫单元格的宽度/深度
    public float WallHeight = 3.0f;         // <--- 墙壁的实际高度
    public float FloorThickness = 0.2f;     // <--- 地板的厚度

    // *** 物品高度修正参数 (可自定义) ***
    [Tooltip("起始物品和宝箱的近似高度。确保此值是预制件的实际总高度。")]
    public float ItemHeight = 1.0f;
    [Tooltip("怪物的近似高度。确保此值是预制件的实际总高度。")]
    public float MonsterHeight = 1.0f;

    // *** 宝箱/起始物品额外的垂直偏移量 (用于不规则预制体微调) ***
    [Tooltip("对宝箱和起始物体的Y轴进行微调。用于补偿预制件锚点不居中的情况。")]
    public float ItemYOffset = 0.0f;

    // 内部结构：用于描述迷宫中的每一个单元格
    private class Cell
    {
        public int x, y, z;
        public bool visited = false;

        // 6 个墙壁 (0:Right, 1:Left, 2:Up, 3:Down, 4:Forward, 5:Backward)
        public GameObject[] walls = new GameObject[6];
    }

    private Cell[,,] mazeCells;
    private Stack<Cell> stack = new Stack<Cell>();


    // --- AI 寻路数据结构 ---
    private Maze mazeData;
    public Maze MazeData => mazeData;
    private NativeArray<MazeFlags> mazeFlagsNativeArray;
    private bool isMazeDataCreated = false;


    // 将 3D 坐标 (x, y, z) 转换为 2D 气味系统的索引 (x, z)
    private int CoordinatesTo2DIndex(int x, int z)
    {
        // 假设 ySize=1，所以迷宫长度是 xSize * zSize
        return z * xSize + x;
    }

    void Start()
    {
        // 确保预制件和材质已设置
        if (WallPrefab == null || StartPrefab == null || EndGatePrefab == null || TreasureChestPrefab == null || MonsterPrefab == null)
        {
            Debug.LogError("One or more prefabs (Wall, Start, EndGate, TreasureChest, Monster) are not assigned.");
            return;
        }
        if (WallMaterial == null || FloorMaterial == null)
        {
            Debug.LogError("WallMaterial or FloorMaterial is not assigned in the Inspector.");
            return;
        }

        mazeCells = new Cell[xSize, ySize, zSize];

        // 初始化 MazeFlags NativeArray (2D 大小: xSize * zSize)
        int mazeLength2D = xSize * zSize;
        mazeFlagsNativeArray = new NativeArray<MazeFlags>(mazeLength2D, Allocator.Persistent);

        InitializeCells();
        StartCoroutine(GenerateMazeDFS());
    }

    // 新增：清理 NativeArray (解决二次释放问题)
    void OnDestroy()
    {
        if (mazeFlagsNativeArray.IsCreated)
        {
            mazeFlagsNativeArray.Dispose();
        }
    }


    // 初始化所有单元格及其墙壁 (保持不变)
    private void InitializeCells()
    {
        GameObject mazeHolder = new GameObject("Maze3D");

        // 计算墙壁的中心Y坐标 (墙壁的底部对齐地板表面 y=0)
        float wallYCenter = WallHeight / 2f;

        // 垂直墙壁的厚度
        float verticalWallThickness = WallScale / 10f;

        // 垂直墙壁的 Scale
        Vector3 verticalWallScaleX = new Vector3(verticalWallThickness, WallHeight, WallScale + verticalWallThickness);
        Vector3 verticalWallScaleZ = new Vector3(WallScale + verticalWallThickness, WallHeight, verticalWallThickness);

        // 地板/天花板的厚度
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

                    // 垂直墙体基准位置：(0, WallHeight/2, 0)
                    Vector3 verticalWallBase = center + new Vector3(0, wallYCenter, 0);


                    // (0:Right, 1:Left, 2:Up, 3:Down, 4:Forward, 5:Backward)

                    // 1. 生成右墙 (x + 1) - 内部墙
                    if (x < xSize - 1)
                        newCell.walls[0] = CreateWall(verticalWallBase + new Vector3(WallScale / 2f, 0, 0), verticalWallScaleX, mazeHolder.transform, currentMaterial);

                    // 2. 生成前墙 (z + 1) - 内部墙
                    if (z < zSize - 1)
                        newCell.walls[4] = CreateWall(verticalWallBase + new Vector3(0, 0, WallScale / 2f), verticalWallScaleZ, mazeHolder.transform, currentMaterial);

                    // 3. 生成上墙/天花板 (y + 1) - 内部墙 (ySize=1 时跳过)
                    if (y < ySize - 1)
                    {
                        // 位于当前单元格的上边界
                        Vector3 ceilingPos = center + new Vector3(0, WallScale - horizontalThickness / 2f, 0);
                        newCell.walls[2] = CreateWall(ceilingPos, new Vector3(WallScale, horizontalThickness, WallScale), mazeHolder.transform, currentMaterial);
                    }

                    // --- 边界墙生成 ---

                    // 4a. 左边界墙 (x = 0)
                    if (x == 0)
                        newCell.walls[1] = CreateWall(verticalWallBase + new Vector3(-WallScale / 2f, 0, 0), verticalWallScaleX, mazeHolder.transform, currentMaterial);

                    // 4b. 后边界墙 (z = 0)
                    if (z == 0)
                        newCell.walls[5] = CreateWall(verticalWallBase + new Vector3(0, 0, -WallScale / 2f), verticalWallScaleZ, mazeHolder.transform, currentMaterial);

                    // 4c. 右边界墙 (x = xSize - 1)
                    if (x == xSize - 1)
                        newCell.walls[0] = CreateWall(verticalWallBase + new Vector3(WallScale / 2f, 0, 0), verticalWallScaleX, mazeHolder.transform, currentMaterial);

                    // 4d. 前边界墙 (z = zSize - 1)
                    if (z == zSize - 1)
                        newCell.walls[4] = CreateWall(verticalWallBase + new Vector3(0, 0, WallScale / 2f), verticalWallScaleZ, mazeHolder.transform, currentMaterial);

                    // 5. 底部边界墙/地板 (y = 0) - 使地板顶部对齐 y=0 坐标
                    if (y == 0)
                    {
                        Vector3 floorPos = center + new Vector3(0, -FloorThickness / 2f, 0);
                        Vector3 floorScale = new Vector3(WallScale, FloorThickness, WallScale);
                        newCell.walls[3] = CreateWall(floorPos, floorScale, mazeHolder.transform, FloorMaterial);
                    }
                }
            }
        }
    }

    // 深度优先搜索迷宫生成算法 (更新了 MazeData 初始化)
    IEnumerator GenerateMazeDFS()
    {
        Cell current = mazeCells[0, 0, 0];
        current.visited = true;
        stack.Push(current);

        while (stack.Count > 0)
        {
            Cell next = GetUnvisitedNeighbour(current);

            if (next != null)
            {
                RemoveWall(current, next);
                next.visited = true;
                stack.Push(next);
                current = next;
                yield return null;
            }
            else
            {
                current = stack.Pop();
            }
        }

        // 迷宫生成完毕后，放置起点、终点和物品
        PlaceStartAndEnd();
        PlaceItems();

        // *** 创建最终的 MazeData 结构体 ***
        mazeData = new Maze(
            new int2(xSize, zSize),
            WallScale,
            mazeFlagsNativeArray
        );
        isMazeDataCreated = true;
        // ***************************************

        Debug.Log("3D Maze Generation Complete. Items placed. Maze Data for AI created.");
    }


    // 放置物品和怪物
    private void PlaceItems()
    {
        Vector2 startCoord = new Vector2(0, 0);
        Vector2 endCoord = new Vector2(xSize - 1, zSize - 1);

        // 物品和怪物中心点的 Y 坐标
        float chestSpawnY = (ItemHeight / 2f) + ItemYOffset;
        float monsterSpawnY = MonsterHeight / 2f;

        // *** 最小间隔要求 (基于单元格距离) ***
        // 宝箱间隔： MinCellSeparation = 4
        const int MinCellSeparation = 4;

        // 怪物间隔：
        const int MonsterMinSep = 5; // <--- 怪物之间的间隔缩小为 5 单元格
        const int MonsterToChestMinSep = 3;

        // 候选的生成位置列表 (所有非起点/终点的单元格)
        List<Cell> spawnCandidates = new List<Cell>();
        for (int x = 0; x < xSize; x++)
        {
            for (int z = 0; z < zSize; z++)
            {
                if (!((x == startCoord.x && z == startCoord.y) || (x == endCoord.x && z == endCoord.y)))
                {
                    spawnCandidates.Add(mazeCells[x, 0, z]);
                }
            }
        }

        // --- 宝箱放置逻辑 ---

        // 1. 确定要放置的宝箱数量 (至少 10 个)
        int densityFactor = MinCellSeparation * 2;
        int maxDensityChests = (xSize * zSize) / densityFactor;

        int targetChests = Mathf.Max(10, maxDensityChests);
        int maxChests = Mathf.Min(targetChests, spawnCandidates.Count);

        List<Cell> chestCells = new List<Cell>();

        // 2. 使用基于距离的采样，确保均匀分布
        List<Cell> availableCandidates = new List<Cell>(spawnCandidates.OrderBy(c => UnityEngine.Random.value));

        int attempts = 0; // <-- 变量定义 1
        int maxAttemptsPerPlacement = 500; // <-- 变量定义 1

        while (chestCells.Count < maxChests && availableCandidates.Count > 0 && attempts < maxAttemptsPerPlacement)
        {
            // 随机选择一个候选单元格
            Cell candidate = availableCandidates[UnityEngine.Random.Range(0, availableCandidates.Count)];
            availableCandidates.Remove(candidate);

            if (IsFarEnough(candidate, chestCells, MinCellSeparation))
            {
                chestCells.Add(candidate);
                attempts = 0;
            }
            else
            {
                attempts++;
            }
        }

        Debug.Log($"目标宝箱数量: {targetChests}，实际放置数量: {chestCells.Count} (Min Separation: {MinCellSeparation})");

        // 3. 放置所有宝箱对象 (安全偏移逻辑)
        foreach (Cell cell in chestCells)
        {
            Vector3 cellPos = GetCellPosition(cell.x, cell.y, cell.z);

            // 随机化宝箱在单元格内的位置 (不超过 WallScale 的 40% 偏移，防止过于靠近墙壁)
            float offsetX = UnityEngine.Random.Range(-WallScale * 0.4f, WallScale * 0.4f);
            float offsetZ = UnityEngine.Random.Range(-WallScale * 0.4f, WallScale * 0.4f);

            Vector3 chestPos = cellPos + new Vector3(offsetX, chestSpawnY, offsetZ);
            Instantiate(TreasureChestPrefab, chestPos, Quaternion.identity);
        }


        // --- 4. 独立放置怪物 ---

        List<Cell> monsterCells = new List<Cell>();

        // 确定需要放置怪物的数量（宝箱总数的 2/3，向下取整）
        int monstersToPlace = Mathf.FloorToInt(chestCells.Count * 2f / 3f); // <--- 使用向下取整

        // 怪物的候选位置：所有未被宝箱占用的单元格
        HashSet<Cell> occupiedByChests = new HashSet<Cell>(chestCells);
        List<Cell> monsterCandidates = spawnCandidates.Except(occupiedByChests).OrderBy(c => UnityEngine.Random.value).ToList();

        // 修正 CS0128 错误：移除 'int' 关键字，重新使用已声明的变量
        attempts = 0;
        maxAttemptsPerPlacement = 1000; // 增加尝试次数，以满足严格的间隔要求

        while (monsterCells.Count < monstersToPlace && monsterCandidates.Count > 0 && attempts < maxAttemptsPerPlacement)
        {
            // 随机选择一个候选单元格
            Cell candidate = monsterCandidates[UnityEngine.Random.Range(0, monsterCandidates.Count)];
            monsterCandidates.Remove(candidate);

            // 使用 IsMonsterPlacementValid 检查双重距离要求
            if (IsMonsterPlacementValid(candidate, monsterCells, chestCells, MonsterMinSep, MonsterToChestMinSep))
            {
                monsterCells.Add(candidate);
                attempts = 0;
            }
            else
            {
                attempts++;
            }
        }

        Debug.Log($"目标怪物数量: {monstersToPlace}，实际放置怪物数量: {monsterCells.Count} (Min Separation: {MonsterMinSep})");

        // 5. 放置怪物对象
        foreach (Cell cell in monsterCells)
        {
            Vector3 cellPos = GetCellPosition(cell.x, cell.y, cell.z);
            // 使用 UnityEngine.Random
            float offsetX = UnityEngine.Random.Range(-WallScale * 0.4f, WallScale * 0.4f);
            float offsetZ = UnityEngine.Random.Range(-WallScale * 0.4f, WallScale * 0.4f);

            Vector3 monsterPos = cellPos + new Vector3(offsetX, monsterSpawnY, offsetZ);
            Instantiate(MonsterPrefab, monsterPos, Quaternion.identity);
        }
    }

    // 检查候选单元格是否与所有已放置的宝箱保持最小距离 (用于宝箱放置)
    private bool IsFarEnough(Cell candidate, List<Cell> existingChests, int minSeparation)
    {
        foreach (Cell existingCell in existingChests)
        {
            // 使用曼哈顿距离 (|dx| + |dz|) 来衡量单元格之间的距离
            int distance = Mathf.Abs(candidate.x - existingCell.x) + Mathf.Abs(candidate.z - existingCell.z);
            if (distance < minSeparation)
            {
                return false;
            }
        }
        return true;
    }

    // 检查怪物放置是否满足所有独立间隔要求
    private bool IsMonsterPlacementValid(Cell candidate, List<Cell> existingMonsters, List<Cell> existingChests, int monsterMinSep, int monsterToChestMinSep)
    {
        // 1. 检查与所有现有怪物的间隔 (优先保证，Min Sep: 5)
        foreach (Cell existingMonster in existingMonsters)
        {
            int distance = Mathf.Abs(candidate.x - existingMonster.x) + Mathf.Abs(candidate.z - existingMonster.z);
            if (distance < monsterMinSep)
            {
                return false;
            }
        }

        // 2. 检查与所有宝箱的间隔 (Min Sep: 3)
        foreach (Cell existingChest in existingChests)
        {
            int distance = Mathf.Abs(candidate.x - existingChest.x) + Mathf.Abs(candidate.z - existingChest.z);
            if (distance < monsterToChestMinSep)
            {
                return false;
            }
        }

        return true;
    }

    // 拐角单元格检测 (保持不变)
    private bool IsCornerCell(Cell cell)
    {
        bool isRightOpen = cell.walls[0] == null;
        bool isLeftOpen = cell.walls[1] == null;
        bool isForwardOpen = cell.walls[4] == null;
        bool isBackwardOpen = cell.walls[5] == null;

        int openCount = 0;
        if (isRightOpen) openCount++;
        if (isLeftOpen) openCount++;
        if (isForwardOpen) openCount++;
        if (isBackwardOpen) openCount++;

        // 死路 (1个出口)
        if (openCount == 1)
        {
            return true;
        }

        // 拐角 (2个相邻出口)
        if (openCount == 2)
        {
            // 检查是否为直道 (两个相反方向的出口)
            if ((isRightOpen && isLeftOpen) || (isForwardOpen && isBackwardOpen))
            {
                return false; // 直道
            }

            return true; // 两个相邻出口，是拐角
        }

        return false;
    }

    // 放置起点和终点（光门） (使用各自的高度参数)
    private void PlaceStartAndEnd()
    {
        Cell startCell = mazeCells[0, 0, 0];
        Cell endCell = mazeCells[xSize - 1, 0, zSize - 1];

        Vector3 startPos = GetCellPosition(startCell.x, startCell.y, startCell.z);

        // 起点放置在 (ItemHeight / 2f) + ItemYOffset 处
        Vector3 spawnPosAboveGround = new Vector3(startPos.x, (ItemHeight / 2f) + ItemYOffset, startPos.z);

        Instantiate(StartPrefab, spawnPosAboveGround, Quaternion.identity);

        Vector3 endPos = GetCellPosition(endCell.x, endCell.y, endCell.z);
        // 终点门的位置保持在 y=0
        Instantiate(EndGatePrefab, endPos, Quaternion.identity);

        // *** 入口边界墙处理 ***
        // 保持入口边界墙 (walls[5] 在 Z=0 处)，阻止玩家后退掉落。
        // 原本的代码块被移除，以保持墙壁存在：
        // if (startCell.walls[5] != null) { Destroy(startCell.walls[5]); }

        // *** 出口边界墙处理 ***
        // 只移除终点处的边界墙 (walls[4] 在 Z=zSize-1 处)
        if (endCell.walls[4] != null)
        {
            Destroy(endCell.walls[4]);
        }
    }


    private Vector3 GetCellPosition(int x, int y, int z)
    {
        return new Vector3(x * WallScale, y * WallScale, z * WallScale);
    }
    private GameObject CreateWall(Vector3 position, Vector3 scale, Transform parent, Material material)
    {
        GameObject wall = Instantiate(WallPrefab, position, Quaternion.identity, parent);
        wall.transform.localScale = scale;

        Renderer renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = material;
        }

        return wall;
    }

    // 核心：移除墙壁并记录 AI 通道
    private void RemoveWall(Cell current, Cell neighbour)
    {
        int dx = current.x - neighbour.x;
        int dy = current.y - neighbour.y;
        int dz = current.z - neighbour.z;

        // X轴 (Right/Left)
        if (dx == 1) // current is Right of neighbour (current.x > neighbour.x)
        {
            Destroy(neighbour.walls[0]);

            // 记录通道: current <-> neighbour
            int current2DIndex = CoordinatesTo2DIndex(current.x, current.z);
            int neighbour2DIndex = CoordinatesTo2DIndex(neighbour.x, neighbour.z);

            // Current open to West (W)
            mazeFlagsNativeArray[current2DIndex] |= MazeFlags.PassageW;
            // Neighbour open to East (E)
            mazeFlagsNativeArray[neighbour2DIndex] |= MazeFlags.PassageE;
        }
        else if (dx == -1) // current is Left of neighbour (current.x < neighbour.x)
        {
            Destroy(current.walls[0]);

            // 记录通道: current <-> neighbour
            int current2DIndex = CoordinatesTo2DIndex(current.x, current.z);
            int neighbour2DIndex = CoordinatesTo2DIndex(neighbour.x, neighbour.z);

            // Current open to East (E)
            mazeFlagsNativeArray[current2DIndex] |= MazeFlags.PassageE;
            // Neighbour open to West (W)
            mazeFlagsNativeArray[neighbour2DIndex] |= MazeFlags.PassageW;
        }

        // Y轴 (Up/Down)
        if (dy == 1)
            Destroy(neighbour.walls[2]);
        else if (dy == -1)
            Destroy(current.walls[2]);
        // Y轴的通道不需要记录到 2D 气味地图中

        // Z轴 (Forward/Backward)
        if (dz == 1) // current is Forward of neighbour (current.z > neighbour.z)
        {
            Destroy(neighbour.walls[4]);

            // 记录通道: current <-> neighbour
            int current2DIndex = CoordinatesTo2DIndex(current.x, current.z);
            int neighbour2DIndex = CoordinatesTo2DIndex(neighbour.x, neighbour.z);

            // Current open to South (S) - Backward
            mazeFlagsNativeArray[current2DIndex] |= MazeFlags.PassageS;
            // Neighbour open to North (N) - Forward
            mazeFlagsNativeArray[neighbour2DIndex] |= MazeFlags.PassageN;
        }
        else if (dz == -1) // current is Backward of neighbour (current.z < neighbour.z)
        {
            Destroy(current.walls[4]);

            // 记录通道: current <-> neighbour
            int current2DIndex = CoordinatesTo2DIndex(current.x, current.z);
            int neighbour2DIndex = CoordinatesTo2DIndex(neighbour.x, neighbour.z);

            // Current open to North (N) - Forward
            mazeFlagsNativeArray[current2DIndex] |= MazeFlags.PassageN;
            // Neighbour open to South (S) - Backward
            mazeFlagsNativeArray[neighbour2DIndex] |= MazeFlags.PassageS;
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
            // 明确使用 UnityEngine.Random
            return neighbours[UnityEngine.Random.Range(0, neighbours.Count)];
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