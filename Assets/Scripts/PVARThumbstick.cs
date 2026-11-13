using System;
using ParaViewLink;
using TMPro;
using UnityEngine;

// Quest: the right thumbstick moves through the data, as in the DataManager
// examples. Right and left step to the next or previous timestep (a flick
// moves one, holding repeats; stepping stops Play) - for every app following
// the state. Up and down switch this headset to the previous or next of the
// DataManager's states, once per push; a label below the line of sight names
// the state for a few seconds. The same label says when the DataManager
// can't be reached (cached data is shown) and when it's back. Made at startup on an object of its own, for
// the scene's MeshReceiver, so scenes need no change. (The receiver's
// SimContainer is inactive until the room is anchored.)
public class PVARThumbstick : MonoBehaviour
{
    private MeshReceiver receiver;
    private int stickDirection;   // ±1 time, ±2 state, 0 centred
    private float repeatAt;
    private string shownState = "";
    private bool? shownOnline;
    private TextMeshPro label;
    private float labelUntil;

#if UNITY_ANDROID
    // Quest only: OVRInput.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var receiver = FindAnyObjectByType<MeshReceiver>(FindObjectsInactive.Include);
        if (receiver == null)
        {
            Debug.LogWarning("[PVARThumbstick] No MeshReceiver in the scene; the thumbstick does nothing");
            return;
        }
        var go = new GameObject("PVAR Thumbstick");
        go.AddComponent<PVARThumbstick>().receiver = receiver;
        Debug.Log($"[PVARThumbstick] Right thumbstick: left/right step time, up/down switch state ({receiver.name})");
    }
#endif

#if UNITY_ANDROID
    private void Update()
    {
        var v = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        var horizontal = Mathf.Abs(v.x) >= Mathf.Abs(v.y);
        var a = horizontal ? v.x : v.y;
        var direction = a > 0.6f ? 1 : a < -0.6f ? -1 : 0;
        if (direction == 0)
        {
            if (v.magnitude < 0.3f)
            {
                stickDirection = 0;
            }
        }
        else
        {
            var code = horizontal ? direction : 2 * direction;
            var now = Time.unscaledTime;
            if (code != stickDirection || horizontal && now >= repeatAt)
            {
                repeatAt = now + (code == stickDirection ? 0.15f : 0.4f);
                stickDirection = code;
                if (horizontal)
                {
                    StepTime(direction);
                }
                else
                {
                    NextState(-direction);   // up: previous
                }
            }
        }
        ShowStateChanges();
    }
#endif

    private void StepTime(int delta)
    {
        if (receiver.Time.Sweeping)
        {
            receiver.Pause();
        }
        receiver.Step(delta);
    }

    private string[] SortedStates()
    {
        var names = (string[])receiver.StateNames.Clone();
        Array.Sort(names, StringComparer.OrdinalIgnoreCase);
        return names;
    }

    private void NextState(int delta)
    {
        receiver.ListStates();   // so a state made since is there next time
        var names = SortedStates();
        var current = receiver.CurrentState?.Name ?? "";
        if (names.Length == 0)
        {
            ShowLabel("No states yet");
            return;
        }
        var i = Array.IndexOf(names, current);
        if (names.Length == 1 && i == 0)
        {
            ShowLabel($"{current}\n(the only state)");
            return;
        }
        var next = i < 0 ? 0 : (i + delta + names.Length) % names.Length;
        receiver.SetState(names[next]);
        ShowLabel($"{names[next]}\n{next + 1} of {names.Length}, loading");
    }

    // Names the state whenever it changes: on connecting, after a switch, or
    // when the DataManager's current state changes.
    private void ShowStateChanges()
    {
        var name = receiver.CurrentState?.Name ?? "";
        if (name != shownState && name.Length > 0)
        {
            shownState = name;
            var names = SortedStates();
            var i = Array.IndexOf(names, name);
            ShowLabel(i >= 0 && names.Length > 1 ? $"{name}\n{i + 1} of {names.Length}" : name);
        }
        var upstream = receiver.UpstreamStatus;
        if (upstream.HasValue && upstream.Value.IsOnline != shownOnline)
        {
            var wasKnown = shownOnline.HasValue;
            shownOnline = upstream.Value.IsOnline;
            if (!upstream.Value.IsOnline)
            {
                // "discovery": it was found by discovery, and can't be found now.
                var who = upstream.Value.Offline == "discovery" ? "the DataManager can't be found"
                                                                : $"{upstream.Value.Offline} can't be reached";
                ShowLabel($"Offline: showing cached data\n{who}", 6f);
            }
            else if (wasKnown)
            {
                ShowLabel("DataManager online again");
            }
        }
        if (label != null && label.enabled && Time.unscaledTime > labelUntil)
        {
            label.enabled = false;
        }
    }

    private void ShowLabel(string text, float seconds = 3f)
    {
        if (label == null)
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null)
            {
                return;
            }
            var go = new GameObject("State Label");
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, -0.15f, 0.8f);
            label = go.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.fontSize = 0.3f;
            // Match the status panel's look where there is one.
            var status = GameObject.Find("Status Text")?.GetComponent<TextMeshPro>();
            if (status != null)
            {
                label.font = status.font;
                label.color = status.color;
            }
        }
        label.text = text;
        label.enabled = true;
        labelUntil = Time.unscaledTime + seconds;
    }
}
