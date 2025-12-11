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
    [Tooltip("用于伤害判定的碰撞体（必须是 Is Trigger）")]
    public Collider AttackCollider;
    [Tooltip("攻击伤害值")]
    public int AttackDamage = 1;

    // *** 核心修改：用于抵消动画位移 ***
    [Tooltip("攻击动画产生的向前位移距离。代码将向后移动此距离进行抵消。")]
    public float AttackForwardDisplacement = 0.2f; // 抵消值 (可在 Inspector 中调整)
    private Vector3 _attackStartPosition; // 用于 StartAttackCheck 时的位置锁定

    // ----------------------------------------
    // 新增：超时机制参数
    // ----------------------------------------
    [Tooltip("攻击动画播放的最大允许时间 (秒)。如果超过此时间仍未解锁，则强制重置。")]
    public float AttackTimeoutDuration = 0.6f;

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

    // *** 存储 AttackTrigger 组件的引用 ***
    private AttackTrigger _attackTriggerScript;

    // 路径追踪变量
    private Vector3 _lastPosition;

    // 动画 ID
    private int _animIDSpeed;
    private int _animIDGrounded;
    private int _animIDAttack; // <-- Trigger ID

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

        // 确保攻击碰撞体初始是禁用的
        if (AttackCollider != null)
        {
            AttackCollider.enabled = false;

            // *** 核心修复：获取 AttackTrigger 脚本的引用和同步伤害值 ***
            _attackTriggerScript = AttackCollider.GetComponent<AttackTrigger>();
            if (_attackTriggerScript != null)
            {
                _attackTriggerScript.damageAmount = AttackDamage;
            }
            else
            {
                Debug.LogError("AttackCollider 缺少 AttackTrigger 脚本。连击伤害判定将失败。");
            }
        }
        else
        {
            Debug.LogError("AttackCollider 字段未赋值。请在 Inspector 中拖入伤害碰撞体。");
        }

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
    // *** 修复：HandleSpeedAdjustment 方法定义 ***
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
    // *** 修复：InitializePathTracker 方法定义 ***
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
    // *** 修复：TrackPath 方法定义 ***
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
    // *** 修复：HandleInputRotation 方法定义 ***
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
            if (_hasAnimator)
            {
                // 1. 启动动画 (使用 Trigger)
                _animator.SetTrigger(_animIDAttack);

                // 2. 如果当前不在攻击状态，启动锁定和超时
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
        // *** 核心修复：攻击时强制停止所有水平位移 ***
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

        // *** 删除了当前的 Debug.Log($"当前水平移动速度: {_currentSpeed:F2} m/s"); ***
    }

    // ---------------------------------------------------
    // 动画事件回调 (用于伤害检测)
    // ---------------------------------------------------

    private void StartAttackCheck()
    {
        if (AttackCollider != null)
        {
            // *** 核心修复：每次攻击开始时，重置伤害脚本状态 ***
            if (_attackTriggerScript != null)
            {
                // 调用 AttackTrigger 中的新方法，清空已击中列表
                _attackTriggerScript.ResetHitTargets();
            }

            AttackCollider.enabled = true; // 开启碰撞体，开始伤害判定
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

        if (AttackCollider != null)
        {
            AttackCollider.enabled = false;
        }

        // 1. 解除代码锁定
        _isAttacking = false;

        // 2. Trigger 会被 Animator 自动重置，此处无需 SetBool/SetTrigger。
    }

    // ---------------------------------------------------
    // 其他动画回调
    // ---------------------------------------------------

    private void OnFootstep(AnimationEvent animationEvent)
    {
        // 实现脚步声播放逻辑...
    }
}