using UnityEngine;
using UnityEngine.UI;

public enum RMF_State
{
    Low,   // Wert: 0
    High   // Wert: 1
}

public class RMF_Script : MonoBehaviour
{
    private Toggle toggle;
    public RMF_State currentRMFstate;

    void Start()
    {
        toggle = GetComponent<Toggle>();
    }

    // Update is called once per frame
    void Update()
    {
        if(currentRMFstate == RMF_State.High)
        {
            toggle.SetIsOnWithoutNotify(false);

        }
        else
        {
            toggle.SetIsOnWithoutNotify(false);
        }
    }
}
