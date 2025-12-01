using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
public struct DisperseScentJob : IJobFor
{
    [ReadOnly]
    public Maze maze;

    [ReadOnly, NativeDisableParallelForRestriction]
    public NativeArray<float> oldScent;

    public NativeArray<float> newScent;

    public void Execute(int i)
    {
        // 恢复当前单元格的旧气味
        MazeFlags cell = maze[i];
        float scent = oldScent[i];

        float fromNeighbors = 0f;
        float dispersalFactor = 0f; // 开放通道的数量

        // 收集邻居气味 (仅检查直角通道)
        // E (Right/x+1)
        if ((cell & MazeFlags.PassageE) != 0) // <-- 修改此处
        {
            fromNeighbors += oldScent[i + maze.StepE];
            dispersalFactor += 1f;
        }
        // W (Left/x-1)
        if ((cell & MazeFlags.PassageW) != 0) // <-- 修改此处
        {
            fromNeighbors += oldScent[i + maze.StepW];
            dispersalFactor += 1f;
        }
        // N (Forward/z+1)
        if ((cell & MazeFlags.PassageN) != 0) // <-- 修改此处
        {
            fromNeighbors += oldScent[i + maze.StepN];
            dispersalFactor += 1f;
        }
        // S (Backward/z-1)
        if ((cell & MazeFlags.PassageS) != 0) // <-- 修改此处
        {
            fromNeighbors += oldScent[i + maze.StepS];
            dispersalFactor += 1f;
        }

        // 气味扩散公式：从邻居获得 20%，自身损失 20%
        scent += (fromNeighbors - scent * dispersalFactor) * 0.2f;

        // 气味衰减：新气味腐烂 50%
        newScent[i] = scent * 0.5f;
    }
}