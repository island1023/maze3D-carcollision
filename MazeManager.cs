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
        // 确保基本依赖项在游戏开始时被赋值
        if (mazeGenerator == null || playerController == null || agents == null || agents.Length == 0)
        {
            Debug.LogError("MazeManager 缺少依赖项 (MazeGenerator3D, PlayerController, Agents)。请在 Inspector 中分配所有字段。");
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
        // 只有在 scent 被初始化过的情况下才调用 Dispose
        scent.Dispose();
    }

    // ----------------------------------------
    // 游戏逻辑
    // ----------------------------------------

    void StartNewGame()
    {
        isPlaying = true;
        // 确保 MazeData 在这里是有效的。
        Maze maze = mazeGenerator.MazeData;

        // 1. 初始化 Scent 系统
        scent.Dispose();
        scent = new Scent(maze);

        // 2. 初始化所有 Agents
        if (agents == null || agents.Length == 0)
        {
            Debug.LogError("[Manager Debug] Agents array is empty or null. Cannot initialize monsters.");
            return;
        }

        for (int i = 0; i < agents.Length; i++)
        {
            // *** 关键修复 1：检查数组元素是否为 null ***
            if (agents[i] == null)
            {
                Debug.LogWarning($"[Manager Debug] Agent slot {i} is null in the Inspector and was skipped.");
                continue;
            }
            // ********************************************

            // 随机起始坐标，将玩家的 Transform 传入 Agent (用于获取 y 轴)
            int2 coordinates = new int2(
                UnityEngine.Random.Range(0, maze.size.x),
                UnityEngine.Random.Range(0, maze.size.y)
            );
            agents[i].StartNewGame(maze, coordinates, playerController.transform);
        }

        Debug.Log("[Manager Debug] Game Started. Agents initialized.");
    }

    void UpdateGame()
    {
        if (agents == null || agents.Length == 0)
        {
            Debug.LogWarning("[Manager Debug] Agents array is null or empty. Stopping game loop.");
            isPlaying = false;
            return;
        }

        // 1. 获取玩家位置
        Vector3 playerPosition = playerController.transform.position;

        // 2. 气味扩散
        NativeArray<float> currentScent = scent.Disperse(mazeGenerator.MazeData, playerPosition);

        // 3. 确定最近的怪物和最小距离
        Agent nearestAgent = null;
        float minSqrDistance = float.MaxValue;

        for (int i = 0; i < agents.Length; i++)
        {
            // *** 关键修复 2：检查数组元素是否为 null ***
            if (agents[i] == null || !agents[i].gameObject.activeInHierarchy) continue;
            // ********************************************

            float sqrDist = (agents[i].transform.position - playerPosition).sqrMagnitude;
            if (sqrDist < minSqrDistance)
            {
                minSqrDistance = sqrDist;
                nearestAgent = agents[i];
            }
        }

        // --- 调试检测点 A: 确认怪物被选中 ---
        if (nearestAgent != null)
        {
            Debug.Log($"[Manager Debug] Nearest Agent Selected: {nearestAgent.name} (Dist Sqr: {minSqrDistance:F2})");
        }
        else
        {
            // 如果此警告出现，请检查所有怪物是否在场景中被禁用（inactive）
            Debug.LogWarning("[Manager Debug] No nearest agent found! All agents inactive or destroyed.");
        }
        // ---------------------------------------


        // 4. 移动怪物并执行“最近怪物”逻辑
        for (int i = 0; i < agents.Length; i++)
        {
            Agent currentAgent = agents[i];

            // *** 关键修复 3：再次检查数组元素是否为 null ***
            if (currentAgent == null || !currentAgent.gameObject.activeInHierarchy) continue;
            // ********************************************

            // 仅让最近的怪物移动，其他怪物原地不动
            if (currentAgent == nearestAgent)
            {
                currentAgent.SetMoving(true);
            }
            else
            {
                currentAgent.SetMoving(false);
            }

            // 调用移动方法
            Vector3 agentPosition = currentAgent.Move(currentScent);

            // 5. 检查游戏结束条件 (抓住玩家)
            float distXZ = new Vector2(
                agentPosition.x - playerPosition.x,
                agentPosition.z - playerPosition.z
            ).sqrMagnitude;

            if (distXZ < 1f) // 距离小于一个单位 (WallScale)
            {
                Debug.Log($"Monster {i} caught/attacked the player! Implement EndGame/Attack logic here.");
                currentAgent.SetMoving(false); // 抓住后停止移动
            }
        }
    }
}