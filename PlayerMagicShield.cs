using UnityEngine;
using System.Collections;

public class PlayerMagicShield : MonoBehaviour
{
    [Header("Shield Configuration")]
    [Tooltip("魔法阵的视觉效果对象 (GameObject)，需设为玩家的子物体。")]
    public GameObject MagicShieldVisual;
    [Tooltip("护盾持续时间 (秒)")]
    public float ShieldDuration = 10.0f;
    [Tooltip("护盾冷却时间 (秒)")]
    public float ShieldCooldown = 15.0f; // 冷却时间应大于持续时间

    private bool _isShieldActive = false;
    private bool _onCooldown = false;

    void Start()
    {
        // 确保魔法阵视觉效果在游戏开始时不可见
        if (MagicShieldVisual != null)
        {
            MagicShieldVisual.SetActive(false);
        }
    }

    void Update()
    {
        // 按 K 键尝试激活护盾
        if (Input.GetKeyDown(KeyCode.K))
        {
            ActivateShield();
        }
    }

    /// <summary>
    /// 供其他脚本调用的方法，用于查询护盾是否激活。
    /// </summary>
    public bool IsShieldActive()
    {
        return _isShieldActive;
    }

    private void ActivateShield()
    {
        if (_isShieldActive || _onCooldown)
        {
            Debug.Log($"Shield unavailable. Active: {_isShieldActive}, Cooldown: {_onCooldown}");
            return;
        }

        _isShieldActive = true;
        _onCooldown = true;

        // 启用魔法阵视觉效果
        if (MagicShieldVisual != null)
        {
            MagicShieldVisual.SetActive(true);
        }

        Debug.Log($"✨ 魔法护盾激活！免疫伤害 {ShieldDuration} 秒。");

        // 启动持续时间计时器
        Invoke(nameof(DeactivateShield), ShieldDuration);

        // 启动冷却计时器 (在持续时间结束后开始计算冷却)
        Invoke(nameof(EndCooldown), ShieldDuration + ShieldCooldown);
    }

    private void DeactivateShield()
    {
        _isShieldActive = false;

        // 禁用魔法阵视觉效果
        if (MagicShieldVisual != null)
        {
            MagicShieldVisual.SetActive(false);
        }

        Debug.Log("✨ 魔法护盾解除。开始冷却。");
    }

    private void EndCooldown()
    {
        _onCooldown = false;
        Debug.Log("✨ 魔法护盾冷却结束，可再次使用。");
    }
}