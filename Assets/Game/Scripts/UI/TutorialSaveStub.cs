using TrafficSim.Core;
using UnityEngine;

namespace TrafficSim.UI
{
    public static class TutorialSaveStub
    {
        public const string TutorialCompleteKey = "TrafficSim.TutorialComplete";
        public const string FreeModuleChoiceKey = "TrafficSim.FreeModuleChoice";
        public const string SkipEodUiKey = "TrafficSim.SkipEodUi";

        public static bool IsTutorialComplete => PlayerPrefs.GetInt(TutorialCompleteKey, 0) == 1;

        public static bool CanSkipEodUi => IsTutorialComplete;

        public static bool ShouldSkipEodUi => CanSkipEodUi && PlayerPrefs.GetInt(SkipEodUiKey, 0) == 1;

        public static bool TryGetFreeModuleChoice(out ServiceModule module)
        {
            if (!PlayerPrefs.HasKey(FreeModuleChoiceKey))
            {
                module = default;
                return false;
            }

            module = (ServiceModule)PlayerPrefs.GetInt(FreeModuleChoiceKey, (int)ServiceModule.Car);
            return true;
        }

        public static void SetFreeModuleChoice(ServiceModule module) =>
            PlayerPrefs.SetInt(FreeModuleChoiceKey, (int)module);

        public static void SetSkipEodUi(bool skip) =>
            PlayerPrefs.SetInt(SkipEodUiKey, skip ? 1 : 0);
    }
}
