using UnityEngine;
using FMODUnity;

public class MetalWall : MonoBehaviour
{
    public void WallGotHit()
    {
        RuntimeManager.PlayOneShot("event:/SFX/WallMetal", transform.position);    // sound
    }
}
