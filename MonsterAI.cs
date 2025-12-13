using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.UI; // 引入 UI 命名空间 (保留用于 Slider/Image)

// 确保怪物上有 NavMeshAgent 和 PatrolAutoBind
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(PatrolAutoBind))]
public class MonsterAI : MonoBehaviour
{
    // ===============================================
    // *** 集成 HealthSystem 的字段 (保留 UI 引用) ***
    // ===============================================
    [Header("Monster Health System")]
    public Color fullHealthColor = Color.red;    // 满血颜色：红色
    public Color lowHealthColor = Color.white;   // 空血颜色：白色
    public int maxHitsToDie = 4; // 怪物最大承受伤害次数

    private Slider healthSlider;
    private Image fillImage;
    private int currentHits;

    // 动画 ID (假设您在 Animator Controller 中使用了 "IsDead" 参数)
    private int _animIDIsDead;

    public int CurrentHealth
    {
        get { return currentHits; }
    }
    // ===============================================

    [Header("AI Settings")]
    [Tooltip("怪物移动速度")]
    public float moveSpeed = 2.0f;
    [Tooltip("怪物能够发现玩家的范围(米)")]
    public float chaseRange = 4.0f;

    [HideInInspector] public int patrolSteps = 3;
    [HideInInspector] public LayerMask obstacleMask;

    // ----------------------------------------
    // 战斗和动画设置
    // ----------------------------------------
    [Header("Combat Settings")]
    public float AttackCooldown = 1.0f; // 怪物攻击频率 (秒)
    public float AttackDistance = 1.0f; // 实际攻击伤害判定的距离
    private float nextAttackTime = 0f;
    private int animIDAttack;
    private int animIDMoveSpeed;
    private Animator monsterAnimator;
    private HealthSystem playerHealth;   // 用于直接攻击玩家

    // *** 玩家强制攻击判定参数 ***
    [Header("Player Interaction")]
    [Tooltip("玩家攻击的强制命中距离 (米)。用于绕过玩家动画事件的调试。")]
    public float PlayerHitCheckRange = 1.0f;

    // *** 新增变量：用于代码强制赋值 Controller ***
    [Header("Animation Debug (Force Controller)")]
    [Tooltip("请将你的 Monster_AnimController 文件拖入此槽位，以防 Inspector 链接丢失。")]
    public RuntimeAnimatorController debugAnimatorController;

    // *** 攻击循环和后退设置 ***
    [Header("Attack Cycle")]
    [Tooltip("怪物连续攻击的最大次数")]
    public int maxAttackCount = 4;
    [Tooltip("怪物后退的距离")]
    public float retreatDistance = 1.0f;
    [Tooltip("怪物后退的速度")]
    public float retreatSpeed = 1.5f;
    [Tooltip("后退完成后，怪物停顿的时间 (秒)，避免立即攻击")]
    public float retreatPauseTime = 0.5f;

    private int attackCount = 0;
    private bool isRetreating = false;
    private Vector3 retreatTarget;
    private float retreatCompleteTime = 0f;

    // *** 预警系统 (重新添加 GameObject 引用，只用于 SetActive) ***
    [Header("Warning System (Simple Toggle)")]
    [Tooltip("请拖入场景中的警告 Canvas 对象。AI只会控制其开关。")]
    public GameObject WarningIndicator; // <--- 重新添加

    [Tooltip("预警持续时间 (秒)，之后怪物开始追逐。")]
    public float WarningDuration = 1.0f;

    private bool isWarning = false;
    private float warningEndTime = 0f;

    // ----------------------------------------
    // 私有变量和状态
    // ----------------------------------------
    private Transform playerTransform;
    private NavMeshAgent agent;
    private PatrolAutoBind patrolScript;

    private bool isChasing = false;
    private bool isDead = false;
    private bool isAttacking = false;
    private bool aiActive = true;

    // ----------------------------------------
    // Awake / Start / Update
    // ----------------------------------------

    void Awake()
    {
        monsterAnimator = GetComponent<Animator>();

        if (monsterAnimator != null && monsterAnimator.runtimeAnimatorController == null && debugAnimatorController != null)
        {
            monsterAnimator.runtimeAnimatorController = debugAnimatorController;
            Debug.Log("MonsterAI: 在 Awake 中强制赋值 Animator Controller，尝试修复运行时引用。");
        }

        if (monsterAnimator != null)
        {
            _animIDIsDead = Animator.StringToHash("IsDead");
        }
    }

