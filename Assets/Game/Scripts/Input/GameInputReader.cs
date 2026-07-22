using TrafficSim.Camera;
using TrafficSim.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrafficSim.Input
{
    public sealed class GameInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset _actions;
        [SerializeField] OrthoPanZoomCamera _camera;

        InputActionMap _gameMap;
        InputAction _panAction;
        InputAction _panHoldAction;
        InputAction _zoomAction;
        InputAction _pauseAction;
        InputAction _speedCycleAction;

        DayClock _clock;

        void Awake()
        {
            if (_actions == null)
            {
                Debug.LogError($"{nameof(GameInputReader)} requires an InputActionAsset.", this);
                return;
            }

            _gameMap = _actions.FindActionMap("Game");
            _panAction = _gameMap.FindAction("Pan");
            _panHoldAction = _gameMap.FindAction("PanHold");
            _zoomAction = _gameMap.FindAction("Zoom");
            _pauseAction = _gameMap.FindAction("Pause");
            _speedCycleAction = _gameMap.FindAction("SpeedCycle");

            _pauseAction.performed += OnPausePerformed;
            _speedCycleAction.performed += OnSpeedCyclePerformed;
        }

        void OnEnable()
        {
            _gameMap?.Enable();
        }

        void OnDisable()
        {
            _gameMap?.Disable();
        }

        void Update()
        {
            if (_panHoldAction != null && _panHoldAction.IsPressed())
            {
                var delta = _panAction.ReadValue<Vector2>();
                if (delta.sqrMagnitude > 0f)
                    _camera?.OnPan(delta);
            }

            if (_zoomAction == null)
                return;

            var zoom = _zoomAction.ReadValue<float>();
            if (Mathf.Abs(zoom) > float.Epsilon)
                _camera?.OnZoom(zoom);
        }

        public void Bind(DayClock clock) => _clock = clock;

        void OnPausePerformed(InputAction.CallbackContext _)
        {
            if (_clock == null)
                return;

            _clock.IsPaused = !_clock.IsPaused;
            SimLog.Info("Input", _clock.IsPaused ? "Paused" : "Unpaused");
        }

        void OnSpeedCyclePerformed(InputAction.CallbackContext _)
        {
            if (_clock == null)
                return;

            var next = _clock.TimeScale >= 3 ? 1 : _clock.TimeScale + 1;
            _clock.SetTimeScale(next);
            SimLog.Info("Input", $"TimeScale → {_clock.TimeScale}x");
        }

        void OnDestroy()
        {
            if (_pauseAction != null)
                _pauseAction.performed -= OnPausePerformed;
            if (_speedCycleAction != null)
                _speedCycleAction.performed -= OnSpeedCyclePerformed;
        }
    }
}
