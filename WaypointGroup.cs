using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 挂载到巡逻路线的父对象上，用于存储该路线中所有巡逻点的有序列表。
/// </summary>
public class WaypointGroup : MonoBehaviour
{
    [Tooltip("请将该路线中所有的子巡逻点 (Transform) 按怪物巡逻的顺序拖入此列表。")]
    public List<Transform> waypointsInOrder = new List<Transform>();

    private void OnDrawGizmos()
    {
        // 确保在编辑模式下也能看到该路线，即使没有选中怪物
        if (waypointsInOrder == null || waypointsInOrder.Count < 2) return;

        // 设置 Gizmo 颜色为绿色
        Gizmos.color = Color.green;

        // i 代表当前点，(i + 1) % Count 代表下一个点 (实现闭环)
        for (int i = 0; i < waypointsInOrder.Count; i++)
        {
            Transform current = waypointsInOrder[i];
            // 关键：使用模运算实现闭环 (最后一个点连接回第一个点)
            Transform next = waypointsInOrder[(i + 1) % waypointsInOrder.Count];

            if (current != null)
            {
                // 绘制点
                Gizmos.DrawSphere(current.position, 0.2f);
            }

            if (current != null && next != null)
            {
                // 绘制连接线 (只会连接相邻的两个点)
                Gizmos.DrawLine(current.position, next.position);
            }
        }
    }
}