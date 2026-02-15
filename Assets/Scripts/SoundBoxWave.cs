using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "SoundBoxWave", menuName = "Scriptable Objects/SoundBoxWave")]
public class SoundBoxWave : ScriptableObject
{
    [Tooltip("SoundBox prefab references to spawn for this wave.")]
    public List<SoundBox> boxes = new List<SoundBox>();
    public List<int> spawnPosNumbers = new List<int>();

    [HideInInspector] public List<SoundBox> activeInstances = new List<SoundBox>();
    [HideInInspector] public bool hasSpawned = false;
}
