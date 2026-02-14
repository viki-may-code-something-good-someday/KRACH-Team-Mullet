using UnityEngine;

public class Destructable : MonoBehaviour
{
    [SerializeField] private Wall_Data wallData;
    [SerializeField] private SoundBox soundBox;


    public void Destruct(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        if (wallData != null)
        {
            wallData.TakeDamage(_damage, _hitPoint, _hitNormal);
        }
        else if (soundBox != null)
        {
            soundBox.TakeDamage(_damage, _hitPoint, _hitNormal);
        }
    }
}
