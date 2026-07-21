using System.Collections.Generic;
using TrafficSim.Core;
using TrafficSim.Data;
using TrafficSim.Demand;
using TrafficSim.Dispatch;
using TrafficSim.Events;
using TrafficSim.Fleet;
using TrafficSim.Hubs;
using TrafficSim.Input;
using TrafficSim.Map;
using TrafficSim.Systems;
using TrafficSim.UI;
using UnityEngine;

namespace TrafficSim.Core
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] MapSkeleton _mapSkeleton;
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

        RunState _state;
        DayClock _clock;
        EodActionQueue _eodQueue;
        RatingSystem _rating;
        EconomySystem _economy;
        FleetManager _fleet;
        HubManager _hubManager;
        DemandSpawner _demand;
        DispatchService _dispatch;
        OverloadSystem _overload;
        EodController _eod;
        RoadGraph _graph;

        readonly List<OrderInstance> _dispatchOrders = new();
        readonly HashSet<ServiceModule> _initializedModules = new();
        readonly HashSet<int> _processedCompletions = new();
        readonly HashSet<int> _processedExpirations = new();

        Dictionary<ServiceModule, ServiceModuleDef> _moduleDefLookup;
        Dictionary<ServiceModule, HubDef> _hubDefLookup;
        Dictionary<VehicleType, VehicleDef> _vehicleDefLookup;
        List<HubDef> _activeHubDefs;

        int _routedOrderCount;
        RunPhase _lastPhase = RunPhase.Playing;

        void Awake()
        {
            InitializeSystems();
            WireUi();
            RestoreFreeModuleChoice();

            if (_state.UnlockedModules.Count == 0)
                _state.UnlockedModules.Add(ServiceModule.Car);
        }

        void OnDestroy()
        {
            if (_clock != null)
                _clock.DayEnded -= OnDayEnded;

            if (_eod != null)
            {
                _eod.EodStarted -= OnEodStarted;
                _eod.DayAdvanced -= OnDayAdvanced;
            }
        }

        void Update()
        {
            if (_state == null || _clock == null)
                return;

            EnsureUnlockedModuleInfrastructure();
            MonitorRunFailure();

            if (_state.Phase != RunPhase.Playing)
                return;

            var deltaTime = Time.deltaTime;

            _clock.Advance(deltaTime);

            _demand.Tick(_clock.DayFraction);
            RouteNewOrders();
            TickOrderPatience(deltaTime);
            TickFleetCooldowns(deltaTime);

            _hubManager.Tick(deltaTime);

            RefreshDispatchOrders();
            _dispatch.Tick();
            _overload.Tick();
            _dispatch.TickPathAgents(deltaTime);

            ProcessOrderOutcomes();
        }

        void InitializeSystems()
        {
            _graph = MapLoader.Load(_mapSkeleton);
            BuildLookups();

            _state = new RunState { Money = _startingMoney };
            _eodQueue = new EodActionQueue();
            _clock = new DayClock(_dayLengthSeconds);
            _rating = new RatingSystem(_state, _ratingDef);
            _economy = new EconomySystem(_state, _ratingDef);
            _fleet = new FleetManager(_state, _eodQueue);
            _hubManager = new HubManager(_state, _eodQueue);
            _demand = new DemandSpawner(_demandWaveDef, _dayLengthSeconds, _moduleDefLookup, _graph);
            _dispatch = new DispatchService(_fleet, _graph, _dispatchOrders, _orderAssignedChannel);
            _overload = new OverloadSystem(_state, _hubManager, _overloadDef);
            _activeHubDefs = new List<HubDef>();
            _eod = new EodController(
                _state,
                _rating,
                _ratingDef,
                _clock,
                _eodQueue,
                _economy,
                _activeHubDefs);

            _clock.DayEnded += OnDayEnded;
            _eod.EodStarted += OnEodStarted;
            _eod.DayAdvanced += OnDayAdvanced;

            _inputReader?.Bind(_clock);
        }

        void WireUi()
        {
            _hud?.Bind(_state, _clock);
            _demandPanel?.Bind(_demand, _clock, _dayLengthSeconds);
            _eodPanel?.Bind(_state, _eod);
            _modulePanel?.Bind(_state);
        }

        void RestoreFreeModuleChoice()
        {
            if (!TutorialSaveStub.TryGetFreeModuleChoice(out var module))
                return;

            if (_state.UnlockedModules.Contains(module))
                return;

            _state.UnlockedModules.Add(module);
        }

        void BuildLookups()
        {
            _moduleDefLookup = new Dictionary<ServiceModule, ServiceModuleDef>();
            if (_moduleDefs != null)
            {
                for (var i = 0; i < _moduleDefs.Length; i++)
                {
                    var def = _moduleDefs[i];
                    if (def != null)
                        _moduleDefLookup[def.module] = def;
                }
            }

            _hubDefLookup = new Dictionary<ServiceModule, HubDef>();
            if (_hubDefs != null)
            {
                for (var i = 0; i < _hubDefs.Length; i++)
                {
                    var def = _hubDefs[i];
                    if (def != null)
                        _hubDefLookup[def.module] = def;
                }
            }

            _vehicleDefLookup = new Dictionary<VehicleType, VehicleDef>();
            if (_vehicleDefs != null)
            {
                for (var i = 0; i < _vehicleDefs.Length; i++)
                {
                    var def = _vehicleDefs[i];
                    if (def != null)
                        _vehicleDefLookup[def.type] = def;
                }
            }
        }

        void EnsureUnlockedModuleInfrastructure()
        {
            if (_modulePanel != null)
            {
                foreach (var module in _state.UnlockedModules)
                    TryInitializeModule(module);

                return;
            }

            foreach (var module in _state.UnlockedModules)
                TryInitializeModule(module);
        }

        void TryInitializeModule(ServiceModule module)
        {
            if (_initializedModules.Contains(module))
                return;

            if (!_moduleDefLookup.TryGetValue(module, out var moduleDef) ||
                !_hubDefLookup.TryGetValue(module, out var hubDef) ||
                !_vehicleDefLookup.TryGetValue(moduleDef.starterVehicleType, out var vehicleDef))
            {
                return;
            }

            var slotId = _initializedModules.Count;
            if (slotId > 0)
                _hubManager.UnlockSlot(slotId);

            if (!_hubManager.PlaceHub(hubDef, slotId))
                return;

            _activeHubDefs.Add(hubDef);

            for (var i = 0; i < moduleDef.starterVehicleCount; i++)
                _fleet.BuyVehicle(module, vehicleDef);

            PositionFleetAtSlot(module, slotId);
            _initializedModules.Add(module);
        }

        void PositionFleetAtSlot(ServiceModule module, int slotId)
        {
            if (_mapSkeleton == null ||
                _mapSkeleton.hubSlotPositions == null ||
                slotId < 0 ||
                slotId >= _mapSkeleton.hubSlotPositions.Length)
            {
                return;
            }

            var nodeId = FindNearestNode(_mapSkeleton.hubSlotPositions[slotId]);
            var position = _graph.GetNodePosition(nodeId);

            foreach (var vehicle in _fleet.GetAllVehicles())
            {
                if (vehicle.Module != module || vehicle.State != VehicleState.Idle)
                    continue;

                vehicle.SetLocation(position, nodeId);
            }
        }

        int FindNearestNode(Vector3 worldPosition)
        {
            var bestNode = 0;
            var bestDistance = float.MaxValue;

            for (var i = 0; i < _graph.NodeCount; i++)
            {
                var distance = Vector3.SqrMagnitude(_graph.GetNodePosition(i) - worldPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestNode = i;
                }
            }

            return bestNode;
        }

        void RouteNewOrders()
        {
            var orders = _demand.Orders;
            while (_routedOrderCount < orders.Count)
            {
                _hubManager.TryAcceptOrder(orders[_routedOrderCount]);
                _routedOrderCount++;
            }
        }

        void TickOrderPatience(float deltaTime)
        {
            foreach (var hub in _hubManager.GetHubs())
            {
                var pending = hub.PendingOrders;
                for (var i = 0; i < pending.Count; i++)
                    pending[i].TickPatience(deltaTime);
            }

            var cityQueue = _hubManager.CityQueue;
            for (var i = 0; i < cityQueue.Count; i++)
                cityQueue[i].TickPatience(deltaTime);
        }

        void TickFleetCooldowns(float deltaTime)
        {
            foreach (var vehicle in _fleet.GetAllVehicles())
                vehicle.TickCooldown(deltaTime);
        }

        void RefreshDispatchOrders()
        {
            _dispatchOrders.Clear();

            foreach (var hub in _hubManager.GetHubs())
            {
                var pending = hub.PendingOrders;
                for (var i = 0; i < pending.Count; i++)
                {
                    var order = pending[i];
                    if (order.State == OrderState.Pending)
                        _dispatchOrders.Add(order);
                }
            }

            var cityQueue = _hubManager.CityQueue;
            for (var i = 0; i < cityQueue.Count; i++)
            {
                var order = cityQueue[i];
                if (order.State == OrderState.Pending)
                    _dispatchOrders.Add(order);
            }
        }

        void ProcessOrderOutcomes()
        {
            ProcessOrdersFromHubs();
            ProcessOrdersFromCityQueue();
        }

        void ProcessOrdersFromHubs()
        {
            foreach (var hub in _hubManager.GetHubs())
                ProcessOrderList(hub.PendingOrders);
        }

        void ProcessOrdersFromCityQueue() => ProcessOrderList(_hubManager.CityQueue);

        void ProcessOrderList(IReadOnlyList<OrderInstance> orders)
        {
            for (var i = 0; i < orders.Count; i++)
            {
                var order = orders[i];

                if (order.State == OrderState.Completed && !_processedCompletions.Contains(order.Id))
                {
                    _rating.ApplyJobOutcome(order.RemainingFraction);
                    _economy.OnJobCompleted(order, _state.CurrentStars);
                    _orderCompletedChannel?.Raise(new OrderEventPayload(order.Id, order.Module));
                    _processedCompletions.Add(order.Id);
                    continue;
                }

                if (order.State == OrderState.Expired && !_processedExpirations.Contains(order.Id))
                {
                    _rating.ApplyJobOutcome(order.RemainingFraction);
                    _processedExpirations.Add(order.Id);
                }
            }
        }

        void MonitorRunFailure()
        {
            if (_lastPhase != RunPhase.Failed && _state.Phase == RunPhase.Failed)
                _runFailedChannel?.Raise();

            _lastPhase = _state.Phase;
        }

        void OnDayEnded()
        {
            _dayEndedChannel?.Raise();

            var skipIntervention = TutorialSaveStub.ShouldSkipEodUi;
            _eod.BeginEod(skipIntervention);
        }

        void OnEodStarted() => _eodStartedChannel?.Raise();

        void OnDayAdvanced() => _dayAdvancedChannel?.Raise();
    }
}
