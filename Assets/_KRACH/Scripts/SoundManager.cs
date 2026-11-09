using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    public EventReference classicSchubertEvent;
    public EventReference remixSchubertEvent;
    public EventReference neighbourEvent;
    public EventReference neightbourlistensEvent;

    private EventInstance classicSchubertInstance;
    private EventInstance remixSchubertInstance;
    //private EventInstance neighbourInstance;
    //private GameObject neighbourGO;

    public EventReference[] soundboxEvents;

    private StudioEventEmitter[] soundboxEmitters;
    //private Bus musicBus;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Initialize the music bus
        //musicBus = RuntimeManager.GetBus("bus:/Music");
        //musicBus.setVolume();

        // Get neighbour GameObject
        //neighbourGO = GameObject.FindWithTag("Neighbour");

        classicSchubertInstance = RuntimeManager.CreateInstance(classicSchubertEvent);
        remixSchubertInstance = RuntimeManager.CreateInstance(remixSchubertEvent);
    }

    public void InitializeSoundboxEmitters()
    {
        // Find all SoundBox Objects and then take the soundemitter component and fill in the array.
        GameObject[] soundboxes = GameObject.FindGameObjectsWithTag("SoundBox");

        soundboxEmitters = new StudioEventEmitter[soundboxes.Length];

        for (int i = 0; i < soundboxes.Length; i++)
        {
            soundboxEmitters[i] = soundboxes[i].GetComponent<StudioEventEmitter>();
            if (soundboxEmitters[i] == null)
            {
                Debug.LogWarning($"[SoundManager] '{soundboxes[i].name}' is tagged as SoundBox but has no StudioEventEmitter.");
                continue;
            }

            if (i < soundboxEvents.Length)
            {
                soundboxEmitters[i].EventReference = soundboxEvents[i];
                PlaySoundBoxEvent(i);    // play soundbox event on start
                //Debug.Log($"SoundManager: Assigned event {soundboxEvents[i].Path} to SoundBox {soundboxes[i].name}");
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        Instance = null;

        // Release the persistent music instances cleanly.
        if (classicSchubertInstance.isValid()) { classicSchubertInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); classicSchubertInstance.release(); }
        if (remixSchubertInstance.isValid()) { remixSchubertInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); remixSchubertInstance.release(); }
    }

    public void PlaySoundBoxEvent(int index)
    {
        if (soundboxEmitters == null) return;

        if (index >= 0 && index < soundboxEmitters.Length && soundboxEmitters[index] != null)
        {
            soundboxEmitters[index].Play();
        }
    }

    public void PlayClassicMusic()
    {
        classicSchubertInstance.getPlaybackState(out PLAYBACK_STATE state);
        if (state != PLAYBACK_STATE.PLAYING)
        {
            classicSchubertInstance.start();
        }
    }

    public void StopClassicMusic()
    {
        classicSchubertInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    }

    public void PlayRemixMusic()
    {
        remixSchubertInstance.getPlaybackState(out PLAYBACK_STATE state);
        if (state != PLAYBACK_STATE.PLAYING)
        {
            remixSchubertInstance.start();
        }
    }

    public void StopRemixMusic()
    {
        remixSchubertInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    }
}
