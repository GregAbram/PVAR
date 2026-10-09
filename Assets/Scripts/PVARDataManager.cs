using ParaViewLink;
using UnityEngine;

// Points the MeshReceiver at the room's DataManager when the room config (or
// a scanned room code) names one ("host" or "host:port"); otherwise the
// receiver finds it itself by its LAN discovery broadcast.
public class PVARDataManager : MonoBehaviour
{
    [SerializeField] private AprilTagRoomLocalizer localizer;
    [SerializeField] private MeshReceiver receiver;

    private string current = "";

    private void OnEnable()
    {
        localizer.ConfigChanged += OnConfigChanged;
        if (localizer.IsConfigLoaded)
        {
            OnConfigChanged(localizer.LoadedConfig);
        }
    }

    private void OnDisable() => localizer.ConfigChanged -= OnConfigChanged;

    private void OnConfigChanged(RoomConfig config)
    {
        var address = (config.dataManager ?? "").Trim();
        if (address.Length == 0 || address == current)
        {
            return;
        }
        current = address;
        var host = address;
        var port = receiver.DataManagerPort;
        var colon = address.LastIndexOf(':');
        if (colon > 0 && int.TryParse(address[(colon + 1)..], out var parsed))
        {
            host = address[..colon];
            port = parsed;
        }
        Debug.Log($"[PVARDataManager] Room '{config.name}' names DataManager {host}:{port}");
        receiver.ConnectToDataManager(host, port);
    }
}
