using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Events;
using TrafficSim.Input;
using TrafficSim.Map;
using TrafficSim.UI;
using UnityEngine;

namespace TrafficSim.Core
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        const string DefaultMapAddress = "Maps/TutorialDistrict";

        [Header("Map")]
        [SerializeField] MapSkeleton _mapSkeleton;
        [SerializeField] string _mapAddress = DefaultMapAddress;
        [SerializeField] bool _useAddressables = true;

        [Header("Data")]
        [SerializeField] DemandWaveDef _demandWaveDef;
        [SerializeField] OverloadDef _overloadDef;
        [SerializeField] RatingDef _ratingDef;
        [SerializeField] ServiceModuleDef[] _moduleDefs;
        [SerializeField] HubDef[] _hubDefs;
        [SerializeField] VehicleDef[] _vehicleDefs;
        [SerializeField] float _dayLengthSeconds = 300f;
        [SerializeField] float _startingMoney = 500f;

        [Header("Events")]
        [SerializeField] GameEventChannel _dayEndedChannel;
        [SerializeField] GameEventChannel _eodStartedChannel;
        [SerializeField] GameEventChannel _dayAdvancedChannel;
        [SerializeField] GameEventChannel _runFailedChannel;
        [SerializeField] OrderEventChannel _orderAssignedChannel;
        [SerializeField] OrderEventChannel _orderCompletedChannel;

        [Header("Scene")]
        [SerializeField] GameInputReader _inputReader;
        [SerializeField] GameHud _hud;
        [SerializeField] DemandCheckpointPanel _demandPanel;
        [SerializeField] EodPanel _eodPanel;
        [SerializeField] ModulePurchasePanel _modulePanel;

        SimSession _session;
        SimLoop _loop;
        SimEventBridge _events;
        MapSkeleton _loadedSkeleton;

        async void Start()
        {
            var graph = await LoadGraphAsync();
            if (graph == null)
                return;

            _session = SimComposition.Build(CreateConfig(), graph);
            _loop = new SimLoop(
                _session,
                _loadedSkeleton ?? _mapSkeleton,
                _orderCompletedChannel,
                _runFailedChannel);

            _events = new SimEventBridge(
                _session.Clock,
                _session.Eod,
                _dayEndedChannel,
                _eodStartedChannel,
                _dayAdvancedChannel);

            WireUi();
            RestoreFreeModuleChoice();

            if (_session.State.UnlockedModules.Count == 0)
                _session.State.UnlockedModules.Add(ServiceModule.Car);

            _inputReader?.Bind(_session.Clock);
        }

        void OnDestroy()
        {
            _events?.Dispose();

            if (_useAddressables && _loadedSkeleton != null && _loadedSkeleton != _mapSkeleton)
                MapLoader.Release(_loadedSkeleton);
        }

        void Update()
        {
            _loop?.Tick(Time.deltaTime);
        }

        SimBootstrapConfig CreateConfig() => new()
        {
            DemandWaveDef = _demandWaveDef,
            OverloadDef = _overloadDef,
            RatingDef = _ratingDef,
            ModuleDefs = _moduleDefs,
            HubDefs = _hubDefs,
            VehicleDefs = _vehicleDefs,
            DayLengthSeconds = _dayLengthSeconds,
            StartingMoney = _startingMoney,
            OrderAssignedChannel = _orderAssignedChannel
        };

        async Awaitable<RoadGraph> LoadGraphAsync()
        {
            if (_useAddressables && !string.IsNullOrWhiteSpace(_mapAddress))
            {
                var result = await MapLoader.LoadAsync(_mapAddress);
                if (result.Graph == null)
                    return null;

                _loadedSkeleton = result.Skeleton;
                return result.Graph;
            }

            if (_mapSkeleton == null)
            {
                Debug.LogError("GameBootstrap: assign MapSkeleton or enable Addressables map loading.");
                return null;
            }

            _loadedSkeleton = _mapSkeleton;
            return MapLoader.Load(_mapSkeleton);
        }

        void WireUi()
        {
            _hud?.Bind(_session.State, _session.Clock);
            _demandPanel?.Bind(_session.Demand, _session.Clock, _dayLengthSeconds);
            _eodPanel?.Bind(_session.State, _session.Eod);
            _modulePanel?.Bind(_session.State);
        }

        void RestoreFreeModuleChoice()
        {
            if (!TutorialSaveStub.TryGetFreeModuleChoice(out var module))
                return;

            if (_session.State.UnlockedModules.Contains(module))
                return;

            _session.State.UnlockedModules.Add(module);
        }
    }
}
