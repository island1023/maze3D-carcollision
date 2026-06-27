using UnityEngine;

[RequireComponent(typeof(VehiclePerformanceSystem))]
public class CollisionManager : MonoBehaviour
{
    private VehiclePerformanceSystem vps;
    private float lastHitTime = 0f;
    private float hitCooldown = 0.5f;

    void Start()
    {
        vps = GetComponent<VehiclePerformanceSystem>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (Time.time - lastHitTime < hitCooldown) return;
        if (collision.gameObject.CompareTag("Road")) return; // 忽略地面摩擦

        float relativeSpeed = collision.relativeVelocity.magnitude;
        float impulseMag = collision.impulse.magnitude;

        if (relativeSpeed < 1f && impulseMag < 5f) return;

        lastHitTime = Time.time;

        string myTag = gameObject.tag;
        string targetTag = collision.gameObject.tag;

        // 1. 接收整个 DamageResult 包裹，而不是单一的 float
        DamageResult result = DamageCalculator.CalculateDamage(myTag, targetTag, collision);

        // 2. 从包裹中取出 finalDamage，传给 1号的扣血接口 
        vps.AddDamage(result.finalDamage);

        // 3. 组装数据，通过事件总线发送给 3号成员
        DamageData data = new DamageData
        {
            // 从包裹中取出 visualImpactForce，确保剧情杀也有震撼特效 
            impactForce = result.visualImpactForce,
            hitPoint = collision.contacts[0].point,
            hitNormal = collision.contacts[0].normal,
            victim = this.gameObject,
            hitPart = collision.contacts[0].thisCollider
        };
        SimulationEvents.TriggerVehicleDamage(data);
    }
}