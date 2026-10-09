using UnityEditor;

// PVAR: the Locator with ParaView geometry from a PVLink DataManager, placed in
// the shared room above the reference post.
// Menu: Tools > PVAR. Batchmode (re-runnable; Build also configures):
//   -buildTarget iOS -executeMethod PVARApp.BuildIOS        (Xcode project in Builds/iOS)
//   -buildTarget Android -executeMethod PVARApp.BuildQuest  (Builds/Quest/PVAR.apk)
public static class PVARApp
{
    public static readonly AprilTagsAppInfo App = new()
    {
        ProductName = "PVAR",
        BundleId = "edu.utexas.tacc.pvar",
        AppleTeamId = "MQSWMVW9A8",
        LogFileName = "pvar.log",
        IPhoneScenes = new[] { "Assets/Scenes/iPhone PVAR.unity" },
        QuestScenes = new[] { "Assets/Scenes/Quest PVAR.unity" },
    };

    [MenuItem("Tools/PVAR/Configure iOS")]
    public static void ConfigureIOS() => AprilTagsIOSSetup.Configure(App, PVARContent.Add);

    [MenuItem("Tools/PVAR/Build iOS Xcode Project")]
    public static void BuildIOS()
    {
        ConfigureIOS();
        AprilTagsIOSSetup.Build(App);
    }

    [MenuItem("Tools/PVAR/Configure Quest")]
    public static void ConfigureQuest() => AprilTagsQuestSetup.Configure(App, PVARContent.Add);

    [MenuItem("Tools/PVAR/Build Quest APK")]
    public static void BuildQuest()
    {
        ConfigureQuest();
        AprilTagsQuestSetup.Build(App);
    }
}
