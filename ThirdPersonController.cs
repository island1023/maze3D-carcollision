using UnityEngine;
using System.Collections.Generic;
using System.Collections; // 引入 IEnumerator

// 确保该脚本挂载在具有 CharacterController 组件的游戏对象上
[RequireComponent(typeof(CharacterController))]
public class LegacyThirdPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("角色最大移动速度 (米/秒)")]
    public float MoveSpeed = 5.0f;
    [Tooltip("角色旋转平滑时间。值越小，旋转越快。例如 0.12f")]
    public float RotationSmoothTime = 0.12f;
    [Tooltip("重力加速度")]
    public float Gravity = -20.0f;

    [Header("Fixed Rotation Settings")]
    [Tooltip("左右箭头每次按下的旋转角度")]
    public float FixedRotationAngle = 90.0f;

    [Header("Path Tracking Settings")]
    public LineRenderer PathLineRenderer;
    [Tooltip("每隔多少距离记录一个路径点")]
    public float MinDistanceForNewPoint = 0.5f;

    // ===============================================
    // 攻击设置
    // ===============================================
    [Header("Combat Settings")]
    [Tooltip("攻击伤害值")]
    public int AttackDamage = 1;

    // *** 攻击范围设置 (2.0f 对应 1.0m 半径) ***
    [Tooltip("玩家前方检测攻击的最大有效距离（米）。设置 2.0f 可实现 1.0m 半径的检测球。")]
    public float AttackRange = 2.0f;

    // *** 核心修改：用于抵消动画位移 ***
    [Tooltip("攻击动画产生的向前位移距离。代码将向后移动此距离进行抵消。")]
    public float AttackForwardDisplacement = 0.2f; // 抵消值 (可在 Inspector 中调整)
    private Vector3 _attackStartPosition; // 用于 StartAttackCheck 时的位置锁定

    // ----------------------------------------
    // 新增：超时机制参数
    // ----------------------------------------
    [Tooltip("攻击动画播放的最大允许时间 (秒)。如果超过此时间仍未解锁，则强制重置。")]
    public float AttackTimeoutDuration = 0.6f;

    // --- 新增攻击音效字段 ---
    [Header("Audio Settings")]
    [Tooltip("按下空格键时播放的攻击音效文件。")]
    public AudioClip AttackSound;

    // ----------------------------------------
    // 新增：速度调整和平滑参数
    // ----------------------------------------
    [Header("Speed Control")]
    [Tooltip("每次调整速度时的步长")]
    public float SpeedAdjustmentStep = 1.0f;
    [Tooltip("移动时的平滑加速时间")]
    public float AccelerationTime = 0.15f;
    [Tooltip("停止时的平滑减速时间")]
    public float DecelerationTime = 0.25f;

    // ----------------------------------------
    // 私有变量
    // ----------------------------------------

    private CharacterController _controller;
    private Animator _animator;

    private float _verticalVelocity;

    // 旋转和速度平滑变量
    private float _targetRotationY;
    private float _rotationDampVelocity;
    private float _speedDampVelocity;
    private float _currentSpeed = 0f;

    private bool _hasAnimator;
    private bool _isAttacking = false; // 用于阻止重复启动协程
    private Coroutine _attackTimeoutCoroutine;

    // 路径追踪变量
    private Vector3 _lastPosition;

    // 动画 ID
    private int _animIDSpeed;
    private int _animIDGrounded;
    private int _animIDAttack; // <-- Trigger ID

    // *** 用于代码攻击逻辑的已击中列表和标签常量 ***
    private HashSet<GameObject> _hitTargets = new HashSet<GameObject>();
    private const string MonsterTag = "Agent";

    // 假设 HealthSystem.cs 存在于项目中

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _hasAnimator = TryGetComponent(out _animator);

        // 初始化动画 ID
        if (_hasAnimator)
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDAttack = Animator.StringToHash("Attack");
        }

        _targetRotationY = transform.eulerAngles.y;

        InitializePathTracker();

        Debug.Log($"初始移动速度 MoveSpeed: {MoveSpeed:F1} m/s");
    }

    void Update()
    {
        HandleInputRotation();
        HandleSpeedAdjustment();
        HandleMovementAndGravity();
        HandleAttackInput();
        TrackPath();
    }

    // ---------------------------------------------------
    // HandleSpeedAdjustment 方法定义
    // ---------------------------------------------------
    private void HandleSpeedAdjustment()
    {
        bool speedChanged = false;

        if (Input.GetKeyDown(KeyCode.Equals))
        {
            MoveSpeed += SpeedAdjustmentStep;
            speedChanged = true;
        }

        if (Input.GetKeyDown(KeyCode.Minus))
        {
            MoveSpeed = Mathf.Max(0.1f, MoveSpeed - SpeedAdjustmentStep);
            speedChanged = true;
        }

        if (speedChanged)
        {
            Debug.Log($"移动速度 MoveSpeed 已调整为: {MoveSpeed:F1} m/s");
        }
    }

    // ---------------------------------------------------
    // InitializePathTracker 方法定义
    // ---------------------------------------------------
    private void InitializePathTracker()
    {
        if (PathLineRenderer != null)
        {
            _lastPosition = transform.position;
            PathLineRenderer.positionCount = 1;
            PathLineRenderer.SetPosition(0, _lastPosition);
        }
        else
        {
            Debug.LogWarning("PathLineRenderer is not assigned. Path tracking will be disabled.");
        }
    }

    // ---------------------------------------------------
    // TrackPath 方法定义
    // ---------------------------------------------------
    private void TrackPath()
    {
        if (PathLineRenderer == null) return;

        if (Vector3.Distance(transform.position, _lastPosition) >= MinDistanceForNewPoint)
        {
            _lastPosition = transform.position;
            PathLineRenderer.positionCount++;
            PathLineRenderer.SetPosition(PathLineRenderer.positionCount - 1, _lastPosition);
        }
    }

    // ---------------------------------------------------
    // HandleInputRotation 方法定义
    // ---------------------------------------------------
    private void HandleInputRotation()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _targetRotationY -= FixedRotationAngle;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            _targetRotationY += FixedRotationAngle;
        }

        float currentYAngle = transform.eulerAngles.y;

        float smoothedRotation = Mathf.SmoothDampAngle(
            currentYAngle,
            _targetRotationY,
            ref _rotationDampVelocity,
            RotationSmoothTime
        );

        transform.rotation = Quaternion.Euler(0.0f, smoothedRotation, 0.0f);
    }

    private void HandleAttackInput()
    {
        // 允许每次按空格都发送 Trigger，实现连击打断
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 1. 播放攻击音效
            if (AttackSound != null)
            {
                // 使用 PlayClipAtPoint 在角色位置播放音效
                AudioSource.PlayClipAtPoint(AttackSound, transform.position);
            }

            if (_hasAnimator)
            {
                // 2. 启动动画 (使用 Trigger)
                _animator.SetTrigger(_animIDAttack);

                // 3. 如果当前不在攻击状态，启动锁定和超时
                if (!_isAttacking)
                {
                    _isAttacking = true; // 锁定代码状态

                    // *** 核心修复：瞬时后退抵消动画位移 ***
                    if (_controller.isGrounded && AttackForwardDisplacement > 0.01f)
                    {
                        // 获取角色当前面向的反方向向量
                        Vector3 backwardDisplacement = -transform.forward * AttackForwardDisplacement;

                        // 使用 CharacterController.Move 执行瞬时后退抵消
                        _controller.Move(backwardDisplacement);

                        Debug.Log($"⚔️ 攻击开始，瞬时向后抵消位移: {AttackForwardDisplacement:F2}m");
                    }

                    _attackTimeoutCoroutine = StartCoroutine(AttackTimeout());
                }
            }
        }
    }

    // *** 攻击超时协程 ***
    private IEnumerator AttackTimeout()
    {
        yield return new WaitForSeconds(AttackTimeoutDuration);

        if (_isAttacking)
        {
            Debug.LogError($"⚔️ 攻击动画超时 ({AttackTimeoutDuration:F2}s)! 强制执行 EndAttackCheck()。");
            // 强制执行解锁逻辑
            EndAttackCheck();
        }
    }


    private void HandleMovementAndGravity()
    {
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        Vector3 inputDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;
        float inputMagnitude = inputDirection.magnitude; // 0.0 或 1.0

        if (_controller.isGrounded)
        {
            _verticalVelocity = -0.5f;
            if (_hasAnimator) _animator.SetBool(_animIDGrounded, true);
        }
        else
        {
            _verticalVelocity += Gravity * Time.deltaTime;
            if (_hasAnimator) _animator.SetBool(_animIDGrounded, false);
        }

        Vector3 worldMoveDirection;
        float targetSpeed;
        float smoothTime;

        // ---------------------------------------------------
        // 核心修复：攻击时强制停止所有水平位移
        if (_isAttacking)
        {
            worldMoveDirection = Vector3.zero; // 无论输入如何，方向都为零
            targetSpeed = 0f;                  // 目标速度强制为零
            inputMagnitude = 0f;               // 动画速度也设为零 (Idle)
            _currentSpeed = 0f;                // 消除平滑残余速度
            smoothTime = DecelerationTime;     // 保持平滑时间变量定义完整
        }
        else
        {
            // 正常移动逻辑
            worldMoveDirection = transform.TransformDirection(inputDirection);
            targetSpeed = inputMagnitude > 0 ? MoveSpeed : 0f;
            smoothTime = (targetSpeed > _currentSpeed) ? AccelerationTime : DecelerationTime;

            // 只有非攻击状态才进行速度平滑计算
            _currentSpeed = Mathf.SmoothDamp(
                _currentSpeed,
                targetSpeed,
                ref _speedDampVelocity,
                smoothTime
            );
        }
        // ---------------------------------------------------

        // --- 实际移动计算 ---
        Vector3 finalMove = worldMoveDirection * _currentSpeed;
        finalMove.y = _verticalVelocity;

        _controller.Move(finalMove * Time.deltaTime);

        // ---------------------------------------------------
        // 4. 动画更新和速度报告
        // ---------------------------------------------------
        if (_hasAnimator)
        {
            // 动画速度使用 InputMagnitude (在攻击时为 0)
            _animator.SetFloat(_animIDSpeed, inputMagnitude);
        }
    }

    // ---------------------------------------------------
    // 动画事件回调 (用于伤害检测)
    // ---------------------------------------------------

    private void StartAttackCheck()
    {
        // 每次攻击开始，清空已击中列表
        _hitTargets.Clear();

        // 1. 定义检测球体的位置和大小
        // 球体中心位于角色前方 AttackRange / 2 的位置 (即 1.0m)
        Vector3 checkCenter = transform.position + transform.forward * (AttackRange / 2f);
        // 半径为 AttackRange / 2 (即 1.0m)
        float checkRadius = AttackRange / 2f;

        // 

        // 2. 使用 Physics.OverlapSphere 检测碰撞体
        Collider[] hitColliders = Physics.OverlapSphere(checkCenter, checkRadius);

        foreach (Collider hit in hitColliders)
        {
            // 获取目标根对象（HealthSystem 所在的最高层级）
            GameObject targetRoot = hit.transform.root.gameObject;

            // 忽略攻击发起者自己、已击中目标、墙体、地面
            if (targetRoot == gameObject || _hitTargets.Contains(targetRoot) || targetRoot.CompareTag("Wall") || targetRoot.CompareTag("Ground"))
            {
                continue;
            }

            // *** 核心修改：检查是否击中了带有 HealthSystem 且标签为 MonsterTag ("Agent") 的目标 ***
            if (targetRoot.CompareTag(MonsterTag))
            {
                // *** 核心修改：直接寻找 HealthSystem 组件并应用伤害 ***
                // 假设 HealthSystem 是在根对象上
                HealthSystem targetHealth = targetRoot.GetComponent<HealthSystem>();

                if (targetHealth != null)
                {
                    // 直接调用 HealthSystem 造成伤害
                    targetHealth.TakeDamage(AttackDamage);
                    _hitTargets.Add(targetRoot); // 标记为已击中，防止重复伤害
                    Debug.Log($"SUCCESS: Player caused {AttackDamage} damage to {targetRoot.name} directly.");
                }
            }
        }
    }

    // *** 核心修复：在结束时手动重置代码锁定并处理协程 ***
    private void EndAttackCheck()
    {
        // 停止超时计时器
        if (_attackTimeoutCoroutine != null)
        {
            StopCoroutine(_attackTimeoutCoroutine);
            _attackTimeoutCoroutine = null;
            Debug.Log("EndAttackCheck() 被调用！解除锁定。");
        }

        // 1. 解除代码锁定
        _isAttacking = false;
    }

    // ---------------------------------------------------
    // 其他动画回调
    // ---------------------------------------------------

    private void OnFootstep(AnimationEvent animationEvent)
    {
        // 实现脚步声播放逻辑...
    }

    // ---------------------------------------------------
    // Debug 辅助
    // ---------------------------------------------------
    void OnDrawGizmos()
    {
        if (_isAttacking)
        {
            // 绘制攻击检测区域的球体 Gizmo
            Gizmos.color = Color.red;
            // 球体中心位于角色前方 AttackRange / 2
            Vector3 checkCenter = transform.position + transform.forward * (AttackRange / 2f);
            float checkRadius = AttackRange / 2f;
            Gizmos.DrawWireSphere(checkCenter, checkRadius);
        }
    }
}