using System.IO;
using System.Linq;
using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AlicizaX.Localization.Editor
{
    internal sealed class LocalizationSettingsBuildProcessor :
#if UNITY_6000_3_OR_NEWER
        IPreprocessBuildWithContext, IPostprocessBuildWithContext
#else
        IPreprocessBuildWithReport, IPostprocessBuildWithReport
#endif
    {
        private const string TempFolder = "Assets/_Temp";
        private const string TempAsset = TempFolder + "/AlicizaX.LocalizationSettings.asset";
        private const string BuildMarker = "Library/AlicizaX.LocalizationSettings.build";
        public int callbackOrder => 0;

#if UNITY_6000_3_OR_NEWER
        public void OnPreprocessBuild(BuildCallbackContext ctx) { if (ctx.IsPlayerBuild) Prepare(); }
        public void OnPostprocessBuild(BuildCallbackContext ctx) { if (ctx.IsPlayerBuild) Cleanup(); }
#else
        public void OnPreprocessBuild(BuildReport report) => Prepare();
        public void OnPostprocessBuild(BuildReport report) => Cleanup();
#endif

        [InitializeOnLoadMethod]
        private static void RecoverOnEditorStart()
        {
            const string sessionKey = "AlicizaX.Localization.CheckedBuildSettings";
            if (AssetDatabase.IsAssetImportWorkerProcess() || SessionState.GetBool(sessionKey, false)) return;
            SessionState.SetBool(sessionKey, true);
            EditorApplication.delayCall += () => { if (!BuildPipeline.isBuildingPlayer) Cleanup(); };
        }

        internal static void Prepare()
        {
            Cleanup();
            if (!File.Exists(LocalizationSettings.ProjectPath)) return;
            if (File.Exists(TempAsset))
                throw new BuildFailedException("Localization build path is occupied: " + TempAsset);

            bool createFolder = !AssetDatabase.IsValidFolder(TempFolder);
            File.WriteAllText(BuildMarker, createFolder ? "created-folder" : "");
            try
            {
                if (createFolder) AssetDatabase.CreateFolder("Assets", "_Temp");
                File.Copy(LocalizationSettings.ProjectPath, TempAsset);
                AssetDatabase.ImportAsset(TempAsset, ImportAssetOptions.ForceSynchronousImport);
                var copy = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(TempAsset);
                if (copy == null) throw new BuildFailedException("Cannot import localization settings: " + TempAsset);
                PlayerSettings.SetPreloadedAssets(PlayerSettings.GetPreloadedAssets().Append(copy).ToArray());
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        internal static void Cleanup()
        {
            if (!File.Exists(BuildMarker)) return;
            var copy = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(TempAsset);
            if (copy != null)
                PlayerSettings.SetPreloadedAssets(PlayerSettings.GetPreloadedAssets().Where(asset => asset != copy).ToArray());
            if (File.Exists(TempAsset) && !AssetDatabase.DeleteAsset(TempAsset))
                throw new IOException("Cannot delete temporary localization settings: " + TempAsset);
            if (File.ReadAllText(BuildMarker) == "created-folder" && Directory.Exists(TempFolder)
                && !Directory.EnumerateFileSystemEntries(TempFolder).Any())
                AssetDatabase.DeleteAsset(TempFolder);
            File.Delete(BuildMarker);
        }
    }
}