    void Start()
    {
        // *** 初始化生命值和 UI (血条 UI 查找) ***
        currentHits = maxHitsToDie;

        healthSlider = GetComponentInChildren<Slider>(true);
        if (healthSlider != null && healthSlider.fillRect != null)
        {
            fillImage = healthSlider.fillRect.GetComponent<Image>();
        }

        if (healthSlider != null)
        {
            healthSlider.maxValue = 1f;
            healthSlider.value = 1f;
        }
        if (fillImage != null)
        {
            fillImage.color = fullHealthColor;
        }
        // **********************************************

        // 获取依赖组件
        playerTransform = FindObjectOfType<LegacyThirdPersonController>()?.transform;

        agent = GetComponent<NavMeshAgent>();
        patrolScript = GetComponent<PatrolAutoBind>();

        if (playerTransform == null || monsterAnimator == null || agent == null || patrolScript == null)
        {
            Debug.LogError("MonsterAI: 缺少关键组件。AI逻辑已禁用。");
            enabled = false;
            return;
        }

        playerHealth = playerTransform.GetComponent<HealthSystem>();
        if (playerHealth == null)
        {
            Debug.LogError("MonsterAI: 玩家缺少 HealthSystem 组件。AI逻辑已禁用。");
            enabled = false;
            return;
        }

        if (monsterAnimator.runtimeAnimatorController == null)
        {
            Debug.LogWarning("MonsterAI: 警告！Animator Controller 引用仍为空，动画将无法激活，但 AI 继续运行。");
        }

        // 配置 NavMeshAgent
        agent.speed = moveSpeed;
        agent.stoppingDistance = AttackDistance;

        animIDAttack = Animator.StringToHash("Attack");
        animIDMoveSpeed = Animator.StringToHash("Speed");

        patrolScript.enabled = true;

        // 预警指示器初始状态：隐藏
        if (WarningIndicator != null)
        {
            WarningIndicator.SetActive(false); // <--- 重新添加 SetActive(false)
        }

        transform.LookAt(new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z));
    }

    void Update()
    {
        if (!aiActive || isDead || playerHealth.IsDead())
        {
            agent.isStopped = true;
            if (WarningIndicator != null) WarningIndicator.SetActive(false); // 确保在AI禁用时关闭预警UI
            return;
        }

        if (CurrentHealth <= 0 && !isDead)
        {
            Die();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);


        // ----------------------------------------
        // *** 玩家强制攻击判定逻辑 (保持不变) ***
        // ----------------------------------------

        if (Input.GetKeyDown(KeyCode.Space) && !isDead && playerTransform != null && !playerHealth.IsDead())
        {
            if (distanceToPlayer <= PlayerHitCheckRange)
            {
                Vector3 playerForward = playerTransform.forward;
                Vector3 directionFromPlayerToMonster = (transform.position - playerTransform.position);

                playerForward.y = 0;
                directionFromPlayerToMonster.y = 0;

                float angle = Vector3.Angle(playerForward, directionFromPlayerToMonster);

                if (angle <= 50f)
                {
                    TakeDamage(1);
                    Debug.Log($"SUCCESS (AI Forced Check): Hit by Player Spacebar. Distance: {distanceToPlayer:F2}m. Angle: {angle:F1}°");
                }
            }
        }
        // ****************************************************

        // ----------------------------------------
        // *** 预警状态处理 (只控制开关和计时) ***
        if (isWarning)
        {
            // 确保 UI 开启
            if (WarningIndicator != null && !WarningIndicator.activeSelf)
            {
                WarningIndicator.SetActive(true); // <--- 确保开启
                Debug.Log("MonsterAI: 预警 UI 激活。");
            }

            if (Time.time >= warningEndTime)
            {
                // 预警结束，开始追逐
                isWarning = false;

                // 确保预警指示器隐藏
                if (WarningIndicator != null) WarningIndicator.SetActive(false); // <--- 确保关闭

                // 切换到追逐状态
                isChasing = true;
                patrolScript.enabled = false;
                Debug.Log("MonsterAI: 预警结束，开始追逐！");

                return;
            }
            // 预警期间，怪物保持静止和Idle动画
            agent.isStopped = true;
            if (monsterAnimator != null) monsterAnimator.SetFloat(animIDMoveSpeed, 0f);
            return;
        }
        // ----------------------------------------

        // *** 状态切换逻辑: 发现玩家，进入预警 ***
        if (distanceToPlayer <= chaseRange && !isChasing)
        {
            // 发现玩家，进入预警状态
            isWarning = true;
            warningEndTime = Time.time + WarningDuration;

            // UI 将在下一帧的 isWarning 循环中被 SetActive(true)

            agent.isStopped = true; // 预警期间停止移动
            patrolScript.enabled = false; // 停止巡逻

            Debug.Log("MonsterAI: 发现玩家！进入预警状态。");
            return; // 预警期间停止其他 Update 逻辑
        }


        // ----------------------------------------
        // 追逐/巡逻逻辑
        // ----------------------------------------

        bool isAnimatorReady = monsterAnimator != null && monsterAnimator.enabled && monsterAnimator.runtimeAnimatorController != null;


        if (isChasing)
        {
            ChaseAndAttackPlayer(distanceToPlayer);
        }
        else
        {
            // 巡逻逻辑
            float velocityMagnitude = agent.velocity.magnitude;
            float speedValue = velocityMagnitude > 0.05f ? 1f : 0f;

            if (isAnimatorReady) monsterAnimator.SetFloat(animIDMoveSpeed, speedValue);
        }
    }

    // ===============================================
    // *** 集成 HealthSystem 的方法 (保留 UI 更新逻辑) ***
    // ===============================================

    /// <summary>
    /// 怪物受击接口，取代 HealthSystem.TakeDamage()。
    /// </summary>
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

    /// <summary>
    /// 怪物死亡逻辑，取代 HealthSystem.Die()。
    /// </summary>
    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (monsterAnimator != null)
        {
            monsterAnimator.SetBool(_animIDIsDead, true);
        }

        // 死亡时，确保血条值和颜色最终为 0 和白色 (保留血条 UI 逻辑)
        if (healthSlider != null)
        {
            healthSlider.value = 0f;
        }
        if (fillImage != null)
        {
            fillImage.color = lowHealthColor;
        }

        // 确保预警 UI 关闭
        if (WarningIndicator != null) WarningIndicator.SetActive(false);

        // 禁用碰撞体
        Collider mainCollider = GetComponent<Collider>();
        if (mainCollider != null) mainCollider.enabled = false;

        HandleDeath();
    }

    // ===============================================
    // *** MonsterAI 原始方法 (保持不变) ***
    // ===============================================

    private void ChaseAndAttackPlayer(float distanceToPlayer)
    {
        // 1. 怪物必须面向玩家
        Vector3 lookDir = playerTransform.position - transform.position;
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 15f);
        }

        // 2. 判断是否在后退状态
        if (isRetreating)
        {
            HandleRetreat();
            return;
        }

        // 3. 后退后的停顿时间
        if (Time.time < retreatCompleteTime)
        {
            agent.isStopped = true;
            if (monsterAnimator != null) monsterAnimator.SetFloat(animIDMoveSpeed, 0f);
            return;
        }

        // 4. 判断是否在攻击距离内 (只有非后退状态才攻击)
        if (distanceToPlayer <= AttackDistance)
        {
            // 停止移动，准备攻击
            agent.isStopped = true;
            if (monsterAnimator != null) monsterAnimator.SetFloat(animIDMoveSpeed, 0f);

            if (Time.time >= nextAttackTime && !isAttacking)
            {
                // 检查是否达到最大攻击次数
                if (attackCount >= maxAttackCount)
                {
                    StartRetreat(); // 达到次数，开始后退
                }
                else
                {
                    AttackPlayer();
                    attackCount++; // 攻击次数增加
                    nextAttackTime = Time.time + AttackCooldown;
                }
            }
        }
        else
        {
            // 追逐玩家
            agent.isStopped = false;
            agent.SetDestination(playerTransform.position);
            if (monsterAnimator != null) monsterAnimator.SetFloat(animIDMoveSpeed, 1f);

            // 重置计数器：如果怪物必须追逐（玩家拉远了距离），则重置攻击计数器
            if (attackCount > 0)
            {
                attackCount = 0;
            }

            // *** 核心修改：如果玩家跑出追逐范围，退出追逐状态 (原始逻辑) ***
            if (distanceToPlayer > chaseRange)
            {
                isChasing = false;
                patrolScript.enabled = true;
                Debug.Log($"MonsterAI ({gameObject.name}): 玩家跑远，退出追逐状态。");
            }
        }
    }

    // *** 攻击循环逻辑方法 (保持不变) ***

    private void StartRetreat()
    {
        isRetreating = true;
        attackCount = 0; // 重置攻击计数器

        // 计算后退目标点 (沿远离玩家的方向后退 retreatDistance)
        Vector3 directionAwayFromPlayer = (transform.position - playerTransform.position).normalized;

        // 使用 NavMesh.SamplePosition 确保目标点在 NavMesh 上
        if (NavMesh.SamplePosition(transform.position + directionAwayFromPlayer * retreatDistance, out NavMeshHit hit, retreatDistance, NavMesh.AllAreas))
        {
            retreatTarget = hit.position;
        }
        else
        {
            // 如果找不到合法的后退点，强制后退到当前位置，立即结束后退阶段
            retreatTarget = transform.position;
        }


        // 设置 NavMesh Agent 速度和目的地
        agent.isStopped = false;
        agent.speed = retreatSpeed;
        agent.SetDestination(retreatTarget);
        Debug.Log("MonsterAI: 完成攻击循环，开始后退。目标距离：" + retreatDistance);
    }

    private void HandleRetreat()
    {
        // 动画同步：播放走路/后退动画
        if (monsterAnimator != null) monsterAnimator.SetFloat(animIDMoveSpeed, agent.velocity.magnitude > 0.05f ? 1f : 0f);

        // 检查是否到达后退目标点 (使用 NavMesh Agent 的 Remaining Distance)
        // 使用 StoppingDistance 避免怪物在到达目标前停下
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            // 到达后退目标点，停止后退，进入停顿状态
            isRetreating = false;
            agent.isStopped = true;
            agent.speed = moveSpeed; // 恢复正常追逐速度
            retreatCompleteTime = Time.time + retreatPauseTime; // 设置停顿结束时间
            Debug.Log("MonsterAI: 后退完成，停顿 " + retreatPauseTime + " 秒。");
        }
    }

    private void AttackPlayer()
    {
        if (playerHealth == null || playerHealth.IsDead()) return;

        isAttacking = true;

        if (monsterAnimator != null && monsterAnimator.runtimeAnimatorController != null)
        {
            monsterAnimator.SetTrigger(animIDAttack);
        }

        StartCoroutine(DealDamageAfterDelay(0.3f, 1));

        Debug.Log("Monster attempted to attack Player!");
    }

    private IEnumerator DealDamageAfterDelay(float delay, int damage)
    {
        yield return new WaitForSeconds(delay);

        isAttacking = false;

        // *** 核心修复：检查玩家是否开启了魔法护盾 ***
        // 确保 PlayerMagicShield 类在项目中存在，且已挂载到玩家对象
        PlayerMagicShield playerShield = playerTransform.GetComponent<PlayerMagicShield>();
        bool isShielded = playerShield != null && playerShield.IsShieldActive();

        // 检查伤害判定
        if (Vector3.Distance(transform.position, playerTransform.position) <= AttackDistance + 0.1f && !playerHealth.IsDead())
        {
            if (isShielded)
            {
                Debug.Log("Monster dealt 0 damage. Player magic shield active!");
            }
            else
            {
                playerHealth.TakeDamage(damage);
                Debug.Log($"Monster dealt {damage} damage to Player.");
            }
        }
    }

    private void HandleDeath()
    {
        aiActive = false;

        agent.isStopped = true;

        if (monsterAnimator != null && monsterAnimator.runtimeAnimatorController != null)
        {
            monsterAnimator.SetTrigger("Death");
        }

        StartCoroutine(DestroyAfterDelay(3f));
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isChasing ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, AttackDistance);

        // 绘制玩家强制攻击检测范围
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(playerTransform != null ? playerTransform.position : transform.position, PlayerHitCheckRange);

        if (isRetreating)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(retreatTarget, 0.5f);
            Gizmos.DrawLine(transform.position, retreatTarget);
        }

        // 绘制预警范围
        if (isWarning)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.75f);
        }
    }
}