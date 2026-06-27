using UnityEngine;

/// <summary>
/// ���Խű������ص����������ϣ����ڼ�� TruckTrailerDeformation �Ƿ���������
/// ��ײʱ�������ϸ��Ϣ������̨
/// </summary>
public class DeformationDebugger : MonoBehaviour
{
    [Header("Ŀ����νű�")]
    public TruckTrailerDeformation deformation;

    private void Start()
    {
        if (deformation == null)
            deformation = GetComponent<TruckTrailerDeformation>();

        if (deformation == null)
            Debug.LogError("�����ε��ԡ�δ�ҵ� TruckTrailerDeformation �������ȷ���ű��ѹ��ء�");
        else
        {
            Debug.Log($"�����ε��ԡ����νű����ҵ���������" +
                      $"maxDepth={deformation.maxDepth}, minSpeed={deformation.minSpeed}, " +
                      $"radius={deformation.radius}, Ŀ��Mesh={deformation.targetMeshFilter?.name}, " +
                      $"�ű�����={deformation.enabled}");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // �����ײ������Ϣ
        Debug.Log($"�����ε��ԡ�������ײ�� {collision.gameObject.name}����ǩ��{collision.gameObject.tag}����" +
                  $"����ٶȣ�{collision.relativeVelocity.magnitude:F2} m/s��ײ���㣺{collision.contacts[0].point}");

        if (deformation == null)
        {
            Debug.LogError("�����ε��ԡ����νű�����Ϊ�գ�");
            return;
        }

        if (!deformation.enabled)
        {
            Debug.LogWarning("�����ε��ԡ����νű��ѽ��ã�");
            return;
        }

        bool isVehicle = TruckTrailerDeformation.IsVehicleCollision(collision.gameObject);
        if (!isVehicle)
            Debug.LogWarning($"�����ε��ԡ���ײ�� {collision.gameObject.name}����ǩ {collision.gameObject.tag}��δʶ��Ϊ Sedan/SUV���������");
        else
            Debug.Log("�����ε��ԡ���ʶ��Ϊ������ײ");

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < deformation.minSpeed)
            Debug.LogWarning($"�����ε��ԡ�ײ���ٶ� {impactSpeed:F2} m/s ������С�����ٶ� {deformation.minSpeed}���������");
        else if (isVehicle)
            Debug.Log($"�����ε��ԡ�ײ���ٶ��������������������Σ������� {deformation.maxDepth}��");
    }
}