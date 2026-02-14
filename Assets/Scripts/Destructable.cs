using UnityEngine;
using UnityEngine.Events;

public class Destructable : MonoBehaviour
{

    public void Destruct(Vector3 _hitPoint, Vector3 _hitNormal)
    {
        Wall_Data wallData = GetComponent<Wall_Data>();
        if (wallData != null)
        {
            wallData.TakeDamage(1f, _hitPoint, _hitNormal);
        }
    }
}
