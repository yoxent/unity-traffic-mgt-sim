#if UNITY_EDITOR
using TrafficSim.Camera;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Events;
using TrafficSim.Input;
using TrafficSim.Map;
using TrafficSim.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

namespace TrafficSim.Editor
{
    public static class MvpDistrictSceneSetup
    {
        const string ScenePath = "Assets/Game/Scenes/MVP_District.unity";
        const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        const string DefaultTmpFontPath = "Assets/Game/UI/Fonts/TMP/IBMPlexSans-Regular SDF.asset";

        [MenuItem("TrafficSim/Setup/MVP District Scene")]
        public static void Setup()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EnsureCamera();
            var bootstrap = EnsureGameObject("SimSystems", typeof(GameBootstrap));
            var input = EnsureGameObject("GameInput", typeof(GameInputReader));
            var worldView = EnsureGameObject("WorldView", typeof(MapWorldView));
            var canvas = EnsureCanvas();
            ClearManagedUi(canvas.transform);

            var hud = BuildHud(canvas.transform);
            var demand = BuildDemandPanel(canvas.transform);
            var eod = BuildEodPanel(canvas.transform);
            var modules = BuildModulePanel(canvas.transform);

            WireBootstrap(
                bootstrap.GetComponent<GameBootstrap>(),
                hud,
                demand,
                eod,
                modules,
                input.GetComponent<GameInputReader>(),
                worldView.GetComponent<MapWorldView>());
            WireInput(input.GetComponent<GameInputReader>());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("MvpDistrictSceneSetup: MVP_District scene wired.");
        }

        static void EnsureCamera()
        {
            var cameraGo = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
            if (cameraGo.GetComponent<UnityEngine.Camera>() == null)
                cameraGo.AddComponent<UnityEngine.Camera>();

            cameraGo.tag = "MainCamera";
            // Top-down orthographic: look down -Y onto the XZ map plane.
            cameraGo.transform.position = new Vector3(5f, 30f, 5f);
            cameraGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (cameraGo.GetComponent<AudioListener>() == null)
                cameraGo.AddComponent<AudioListener>();

            if (cameraGo.GetComponent<OrthoPanZoomCamera>() == null)
                cameraGo.AddComponent<OrthoPanZoomCamera>();

            var cam = cameraGo.GetComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 14f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
        }

        static GameObject EnsureCanvas()
        {
            var canvasGo = GameObject.Find("Canvas") ?? new GameObject("Canvas");
            var canvas = canvasGo.GetComponent<Canvas>() ?? canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (canvasGo.GetComponent<CanvasScaler>() == null)
                canvasGo.AddComponent<CanvasScaler>();
            if (canvasGo.GetComponent<GraphicRaycaster>() == null)
                canvasGo.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            return canvasGo;
        }

        static void ClearManagedUi(Transform canvas)
        {
            var managedNames = new[] { "Hud", "DemandPanel", "EodPanel", "ModulePanel" };

            for (var i = canvas.childCount - 1; i >= 0; i--)
            {
                var child = canvas.GetChild(i);
                foreach (var panelName in managedNames)
                {
                    if (child.name != panelName)
                        continue;

                    Object.DestroyImmediate(child.gameObject);
                    break;
                }
            }
        }

        static GameObject EnsureGameObject(string name, System.Type componentType)
        {
            var go = GameObject.Find(name) ?? new GameObject(name);
            if (go.GetComponent(componentType) == null)
                go.AddComponent(componentType);
            return go;
        }

        static GameHud BuildHud(Transform parent)
        {
            var root = CreatePanel("Hud", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -12f), new Vector2(360f, 156f));
            var hud = root.AddComponent<GameHud>();

