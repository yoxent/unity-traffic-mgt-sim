using LitMotion;
using UnityEngine;

namespace TrafficSim.Camera
{
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class OrthoPanZoomCamera : MonoBehaviour
    {
        [Header("Zoom")]
        [SerializeField] float _minOrthoSize = 3f;
        [SerializeField] float _maxOrthoSize = 20f;
        [SerializeField] float _zoomSpeed = 0.5f;
        [SerializeField] float _zoomDuration = 0.25f;

        [Header("Pan (Right-Click Drag)")]
        [SerializeField, Tooltip("World units moved per pixel of right-click drag, scaled by current ortho size.")]
        float _panSensitivity = 0.0015f;

        UnityEngine.Camera _camera;
        float _targetOrthoSize;
        MotionHandle _zoomHandle;

        void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _camera.orthographic = true;
            _targetOrthoSize = _camera.orthographicSize;

            // Keep a true top-down view onto the XZ map plane.
            if (Vector3.Dot(transform.forward, Vector3.down) < 0.95f)
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        public void OnPan(Vector2 delta)
        {
            var scale = _camera.orthographicSize * _panSensitivity;
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
                .WithEase(Ease.OutQuad)
                .Bind(size => _camera.orthographicSize = size)
                .AddTo(gameObject);
        }
    }
}
