using UnityEngine;

public class EnvironmentImpact : MonoBehaviour
{
    private bool isBroken = false;
    private Rigidbody rb;

    // 内部存储获取到的模型节点
    private Transform goodModel;
    private Transform damageModel;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // 动态遍历所有子物体，通过 Tag 识别完好模型和损坏模型
        foreach (Transform child in transform)
        {
            if (child.CompareTag("Good")) goodModel = child;
            if (child.CompareTag("Damage")) damageModel = child;
        }

        // 确保初始状态下，只有 Good 模型显示
        if (goodModel != null) goodModel.gameObject.SetActive(true);
        if (damageModel != null) damageModel.gameObject.SetActive(false);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isBroken) return;

        float impactForce = collision.impulse.magnitude;
        string targetTag = gameObject.tag;
        string attackerTag = collision.gameObject.tag;

        bool shouldBreak = false;

        // 树木或路灯的破坏判定
        if (targetTag == "Tree" || targetTag == "Streetlight")
        {
            if (impactForce > 20f || attackerTag == "SUV" || attackerTag == "Truck" || attackerTag == "Sedan")
            {
                shouldBreak = true;
            }
        }
        else if (targetTag == "Wall")
        {
            if (impactForce > 40f || attackerTag == "SUV" || attackerTag == "Truck" || attackerTag == "Sedan")
            {
                shouldBreak = true;
            }
        }

        // 执行破坏与模型替换逻辑
        if (shouldBreak)
        {
            isBroken = true;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddForceAtPosition(collision.impulse * 0.2f, collision.contacts[0].point, ForceMode.Impulse);
            }

            // 核心修复：关掉 Good 模型，开启 Damage 模型
            if (goodModel != null) goodModel.gameObject.SetActive(false);
            if (damageModel != null) damageModel.gameObject.SetActive(true);

            SimulationEvents.TriggerEnvironmentDestroy(gameObject, collision.contacts[0].point, impactForce);
        }
    }
}