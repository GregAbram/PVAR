using ParaViewLink;
using UnityEditor;
using UnityEngine;

// PVAR's content, added to each new scene: PVLink's SimContainer (the volume
// the ParaView data is fitted into, with its MeshReceiver), a child of the
// room anchor at a fixed room position and size, so every viewer sees the
// data in the same place; and PVARDataManager to use a room's DataManager
// address. To place the data elsewhere, move SimContainer in the scene.
// Quest scenes also get PVARStatusToggle (Y hides the status panel).
public static class PVARContent
{
    // Room frame (meters, y up): the TACC room's origin is a floor corner.
    private static readonly Vector3 DataCenter = new(-2f, 1.4f, -2f);
    private const float DataSize = 1f;

    private const string ScalarMaterialPath = "Packages/com.paraviewlink.unity/Materials/M_ScalarField.mat";

    public static void Add(string scenePath, AprilTagsSceneParts parts)
    {
        var container = GameObject.CreatePrimitive(PrimitiveType.Cube);
        container.name = "SimContainer";
        Object.DestroyImmediate(container.GetComponent<Collider>());
        container.transform.SetParent(parts.Anchor.transform, false);
        container.transform.localPosition = DataCenter;
        container.transform.localScale = Vector3.one * DataSize;
        container.AddComponent<SimContainerObject>();
        var receiver = container.AddComponent<MeshReceiver>();
        receiver.BaseScalarMaterial = AssetDatabase.LoadAssetAtPath<Material>(ScalarMaterialPath);

        var link = parts.Localizer.gameObject.AddComponent<PVARDataManager>();
        var so = new SerializedObject(link);
        so.FindProperty("localizer").objectReferenceValue = parts.Localizer;
        so.FindProperty("receiver").objectReferenceValue = receiver;
        so.ApplyModifiedPropertiesWithoutUndo();

        var status = GameObject.Find("Status Text")?.GetComponent<TMPro.TextMeshPro>();
        if (status != null && scenePath.Contains("Quest"))
        {
            var toggle = new SerializedObject(status.gameObject.AddComponent<PVARStatusToggle>());
            toggle.FindProperty("statusText").objectReferenceValue = status;
            toggle.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
