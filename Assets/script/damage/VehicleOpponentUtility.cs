using UnityEngine;

/// <summary>
/// 判断卡车对撞目标（玩家 Truck、移动 AITruck、静止车厢 SedenAItruck 等）。
/// </summary>
public static class VehicleOpponentUtility
{
  public static readonly string[] TruckOpponentTags = { "AITruck1", "Truck", "TruckBack" };
  public static readonly string[] PlayableVehicleTags = { "Truck", "Sedan", "SUV", "AITruck1" };

  public static bool IsTruckOpponent(GameObject hitObject)
  {
    if (hitObject == null)
      return false;

    if (TruckTrailerDeformation.IsCarriageObject(hitObject))
      return true;

    Transform current = hitObject.transform;
    while (current != null)
    {
      foreach (string tag in TruckOpponentTags)
      {
        if (!string.IsNullOrEmpty(tag) && current.CompareTag(tag))
          return true;
      }

      current = current.parent;
    }

    return false;
  }

  public static string ResolveOpponentTag(GameObject hitObject)
  {
    if (hitObject == null)
      return string.Empty;

    if (TruckTrailerDeformation.IsCarriageObject(hitObject))
      return "TruckBack";

    Transform current = hitObject.transform;
    while (current != null)
    {
      foreach (string tag in TruckOpponentTags)
      {
        if (!string.IsNullOrEmpty(tag) && current.CompareTag(tag))
          return tag;
      }

      current = current.parent;
    }

    return hitObject.tag;
  }

  public static bool IsPlayableVehicle(GameObject hitObject)
  {
    if (hitObject == null)
      return false;

    Transform current = hitObject.transform;
    while (current != null)
    {
      foreach (string tag in PlayableVehicleTags)
      {
        if (!string.IsNullOrEmpty(tag) && current.CompareTag(tag))
          return true;
      }

      if (current.GetComponent<AITruckWaypointController>() != null)
        return true;

      current = current.parent;
    }

    return false;
  }

  public static bool IsAITruck(GameObject hitObject)
  {
    if (hitObject == null)
      return false;

    Transform current = hitObject.transform;
    while (current != null)
    {
      if (current.CompareTag("AITruck1"))
        return true;

      if (current.GetComponent<AITruckWaypointController>() != null)
        return true;

      current = current.parent;
    }

    return false;
  }

  public static bool ShouldPlayTruckImpactVfx(GameObject vehicleRoot)
  {
    return IsTruckOpponent(vehicleRoot) || IsAITruck(vehicleRoot);
  }

  public static GameObject GetVehicleRoot(GameObject hitObject)
  {
    if (hitObject == null)
      return null;

    Rigidbody rb = hitObject.GetComponentInParent<Rigidbody>();
    return rb != null ? rb.gameObject : hitObject.transform.root.gameObject;
  }
}
