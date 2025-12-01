using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using System.Linq;

// 需要挂载在场景中的某个空 GameObject 上
public class MazeManager : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("迷宫生成器，用于获取迷宫数据")]
    public MazeGenerator3D mazeGenerator;

    [Tooltip("玩家的角色控制器，用于获取玩家位置")]
    public LegacyThirdPersonController playerController;

    [Tooltip("场景中所有的怪物 Agent 组件")]
    public Agent[] agents;

    [Header("Game State")]
    public bool isPlaying = false;

    private Scent scent;

    // ----------------------------------------
    // Unity 方法
    // ----------------------------------------

    void Start()
    {
        if (mazeGenerator == null || playerController == null || agents == null || agents.Length == 0)
        {
            Debug.LogError("MazeManager 缺少依赖项 (MazeGenerator3D, PlayerController, Agents)。请在 Inspector 中分配。");
            enabled = false;
            return;
        }
        // 等待 MazeGenerator3D 完成迷宫生成并创建 MazeData。
    }

    void Update()
    {
        // 检查迷宫数据是否已创建 (通过 MazeGenerator3D 的标志)
        if (!isPlaying && mazeGenerator != null && mazeGenerator.MazeData.cellFlags.IsCreated)
        {
            StartNewGame();
        }

        if (isPlaying)
        {
            UpdateGame();
        }
    }

    void OnDestroy()
    {
        // 清理 Scent 结构体的 NativeArray
        scent.Dispose();
    }

    // ----------------------------------------
    // 游戏逻辑
    // ----------------------------------------

    void StartNewGame()
    {
        isPlaying = true;
        Maze maze = mazeGenerator.MazeData;

        // 1. 初始化 Scent 系统
        scent.Dispose(); // 确保清理旧的
        scent = new Scent(maze);

        // 2. 初始化所有 Agents
        for (int i = 0; i < agents.Length; i++)
        {
            // 随机起始坐标，将玩家的 Transform 传入 Agent (用于获取 y 轴)
            int2 coordinates = new int2(
                UnityEngine.Random.Range(0, maze.size.x),
                UnityEngine.Random.Range(0, maze.size.y)
            );
            agents[i].StartNewGame(maze, coordinates, playerController.transform);
        }

        Debug.Log("Game Started. Agents initialized.");
    }

    void UpdateGame()
    {
        // 1. 获取玩家位置
        Vector3 playerPosition = playerController.transform.position;

        // 2. 气味扩散
        NativeArray<float> currentScent = scent.Disperse(mazeGenerator.MazeData, playerPosition);

        // 3. 确定最近的怪物和最小距离
        Agent nearestAgent = null;
        float minSqrDistance = float.MaxValue;

        // 用于存储最近的怪物与玩家的距离，以便进行游戏结束判断
        float nearestAgentSqrDistance = float.MaxValue;

        for (int i = 0; i < agents.Length; i++)
        {
            // 只考虑当前激活的 Agent
            if (!agents[i].gameObject.activeInHierarchy) continue;

            float sqrDist = (agents[i].transform.position - playerPosition).sqrMagnitude;
            if (sqrDist < minSqrDistance)
            {
                minSqrDistance = sqrDist;
                nearestAgent = agents[i];
            }
        }

        // 4. 移动怪物并执行“最近怪物”逻辑
        for (int i = 0; i < agents.Length; i++)
        {
            // 仅让最近的怪物移动，其他怪物原地不动
            if (agents[i] == nearestAgent)
            {
                agents[i].SetMoving(true);
            }
            else
            {
                agents[i].SetMoving(false);
            }

            // 调用移动方法
            Vector3 agentPosition = agents[i].Move(currentScent);

            // 5. 检查游戏结束条件 (抓住玩家)
            // 使用 XZ 平面上的距离
            float distXZ = new Vector2(
                agentPosition.x - playerPosition.x,
                agentPosition.z - playerPosition.z
            ).sqrMagnitude;

            if (distXZ < 1f) // 距离小于一个单位 (WallScale)
            {
                // ** 游戏结束/攻击逻辑占位符 **
                Debug.Log($"Monster {i} caught/attacked the player! Implement EndGame/Attack logic here.");
                // EndGame(); // 假设您有 EndGame 方法来处理游戏结束
                agents[i].SetMoving(false); // 抓住后停止移动
            }
        }
    }
}