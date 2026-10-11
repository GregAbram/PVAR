using ParaViewLink;
using UnityEditor;
using UnityEngine;

// PVAR's content, added to each new scene: PVLink's SimContainer (the volume
// the ParaView data is fitted into, with its MeshReceiver) centered on the
// magenta reference post's crossbar (room height 0, the first surveyed
// tag's center height), so every viewer sees the data in the same place,
// and PVARDataManager to use a room's DataManager address.
public static class PVARContent
{
    private const string ScalarMaterialPath = "Packages/com.paraviewlink.unity/Materials/M_ScalarField.mat";

    public static void Add(string scenePath, AprilTagsSceneParts parts)
    {
        var reference = parts.Anchor.transform.Find("Reference Marker");
        var container = GameObject.CreatePrimitive(PrimitiveType.Cube);
        container.name = "SimContainer";
        Object.DestroyImmediate(container.GetComponent<Collider>());
        container.transform.SetParent(reference != null ? reference : parts.Content, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localScale = Vector3.one * 0.6f;
        container.AddComponent<SimContainerObject>();
        var receiver = container.AddComponent<MeshReceiver>();
        receiver.BaseScalarMaterial = AssetDatabase.LoadAssetAtPath<Material>(ScalarMaterialPath);

        var link = parts.Localizer.gameObject.AddComponent<PVARDataManager>();
        var so = new SerializedObject(link);
        so.FindProperty("localizer").objectReferenceValue = parts.Localizer;
        so.FindProperty("receiver").objectReferenceValue = receiver;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
