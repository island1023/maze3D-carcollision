using UnityEngine;

/// <summary>
/// 伤害计算结果载体
/// </summary>
public struct DamageResult
{
    public float finalDamage;       // 实际扣血量（传给 1号 降速和切换状态）
    public float visualImpactForce; // 视觉表现冲击力（传给 3号 播特效，街机规则下会被放大）
}

public static class DamageCalculator
{
    // 街机规则损伤档位与对应的视觉表现力（确保剧情杀也有大特效）
    private const float LIGHT_DAMAGE = 40f;
    private const float LIGHT_VISUAL_FORCE = 1500f; // 强制产生明显的刮蹭火花

    private const float HEAVY_DAMAGE = 75f;
    private const float HEAVY_VISUAL_FORCE = 5000f; // 强制产生中等变形和烟雾

    private const float DESTROY_DAMAGE = 100f;
    private const float DESTROY_VISUAL_FORCE = 15000f; // 强制产生大爆炸特效

    // 纯物理计算微调系数
    private const float PHYSICS_MULTIPLIER = 0.02f;

    public static DamageResult CalculateDamage(string myTag, string targetTag, Collision collision)
    {
        float relativeSpeed = collision.relativeVelocity.magnitude;
        float actualImpulse = collision.impulse.magnitude;

        // ==========================================
        // 规则 1：SUV 的碰撞逻辑
        // ==========================================
        if (myTag == "SUV" && targetTag == "Wall")
        {
            if (relativeSpeed < 10f) return new DamageResult { finalDamage = LIGHT_DAMAGE, visualImpactForce = Mathf.Max(actualImpulse, LIGHT_VISUAL_FORCE) };
            if (relativeSpeed < 20f) return new DamageResult { finalDamage = HEAVY_DAMAGE, visualImpactForce = Mathf.Max(actualImpulse, HEAVY_VISUAL_FORCE) };
            return new DamageResult { finalDamage = DESTROY_DAMAGE, visualImpactForce = Mathf.Max(actualImpulse, DESTROY_VISUAL_FORCE) };
        }

        // ==========================================
        // 规则 2：卡车 (Truck) 的碰撞逻辑
        // ==========================================
        if (myTag == "Truck")
        {
            if (targetTag == "Wall")
                return new DamageResult { finalDamage = LIGHT_DAMAGE, visualImpactForce = Mathf.Max(actualImpulse, LIGHT_VISUAL_FORCE) };

            if (targetTag == "Truck")
            {
                if (relativeSpeed > 15f) return new DamageResult { finalDamage = DESTROY_DAMAGE, visualImpactForce = Mathf.Max(actualImpulse, DESTROY_VISUAL_FORCE) };
                else return new DamageResult { finalDamage = HEAVY_DAMAGE, visualImpactForce = Mathf.Max(actualImpulse, HEAVY_VISUAL_FORCE) };
            }
        }

        // ==========================================
        // 规则 3：轿车 (Sedan) 的碰撞逻辑
        // ==========================================
        if (myTag == "Sedan")
        {
            if (targetTag == "Streetlight")
                return new DamageResult { finalDamage = LIGHT_DAMAGE, visualImpactForce = Mathf.Max(actualImpulse, LIGHT_VISUAL_FORCE) };

            if (targetTag == "Tree")
                return new DamageResult { finalDamage = HEAVY_DAMAGE, visualImpactForce = Mathf.Max(actualImpulse, HEAVY_VISUAL_FORCE) };

            if (targetTag == "Truck")
                return new DamageResult { finalDamage = DESTROY_DAMAGE, visualImpactForce = Mathf.Max(actualImpulse, DESTROY_VISUAL_FORCE) };
        }

        // ==========================================
        // 默认保底：纯物理模拟计算 F = Δp/Δt
        // ==========================================
        ContactPoint contact = collision.contacts[0];
        Vector3 relativeVelocityDir = collision.relativeVelocity.normalized;
        float impactAngleWeight = Mathf.Abs(Vector3.Dot(contact.normal, relativeVelocityDir));

        float physicsDamage = actualImpulse * impactAngleWeight * PHYSICS_MULTIPLIER;

        return new DamageResult
        {
            finalDamage = physicsDamage,
            visualImpactForce = actualImpulse
        };
    }
}