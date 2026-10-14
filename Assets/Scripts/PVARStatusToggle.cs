using TMPro;
using UnityEngine;

// Quest: Y shows or hides the floating status panel (it keeps updating while
// hidden). Y is free in PVAR, which has no other scene to switch to.
public class PVARStatusToggle : MonoBehaviour
{
    [SerializeField] private TextMeshPro statusText;

#if UNITY_ANDROID
    private void Update()
    {
        if (statusText != null && OVRInput.GetDown(OVRInput.Button.Four))
        {
            statusText.enabled = !statusText.enabled;
        }
    }
#endif
}
