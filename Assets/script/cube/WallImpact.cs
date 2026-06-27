using UnityEngine;

/// <summary>
/// 挂载位置：完好的墙壁模型（必须带有 BoxCollider, Tag 设置为 Wall）
/// </summary>
public class WallImpact : MonoBehaviour
{
    [Header("--- 碎片预制体设置 ---")]
    [Tooltip("包含几百个 MeshRenderer 碎片的根节点 Prefab")]
    public GameObject fracturedPrefab;

    [Header("--- 爆炸受力参数 ---")]
    public float explosionForce = 15f; // 使用 Impulse 模式，数值不需要极大
    public float explosionRadius = 8f;

    private bool isBroken = false;

    void OnCollisionEnter(Collision collision)
    {
        if (isBroken) return;

        // 检测撞击者是否为车辆
        if (collision.gameObject.CompareTag("Truck") || collision.gameObject.CompareTag("SUV") || collision.gameObject.CompareTag("Sedan"))
        {
            isBroken = true;

            // 1. 发送全局事件给 3号成员（生成撞墙瞬间的巨大烟尘）
            float impactForce = collision.impulse.magnitude / Time.fixedDeltaTime;
            Vector3 hitPoint = collision.contacts[0].point;
            SimulationEvents.TriggerEnvironmentDestroy(this.gameObject, hitPoint, impactForce);

            // 2. 隐藏完好的墙壁
            this.gameObject.SetActive(false);

            // 3. 生成破碎模型
            if (fracturedPrefab != null)
            {
                GameObject fracturedObj = Instantiate(fracturedPrefab, transform.position, transform.rotation);

                // 4. 获取碎片管理器，并下达“注入物理并炸开”的指令
                DebrisManager manager = fracturedObj.GetComponent<DebrisManager>();
                if (manager != null)
                {
                    manager.ExplodeAndScatter(hitPoint, explosionForce, explosionRadius);
                }
                else
                {
                    Debug.LogError("碎片预制体根节点缺少 DebrisManager 脚本！");
                }
            }
        }
    }
}