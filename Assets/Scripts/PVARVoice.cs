using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

// Quest: talk to the PVLink voice service (pvbridge SPEC-pvlink-voice.md 9,
// voice/nli_server.py). Hold X (left controller) and speak; release to send.
// What was heard and the reply show below the line of sight, and the reply is
// spoken through the headset. Pressing X again cuts a spoken reply short.
//
// Configured by pvar_voice.json in the app's files folder
// (/sdcard/Android/data/<package>/files):
//     {"service": "http://10.0.0.171:9030", "token": "...", "button": "X"}
// Without that file voice control is off. Made at startup on an object of its
// own, so scenes need no change. Uses .NET HttpClient, not UnityWebRequest: the
// project doesn't allow plain http for UnityWebRequest, and the service is on
// the LAN without TLS (the token is the protection).
public class PVARVoice : MonoBehaviour
{
    [Serializable]
    private class Config
    {
        public string service = "";
        public string token = "";
        public string button = "X";
        public float maxSeconds = 15f;
    }

    [Serializable]
    private class Reply
    {
        public string request_id = "";
        public string heard = "";
        public string reply = "";
        public string audio_url = "";
        public bool cancelled;
        public bool ignored;
        public bool pending_question;
        public string error = "";
    }

    private const string ConfigFile = "pvar_voice.json";

    private Config cfg;
    private HttpClient http;
    private AudioSource speaker;
    private TextMeshPro label;
    private float labelUntil;
    private string mic;
    private AudioClip clip;
    private bool recording;
    private float recordStart;
    private Task<string> asking;
    private Task<byte[]> fetchingAudio;

#if UNITY_ANDROID
    private OVRInput.Button button = OVRInput.Button.Three;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var path = Path.Combine(Application.persistentDataPath, ConfigFile);
        if (!File.Exists(path))
        {
            Debug.Log($"[PVARVoice] No {path}: voice control is off");
            return;
        }
        Config cfg;
        try
        {
            cfg = JsonUtility.FromJson<Config>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PVARVoice] Can't read {path}: {e.Message}");
            return;
        }
        if (string.IsNullOrEmpty(cfg.service) || string.IsNullOrEmpty(cfg.token))
        {
            Debug.LogWarning($"[PVARVoice] {path} needs \"service\" and \"token\": voice control is off");
            return;
        }
        var go = new GameObject("PVAR Voice");
        go.AddComponent<PVARVoice>().cfg = cfg;
        Debug.Log($"[PVARVoice] Hold {cfg.button} to talk to {cfg.service}");
    }

    private void Start()
    {
        button = cfg.button switch
        {
            "A" => OVRInput.Button.One,
            "B" => OVRInput.Button.Two,
            "Y" => OVRInput.Button.Four,
            _ => OVRInput.Button.Three,
        };
        if (button == OVRInput.Button.Three)
        {
            // X is the locator's "forget learned tag positions"; take it over.
            foreach (var controls in FindObjectsByType<QuestRoomControls>(FindObjectsSortMode.None))
            {
                controls.ForgetLearnedOnX = false;
                controls.XHint = "X: hold to talk";
            }
        }
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
        }
        http = new HttpClient
        {
            BaseAddress = new Uri(cfg.service.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(180),
        };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.token);
        speaker = gameObject.AddComponent<AudioSource>();
        speaker.playOnAwake = false;
        speaker.spatialBlend = 0f;   // in the head, not placed in the room
        speaker.volume = 1f;
        // Unity plays nothing without a listener, and the scene may have none.
        var listener = FindAnyObjectByType<AudioListener>();
        if (listener == null)
        {
            var head = Camera.main != null ? Camera.main.gameObject : gameObject;
            head.AddComponent<AudioListener>();
            Debug.Log($"[PVARVoice] No AudioListener in the scene; added one to {head.name}");
        }
        else
        {
            Debug.Log($"[PVARVoice] AudioListener on {listener.gameObject.name} (enabled {listener.enabled})");
        }
    }

    private void OnDestroy()
    {
        http?.Dispose();
    }

    private void Update()
    {
        if (OVRInput.GetDown(button))
        {
            if (speaker.isPlaying)
            {
                speaker.Stop();   // a press during a spoken reply only cuts it short
            }
            else
            {
                StartRecording();
            }
        }
        if (recording && (OVRInput.GetUp(button) || Time.unscaledTime - recordStart > cfg.maxSeconds))
        {
            StopAndSend();
        }
        if (asking != null && asking.IsCompleted)
        {
            HandleReply();
        }
        if (fetchingAudio != null && fetchingAudio.IsCompleted)
        {
            PlayReply();
        }
        if (label != null && label.enabled && !recording && asking == null && Time.unscaledTime > labelUntil)
        {
            label.enabled = false;
        }
    }

    private void StartRecording()
    {
        if (asking != null)
        {
            ShowLabel("Still working on the last request...", 2f);
            return;
        }
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            ShowLabel("Allow the microphone, then try again", 4f);
            return;
        }
        mic = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
        Microphone.GetDeviceCaps(mic, out var minFreq, out var maxFreq);
        var freq = 16000;   // what speech recognition wants; any rate works
        if (maxFreq > 0 && (freq < minFreq || freq > maxFreq))
        {
            freq = maxFreq;
        }
        clip = Microphone.Start(mic, false, Mathf.CeilToInt(cfg.maxSeconds) + 1, freq);
        recording = clip != null;
        recordStart = Time.unscaledTime;
        ShowLabel(recording ? $"Listening... (release {cfg.button} to send)" : "No microphone", recording ? 999f : 3f);
    }

    private void StopAndSend()
    {
        var length = Microphone.GetPosition(mic);
        Microphone.End(mic);
        recording = false;
        if (clip == null || length < clip.frequency / 4)
        {
            ShowLabel($"Too short: hold {cfg.button} while you speak", 2.5f);
            return;
        }
        var samples = new float[length * clip.channels];
        clip.GetData(samples, 0);
        var wav = ToWav(samples, clip.channels, clip.frequency);
        var id = Guid.NewGuid().ToString("N").Substring(0, 8);
        ShowLabel("Thinking...", 999f);
        asking = Ask(wav, id);
    }
