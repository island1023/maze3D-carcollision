using UnityEngine;

/// <summary>
/// 挂载到树木根物体，为所有叶子（Tag = "Leaf"）添加物理摆动组件
/// </summary>
public class TreeLeafPhysicsSetup : MonoBehaviour
{
    [Header("叶子物理参数")]
    public float leafMass = 0.02f;
    public float sphereRadius = 0.05f;
    public bool useGravity = true;

    [Header("Joint 弹簧参数")]
    public float spring = 50f;      // 弹性刚度
    public float damper = 5f;       // 阻尼
    public float maxForce = 100f;   // 最大力

    [Header("执行")]
    public bool autoSetupOnStart = true;

    void Start()
    {
        if (autoSetupOnStart) SetupAllLeaves();
    }

    [ContextMenu("为所有叶子添加物理摆动组件")]
    public void SetupAllLeaves()
    {
        int setupCount = 0;
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child == transform) continue;
            if (child.CompareTag("Leaf"))
            {
                SetupLeaf(child);
                setupCount++;
            }
        }
        Debug.Log($"已为 {setupCount} 个叶子添加物理摆动组件");
    }

    private void SetupLeaf(Transform leaf)
    {
        // 1. SphereCollider（非触发器，用于碰撞）
        SphereCollider col = leaf.gameObject.GetComponent<SphereCollider>();
        if (col == null) col = leaf.gameObject.AddComponent<SphereCollider>();
        col.radius = sphereRadius;
        col.isTrigger = false;

        // 2. Rigidbody
        Rigidbody rb = leaf.gameObject.GetComponent<Rigidbody>();
        if (rb == null) rb = leaf.gameObject.AddComponent<Rigidbody>();
        rb.mass = leafMass;
        rb.useGravity = useGravity;
        rb.isKinematic = false;
        // 可选：冻结位置移动（只允许旋转摆动）
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;

        // 3. ConfigurableJoint（连接到父物体）
        ConfigurableJoint joint = leaf.gameObject.GetComponent<ConfigurableJoint>();
        if (joint == null) joint = leaf.gameObject.AddComponent<ConfigurableJoint>();

        // 连接父物体（如果父物体有 Rigidbody 则连接，否则连接世界）
        Rigidbody parentRb = leaf.parent.GetComponent<Rigidbody>();
        joint.connectedBody = parentRb;

        // 锁定所有位移自由度
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;

        // 允许所有旋转自由度（可摆动）
        joint.angularXMotion = ConfigurableJointMotion.Limited;
        joint.angularYMotion = ConfigurableJointMotion.Limited;
        joint.angularZMotion = ConfigurableJointMotion.Limited;

        // 设置弹簧驱动（使叶片受外力后回正）
        JointDrive drive = new JointDrive
        {
            positionSpring = spring,
            positionDamper = damper,
            maximumForce = maxForce
        };
        joint.slerpDrive = drive;

        Debug.Log($"叶子 {leaf.name} 设置完成（质量={leafMass}, 弹簧={spring}）");
    }
}