using UnityEngine;
using FMODUnity;

public class MetalWall : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void WallGotHit()
    {
        RuntimeManager.PlayOneShot("event:/SFX/WallMetal", transform.position);    // sound
    }
}
