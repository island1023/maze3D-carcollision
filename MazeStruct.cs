using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using System.Linq;

// 定义通道标志（仅使用直角通道，因为MazeGenerator3D只创建直角通道）
public enum MazeFlags : byte
{
    // None = 0,
    PassageE = 1 << 0, // Right (x+1)
    PassageW = 1 << 1, // Left (x-1)
    PassageN = 1 << 2, // Forward (z+1)
    PassageS = 1 << 3, // Backward (z-1)
}

// 迷宫结构，用于 Burst Job
public struct Maze : System.IDisposable
{
    public int2 size;
    public int Length => size.x * size.y;

    // 步长：索引的偏移量
    public int StepE => 1;
    public int StepW => -1;
    public int StepN => size.x;
    public int StepS => -size.x;

    // 迷宫单元格数据 (MazeFlags)
    [ReadOnly]
    public NativeArray<MazeFlags> cellFlags;

    // 存储 WallScale, 用于坐标转换
    float wallScale;

    public Maze(int2 size, float wallScale, NativeArray<MazeFlags> flags)
    {
        this.size = size;
        this.wallScale = wallScale;
        this.cellFlags = flags;
    }

    public void Dispose()
    {
        if (cellFlags.IsCreated)
        {
            cellFlags.Dispose();
        }
    }

    public MazeFlags this[int i] => cellFlags[i];

    // 坐标转索引 (XZ 平面)
    public int CoordinatesToIndex(int2 coordinates) =>
        coordinates.y * size.x + coordinates.x;

    // 索引转坐标 (XZ 平面)
    public int2 IndexToCoordinates(int index) =>
        new int2(index % size.x, index / size.x);

    // 世界位置转迷宫坐标 (XZ 平面)
    public int2 WorldPositionToCoordinates(Vector3 position)
    {
        // 根据 MazeGenerator3D 的中心点逻辑计算
        int x = Mathf.FloorToInt(position.x / wallScale + 0.5f);
        int z = Mathf.FloorToInt(position.z / wallScale + 0.5f);

        return new int2(
            Mathf.Clamp(x, 0, size.x - 1),
            Mathf.Clamp(z, 0, size.y - 1)
        );
    }

    // 世界位置转索引
    public int WorldPositionToIndex(Vector3 position) =>
        CoordinatesToIndex(WorldPositionToCoordinates(position));

    // 坐标转世界位置 (XZ 平面)
    public Vector3 CoordinatesToWorldPosition(int2 coordinates, float y)
    {
        return new Vector3(coordinates.x * wallScale, y, coordinates.y * wallScale);
    }

    // 索引转世界位置 (XZ 平面)
    public Vector3 IndexToWorldPosition(int index, float y)
    {
        int2 coordinates = IndexToCoordinates(index);
        return CoordinatesToWorldPosition(coordinates, y);
    }
}