using UnityEngine;
using UnityEngine.Events;
using FMODUnity;

public class Interactable : MonoBehaviour
{
    public UnityEvent onInteracted;

    public void Interact()
    {
        onInteracted?.Invoke();
    }
}