            var money = CreateText("MoneyText", root.transform, "$0", new Vector2(0f, 0f));
            var stars = CreateText("StarsText", root.transform, "3.0 stars", new Vector2(0f, -24f));
            var day = CreateText("DayText", root.transform, "Day 1", new Vector2(0f, -48f));
            var clock = CreateText("ClockText", root.transform, "06:00 · Morning", new Vector2(0f, -72f));
            var time = CreateText("TimeOfDayText", root.transform, "Morning", new Vector2(0f, -96f));
            time.gameObject.SetActive(false); // period is shown on ClockText; keep ref for older layouts
            var speed = CreateText("SpeedText", root.transform, "1x", new Vector2(0f, -96f));
            var progressFill = CreateDayProgressBar(root.transform, new Vector2(8f, -128f), new Vector2(344f, 12f));

            SetUiTextRef(hud, "_moneyText", money);
            SetUiTextRef(hud, "_starsText", stars);
            SetUiTextRef(hud, "_dayText", day);
            SetUiTextRef(hud, "_clockText", clock);
            SetUiTextRef(hud, "_timeOfDayText", time);
            SetUiTextRef(hud, "_speedText", speed);

            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("_dayProgressFill").objectReferenceValue = progressFill;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            return hud;
        }

        static Image CreateDayProgressBar(Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            var trackGo = new GameObject("DayProgressTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trackGo.transform.SetParent(parent, false);
            var trackImage = trackGo.GetComponent<Image>();
            trackImage.color = new Color(0.15f, 0.15f, 0.18f, 0.85f);

            var trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 1f);
            trackRect.anchorMax = new Vector2(0f, 1f);
            trackRect.pivot = new Vector2(0f, 1f);
            trackRect.anchoredPosition = anchoredPos;
            trackRect.sizeDelta = size;

            var fillGo = new GameObject("DayProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            var fillImage = fillGo.GetComponent<Image>();
            fillImage.color = new Color(0.95f, 0.78f, 0.28f, 1f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;

            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            return fillImage;
        }

        static DemandCheckpointPanel BuildDemandPanel(Transform parent)
        {
            var root = CreatePanel("DemandPanel", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -12f), new Vector2(280f, 90f));
            var panel = root.AddComponent<DemandCheckpointPanel>();

            var line0 = CreateText("Checkpoint0", root.transform, string.Empty, new Vector2(0f, 0f));
            var line1 = CreateText("Checkpoint1", root.transform, string.Empty, new Vector2(0f, -24f));
            var line2 = CreateText("Checkpoint2", root.transform, string.Empty, new Vector2(0f, -48f));

            var so = new SerializedObject(panel);
            so.FindProperty("_checkpointLines").arraySize = 3;
            SetUiTextRefElement(so.FindProperty("_checkpointLines").GetArrayElementAtIndex(0), line0);
            SetUiTextRefElement(so.FindProperty("_checkpointLines").GetArrayElementAtIndex(1), line1);
            SetUiTextRefElement(so.FindProperty("_checkpointLines").GetArrayElementAtIndex(2), line2);
            so.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        static EodPanel BuildEodPanel(Transform parent)
        {
            var root = CreatePanel("EodPanel", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 320f));
            root.SetActive(false);
            var panel = root.AddComponent<EodPanel>();

            var summary = CreateText("SummaryText", root.transform, "EOD Summary", new Vector2(0f, 80f));
            var queue = CreateText("QueueText", root.transform, string.Empty, new Vector2(0f, 20f));
            var continueBtn = CreateButton("ContinueButton", root.transform, "Continue", new Vector2(0f, -80f));
            var skipToggle = CreateToggle("SkipToggle", root.transform, "Skip EOD UI", new Vector2(0f, -120f));

            var so = new SerializedObject(panel);
            so.FindProperty("_root").objectReferenceValue = root;
            SetUiTextRefProperty(so.FindProperty("_summaryText"), summary);
            SetUiTextRefProperty(so.FindProperty("_queueText"), queue);
            so.FindProperty("_continueButton").objectReferenceValue = continueBtn;
            so.FindProperty("_skipToggle").objectReferenceValue = skipToggle;
            so.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        static ModulePurchasePanel BuildModulePanel(Transform parent)
        {
            var root = CreatePanel("ModulePanel", parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(520f, 120f));
            var panel = root.AddComponent<ModulePurchasePanel>();

            var carBtn = CreateButton("BuyCar", root.transform, "Car", new Vector2(-120f, 0f));
            var foodBtn = CreateButton("BuyFood", root.transform, "Food", new Vector2(120f, 0f));
            var status = CreateText("StatusText", root.transform, string.Empty, new Vector2(0f, -48f));

            var carDef = LoadAsset<ServiceModuleDef>("Assets/Game/Data/Modules/CarModule.asset");
            var foodDef = LoadAsset<ServiceModuleDef>("Assets/Game/Data/Modules/FoodModule.asset");

            var so = new SerializedObject(panel);
            so.FindProperty("_offers").arraySize = 2;

            var offer0 = so.FindProperty("_offers").GetArrayElementAtIndex(0);
            offer0.FindPropertyRelative("def").objectReferenceValue = carDef;
            offer0.FindPropertyRelative("buyButton").objectReferenceValue = carBtn;
            SetUiTextRefProperty(offer0.FindPropertyRelative("label"), carBtn.GetComponentInChildren<TextMeshProUGUI>());

            var offer1 = so.FindProperty("_offers").GetArrayElementAtIndex(1);
            offer1.FindPropertyRelative("def").objectReferenceValue = foodDef;
            offer1.FindPropertyRelative("buyButton").objectReferenceValue = foodBtn;
            SetUiTextRefProperty(offer1.FindPropertyRelative("label"), foodBtn.GetComponentInChildren<TextMeshProUGUI>());

            SetUiTextRefProperty(so.FindProperty("_statusText"), status);
            so.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        static void WireBootstrap(
            GameBootstrap bootstrap,
            GameHud hud,
            DemandCheckpointPanel demand,
            EodPanel eod,
            ModulePurchasePanel modules,
            GameInputReader input,
            MapWorldView worldView)
        {
            var so = new SerializedObject(bootstrap);
            so.FindProperty("_mapSkeleton").objectReferenceValue =
                LoadAsset<MapSkeleton>("Assets/Game/Data/Maps/TutorialDistrict.asset");
            so.FindProperty("_mapAddress").stringValue = "Maps/TutorialDistrict";
            so.FindProperty("_useAddressables").boolValue = true;
            so.FindProperty("_demandWaveDef").objectReferenceValue =
                LoadAsset<DemandWaveDef>("Assets/Game/Data/Demand/TutorialDemand.asset");
            so.FindProperty("_overloadDef").objectReferenceValue =
                LoadAsset<OverloadDef>("Assets/Game/Data/Overload/DefaultOverload.asset");
            so.FindProperty("_ratingDef").objectReferenceValue =
                LoadAsset<RatingDef>("Assets/Game/Data/Rating/DefaultRating.asset");

            SetArray(so.FindProperty("_moduleDefs"),
                LoadAsset<ServiceModuleDef>("Assets/Game/Data/Modules/CarModule.asset"),
                LoadAsset<ServiceModuleDef>("Assets/Game/Data/Modules/FoodModule.asset"));
            SetArray(so.FindProperty("_hubDefs"),
                LoadAsset<HubDef>("Assets/Game/Data/Hubs/CarHub.asset"),
                LoadAsset<HubDef>("Assets/Game/Data/Hubs/FoodHub.asset"));
            SetArray(so.FindProperty("_vehicleDefs"),
                LoadAsset<VehicleDef>("Assets/Game/Data/Vehicles/Bicycle.asset"),
                LoadAsset<VehicleDef>("Assets/Game/Data/Vehicles/Motorbike.asset"),
                LoadAsset<VehicleDef>("Assets/Game/Data/Vehicles/FourSeater.asset"));

            so.FindProperty("_dayEndedChannel").objectReferenceValue =
                LoadAsset<GameEventChannel>("Assets/Game/Data/Events/DayEnded.asset");
            so.FindProperty("_eodStartedChannel").objectReferenceValue =
                LoadAsset<GameEventChannel>("Assets/Game/Data/Events/EodStarted.asset");
            so.FindProperty("_dayAdvancedChannel").objectReferenceValue =
                LoadAsset<GameEventChannel>("Assets/Game/Data/Events/DayAdvanced.asset");
            so.FindProperty("_runFailedChannel").objectReferenceValue =
                LoadAsset<GameEventChannel>("Assets/Game/Data/Events/RunFailed.asset");
            so.FindProperty("_orderAssignedChannel").objectReferenceValue =
                LoadAsset<OrderEventChannel>("Assets/Game/Data/Events/OrderAssigned.asset");
            so.FindProperty("_orderCompletedChannel").objectReferenceValue =
                LoadAsset<OrderEventChannel>("Assets/Game/Data/Events/OrderCompleted.asset");

            so.FindProperty("_inputReader").objectReferenceValue = input;
            so.FindProperty("_hud").objectReferenceValue = hud;
            so.FindProperty("_demandPanel").objectReferenceValue = demand;
            so.FindProperty("_eodPanel").objectReferenceValue = eod;
            so.FindProperty("_modulePanel").objectReferenceValue = modules;
            so.FindProperty("_worldView").objectReferenceValue = worldView;
            so.ApplyModifiedPropertiesWithoutUndo();

            WireWorldView(worldView);
        }

        static void WireWorldView(MapWorldView worldView)
        {
            var so = new SerializedObject(worldView);
            so.FindProperty("_roadStraightPrefab").objectReferenceValue =
                LoadAsset<GameObject>("Assets/Game/Prefabs/World/Roads/Road_NS.prefab");
            so.FindProperty("_roadCornerPrefab").objectReferenceValue =
                LoadAsset<GameObject>("Assets/Game/Prefabs/World/Roads/Road_ES.prefab");
            so.FindProperty("_roadTJunctionPrefab").objectReferenceValue =
                LoadAsset<GameObject>("Assets/Game/Prefabs/World/Roads/Road_EWS.prefab");
            so.FindProperty("_roadCrossPrefab").objectReferenceValue =
                LoadAsset<GameObject>("Assets/Game/Prefabs/World/Roads/Road_NEWS.prefab");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void WireInput(GameInputReader input)
        {
            var so = new SerializedObject(input);
            so.FindProperty("_actions").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            so.FindProperty("_camera").objectReferenceValue =
                Object.FindFirstObjectByType<OrthoPanZoomCamera>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return go;
        }

        static TextMeshProUGUI CreateText(string name, Transform parent, string content, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultTmpFontPath);
            text.fontSize = 16f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(-16f, 22f);
            return text;
        }

        static Button CreateButton(string name, Transform parent, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.2f, 0.45f, 0.85f, 1f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(160f, 36f);

            CreateText("Label", go.transform, label, Vector2.zero).alignment = TextAlignmentOptions.Center;
            return go.GetComponent<Button>();
        }

        static Toggle CreateToggle(string name, Transform parent, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(220f, 24f);

            var labelText = CreateText("Label", go.transform, label, new Vector2(24f, 0f));
            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = go.AddComponent<Image>();
            return toggle;
        }

        static void SetUiTextRef(Object host, string propertyName, TMP_Text text)
        {
            var so = new SerializedObject(host);
            SetUiTextRefProperty(so.FindProperty(propertyName), text);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetUiTextRefProperty(SerializedProperty property, TMP_Text text)
        {
            property.FindPropertyRelative("_target").objectReferenceValue = text;
        }

        static void SetUiTextRefElement(SerializedProperty property, TMP_Text text) =>
            SetUiTextRefProperty(property, text);

        static void SetArray(SerializedProperty property, params Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static T LoadAsset<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
#endif
