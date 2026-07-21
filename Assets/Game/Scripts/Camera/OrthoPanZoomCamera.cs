using LitMotion;
using UnityEngine;

namespace TrafficSim.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class OrthoPanZoomCamera : MonoBehaviour
    {
        [SerializeField] float _minOrthoSize = 3f;
        [SerializeField] float _maxOrthoSize = 20f;
        [SerializeField] float _panSpeed = 0.01f;
        [SerializeField] float _zoomSpeed = 0.5f;
        [SerializeField] float _zoomDuration = 0.25f;

        UnityEngine.Camera _camera;
        float _targetOrthoSize;
        MotionHandle _zoomHandle;

        void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _camera.orthographic = true;
            _targetOrthoSize = _camera.orthographicSize;
        }

        public void OnPan(Vector2 delta)
        {
            var scale = _camera.orthographicSize * _panSpeed;
            transform.position += new Vector3(-delta.x, 0f, -delta.y) * scale;
        }

        public void OnZoom(float delta)
        {
            _targetOrthoSize = Mathf.Clamp(
                _targetOrthoSize - delta * _zoomSpeed,
                _minOrthoSize,
                _maxOrthoSize);

            if (_zoomHandle.IsActive())
                _zoomHandle.Cancel();

            var start = _camera.orthographicSize;
            _zoomHandle = LMotion.Create(start, _targetOrthoSize, _zoomDuration)
                .WithEase(Ease.OutBack)
                .Bind(size => _camera.orthographicSize = size)
                .AddTo(gameObject);
        }
    }
}
