using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Animator animator;
    public float maxMovementSpeed = 5f; // 角色的最大移动速度

    void Start()
    {
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // 获取玩家输入（例如 WASD 键）
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 计算当前移动速度的标量 (Magnitude)
        Vector3 movement = new Vector3(horizontal, 0f, vertical);
        float currentSpeed = movement.magnitude;

        // 将实际速度映射到 Animator Controller 需要的 0.0 到 1.0 范围
        // 如果您的 Blend Tree 阈值是 0.0 和 1.0，则将 currentSpeed 限制在 0 到 1 即可。
        // 如果您使用了 0.0 和 2.0 (包含跑步)，则需要进行映射。

        // 简单映射到 0 到 1 的范围 (假设您只有 Idle 和 Walk)
        float animSpeed = Mathf.Clamp01(currentSpeed);

        // 更新 Animator 中的 Speed 参数
        animator.SetFloat("Speed", animSpeed);

        // TODO: 实际的移动代码 (例如使用 Rigidbody 或 CharacterController)
        // transform.Translate(movement.normalized * maxMovementSpeed * Time.deltaTime);
    }
}