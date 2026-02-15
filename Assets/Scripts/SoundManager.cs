using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    public EventInstance classicSchubertEvent;
    public EventInstance remixSchubertEvent;
    
    public EventReference[] soundboxEvents;
    StudioEventEmitter [] soundboxEmitters;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        InitializeSoundboxEmitters();
        classicSchubertEvent.start();
    }

    void InitializeSoundboxEmitters()
    {
        // Find all SoundBox Objects and then take the soundemitter component and fill in the array.
        GameObject[] soundboxes = GameObject.FindGameObjectsWithTag("SoundBox");
        
        soundboxEmitters = new StudioEventEmitter[soundboxes.Length];
        
        for (int i = 0; i < soundboxes.Length; i++)
        {
            soundboxEmitters[i] = soundboxes[i].GetComponent<StudioEventEmitter>();
            if (i < soundboxEvents.Length)
            {
                soundboxEmitters[i].EventReference = soundboxEvents[i];
                PlaySoundBoxEvent(i);    // play soundbox event on start
                Debug.Log($"SoundManager: Assigned event {soundboxEvents[i].Path} to SoundBox {soundboxes[i].name}");
            }
        }
    }

    public void PlaySoundBoxEvent(int index)
    {
        if (index >= 0 && index < soundboxEmitters.Length && soundboxEmitters[index] != null)
        {
            soundboxEmitters[index].Play();
        }
    }

    public void PlayClassicMusic()
    {
        classicSchubertEvent.getPlaybackState(out PLAYBACK_STATE state);
        if (state != PLAYBACK_STATE.PLAYING)
        {
            classicSchubertEvent.setPaused(false);
        }
    }

    public void StopClassicMusic()
    {
        classicSchubertEvent.setPaused(true);
    }

    public void PlayRemixMusic()
    {
        remixSchubertEvent.getPlaybackState(out PLAYBACK_STATE state);
        if (state != PLAYBACK_STATE.PLAYING)
        {
            remixSchubertEvent.setPaused(false);
        }
    }

    public void StopRemixMusic()
    {
        remixSchubertEvent.setPaused(true);
    }
}
