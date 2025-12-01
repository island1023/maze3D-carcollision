using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public struct Scent : System.IDisposable
{
    NativeArray<float> scentA, scentB;

    bool useA;

    float cooldown;

    // 冷却时间，每秒运行 10 次
    const float COOLDOWN_TIME = 0.1f;

    public Scent(Maze maze)
    {
        scentA = new NativeArray<float>(
            maze.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory
        );
        scentB = new NativeArray<float>(maze.Length, Allocator.Persistent);
        useA = false;
        cooldown = 0f;
    }

    public void Dispose()
    {
        if (scentA.IsCreated)
        {
            scentA.Dispose();
            scentB.Dispose();
        }
    }

    public NativeArray<float> Disperse(Maze maze, Vector3 playerPosition)
    {
        bool jobScheduled = false;
        cooldown -= Time.deltaTime;
        if (cooldown <= 0f)
        {
            cooldown += COOLDOWN_TIME;
            new DisperseScentJob
            {
                maze = maze,
                oldScent = useA ? scentA : scentB,
                newScent = useA ? scentB : scentA,
            }.ScheduleParallel(maze.Length, maze.size.x, default).Complete();

            useA = !useA;
            jobScheduled = true;
        }

        // 获取当前正在使用的气味数组
        NativeArray<float> current = useA ? scentA : scentB;

        // 将玩家当前位置的气味设置为最大值 (1f)
        int playerIndex = maze.WorldPositionToIndex(playerPosition);
        current[playerIndex] = 1f;

        // --- 调试检测点 B: 确认 Job 运行和气味设置 ---
        if (jobScheduled)
        {
            Debug.Log($"[Scent Debug] Job Run. Player Scent set at index: {playerIndex} ({maze.IndexToCoordinates(playerIndex)})");
        }
        // ------------------------------------------------

        return current;
    }
}