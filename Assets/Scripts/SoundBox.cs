using UnityEngine;

public class SoundBox : MonoBehaviour
{
    [SerializeField] private float health;

    public void TakeDamage(float _damage, Vector3 _hitPoint, Vector3 _hitNormal)
    {
        health -= _damage;

        if (health <= 0f)
        {
            GetDestroyed(_hitPoint, _hitNormal);
        }
    }

    private void GetDestroyed(Vector3 _hitPoint, Vector3 _hitNormal)
    {
        SoundBoxSpawner.Instance.DestroyedSoundbox(this);
        Destroy(gameObject);
    }
}
