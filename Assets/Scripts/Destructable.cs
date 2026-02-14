using UnityEngine;

public class Destructable : MonoBehaviour
{
    [SerializeField] private Wall_Data wallData;

    public void Destruct(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        wallData.TakeDamage(_damage, _hitPoint, _hitNormal);
    }
}
