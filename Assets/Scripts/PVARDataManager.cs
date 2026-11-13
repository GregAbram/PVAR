using System;
using System.IO;
using ParaViewLink;
using UnityEngine;

// PVAR's data comes through a DataManager inside the app, whose store caches
// the room's DataManager: what it sends is kept on the device, and shown from
// there while it can't be reached (DataManager SPEC.md 15). The room config (or
// a scanned room code) names the DataManager ("host" or "host:port"); without
// one it's found by discovery.
//
// The store: on Quest /sdcard/DataManager/store, shared with the DataManager
// example apps (needs All files access, which the AprilTags Quest build
// declares; grant it once with adb shell appops set edu.utexas.tacc.pvar
// MANAGE_EXTERNAL_STORAGE allow); elsewhere a store in the app's own folder.
public class PVARDataManager : MonoBehaviour
{
    [SerializeField] private AprilTagRoomLocalizer localizer;
    [SerializeField] private MeshReceiver receiver;

    public const string QuestStore = "/sdcard/DataManager/store";

    private string current;

    public static string StorePath =>
        Application.platform == RuntimePlatform.Android
            ? QuestStore
            : Path.Combine(Application.persistentDataPath, "store");

    private void OnEnable()
    {
        localizer.ConfigChanged += OnConfigChanged;
        if (localizer.IsConfigLoaded)
        {
            OnConfigChanged(localizer.LoadedConfig);
        }
        else
        {
            Use("");   // discovery, until a room names a DataManager
        }
    }

    private void OnDisable() => localizer.ConfigChanged -= OnConfigChanged;

    private void OnConfigChanged(RoomConfig config)
    {
        var address = (config.dataManager ?? "").Trim();
        if (address == current)
        {
            return;
        }
        Debug.Log(address.Length > 0
            ? $"[PVARDataManager] Room '{config.name}' names DataManager {address}"
            : $"[PVARDataManager] Room '{config.name}' names no DataManager; finding one by discovery");
        Use(address);
    }

    private void Use(string address)
    {
        current = address;
        var host = address;
        var port = receiver.DataManagerPort;
        var colon = address.LastIndexOf(':');
        if (colon > 0 && int.TryParse(address[(colon + 1)..], out var parsed))
        {
            host = address[..colon];
            port = parsed;
        }
        var store = StorePath;
        try
        {
            Directory.CreateDirectory(store);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[PVARDataManager] Can't use the store {store} ({e.Message}); connecting without a cache");
            if (host.Length > 0)
            {
                receiver.ConnectToDataManager(host, port);
            }
            return;
        }
        Debug.Log($"[PVARDataManager] Caching {(host.Length > 0 ? $"{host}:{port}" : "a discovered DataManager")} in {store}");
        receiver.CacheDataManager(store, host, port);
    }
}
