using UnityEngine;
using UnityEngine.UI;

public class HealthSystem : MonoBehaviour
{
    [Header("血条颜色设置")]
    public Color fullHealthColor = Color.red;    // 满血颜色：红色
    public Color lowHealthColor = Color.white;   // 空血颜色：白色

    [Header("生命值设置")]
    public int maxHitsToDie = 4;

    // 私有引用，脚本将自动查找子物体中的组件
    private Slider healthSlider;
    private Image fillImage;

    // *** 修改点 1: currentHits 保持私有 ***
    private int currentHits;

    private Animator animator;
    private bool isDead = false;

    // 动画 ID (假设您在 Animator Controller 中使用了 "IsDead" 参数)
    private int _animIDIsDead;

    // *** 修改点 2: 新增公共属性，供外部脚本读取当前生命值 ***
    public int CurrentHealth
    {
        get { return currentHits; }
    }
    // **********************************************

    void Start()
    {
        currentHits = maxHitsToDie;
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            // 假设死亡状态使用 SetBool("IsDead", true) 来触发
            _animIDIsDead = Animator.StringToHash("IsDead");
        }

        // 自动查找子物体中的血条组件 (Slider)
        healthSlider = GetComponentInChildren<Slider>(true);

        // 自动查找填充 Image 组件
        if (healthSlider != null && healthSlider.fillRect != null)
        {
            fillImage = healthSlider.fillRect.GetComponent<Image>();
        }

        // 初始化血条 UI：满值和满血颜色
        if (healthSlider != null)
        {
            healthSlider.maxValue = 1f;
            healthSlider.value = 1f;
        }
        if (fillImage != null)
        {
            fillImage.color = fullHealthColor;
        }
    }

    public void TakeDamage(int damageHits = 1)
    {
        if (isDead) return;

        currentHits -= damageHits;
        Debug.Log(gameObject.name + " 剩余生命值: " + currentHits);

        UpdateHealthUI();

        if (currentHits <= 0)
        {
            Die();
        }
        else
        {
            // TODO: 播放被击中反馈动画或音效
        }
    }

    private void UpdateHealthUI()
    {
        float healthRatio = (float)currentHits / maxHitsToDie;

        // 1. 更新 Slider 的值
        if (healthSlider != null)
        {
            healthSlider.value = healthRatio;
        }

        // 2. 更新填充颜色：从白色平滑过渡到红色
        if (fillImage != null)
        {
            fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, healthRatio);
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        // 触发死亡动画参数
        if (animator != null)
        {
            // 假设死亡动画使用 IsDead 参数
            animator.SetBool(_animIDIsDead, true);
        }

        // 死亡时，确保血条值和颜色最终为 0 和白色
        if (healthSlider != null)
        {
            healthSlider.value = 0f;
        }
        if (fillImage != null)
        {
            fillImage.color = lowHealthColor;
        }

        // 禁用碰撞体和移动，防止角色继续互动
        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null) mainCollider.enabled = false;

        // ----------------------------------------------------------------------
        // *** 关键添加点：调用 UIManager 显示玩家死亡界面 ***
        // ----------------------------------------------------------------------
        if (UIManager.Instance != null)
        {
            // 调用 ShowDeathUI() 来显示带有 "Restart" 和 "Exit" 按钮的 Canvas
            UIManager.Instance.ShowDeathUI();
        }
        else
        {
            Debug.LogError("玩家死亡：UIManager 实例未找到，无法显示死亡UI！请确保 UIManager 脚本已挂载并初始化。");
        }

        // *** 移除自动销毁逻辑 (保持不变) ***
        // Destroy(gameObject, 5f);  
    }

    public bool IsDead()
    {
        return isDead;
    }
}