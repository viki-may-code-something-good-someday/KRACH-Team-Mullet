using UnityEngine;

public class Wall_Data : MonoBehaviour
{
    private float health = 1f;

    public float Health { get { return health; } }


    public void TakeDamage(float _damage)
    {
        health -= _damage;

        if (health <= 0f)
        {
            GetDestroyed();
        }
    }

    private void GetDestroyed()
    {
        
    }
}
