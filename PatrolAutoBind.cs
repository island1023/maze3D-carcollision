// 文件名: PatrolAutoBind.cs

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
public class PatrolAutoBind : MonoBehaviour
{
    // *** 巡逻参数 ***
    public float stoppingDistance = 0.5f;
    public float patrolSpeed = 3.5f;

    [Header("容错机制")]
    [Tooltip("怪物被视为卡住的最大时间（秒）。超过此时间将强制重新寻路。")]
    public float MaxStuckTime = 2.0f;

    private NavMeshAgent agent;
    private List<Transform> boundPatrolPoints; // 怪物绑定的巡逻点列表
    private int destPointIndex = 0;

    // *** 关键新增：往返巡逻状态 ***
    private bool isReversing = false; // true = 后退 (3->2->1)，false = 前进 (1->2->3)

    // *** 容错所需变量 ***
    private float stuckTimer = 0f; // 卡住计时器
    private const float MinVelocityForStuckCheck = 0.1f; // 速度低于此值视为停顿
    // *************************

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component not found on the monster!");
            enabled = false;
            return;
        }

        agent.speed = patrolSpeed;
        agent.stoppingDistance = stoppingDistance;

        // 1. 自动搜索最近的巡逻点并绑定路线
        if (!FindClosestWaypointAndBindRoute())
        {
            Debug.LogError($"Monster {gameObject.name}: Failed to find any suitable starting waypoint group.");
            enabled = false;
            return;
        }

        // 2. 开始巡逻
        GotoNextPoint();
    }

    // FindClosestWaypointAndBindRoute() 方法保持不变
    private bool FindClosestWaypointAndBindRoute()
    {
        // 查找场景中所有定义了 WaypointGroup 的对象 (即所有巡逻路线的父对象)
        WaypointGroup[] allRouteGroups = FindObjectsOfType<WaypointGroup>();

        if (allRouteGroups.Length == 0)
        {
            Debug.LogError("No WaypointGroup components found in the scene.");
            return false;
        }

        float closestDistanceSqr = Mathf.Infinity;
        List<Transform> bestRoute = null;
        int startingWaypointIndex = 0;

        Vector3 monsterPosition = transform.position;

        // 遍历所有巡逻路线组
        foreach (WaypointGroup group in allRouteGroups)
        {
            // 确保路线列表不为空
            if (group.waypointsInOrder == null || group.waypointsInOrder.Count == 0)
            {
                Debug.LogWarning($"WaypointGroup on {group.gameObject.name} has an empty waypoint list.");
                continue;
            }

            // 遍历当前路线组中的所有点，寻找最近的点
            for (int i = 0; i < group.waypointsInOrder.Count; i++)
            {
                Transform waypoint = group.waypointsInOrder[i];
                if (waypoint == null) continue;

                // 计算距离平方 (比计算开方后的距离更快)
                Vector3 directionToWaypoint = waypoint.position - monsterPosition;
                float dSqrToWaypoint = directionToWaypoint.sqrMagnitude;

                // 找到更近的巡逻点
                if (dSqrToWaypoint < closestDistanceSqr)
                {
                    closestDistanceSqr = dSqrToWaypoint;
                    bestRoute = group.waypointsInOrder;
                    startingWaypointIndex = i; // 记录最近点在该路线中的索引
                }
            }
        }

        if (bestRoute != null)
        {
            boundPatrolPoints = bestRoute;

            // 将巡逻起点设置为找到的最近点
            destPointIndex = startingWaypointIndex;

            // 增加日志显示怪物绑定到的具体路线名称
            string routeName = boundPatrolPoints.Count > 0 && boundPatrolPoints[0] != null && boundPatrolPoints[0].parent != null
                               ? boundPatrolPoints[0].parent.name
                               : "Unknown Route";

            Debug.Log($"Monster {gameObject.name} auto-bound to route '{routeName}' starting at: {boundPatrolPoints[destPointIndex].name}");
            return true;
        }

        return false;
    }


    // *** 往返逻辑实现 (保持不变) ***
    private void GotoNextPoint()
    {
        if (boundPatrolPoints == null || boundPatrolPoints.Count == 0) return;

        // 1. 设置下一个目标点
        Vector3 nextTarget = boundPatrolPoints[destPointIndex].position;
        agent.SetDestination(nextTarget);

        // 2. ----------------------------------------------------
        // --- 核心往返逻辑：更新 destPointIndex ---

        int lastIndex = boundPatrolPoints.Count - 1;

        if (!isReversing) // 当前在前进方向 (0 -> lastIndex)
        {
            if (destPointIndex < lastIndex)
            {
                // 正常前进：0, 1, 2...
                destPointIndex++;
            }
            else
            {
                // 到达终点 (LastIndex)，调转方向
                isReversing = true;

                // 新目标索引设为倒数第二个点，确保下一次调用从 (Count - 2) 开始
                destPointIndex = Mathf.Max(0, lastIndex - 1);
            }
        }
        else // 当前在后退方向 (lastIndex -> 0)
        {
            if (destPointIndex > 0)
            {
                // 正常后退：...2, 1, 0
                destPointIndex--;
            }
            else
            {
                // 到达起点 (0)，调转方向
                isReversing = false;

                // 新目标索引设为第二个点 (索引 1)，确保下一次调用从 1 开始
                destPointIndex = Mathf.Min(lastIndex, 1);
            }
        }
        // ----------------------------------------------------
    }

    void Update()
    {
        // 确保 NavMesh Agent 正在工作且有路径
        if (!agent.enabled || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        // 1. *** 容错机制：检测怪物是否卡住 ***
        // 如果怪物速度很慢，但距离目标点仍然很远 (超过 stoppingDistance)
        if (agent.velocity.magnitude < MinVelocityForStuckCheck && agent.remainingDistance > stoppingDistance)
        {
            stuckTimer += Time.deltaTime;

            if (stuckTimer >= MaxStuckTime)
            {
                Debug.LogWarning(gameObject.name + " seems stuck! Forcing path recalculation to target index " + destPointIndex);

                // 强制将当前目标点重新设置为目的地 (迫使 NavMesh Agent 重新规划路径)
                agent.SetDestination(boundPatrolPoints[destPointIndex].position);
                stuckTimer = 0f; // 重置计时器
            }
        }
        else
        {
            // 怪物正在移动，重置计时器
            stuckTimer = 0f;
        }
        // -----------------------------------------

        // 2. 正常巡逻逻辑：检查是否到达目标点 (触发 GotoNextPoint)
        if (!agent.pathPending && agent.remainingDistance < stoppingDistance)
        {
            GotoNextPoint();
        }
    }

    // OnDrawGizmosSelected() 保持不变
    private void OnDrawGizmosSelected()
    {
        if (boundPatrolPoints == null || boundPatrolPoints.Count < 2) return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < boundPatrolPoints.Count; i++)
        {
            Transform current = boundPatrolPoints[i];
            // 注意：这里仍然是闭环绘制，它只用于可视化，不影响运行时往返逻辑
            Transform next = boundPatrolPoints[(i + 1) % boundPatrolPoints.Count];

            if (current != null)
            {
                Gizmos.DrawSphere(current.position, 0.3f);
            }

            if (current != null && next != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(current.position, next.position);
            }
        }
    }
}