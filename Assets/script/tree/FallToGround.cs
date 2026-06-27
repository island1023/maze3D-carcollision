using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FallToGround : MonoBehaviour
{
    [Tooltip("地面高度（Y轴），到达后停止运动")]
    public float groundY = 0f;

    private Rigidbody rb;
    private bool grounded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!grounded && transform.position.y <= groundY)
        {
            grounded = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            Debug.Log($"[落地] {name} 已落地，停止运动");
        }
    }
}