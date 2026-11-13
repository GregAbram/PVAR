using TMPro;
using UnityEngine;

// Quest: the room's guides - the floating status panel and the room's markers
// (tag cubes, the magenta reference post and the white origin post) - hide by
// themselves a few seconds after the room is anchored, leaving only the data.
// Y shows or hides them (the status keeps updating while hidden). They come
// back by themselves if the room stops being anchored (a new room, say).
// Y is free in PVAR, which has no other scene to switch to.
public class PVARStatusToggle : MonoBehaviour
{
    [SerializeField] private TextMeshPro statusText;
    [Tooltip("Seconds after the room is anchored before the guides hide; negative: never by themselves.")]
    [SerializeField] private float hideAfterAnchoredSeconds = 3f;

#if UNITY_ANDROID
    private RoomAnchor anchor;
    private bool shown = true;
    private bool wasAnchored;
    private float anchoredAt;

    private void Update()
    {
        anchor ??= FindAnyObjectByType<RoomAnchor>();
        var anchored = anchor != null && anchor.IsAnchored;
        if (anchored && !wasAnchored)
        {
            anchoredAt = Time.time;
        }
        else if (!anchored && wasAnchored)
        {
            shown = true;   // help find the room again
        }
        if (anchored && hideAfterAnchoredSeconds >= 0f && anchoredAt > 0f &&
            Time.time - anchoredAt >= hideAfterAnchoredSeconds)
        {
            shown = false;
            anchoredAt = 0f;   // once; Y shows them again
        }
        wasAnchored = anchored;

        if (OVRInput.GetDown(OVRInput.Button.Four))
        {
            shown = !shown;
            anchoredAt = 0f;
        }
    }

    // After RoomTagMarkers has placed (and possibly created) its markers.
    private void LateUpdate()
    {
        if (statusText != null)
        {
            statusText.enabled = shown;
        }
        if (anchor == null)
        {
            return;
        }
        foreach (var r in anchor.GetComponentsInChildren<Renderer>(true))
        {
            if (IsGuide(r.transform))
            {
                r.enabled = shown;
            }
        }
    }

    // A tag marker, the reference post or the origin post - not the data,
    // which is also under the room.
    private bool IsGuide(Transform t)
    {
        for (; t != null && t != anchor.transform; t = t.parent)
        {
            if (t.GetComponent<RoomReferenceMarker>() != null || t.parent == anchor.transform &&
                (t.name == "Origin" || t.name.StartsWith("Tag ") && t.name.EndsWith(" marker")))
            {
                return true;
            }
        }
        return false;
    }
#endif
}
