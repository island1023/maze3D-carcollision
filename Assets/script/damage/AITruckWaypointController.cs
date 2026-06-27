using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class AITruckWaypointController : MonoBehaviour
{
    [Header("--- 目标与触发 ---")]
    public Transform 玩家目标;
    [Tooltip("玩家开车进入这个距离时，AI开始沿路点行驶")]
    public float 启动距离 = 60f;
    [Tooltip("玩家开得很近时，AI放弃路点，直接强制撞向玩家")]
    public float 强制碰撞距离 = 20f;

    [Header("--- 路点设置 ---")]
    [Tooltip("按顺序拖入场景中设置好的路点")]
    public Transform[] 巡逻路点;
    [Tooltip("到达判定距离，越靠近会提前转向下一个点，建议8-12米")]
    public float 到达判定距离 = 10f;

    [Header("--- AI 运动操控参数 ---")]
    public float 巡逻速度 = 15f;
    public float 碰撞追击速度 = 25f;
    [Tooltip("起步和加速的速度")]
    public float 加速度 = 8f;
    [Tooltip("转弯时的角速度，值越小转弯越急，建议30-60")]
    public float 转弯角速度 = 40f;

    [Header("--- 碰撞后行为参数 ---")]
    public float 碰撞后退距离 = 3f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private int currentWaypointIndex = 0;

    private Transform goodModel;
    private Transform damageModel;

    public enum AIState { 等待, 沿路点行驶, 主动碰撞, 碰撞后停止 }
    public AIState 当前状态 = AIState.等待;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        agent.speed = 巡逻速度;
        agent.acceleration = 加速度;
        agent.angularSpeed = 转弯角速度;
        agent.autoBraking = false;

        EnsureAgentOnNavMesh();
        SetAgentStopped(true);

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.CompareTag("Good")) goodModel = child;
            if (child.CompareTag("Damage")) damageModel = child;
        }

        if (goodModel != null) goodModel.gameObject.SetActive(true);
        if (damageModel != null) damageModel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (玩家目标 == null) return;
        if (当前状态 == AIState.碰撞后停止) return;

        float distanceToPlayer = Vector3.Distance(transform.position, 玩家目标.position);

        switch (当前状态)
        {
            case AIState.等待:
                if (distanceToPlayer <= 启动距离)
                {
                    当前状态 = AIState.沿路点行驶;
                    SetAgentStopped(false);
                    GoToNextWaypoint();
                }
                break;

            case AIState.沿路点行驶:
                if (distanceToPlayer <= 强制碰撞距离)
                {
                    当前状态 = AIState.主动碰撞;
                    agent.speed = 碰撞追击速度;
                    agent.angularSpeed = 转弯角速度 * 2f;
                    return;
                }

                if (IsAgentReady() && !agent.pathPending && agent.remainingDistance < 到达判定距离)
                    GoToNextWaypoint();
                break;

            case AIState.主动碰撞:
                if (IsAgentReady())
                    agent.SetDestination(玩家目标.position);
                break;
        }
    }

    void GoToNextWaypoint()
    {
        if (巡逻路点 == null || 巡逻路点.Length == 0) return;
        if (!IsAgentReady()) return;

        agent.SetDestination(巡逻路点[currentWaypointIndex].position);
        currentWaypointIndex = (currentWaypointIndex + 1) % 巡逻路点.Length;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (当前状态 == AIState.碰撞后停止) return;

        if (!VehicleOpponentUtility.IsPlayableVehicle(collision.gameObject))
            return;

        VehicleTruckImpactVfx.PlayForCollisionPair(collision, gameObject);

        当前状态 = AIState.碰撞后停止;

        if (goodModel != null) goodModel.gameObject.SetActive(false);
        if (damageModel != null) damageModel.gameObject.SetActive(true);

        StartCoroutine(HandleCollisionBounce());
    }

    private IEnumerator HandleCollisionBounce()
    {
        SetAgentStopped(true);
        if (agent != null)
            agent.enabled = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 startPos = transform.position;
        Vector3 targetPos = transform.position - transform.forward * 碰撞后退距离;

        float elapsed = 0;
        while (elapsed < 0.5f)
        {
            if (rb != null)
                rb.MovePosition(Vector3.Lerp(startPos, targetPos, elapsed / 0.5f));

            elapsed += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.drag = 10f;
            rb.isKinematic = true;
        }
    }

    private bool IsAgentReady()
    {
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    private void EnsureAgentOnNavMesh()
    {
        if (agent == null || !agent.enabled)
            return;

        if (agent.isOnNavMesh)
            return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    private void SetAgentStopped(bool stopped)
    {
        if (agent == null || !agent.isActiveAndEnabled)
            return;

        if (!agent.isOnNavMesh)
        {
            EnsureAgentOnNavMesh();
            if (!agent.isOnNavMesh)
                return;
        }

        agent.isStopped = stopped;
    }
}
