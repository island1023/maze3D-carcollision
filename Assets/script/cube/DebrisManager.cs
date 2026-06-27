using UnityEngine;

/// <summary>
/// ����λ�ã�����ģ��Ԥ���壨fracturedPrefab���ġ����ڵ㡿
/// </summary>
public class DebrisManager : MonoBehaviour
{
    /// <summary>
    /// �� WallImpact ������˲����ã���̬ע������������
    /// </summary>
    public void ExplodeAndScatter(Vector3 hitPoint, float force, float radius)
    {
        // �������ڵ��µļ��ٸ�����Ƭ
        foreach (Transform child in transform)
        {
            // ȷ��ֻ�������� MeshRenderer ����ʵ��Ƭ
            if (child.GetComponent<MeshRenderer>() != null)
            {
                // 1. ��̬ע����ײ�壨MeshCollider ���뿪�� convex ������ Rigidbody ��ϣ�
                MeshCollider mc = child.gameObject.AddComponent<MeshCollider>();
                mc.convex = true;

                // 2. ��̬ע����壬��������
                Rigidbody rb = child.gameObject.AddComponent<Rigidbody>();
                // ����Ƭ��������ͣ�ȷ��������ѹʱ���ᱻ����
                rb.mass = 0.5f;

                // 3. ��̬ע����ײ��������������Ҫ���ֶ����أ�
                DebrisPieceAgent agent = child.gameObject.AddComponent<DebrisPieceAgent>();
                agent.manager = this;

                // 4. ʩ�ӱ�ը�����ø����ɵ���Ƭ˲����ɢ�ɳ�
                // ForceMode.Impulse ������˲�䱬������
                rb.AddExplosionForce(force, hitPoint, radius, 1f, ForceMode.Impulse);
            }
        }
    }

    /// <summary>
    /// ��ĳһ��С��Ƭ��������ѹʱ������ô��߼�
    /// </summary>
    public void HandleCrushedPiece(DebrisPieceAgent piece, Vector3 hitPoint, float fragmentMass)
    {
        // ==========================================
        // �����ķ��롿���Ӿ��߼����� 3�ų�Ա
        // 3�ų�Ա���������¼���Ӧ�Ը� piece ����������Ч���޸� Shader �ܽ��
        // ==========================================
        SimulationEvents.TriggerEnvironmentDestroy(piece.gameObject, hitPoint, fragmentMass * 10f);

        // �������߼�������ѹ�����̹رո���Ƭ����ײ�壡
        // �����������ܺ��������ؿ���ȥ�����������ڴ��°���һ����������Ծ
        Collider col = piece.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        // �߼���������������ٸ��������ͷ��ڴ�
        Destroy(piece.gameObject, 3f);
    }
}


/// <summary>
/// ������������ײ����̽�루������Զ�Ϊ���ٸ���Ƭ���ش˽ű��������ֶ�������
/// </summary>
public class DebrisPieceAgent : MonoBehaviour
{
    [HideInInspector]
    public DebrisManager manager;
    private bool hasBeenCrushed = false;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        TryCrushByVehicle(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        TryCrushByVehicle(collision);
    }

    private void TryCrushByVehicle(Collision collision)
    {
        if (hasBeenCrushed || manager == null)
            return;

        if (!IsVehicleCollider(collision.gameObject))
            return;

        hasBeenCrushed = true;
        float mass = rb != null ? rb.mass : 1f;
        manager.HandleCrushedPiece(this, collision.contacts[0].point, mass);
    }

    private static bool IsVehicleCollider(GameObject hitObject)
    {
        Transform current = hitObject.transform;
        while (current != null)
        {
            if (current.CompareTag("Truck")
                || current.CompareTag("SUV")
                || current.CompareTag("Sedan"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
