using UnityEngine;
using System.Collections;
using UnityEngine.UI; // <-- 必须引入 UI 命名空间

public class PlayerMagicShield : MonoBehaviour
{
    // ===============================================
    // *** 配置字段 (保持不变) ***
    // ===============================================
    [Header("Shield Configuration")]
    [Tooltip("魔法阵的视觉效果对象 (GameObject)，需设为玩家的子物体。")]
    public GameObject MagicShieldVisual;
    [Tooltip("护盾持续时间 (秒)")]
    public float ShieldDuration = 10.0f;
    [Tooltip("护盾冷却时间 (秒)")]
    public float ShieldCooldown = 15.0f;

    // ===============================================
    // *** UI 字段 (新增) ***
    // ===============================================
    [Header("UI Display")]
    [Tooltip("显示魔法盾冷却/状态的 Text 组件")]
    public Text CooldownText;

    // ===============================================
    // *** 内部计时和状态字段 ***
    // ===============================================
    private bool _isShieldActive = false;

    // 护盾何时解除 (DeactivateShield 发生的时间点)
    private float _shieldEndTime = 0f;

    // 护盾何时可用 (Cooldown 结束的时间点)
    private float _nextAvailableTime = 0f;

    // ----------------------------------------
    // Start / Update
    // ----------------------------------------

    void Start()
    {
        // 确保魔法阵视觉效果在游戏开始时不可见
        if (MagicShieldVisual != null)
        {
            MagicShieldVisual.SetActive(false);
        }

        // 初始 UI 状态
        UpdateCooldownUI();
    }

    void Update()
    {
        // 1. 检查护盾持续时间是否结束
        if (_isShieldActive && Time.time >= _shieldEndTime)
        {
            // 自动解除护盾
            DeactivateShield();
        }

        // 2. 按 K 键尝试激活护盾
        if (Input.GetKeyDown(KeyCode.K))
        {
            ActivateShield();
        }

        // 3. 实时更新冷却 UI
        UpdateCooldownUI();
    }

    // ----------------------------------------
    // 核心逻辑方法
    // ----------------------------------------

    /// <summary>
    /// 供其他脚本调用的方法，用于查询护盾是否激活。
    /// </summary>
    public bool IsShieldActive()
    {
        return _isShieldActive;
    }

    private void ActivateShield()
    {
        // 检查是否在冷却中
        if (Time.time < _nextAvailableTime)
        {
            return;
        }

        _isShieldActive = true;

        // 设置护盾结束时间和下次可用时间
        _shieldEndTime = Time.time + ShieldDuration;
        _nextAvailableTime = _shieldEndTime + ShieldCooldown; // 冷却在持续时间结束后开始计算

        // 启用魔法阵视觉效果
        if (MagicShieldVisual != null)
        {
            MagicShieldVisual.SetActive(true);
        }

        Debug.Log($"✨ 魔法护盾激活！免疫伤害 {ShieldDuration} 秒。");
        UpdateCooldownUI(); // 立即更新 UI
    }

    private void DeactivateShield()
    {
        if (!_isShieldActive) return;

        _isShieldActive = false;

        // 禁用魔法阵视觉效果
        if (MagicShieldVisual != null)
        {
            MagicShieldVisual.SetActive(false);
        }

        Debug.Log("✨ 魔法护盾解除。开始冷却。");
        UpdateCooldownUI(); // 立即更新 UI
    }

    // ----------------------------------------
    // UI 逻辑方法 (根据要求修改)
    // ----------------------------------------

    private void UpdateCooldownUI()
    {
        if (CooldownText == null) return;

        if (_isShieldActive)
        {
            // 状态：持续中 (蓝色) - 只显示数字
            float remainingDuration = Mathf.Max(0f, _shieldEndTime - Time.time);
            CooldownText.text = remainingDuration.ToString("F1");
            CooldownText.color = Color.blue; // 蓝色为剩余时间倒计时
        }
        else if (Time.time < _nextAvailableTime)
        {
            // 状态：冷却中 (红色) - 显示文字 (包含标签和倒计时)
            float remainingCooldown = Mathf.Max(0f, _nextAvailableTime - Time.time);
            CooldownText.text = "CD:" + remainingCooldown.ToString("F1") + "s";
            CooldownText.color = Color.red; // 红色为冷却倒计时
        }
        else
        {
            // 状态：可用 (黑色) - 显示 "OK"
            CooldownText.text = "OK";
            CooldownText.color = Color.black; // 冷却完成为黑色
        }
    }
}