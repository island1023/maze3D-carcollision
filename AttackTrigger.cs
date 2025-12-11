using UnityEngine;
using System.Collections.Generic;

public class AttackTrigger : MonoBehaviour
{
    [HideInInspector]
    public int damageAmount = 1;

    private HashSet<GameObject> hitTargets = new HashSet<GameObject>();
    private GameObject attackerRoot;
    private bool isPlayerAttacker = false;

    // *** 确保这些值与你的 Unity 标签完全匹配 ***
    private const string PlayerTag = "Player";
    private const string MonsterTag = "Agent"; // 假设怪物的标签是 Agent

    private const string WallTag = "Wall";
    private const string GroundTag = "Ground";


    void Start()
    {
        attackerRoot = transform.root.gameObject;

        if (attackerRoot.CompareTag(PlayerTag) || attackerRoot.GetComponent<LegacyThirdPersonController>() != null)
        {
            isPlayerAttacker = true;
        }

        hitTargets.Clear();
        Debug.Log($"Attacker Initialized. Name: {attackerRoot.name}, Is Player: {isPlayerAttacker}");
    }

    /// <summary>
    /// 公共方法：在每次攻击动画开始时，由主控制器调用，清空已击中列表。
    /// </summary>
    public void ResetHitTargets()
    {
        hitTargets.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        // 核心：获取目标根对象（HealthSystem 所在的最高层级）
        GameObject targetRoot = other.transform.root.gameObject;

        // 1. 忽略攻击发起者自己
        if (targetRoot == attackerRoot)
        {
            return;
        }

        // 2. 忽略环境对象和无标签对象
        if (other.CompareTag(WallTag) || other.CompareTag(GroundTag) || other.CompareTag("Untagged"))
        {
            return;
        }

        // ----------------------------------------------------
        // *** 关键诊断点：确认目标标签和攻击者类型 ***
        // ----------------------------------------------------

        bool isTargetValid = false;

        if (isPlayerAttacker && targetRoot.CompareTag(MonsterTag))
        {
            // 玩家攻击怪物 (Player -> Agent)
            isTargetValid = true;
        }
        else if (!isPlayerAttacker && targetRoot.CompareTag(PlayerTag))
        {
            // 怪物攻击玩家 (Agent -> Player)
            isTargetValid = true;
        }

        if (!isTargetValid)
        {
            // 如果过滤失败，打印详细信息
            Debug.LogWarning($"Attack Filter Failed. Attacker is Player: {isPlayerAttacker}. Target Root Tag: {targetRoot.tag}. Expected Tag: {(isPlayerAttacker ? MonsterTag : PlayerTag)}.");
            return;
        }


        // 4. 检查是否击中了带有 HealthSystem 的目标
        HealthSystem targetHealth = targetRoot.GetComponent<HealthSystem>();

        // 确保击中了 HealthSystem 且目标不在已击中列表中
        if (targetHealth != null && !hitTargets.Contains(targetRoot))
        {
            // *** 免疫检查 (只在怪物攻击玩家时检查护盾) ***
            if (!isPlayerAttacker)
            {
                PlayerMagicShield playerShield = targetRoot.GetComponent<PlayerMagicShield>();

                if (playerShield != null && playerShield.IsShieldActive())
                {
                    Debug.Log("Monster attack blocked by Player Magic Shield!");
                    hitTargets.Add(targetRoot);
                    return; // 免疫伤害
                }
            }

            // 目标受到伤害
            targetHealth.TakeDamage(damageAmount);

            // 将目标添加到已击中列表
            hitTargets.Add(targetRoot);

            Debug.Log($"SUCCESS: {attackerRoot.name} ({attackerRoot.tag}) caused {damageAmount} damage to {targetRoot.name} ({targetRoot.tag}).");
        }
        else if (targetHealth == null)
        {
            Debug.LogWarning($"Target {targetRoot.name} lacks HealthSystem, attack ignored.");
        }
    }
}