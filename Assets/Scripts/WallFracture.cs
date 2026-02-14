using UnityEngine;

public class WallFracture : MonoBehaviour
{
    private void OnEnable()
    {
        Destroy(gameObject, 5f);
    }
}
