#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TrafficSim.Editor
{
    public static class AddressablesMapSetup
    {
        const string MapsGroupName = "Maps";
        const string TutorialDistrictPath = "Assets/Game/Data/Maps/TutorialDistrict.asset";
        const string TutorialDistrictAddress = "Maps/TutorialDistrict";

        [MenuItem("TrafficSim/Setup/Addressables Maps")]
        public static void SetupMapsGroup()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                Debug.LogError("AddressablesMapSetup: could not create Addressable Asset Settings.");
                return;
            }

            var group = settings.FindGroup(MapsGroupName);
            if (group == null)
                group = settings.CreateGroup(MapsGroupName, false, false, true, null);

            var guid = AssetDatabase.AssetPathToGUID(TutorialDistrictPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"AddressablesMapSetup: asset not found at {TutorialDistrictPath}");
                return;
            }

            var entry = settings.CreateOrMoveEntry(guid, group);
            entry.address = TutorialDistrictAddress;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"AddressablesMapSetup: registered {TutorialDistrictAddress} in group '{MapsGroupName}'.");
        }
    }
}
#endif