#endif

    private async Task<string> Ask(byte[] wav, string id)
    {
        using var content = new ByteArrayContent(wav);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using var response = await http.PostAsync($"ask?speak=1&request_id={id}", content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return JsonUtility.ToJson(new Reply { error = $"HTTP {(int)response.StatusCode}: {body}" });
        }
        return body;
    }

    private void HandleReply()
    {
        var task = asking;
        asking = null;
        Reply r;
        if (task.IsFaulted)
        {
            r = new Reply { error = task.Exception?.GetBaseException().Message ?? "failed" };
        }
        else
        {
            try
            {
                r = JsonUtility.FromJson<Reply>(task.Result);
            }
            catch (Exception e)
            {
                r = new Reply { error = e.Message };
            }
        }
        if (!string.IsNullOrEmpty(r.error) && string.IsNullOrEmpty(r.reply))
        {
            Debug.LogWarning($"[PVARVoice] {r.error}");
            ShowLabel("Voice service: " + Shorten(r.error, 120), 6f);
            return;
        }
        if (r.ignored)
        {
            ShowLabel("Didn't catch that", 2.5f);
            return;
        }
        if (r.cancelled)
        {
            ShowLabel($"Cancelled\n\"{Shorten(r.heard, 80)}\"", 3f);
            return;
        }
        Debug.Log($"[PVARVoice] heard \"{r.heard}\"; reply {r.reply.Length} chars; audio {(string.IsNullOrEmpty(r.audio_url) ? "none" : r.audio_url)}");
        var seconds = Mathf.Clamp(3f + r.reply.Length / 15f, 4f, 15f);
        ShowLabel($"\"{Shorten(r.heard, 100)}\"\n{r.reply}", seconds);
        if (!string.IsNullOrEmpty(r.audio_url))
        {
            fetchingAudio = http.GetByteArrayAsync(r.audio_url.TrimStart('/'));
        }
    }

    private void PlayReply()
    {
        var task = fetchingAudio;
        fetchingAudio = null;
        if (task.IsFaulted)
        {
            Debug.LogWarning($"[PVARVoice] Reply audio: {task.Exception?.GetBaseException().Message}");
            return;
        }
        var reply = FromWav(task.Result);
        if (reply == null)
        {
            Debug.LogWarning($"[PVARVoice] Reply audio ({task.Result?.Length ?? 0} bytes) isn't 16-bit PCM WAV");
            return;
        }
        speaker.clip = reply;
        speaker.Play();
        Debug.Log($"[PVARVoice] Playing the reply: {reply.length:F1} s, {reply.frequency} Hz, " +
                  $"listener volume {AudioListener.volume}, playing {speaker.isPlaying}");
    }

    private static string Shorten(string s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s.Substring(0, n - 3) + "...";

    // 16-bit PCM WAV.
    private static byte[] ToWav(float[] samples, int channels, int rate)
    {
        using var ms = new MemoryStream(44 + samples.Length * 2);
        using var w = new BinaryWriter(ms);
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + samples.Length * 2);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16);
        w.Write((short)1);
        w.Write((short)channels);
        w.Write(rate);
        w.Write(rate * channels * 2);
        w.Write((short)(channels * 2));
        w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(samples.Length * 2);
        foreach (var s in samples)
        {
            w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
        w.Flush();
        return ms.ToArray();
    }

    // The service's replies: 16-bit PCM WAV (macOS `say`).
    private static AudioClip FromWav(byte[] b)
    {
        if (b == null || b.Length < 44 || System.Text.Encoding.ASCII.GetString(b, 0, 4) != "RIFF")
        {
            return null;
        }
        int channels = 1, rate = 22050, bits = 16, pos = 12;
        while (pos + 8 <= b.Length)
        {
            var id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
            var size = BitConverter.ToInt32(b, pos + 4);
            if (id == "fmt ")
            {
                channels = BitConverter.ToInt16(b, pos + 10);
                rate = BitConverter.ToInt32(b, pos + 12);
                bits = BitConverter.ToInt16(b, pos + 22);
            }
            else if (id == "data")
            {
                if (bits != 16)
                {
                    return null;
                }
                var n = Math.Min(size, b.Length - pos - 8) / 2;
                var data = new float[n];
                for (var i = 0; i < n; i++)
                {
                    data[i] = BitConverter.ToInt16(b, pos + 8 + 2 * i) / 32768f;
                }
                var clip = AudioClip.Create("Voice reply", n / channels, channels, rate, false);
                clip.SetData(data, 0);
                return clip;
            }
            pos += 8 + size + (size & 1);
        }
        return null;
    }

    private void ShowLabel(string text, float seconds)
    {
        if (label == null)
        {
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head == null)
            {
                return;
            }
            var go = new GameObject("Voice Label");
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, -0.3f, 0.8f);   // below PVARThumbstick's label
            label = go.AddComponent<TextMeshPro>();
            label.alignment = TextAlignmentOptions.Top;
            label.rectTransform.sizeDelta = new Vector2(0.7f, 0.3f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.fontSize = 0.25f;
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
