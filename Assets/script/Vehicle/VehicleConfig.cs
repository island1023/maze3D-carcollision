using UnityEngine;

[CreateAssetMenu(menuName="Vehicle/Config")]
public class VehicleConfig : ScriptableObject
{
    public float mass;

    public float maxSpeed;

    public float motorTorque;

    public float brakeForce;

    public float steerAngle;

    public float durability;
}