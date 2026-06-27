using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    public float distance = 10f;

    public float height = 4f;

    public float rotateSpeed = 5f;

    private float yaw = 0;

    private float pitch = 20;

    void LateUpdate()
    {
        if (target == null)
            return;

        // 鼠标旋转观察

        yaw += Input.GetAxis("Mouse X")
               * rotateSpeed;

        pitch -= Input.GetAxis("Mouse Y")
                 * rotateSpeed;

        pitch = Mathf.Clamp(
                    pitch,
                    -20,
                    80);

        // Q E 快速切换90度

        if (Input.GetKeyDown(KeyCode.Q))
            yaw -= 90;

        if (Input.GetKeyDown(KeyCode.E))
            yaw += 90;

        Quaternion rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0);

        Vector3 offset =
            rotation *
            new Vector3(
                0,
                0,
                -distance);

        transform.position =
            target.position +
            Vector3.up * height +
            offset;

        transform.LookAt(
            target.position +
            Vector3.up * 1.5f);
    }
}