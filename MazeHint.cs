using UnityEngine;
using UnityEngine.AI; // 引入 NavMesh 命名空间
using System.Collections.Generic;

public class MazeHint : MonoBehaviour
{
    // NavMesh 寻路所需
    [Tooltip("场景中代表玩家的 Transform (寻路起点)。")]
    public Transform playerTransform;

    [Tooltip("场景中代表迷宫终点的 Transform (可拖动)。")]
    public Transform targetTransform;

    // 绘制路径所需
    [Tooltip("用于在路径起点给出方向指示的预制体 (例如箭头)。")]
    public GameObject HintMarkerPrefab;
    private LineRenderer pathLineRenderer;

    // 箭头实例的引用，用于清除
    private GameObject directionalArrowInstance;

    // 提示信息是否显示
    private bool isHintVisible = false;

    // ----------------------------------------------------------------------
    // Start() - 初始化组件
    // ----------------------------------------------------------------------
    void Start()
    {
        // 尝试获取或添加 LineRenderer 组件
        pathLineRenderer = GetComponent<LineRenderer>();
        if (pathLineRenderer == null)
        {
            pathLineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // 设置 LineRenderer 默认样式
        pathLineRenderer.startWidth = 0.15f;
        pathLineRenderer.endWidth = 0.15f;
        pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        pathLineRenderer.startColor = new Color(1f, 1f, 0f, 0.7f);
        pathLineRenderer.endColor = new Color(1f, 0.5f, 0f, 0.7f);
        pathLineRenderer.positionCount = 0;
        pathLineRenderer.enabled = false;
    }

    // ----------------------------------------------------------------------
    // Update() 
    // ----------------------------------------------------------------------
    void Update()
    {
        // 按 Tab 键切换路径显示
        if (Input.GetKeyDown(KeyCode.Tab) && playerTransform != null && targetTransform != null)
        {
            isHintVisible = !isHintVisible;
            if (isHintVisible)
            {
                ShowHintPath();
            }
            else
            {
                ClearHintPath();
            }
        }

        // 如果路径可见，每一帧都重新计算路径，以跟随玩家和终点移动
        if (isHintVisible)
        {
            ShowHintPath();
        }
    }

    // ----------------------------------------------------------------------
    // ShowHintPath() - 核心 NavMesh 寻路逻辑
    // ----------------------------------------------------------------------
    public void ShowHintPath()
    {
        // 关键安全检查
        if (playerTransform == null || targetTransform == null)
        {
            Debug.LogError("NavMesh寻路失败：玩家或终点对象未设置。");
            return;
        }

        NavMeshPath path = new NavMeshPath();

        // --- 关键改进：使用 SamplePosition 确保起点和终点有效，并增大采样半径 ---
        NavMeshHit startHit;
        NavMeshHit targetHit;

        // 增大采样半径到 5.0f，以覆盖更宽的水平和垂直搜索距离
        const float SampleRadius = 5.0f;

        // 1. 确定有效的起点
        if (!NavMesh.SamplePosition(playerTransform.position, out startHit, SampleRadius, NavMesh.AllAreas))
        {
            Debug.LogWarning("NavMesh 错误：玩家位置 (" + playerTransform.position + ") 未落在有效的 NavMesh 区域内。");
            ClearHintPath();
            return;
        }

        // 2. 确定有效的终点 (修复 Y 轴识别错误)
        Vector3 searchPosition = targetTransform.position;
        // 强制从玩家 NavMesh 点的上方开始搜索，应对 Y 轴偏移问题。
        searchPosition.y = startHit.position.y + 5.0f;

        if (!NavMesh.SamplePosition(searchPosition, out targetHit, SampleRadius, NavMesh.AllAreas))
        {
            Debug.LogWarning("NavMesh 错误：终点位置 (" + targetTransform.position + ") 未落在有效的 NavMesh 区域内。请检查 NavMesh 烘焙Y轴是否严重偏移。");
            ClearHintPath();
            return;
        }

        // 尝试计算路径
        if (NavMesh.CalculatePath(startHit.position, targetHit.position, NavMesh.AllAreas, path))
        {
            // 路径计算成功
            DrawPath(path.corners);
        }
        else
        {
            // 路径计算失败，通常是起点和终点之间被障碍物完全隔断
            ClearHintPath();
            Debug.LogWarning("NavMesh 无法找到到终点的路径。");
        }
    }

    // ----------------------------------------------------------------------
    // DrawPath() - 绘制逻辑
    // ----------------------------------------------------------------------
    private void DrawPath(Vector3[] pathCorners)
    {
        if (pathCorners.Length == 0) return;

        // 1. LineRenderer 绘制整个路径
        pathLineRenderer.positionCount = pathCorners.Length;
        pathLineRenderer.SetPositions(pathCorners);
        pathLineRenderer.enabled = true;

        // 2. 绘制箭头提示
        DrawArrowHint(pathCorners);
    }

    // 使用箭头预制体，只在第一步给出方向提示
    private void DrawArrowHint(Vector3[] pathCorners)
    {
        // 清除旧箭头
        ClearArrowHint();

        // 至少需要起点和路径上的一个转角点
        if (pathCorners.Length < 2 || HintMarkerPrefab == null) return;

        Vector3 navMeshStartPoint = pathCorners[0];
        Vector3 nextPoint = pathCorners[1];

        // 1. 计算箭头位置
        Vector3 directionVector = nextPoint - navMeshStartPoint;
        directionVector.y = 0; // 仅考虑水平方向

        float arrowSpawnDistance = 0.5f;
        float actualDistance = Mathf.Min(arrowSpawnDistance, directionVector.magnitude * 0.9f);

        Vector3 markerPos = navMeshStartPoint + directionVector.normalized * actualDistance;

        // 2. 抬高箭头，使其略高于 NavMesh 表面
        // 使用 NavMesh 点的高度+一个偏移量，确保箭头紧贴地面
        markerPos.y = navMeshStartPoint.y + 0.3f;

        // 3. 计算旋转：面向下一个点
        Quaternion rotation = Quaternion.LookRotation(directionVector.normalized);

        // 4. 实例化箭头
        directionalArrowInstance = Instantiate(HintMarkerPrefab, markerPos, rotation, this.transform);
        directionalArrowInstance.name = "DirectionalHintArrow";
    }

    // ----------------------------------------------------------------------
    // ClearHintPath() - 清理逻辑
    // ----------------------------------------------------------------------
    private void ClearHintPath()
    {
        if (pathLineRenderer != null)
        {
            pathLineRenderer.enabled = false;
            pathLineRenderer.positionCount = 0;
        }
        ClearArrowHint();
    }

    private void ClearArrowHint()
    {
        if (directionalArrowInstance != null)
        {
            Destroy(directionalArrowInstance);
            directionalArrowInstance = null;
        }
    }
}