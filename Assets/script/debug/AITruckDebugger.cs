using UnityEngine;
using UnityEngine.AI;

public class AITruckDebugger : MonoBehaviour
{
    [Header("调试目标")]
    public AITruckWaypointController aiTruck;   // 拖入 AI 卡车物体
    public Transform playerTarget;              // 拖入玩家（轿车）的 Transform

    [Header("显示设置")]
    public bool showGUI = true;
    public Vector2 guiPosition = new Vector2(20, 20);
    public int fontSize = 14;

    private float lastDistance = 0f;
    private float currentSpeed = 0f;

    void Update()
    {
        if (aiTruck == null || playerTarget == null) return;

        // 实时计算距离
        lastDistance = Vector3.Distance(aiTruck.transform.position, playerTarget.position);

        // 获取当前速度（优先取 Rigidbody，否则取 NavMeshAgent）
        currentSpeed = GetCurrentSpeed();

        // 快捷键 M：手动启动 AI（用于调试）
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (aiTruck != null)
            {
                // 调用 AI 脚本中的启动方法（注意：原脚本中没有 StartAI 方法，需要先添加）
                // 原脚本启动是基于距离的，我们通过反射或添加公共方法来实现强制启动。
                // 为了不修改原脚本，这里用反射调用私有方法设置状态（谨慎使用）。
                // 更好的做法：在原 AITruckWaypointController 中添加 public void ForceStart() 方法。
                // 这里给出两种兼容写法，请根据实际是否修改原脚本选择。
                ForceStartAI();
                Debug.Log("[调试] 手动强制启动 AI");
            }
        }
    }

    private float GetCurrentSpeed()
    {
        if (aiTruck == null) return 0f;

        Rigidbody rb = aiTruck.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
            return rb.velocity.magnitude;

        NavMeshAgent agent = aiTruck.GetComponent<NavMeshAgent>();
        if (agent != null && agent.isActiveAndEnabled)
            return agent.velocity.magnitude;

        return 0f;
    }

    private void ForceStartAI()
    {
        // 方法1：直接修改状态（需要访问 internal 字段，不推荐但可行）
        // 使用反射来设置私有字段 当前状态 为 沿路点行驶，并启用 agent
        var stateField = typeof(AITruckWaypointController).GetField("当前状态",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (stateField != null)
        {
            stateField.SetValue(aiTruck, AITruckWaypointController.AIState.沿路点行驶);
            NavMeshAgent agent = aiTruck.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                // 调用 GoToNextWaypoint 私有方法
                var method = typeof(AITruckWaypointController).GetMethod("GoToNextWaypoint",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(aiTruck, null);
            }
        }
        else
        {
            Debug.LogError("无法通过反射设置状态，请确保 AITruckWaypointController 中有 public void ForceStart() 方法");
        }
    }

    void OnGUI()
    {
        if (!showGUI) return;
        if (aiTruck == null || playerTarget == null)
        {
            GUI.Box(new Rect(guiPosition.x, guiPosition.y, 300, 60), "调试器：请拖入 AI 卡车和玩家目标");
            return;
        }

        GUIStyle boxStyle = GUI.skin.box;
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = fontSize;
        labelStyle.richText = true;

        GUILayout.BeginArea(new Rect(guiPosition.x, guiPosition.y, 450, 350));
        GUILayout.BeginVertical(boxStyle);

        GUILayout.Label("<b>=== AI 卡车调试信息 ===</b>", labelStyle);
        GUILayout.Label($"<color=yellow>AI 状态 : {aiTruck.当前状态}</color>", labelStyle);
        GUILayout.Label($"距离玩家 : {lastDistance:F2} 米", labelStyle);
        GUILayout.Label($"当前速度 : {currentSpeed * 3.6f:F1} km/h  ({currentSpeed:F2} m/s)", labelStyle);

        // 启动条件检查
        GUILayout.Space(5);
        GUILayout.Label("<b>--- 启动条件检测 ---</b>", labelStyle);
        if (aiTruck.玩家目标 == null)
            GUILayout.Label("<color=red>【错误】AI 脚本中的「玩家目标」未赋值</color>", labelStyle);
        else
        {
            float distanceToPlayer = Vector3.Distance(aiTruck.transform.position, aiTruck.玩家目标.position);
            GUILayout.Label($"脚本内距离计算 : {distanceToPlayer:F2} 米", labelStyle);
            GUILayout.Label($"启动距离阈值 : {aiTruck.启动距离} 米", labelStyle);
            if (distanceToPlayer <= aiTruck.启动距离)
                GUILayout.Label("<color=green>✓ 已满足启动距离条件</color>", labelStyle);
            else
                GUILayout.Label("<color=orange>✗ 未达到启动距离（需再靠近）</color>", labelStyle);
        }

        if (aiTruck.巡逻路点 == null || aiTruck.巡逻路点.Length == 0)
            GUILayout.Label("<color=red>【错误】巡逻路点数组为空</color>", labelStyle);
        else
            GUILayout.Label($"路点数量 : {aiTruck.巡逻路点.Length}", labelStyle);

        NavMeshAgent agentComp = aiTruck.GetComponent<NavMeshAgent>();
        if (agentComp == null)
            GUILayout.Label("<color=red>【错误】缺少 NavMeshAgent 组件</color>", labelStyle);
        else if (!agentComp.isOnNavMesh)
            GUILayout.Label("<color=orange>警告：Agent 不在 NavMesh 上</color>", labelStyle);

        GUILayout.Space(5);
        GUILayout.Label("<b>--- 操作提示 ---</b>", labelStyle);
        GUILayout.Label("按 <b>M 键</b> 强制启动 AI（调试用）", labelStyle);
        GUILayout.Label("请确保玩家轿车的 Transform 已拖入「玩家目标」字段", labelStyle);

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}