using UnityEngine;
using FMODUnity;

public class SoundManager : MonoBehaviour
{
    StudioEventEmitter classicMusicEvent;
    StudioEventEmitter [] remixMusicEvents;

    void Start()
    {
        classicMusicEvent.Play();
    }

    void Update()
    {
        
    }
    public void PlayClassicMusic()
    {
        if (classicMusicEvent != null && !classicMusicEvent.IsPlaying())
        {
            classicMusicEvent.Play();
        }
    }
}
