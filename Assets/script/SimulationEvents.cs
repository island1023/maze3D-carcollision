using UnityEngine;
using System;

/// <summary>
/// 碰撞通用数据结构（数据消费载体）
/// 用于在 1号（动力衰减）、2号（力学解算）、3号（变形与视觉表现）之间进行信息交换
/// </summary>
public struct DamageData
{
    public float impactForce;        // 碰撞力大小（由 2号 依据冲量定理计算后传出）
    public Vector3 hitPoint;         // 碰撞核心接触点（用于 3号 空间精准定位火花、烟雾或爆炸特效）
    public Vector3 hitNormal;        // 碰撞接触面法线（用于判断车辆受击角度及特效喷射方向）
    public GameObject victim;        // 被撞击/受损的目标游戏对象（哪辆车，或哪棵树、哪扇墙）
    public Collider hitPart;         // 具体发生碰撞的车辆碰撞体部位（例如：前保险杠碰撞体、左侧门碰撞体）
}

/// <summary>
/// 全局事件总线（Event Bus）
/// 严格采用事件驱动架构（EDA），实现 1号、2号、3号 系统间的完全解耦
/// </summary>
public static class SimulationEvents
{
    // ==========================================
    // 1. 车辆损伤事件流
    // ==========================================
    public static event Action<DamageData> OnVehicleDamaged;

    public static void TriggerVehicleDamage(DamageData data)
    {
        OnVehicleDamaged?.Invoke(data);
    }

    // ==========================================
    // 2. 环境破坏事件流
    // ==========================================
    public static event Action<GameObject, Vector3, float> OnEnvironmentDestroyed;

    public static void TriggerEnvironmentDestroy(GameObject victim, Vector3 hitPoint, float impactForce)
    {
        OnEnvironmentDestroyed?.Invoke(victim, hitPoint, impactForce);
    }

    // ==========================================
    // 3. UI 物理参数交互事件流
    // ==========================================
    public static event Action<float, float> OnPhysicsSettingsChanged;

    public static void TriggerPhysicsChange(float dynamicFriction, float staticFriction)
    {
        OnPhysicsSettingsChanged?.Invoke(dynamicFriction, staticFriction);
    }

    // ==========================================
    // 4. UI 车辆状态可视化事件流 (为 3号成员新增)
    // ==========================================
    /// <summary>
    /// 用于向 3号成员的 UI 系统传递车辆的实时状态。
    /// 建议由 1号成员在 VehiclePerformanceSystem 的 Update 中调用此方法广播。
    /// </summary>
    public static event Action<GameObject, float, float, string> OnVehicleStatusUpdated;

    public static void TriggerVehicleStatusUpdate(GameObject vehicle, float currentSpeed, float currentDamage, string currentState)
    {
        OnVehicleStatusUpdated?.Invoke(vehicle, currentSpeed, currentDamage, currentState);
    }

    // ==========================================
    // 5. 架构安全与生命周期管理
    // ==========================================
    public static void ResetAllEvents()
    {
        OnVehicleDamaged = null;
        OnEnvironmentDestroyed = null;
        OnPhysicsSettingsChanged = null;
        OnVehicleStatusUpdated = null;
    }
}