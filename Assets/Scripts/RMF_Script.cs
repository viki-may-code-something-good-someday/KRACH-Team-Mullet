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
    private float rmfCurrentValue = 0f;
    public float rmfValueTriggeringHigh = 0.6f;
    public float waitingThreshold = 0.5f;
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
            toggle.SetIsOnWithoutNotify(true);

        }
        else
        {
            toggle.SetIsOnWithoutNotify(false);
        }
    }

    public void SetRMFValue(float value)
    {
            rmfCurrentValue = value;
    
            if(rmfCurrentValue >= rmfValueTriggeringHigh)
            {
                currentRMFstate = RMF_State.High;
            }
            else
            {
                currentRMFstate = RMF_State.Low;
            }
    }
}
