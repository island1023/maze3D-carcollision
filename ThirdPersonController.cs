using UnityEngine;
using System.Collections.Generic;
using System.Collections;

// 确保该脚本挂载在具有 CharacterController 组件的游戏对象上
[RequireComponent(typeof(CharacterController))]
public class LegacyThirdPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("角色移动速度 (米/秒)")]
    public float MoveSpeed = 5.0f;
    [Tooltip("角色旋转平滑时间。值越小，旋转越快。例如 0.12f")]
    public float RotationSmoothTime = 0.12f;
    [Tooltip("重力加速度")]
    public float Gravity = -20.0f;
    [Tooltip("角色跳跃高度")]
    public float JumpHeight = 1.5f;

    [Header("Fixed Rotation Settings")]
    [Tooltip("左右箭头每次按下的旋转角度")]
    public float FixedRotationAngle = 90.0f;

    [Header("Path Tracking Settings")]
    [Tooltip("用于绘制路径的 Line Renderer 组件")]
    public LineRenderer PathLineRenderer; // <-- 新增的公共字段
    [Tooltip("每隔多少距离记录一个路径点")]
    public float MinDistanceForNewPoint = 0.5f;

    // ----------------------------------------
    // 私有变量
    // ----------------------------------------

    private CharacterController _controller;
    private Animator _animator;

    private float _verticalVelocity;

    // 旋转变量
    private float _targetRotationY;
    private float _rotationDampVelocity;

    private bool _hasAnimator;

    // 路径追踪变量
    private Vector3 _lastPosition; // 上一个记录的路径点位置

    // 动画 ID
    private int _animIDSpeed;
    private int _animIDJump;
    private int _animIDGrounded;

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _hasAnimator = TryGetComponent(out _animator);

        // 初始化动画 ID
        if (_hasAnimator)
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDGrounded = Animator.StringToHash("Grounded");
        }

        // 初始目标旋转角度设置为当前角色的 Y 轴角度
        _targetRotationY = transform.eulerAngles.y;

        // 初始化路径追踪
        InitializePathTracker();
    }

    void Update()
    {
        HandleInputRotation();
        HandleMovementAndGravity();

        // 调用路径追踪方法
        TrackPath();
    }

    private void InitializePathTracker()
    {
        if (PathLineRenderer != null)
        {
            // 确保 Line Renderer 的初始起点是角色的当前位置
            _lastPosition = transform.position;
            PathLineRenderer.positionCount = 1;
            PathLineRenderer.SetPosition(0, _lastPosition);
        }
        else
        {
            Debug.LogWarning("PathLineRenderer is not assigned. Path tracking will be disabled.");
        }
    }


    private void TrackPath()
    {
        if (PathLineRenderer == null) return;

        // 只在角色移动超过最小距离时记录新点
        if (Vector3.Distance(transform.position, _lastPosition) >= MinDistanceForNewPoint)
        {
            // 记录新路径点
            _lastPosition = transform.position;

            // 增加 Line Renderer 的点数
            PathLineRenderer.positionCount++;

            // 将新点添加到 Line Renderer 的末尾
            // 注意：LineRenderer 会在世界空间绘制，所以不需要额外的偏移
            PathLineRenderer.SetPosition(PathLineRenderer.positionCount - 1, _lastPosition);
        }
    }

    private void HandleInputRotation()
    {
        // ---------------------------------------------------
        // 1. 捕捉按键输入并设置目标角度 (_targetRotationY)
        // ---------------------------------------------------

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            // 更新目标角度
            _targetRotationY -= FixedRotationAngle;
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            // 更新目标角度
            _targetRotationY += FixedRotationAngle;
        }

        // ---------------------------------------------------
        // 2. 平滑过渡到目标角度
        // ---------------------------------------------------

        float currentYAngle = transform.eulerAngles.y;

        // 使用 SmoothDampAngle 平滑地将当前角度转向目标角度
        float smoothedRotation = Mathf.SmoothDampAngle(
            currentYAngle,
            _targetRotationY,
            ref _rotationDampVelocity,
            RotationSmoothTime
        );

        // 应用旋转
        transform.rotation = Quaternion.Euler(0.0f, smoothedRotation, 0.0f);
    }

    private void HandleMovementAndGravity()
    {
        // ---------------------------------------------------
        // 1. 移动输入
        // ---------------------------------------------------

        // 获取 WASD 输入 (返回 -1, 0, 或 1)
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        // 构建输入方向向量 (局部空间)
        Vector3 inputDirection = new Vector3(horizontalInput, 0f, verticalInput).normalized;

        // ---------------------------------------------------
        // 2. 重力与跳跃
        // ---------------------------------------------------

        if (_controller.isGrounded)
        {
            _verticalVelocity = -0.5f; // 确保角色粘在地面上

            // 跳跃 (默认映射为 Space)
            if (Input.GetButtonDown("Jump"))
            {
                // V = sqrt(H * -2 * G)
                _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                if (_hasAnimator) _animator.SetBool(_animIDJump, true);
            }
            if (_hasAnimator) _animator.SetBool(_animIDGrounded, true);
        }
        else
        {
            // 应用重力
            _verticalVelocity += Gravity * Time.deltaTime;
            if (_hasAnimator) _animator.SetBool(_animIDGrounded, false);
            if (_hasAnimator && _verticalVelocity < 0) _animator.SetBool(_animIDJump, false);
        }

        // ---------------------------------------------------
        // 3. 应用移动
        // ---------------------------------------------------

        // 将局部空间方向转换为世界空间方向 (基于当前角色旋转)
        Vector3 worldMoveDirection = transform.TransformDirection(inputDirection);

        Vector3 finalMove = worldMoveDirection * MoveSpeed;
        finalMove.y = _verticalVelocity;

        _controller.Move(finalMove * Time.deltaTime);

        // ---------------------------------------------------
        // 4. 动画更新
        // ---------------------------------------------------

        // 计算水平速度 (用于动画混合)
        float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0f, _controller.velocity.z).magnitude;

        if (_hasAnimator)
        {
            // 使用输入幅度和当前速度驱动动画
            float animationSpeed = inputDirection.magnitude > 0 ? currentHorizontalSpeed : 0;
            _animator.SetFloat(_animIDSpeed, animationSpeed);
        }
    }

    // ---------------------------------------------------
    // 动画事件回调
    // ---------------------------------------------------

    private void OnFootstep(AnimationEvent animationEvent)
    {
        // 实现脚步声播放逻辑...
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f && _hasAnimator)
        {
            // 确保跳跃动画参数被重置
            _animator.SetBool(_animIDJump, false);
        }
    }
}