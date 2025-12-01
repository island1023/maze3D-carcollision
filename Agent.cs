using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public class Agent : MonoBehaviour
{
    [SerializeField]
    Color color = Color.red;

    [SerializeField, Min(0f)]
    float speed = 2.5f;

    // ----------------------------------------
    // 私有变量
    // ----------------------------------------

    private Maze maze;

    private int targetIndex;

    private Vector3 targetPosition;

    private Transform playerTransform;

    // 控制是否移动的标志
    private bool isMoving = false;

    // ----------------------------------------
    // Unity 方法
    // ----------------------------------------

    void Awake()
    {
        // 初始化可视化属性
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.material.color = color;
        }

        // 修正 2: 移除默认禁用，让怪物由场景或预制件的启用状态控制。
        // gameObject.SetActive(false); 
    }

    // ----------------------------------------
    // 公共方法
    // ----------------------------------------

    public void StartNewGame(Maze maze, int2 coordinates, Transform player)
    {
        this.maze = maze;
        this.playerTransform = player;
        targetIndex = maze.CoordinatesToIndex(coordinates);

        // 设置初始位置，保持 Y 轴不变 (假设 y=1.0f)
        targetPosition = transform.localPosition =
            maze.CoordinatesToWorldPosition(coordinates, transform.localPosition.y);

        isMoving = false;
        gameObject.SetActive(true);
    }

    public void EndGame() => gameObject.SetActive(false);

    // 启用/禁用寻路移动 (用于“最近怪物”逻辑)
    public void SetMoving(bool state)
    {
        isMoving = state;
    }

    // 核心移动和寻路逻辑
    public Vector3 Move(NativeArray<float> scent)
    {
        if (!isMoving)
        {
            return transform.localPosition;
        }

        Vector3 position = transform.localPosition;
        Vector3 targetVector = targetPosition - position;
        float targetDistance = targetVector.magnitude;
        float movement = speed * Time.deltaTime;

        while (movement > targetDistance)
        {
            position = targetPosition;
            if (TryFindNewTarget(scent))
            {
                movement -= targetDistance;
                // 重新计算目标向量和距离
                targetVector = targetPosition - position;
                targetDistance = targetVector.magnitude;
            }
            else
            {
                // 无法找到新目标，停止移动
                return transform.localPosition = position;
            }
        }

        // 应用剩余移动
        return transform.localPosition =
            position + targetVector * (movement / targetDistance);
    }

    // ----------------------------------------
    // 私有寻路方法
    // ----------------------------------------

    // 嗅探并更新气味最强的方向
    void Sniff(ref (int, float) trail, NativeArray<float> scent, int indexOffset)
    {
        int sniffIndex = targetIndex + indexOffset;

        // 追逐玩家：寻找更强的气味
        float detectedScent = scent[sniffIndex];
        if (detectedScent > trail.Item2)
        {
            trail = (sniffIndex, detectedScent);
        }
    }

    // 尝试在邻居单元格中找到最强的气味方向
    bool TryFindNewTarget(NativeArray<float> scent)
    {
        MazeFlags cell = maze[targetIndex];
        // trail 存储 (索引, 气味强度)
        (int, float) trail = (0, 0f);

        // 仅检查直角通道（使用位运算 & 检查通道）

        // E (Right)
        if ((cell & MazeFlags.PassageE) != 0)
        {
            Sniff(ref trail, scent, maze.StepE);
        }
        // W (Left)
        if ((cell & MazeFlags.PassageW) != 0)
        {
            Sniff(ref trail, scent, maze.StepW);
        }
        // N (Forward)
        if ((cell & MazeFlags.PassageN) != 0)
        {
            Sniff(ref trail, scent, maze.StepN);
        }
        // S (Backward)
        if ((cell & MazeFlags.PassageS) != 0)
        {
            Sniff(ref trail, scent, maze.StepS);
        }

        if (trail.Item2 > 0f)
        {
            // 找到了有气味的新目标
            targetIndex = trail.Item1;
            // 设置新的目标位置，保持 Y 轴不变
            targetPosition = maze.IndexToWorldPosition(trail.Item1, transform.localPosition.y);
            // --- 调试检测点 C1: 确认找到目标 ---
            Debug.Log($"[Agent Debug] {gameObject.name} found new target at index: {targetIndex} (Scent: {trail.Item2:F3})");
            // ---------------------------------------
            return true;
        }

        // --- 调试检测点 C2: 确认停止 ---
        if (isMoving)
        {
            // 如果怪物被选中移动 (isMoving=true) 但找不到目标，说明它停下来了。
            Debug.LogWarning($"[Agent Debug] {gameObject.name} failed to find target from index {targetIndex}. Scent too weak (Max Scent Detected: {trail.Item2:F3}) or blocked.");
        }
        // ----------------------------------

        return false;
    }
}