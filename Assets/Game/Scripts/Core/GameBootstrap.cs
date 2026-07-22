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
        [SerializeField] MapWorldView _worldView;

        SimSession _session;
        SimLoop _loop;
        SimEventBridge _events;
        MapSkeleton _loadedSkeleton;
        MapLoadResult _mapLoad;

        async void Start()
        {
            SimLog.BootstrapInfo(
                $"Start mapAddress='{_mapAddress}' useAddressables={_useAddressables} dayLength={_dayLengthSeconds}s money={_startingMoney}");

            _mapLoad = await LoadMapAsync();
            if (_mapLoad.Graph == null)
            {
                SimLog.Error("Bootstrap", "Map load failed — sim will not start.");
                return;
            }

            SimLog.MapInfo(
                $"Graph ready nodes={_mapLoad.Graph.NodeCount} houses={_mapLoad.Houses?.Count ?? 0} " +
                $"skeleton='{(_loadedSkeleton != null ? _loadedSkeleton.name : "null")}'");

            _session = SimComposition.Build(CreateConfig(), _mapLoad);
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
            _worldView?.Bind(_session, _loadedSkeleton ?? _mapSkeleton);

            RestoreFreeModuleChoice();

            if (_session.State.UnlockedModules.Count == 0)
            {
                _session.State.UnlockedModules.Add(ServiceModule.Car);
                SimLog.ModuleInfo("Default unlock: Car");
            }

            _inputReader?.Bind(_session.Clock);
            SimLog.BootstrapInfo(
                $"Session ready phase={_session.State.Phase} money={_session.State.Money:F0} unlocked=[{string.Join(",", _session.State.UnlockedModules)}]");
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

        async Awaitable<MapLoadResult> LoadMapAsync()
        {
            if (_useAddressables && !string.IsNullOrWhiteSpace(_mapAddress))
            {
                var result = await MapLoader.LoadAsync(_mapAddress);
                if (result.Graph == null)
                    return default;

                _loadedSkeleton = result.Skeleton;
                return result;
            }

            if (_mapSkeleton == null)
            {
                SimLog.Error("Bootstrap", "Assign MapSkeleton or enable Addressables map loading.");
                return default;
            }

            _loadedSkeleton = _mapSkeleton;
            SimLog.MapInfo($"Using serialized MapSkeleton '{_mapSkeleton.name}'");
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
