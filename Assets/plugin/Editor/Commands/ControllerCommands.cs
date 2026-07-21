using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMcpPro
{
    public class ControllerCommands : BaseCommand
    {
        public static void Register(CommandRouter router)
        {
            router.Register("create_character_controller", CreateCharacterController);
            router.Register("create_fps_controller", CreateFPSController);
            router.Register("create_tps_controller", CreateTPSController);
            router.Register("create_platformer_controller", CreatePlatformerController);
        }

        #region Helper Methods

        private static void WriteScriptFile(string scriptPath, string content)
        {
            if (!scriptPath.StartsWith("Assets/"))
                scriptPath = "Assets/" + scriptPath;

            if (!scriptPath.EndsWith(".cs"))
                scriptPath += ".cs";

            string fullPath = Path.Combine(Application.dataPath.Replace("/Assets", ""), scriptPath);
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();
        }

        private static string GetScriptPath(Dictionary<string, object> p, string defaultPath)
        {
            string path = GetStringParam(p, "script_path", defaultPath);
            if (!path.StartsWith("Assets/"))
                path = "Assets/" + path;
            if (!path.EndsWith(".cs"))
                path += ".cs";
            return path;
        }

        #endregion

        #region Character Controller

        private static object CreateCharacterController(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_character_controller");

            string targetPath = GetStringParam(p, "target");
            float speed = GetFloatParam(p, "speed", 5f);
            float jumpHeight = GetFloatParam(p, "jump_height", 1.5f);
            float gravity = GetFloatParam(p, "gravity", -9.81f);
            string scriptPath = GetScriptPath(p, "Assets/Scripts/CharacterMovement.cs");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");

            var go = FindGameObject(targetPath);

            // Add CharacterController if not present
            var cc = go.GetComponent<CharacterController>();
            if (cc == null)
                cc = Undo.AddComponent<CharacterController>(go);

            // Generate the movement script
            string className = Path.GetFileNameWithoutExtension(scriptPath);
            string scriptContent = GenerateCharacterMovementScript(className, speed, jumpHeight, gravity);
            WriteScriptFile(scriptPath, scriptContent);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "path", GetGameObjectPath(go) },
                { "characterController", true },
                { "scriptPath", scriptPath },
                { "note", "Script has been created. After compilation, attach it manually or use attach_script." },
                { "settings", new Dictionary<string, object>
                    {
                        { "speed", speed },
                        { "jumpHeight", jumpHeight },
                        { "gravity", gravity }
                    }
                }
            };
        }

        private static string GenerateCharacterMovementScript(string className, float speed, float jumpHeight, float gravity)
        {
            return $@"using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Basic character movement controller using CharacterController.
/// Supports WASD movement, jumping, and gravity.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class {className} : MonoBehaviour
{{
    [Header(""Movement"")]
    [SerializeField] private float moveSpeed = {FormatFloat(speed)};
    [SerializeField] private float jumpHeight = {FormatFloat(jumpHeight)};
    [SerializeField] private float gravity = {FormatFloat(gravity)};

    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;

    private void Awake()
    {{
        _controller = GetComponent<CharacterController>();
    }}

    private void Update()
    {{
        _isGrounded = _controller.isGrounded;

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        Vector2 input = GetMoveInput();
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        _controller.Move(move * moveSpeed * Time.deltaTime);

        if (GetJumpInput() && _isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }}

    private Vector2 GetMoveInput()
    {{
#if ENABLE_INPUT_SYSTEM
        var keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;
        float x = 0f, y = 0f;
        if (keyboard.dKey.isPressed) x += 1f;
        if (keyboard.aKey.isPressed) x -= 1f;
        if (keyboard.wKey.isPressed) y += 1f;
        if (keyboard.sKey.isPressed) y -= 1f;
        return new Vector2(x, y).normalized;
#else
        return new Vector2(Input.GetAxis(""Horizontal""), Input.GetAxis(""Vertical""));
#endif
    }}

    private bool GetJumpInput()
    {{
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetButtonDown(""Jump"");
#endif
    }}
}}
";
        }

        #endregion

        #region FPS Controller

        private static object CreateFPSController(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_fps_controller");

            string targetPath = GetStringParam(p, "target");
            float moveSpeed = GetFloatParam(p, "move_speed", 5f);
            float lookSensitivity = GetFloatParam(p, "look_sensitivity", 2f);
            float sprintMultiplier = GetFloatParam(p, "sprint_multiplier", 1.5f);
            string scriptPath = GetScriptPath(p, "Assets/Scripts/FPSController.cs");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");

            var go = FindGameObject(targetPath);

            // Add CharacterController if not present
            var cc = go.GetComponent<CharacterController>();
            if (cc == null)
                cc = Undo.AddComponent<CharacterController>(go);

            // Create Camera child if not present
            var cam = go.GetComponentInChildren<Camera>();
            string cameraInfo;
            if (cam == null)
            {
                var camGo = new GameObject("FPSCamera");
                Undo.RegisterCreatedObjectUndo(camGo, "Create FPS Camera");
                camGo.transform.SetParent(go.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
                cameraInfo = "Created new FPSCamera child";
            }
            else
            {
                cameraInfo = $"Using existing camera: {cam.gameObject.name}";
            }

            // Generate the FPS controller script
            string className = Path.GetFileNameWithoutExtension(scriptPath);
            string scriptContent = GenerateFPSControllerScript(className, moveSpeed, lookSensitivity, sprintMultiplier);
            WriteScriptFile(scriptPath, scriptContent);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "path", GetGameObjectPath(go) },
                { "characterController", true },
                { "camera", cameraInfo },
                { "scriptPath", scriptPath },
                { "note", "Script has been created. After compilation, attach it manually or use attach_script." },
                { "settings", new Dictionary<string, object>
                    {
                        { "moveSpeed", moveSpeed },
                        { "lookSensitivity", lookSensitivity },
                        { "sprintMultiplier", sprintMultiplier }
                    }
                }
            };
        }

        private static string GenerateFPSControllerScript(string className, float moveSpeed, float lookSensitivity, float sprintMultiplier)
        {
            return $@"using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First-person controller with mouse look, WASD movement, sprint, and jump.
/// Automatically finds or creates a child Camera for the view.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class {className} : MonoBehaviour
{{
    [Header(""Movement"")]
    [SerializeField] private float moveSpeed = {FormatFloat(moveSpeed)};
    [SerializeField] private float sprintMultiplier = {FormatFloat(sprintMultiplier)};
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;

    [Header(""Mouse Look"")]
    [SerializeField] private float lookSensitivity = {FormatFloat(lookSensitivity)};
    [SerializeField] private float maxLookAngle = 85f;

    private CharacterController _controller;
    private Transform _cameraTransform;
    private Vector3 _velocity;
    private float _xRotation;
    private bool _isGrounded;

    private void Awake()
    {{
        _controller = GetComponent<CharacterController>();

        // Find camera in children
        var cam = GetComponentInChildren<Camera>();
        if (cam != null)
            _cameraTransform = cam.transform;
        else
            Debug.LogWarning($""[{{GetType().Name}}] No Camera found in children. Mouse look will not work."");
    }}

    private void Start()
    {{
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }}

    private void Update()
    {{
        HandleMouseLook();
        HandleMovement();
    }}

    private void HandleMouseLook()
    {{
        if (_cameraTransform == null) return;

        Vector2 look = GetLookInput();
        float mouseX = look.x * lookSensitivity;
        float mouseY = look.y * lookSensitivity;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -maxLookAngle, maxLookAngle);

        _cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }}

    private void HandleMovement()
    {{
        _isGrounded = _controller.isGrounded;

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        Vector2 input = GetMoveInput();
        Vector3 move = transform.right * input.x + transform.forward * input.y;

        float currentSpeed = moveSpeed;
        if (GetSprintInput())
            currentSpeed *= sprintMultiplier;

        _controller.Move(move * currentSpeed * Time.deltaTime);

        if (GetJumpInput() && _isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }}

    private Vector2 GetMoveInput()
    {{
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        float x = 0f, y = 0f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.wKey.isPressed) y += 1f;
        if (kb.sKey.isPressed) y -= 1f;
        return new Vector2(x, y).normalized;
#else
        return new Vector2(Input.GetAxis(""Horizontal""), Input.GetAxis(""Vertical""));
#endif
    }}

    private Vector2 GetLookInput()
    {{
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;
        return mouse.delta.ReadValue() * 0.1f;
#else
        return new Vector2(Input.GetAxis(""Mouse X""), Input.GetAxis(""Mouse Y""));
#endif
    }}

    private bool GetJumpInput()
    {{
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetButtonDown(""Jump"");
#endif
    }}

    private bool GetSprintInput()
    {{
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftShift);
#endif
    }}

    private void OnDisable()
    {{
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }}
}}
";
        }

        #endregion

        #region TPS Controller

        private static object CreateTPSController(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_tps_controller");

            string targetPath = GetStringParam(p, "target");
            float moveSpeed = GetFloatParam(p, "move_speed", 5f);
            float rotationSpeed = GetFloatParam(p, "rotation_speed", 10f);
            float cameraDistance = GetFloatParam(p, "camera_distance", 5f);
            string scriptPath = GetScriptPath(p, "Assets/Scripts/TPSController.cs");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");

            var go = FindGameObject(targetPath);

            // Add CharacterController if not present
            var cc = go.GetComponent<CharacterController>();
            if (cc == null)
                cc = Undo.AddComponent<CharacterController>(go);

            // Generate the TPS controller script
            string className = Path.GetFileNameWithoutExtension(scriptPath);
            string scriptContent = GenerateTPSControllerScript(className, moveSpeed, rotationSpeed, cameraDistance);
            WriteScriptFile(scriptPath, scriptContent);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "path", GetGameObjectPath(go) },
                { "characterController", true },
                { "scriptPath", scriptPath },
                { "note", "Script has been created. After compilation, attach it and assign the camera reference in the Inspector." },
                { "settings", new Dictionary<string, object>
                    {
                        { "moveSpeed", moveSpeed },
                        { "rotationSpeed", rotationSpeed },
                        { "cameraDistance", cameraDistance }
                    }
                }
            };
        }

        private static string GenerateTPSControllerScript(string className, float moveSpeed, float rotationSpeed, float cameraDistance)
        {
            return $@"using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Third-person controller with orbiting camera, smooth character rotation,
/// and CharacterController-based movement relative to camera direction.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class {className} : MonoBehaviour
{{
    [Header(""Movement"")]
    [SerializeField] private float moveSpeed = {FormatFloat(moveSpeed)};
    [SerializeField] private float rotationSpeed = {FormatFloat(rotationSpeed)};
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -9.81f;

    [Header(""Camera"")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float cameraDistance = {FormatFloat(cameraDistance)};
    [SerializeField] private float cameraSensitivity = 2f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 60f;

    private CharacterController _controller;
    private Camera _camera;
    private Vector3 _velocity;
    private float _yaw;
    private float _pitch = 15f;
    private bool _isGrounded;

    private void Awake()
    {{
        _controller = GetComponent<CharacterController>();

        // Find or create camera
        _camera = Camera.main;
        if (_camera == null)
        {{
            var camGo = new GameObject(""TPSCamera"");
            _camera = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }}

        if (cameraTarget == null)
            cameraTarget = transform;
    }}

    private void Start()
    {{
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        _yaw = transform.eulerAngles.y;
    }}

    private void Update()
    {{
        HandleMovement();
    }}

    private void LateUpdate()
    {{
        HandleCamera();
    }}

    private void HandleMovement()
    {{
        _isGrounded = _controller.isGrounded;

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        Vector2 input = GetMoveInput();
        Vector3 forward = _camera.transform.forward;
        Vector3 right = _camera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 move = forward * input.y + right * input.x;

        if (move.sqrMagnitude > 0.01f)
        {{
            // Smoothly rotate character to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(move);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            _controller.Move(move.normalized * moveSpeed * Time.deltaTime);
        }}

        if (GetJumpInput() && _isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }}

    private void HandleCamera()
    {{
        if (_camera == null) return;

        Vector2 look = GetLookInput();
        _yaw += look.x * cameraSensitivity;
        _pitch -= look.y * cameraSensitivity;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -cameraDistance);
        Vector3 targetPos = cameraTarget.position + Vector3.up * 1.5f;

        _camera.transform.position = targetPos + offset;
        _camera.transform.LookAt(targetPos);
    }}

    private Vector2 GetMoveInput()
    {{
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        float x = 0f, y = 0f;
        if (kb.dKey.isPressed) x += 1f;
        if (kb.aKey.isPressed) x -= 1f;
        if (kb.wKey.isPressed) y += 1f;
        if (kb.sKey.isPressed) y -= 1f;
        return new Vector2(x, y).normalized;
#else
        return new Vector2(Input.GetAxis(""Horizontal""), Input.GetAxis(""Vertical""));
#endif
    }}

    private Vector2 GetLookInput()
    {{
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;
        return mouse.delta.ReadValue() * 0.1f;
#else
        return new Vector2(Input.GetAxis(""Mouse X""), Input.GetAxis(""Mouse Y""));
#endif
    }}

    private bool GetJumpInput()
    {{
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetButtonDown(""Jump"");
#endif
    }}

    private void OnDisable()
    {{
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }}
}}
";
        }

        #endregion

        #region Platformer Controller

        private static object CreatePlatformerController(Dictionary<string, object> p)
        {
            ThrowIfPlaying("create_platformer_controller");

            string targetPath = GetStringParam(p, "target");
            float moveSpeed = GetFloatParam(p, "move_speed", 8f);
            float jumpForce = GetFloatParam(p, "jump_force", 12f);
            float coyoteTime = GetFloatParam(p, "coyote_time", 0.1f);
            string scriptPath = GetScriptPath(p, "Assets/Scripts/PlatformerController.cs");

            if (string.IsNullOrEmpty(targetPath))
                throw new ArgumentException("target is required");

            var go = FindGameObject(targetPath);

            // Add Rigidbody2D if not present
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = Undo.AddComponent<Rigidbody2D>(go);
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            // Add BoxCollider2D if no 2D collider present
            var collider = go.GetComponent<Collider2D>();
            if (collider == null)
                Undo.AddComponent<BoxCollider2D>(go);

            // Generate the platformer controller script
            string className = Path.GetFileNameWithoutExtension(scriptPath);
            string scriptContent = GeneratePlatformerControllerScript(className, moveSpeed, jumpForce, coyoteTime);
            WriteScriptFile(scriptPath, scriptContent);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "path", GetGameObjectPath(go) },
                { "rigidbody2D", true },
                { "collider2D", go.GetComponent<Collider2D>().GetType().Name },
                { "scriptPath", scriptPath },
                { "note", "Script has been created. After compilation, attach it manually or use attach_script." },
                { "settings", new Dictionary<string, object>
                    {
                        { "moveSpeed", moveSpeed },
                        { "jumpForce", jumpForce },
                        { "coyoteTime", coyoteTime }
                    }
                }
            };
        }

        private static string GeneratePlatformerControllerScript(string className, float moveSpeed, float jumpForce, float coyoteTime)
        {
            return $@"using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 2D platformer controller with Rigidbody2D movement, ground detection via raycast,
/// coyote time, jump buffering, and variable jump height.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class {className} : MonoBehaviour
{{
    [Header(""Movement"")]
    [SerializeField] private float moveSpeed = {FormatFloat(moveSpeed)};

    [Header(""Jumping"")]
    [SerializeField] private float jumpForce = {FormatFloat(jumpForce)};
    [SerializeField] private float coyoteTime = {FormatFloat(coyoteTime)};
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;

    [Header(""Ground Check"")]
    [SerializeField] private float groundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayer = ~0;

    private Rigidbody2D _rb;
    private Collider2D _collider;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private bool _isGrounded;
    private bool _isJumping;

    private void Awake()
    {{
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();

        // Ensure rotation is frozen for a 2D platformer
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }}

    private void Update()
    {{
        CheckGround();
        HandleTimers();
        HandleJumpInput();
    }}

    private void FixedUpdate()
    {{
        HandleMovement();
    }}

    private void CheckGround()
    {{
        Bounds bounds = _collider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y);
        float width = bounds.extents.x * 0.9f;

        // Cast three rays (center, left, right) for reliable ground detection
        bool hitCenter = Physics2D.Raycast(origin, Vector2.down, groundCheckDistance, groundLayer);
        bool hitLeft = Physics2D.Raycast(origin + Vector2.left * width, Vector2.down, groundCheckDistance, groundLayer);
        bool hitRight = Physics2D.Raycast(origin + Vector2.right * width, Vector2.down, groundCheckDistance, groundLayer);

        bool wasGrounded = _isGrounded;
        _isGrounded = hitCenter || hitLeft || hitRight;

        // Reset coyote timer when landing
        if (_isGrounded)
        {{
            _coyoteTimer = coyoteTime;
            if (wasGrounded == false)
                _isJumping = false;
        }}
    }}

    private void HandleTimers()
    {{
        // Coyote time countdown
        if (!_isGrounded)
            _coyoteTimer -= Time.deltaTime;

        // Jump buffer countdown
        _jumpBufferTimer -= Time.deltaTime;
    }}

    private void HandleJumpInput()
    {{
        if (GetJumpDown())
            _jumpBufferTimer = jumpBufferTime;

        // Execute jump if within buffer and coyote time
        if (_jumpBufferTimer > 0f && _coyoteTimer > 0f && !_isJumping)
        {{
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _isJumping = true;
        }}

        // Variable jump height: cut velocity when releasing jump button
        if (GetJumpUp() && _rb.linearVelocity.y > 0f && _isJumping)
        {{
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.y * jumpCutMultiplier);
        }}
    }}

    private void HandleMovement()
    {{
        float input = GetMoveInput();
        _rb.linearVelocity = new Vector2(input * moveSpeed, _rb.linearVelocity.y);

        // Flip sprite direction
        if (input != 0f)
            transform.localScale = new Vector3(Mathf.Sign(input), 1f, 1f);
    }}

    private float GetMoveInput()
    {{
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return 0f;
        float val = 0f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) val += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) val -= 1f;
        return val;
#else
        return Input.GetAxisRaw(""Horizontal"");
#endif
    }}

    private bool GetJumpDown()
    {{
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        return Input.GetButtonDown(""Jump"");
#endif
    }}

    private bool GetJumpUp()
    {{
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame;
#else
        return Input.GetButtonUp(""Jump"");
#endif
    }}

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {{
        // Visualize ground check rays
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        Bounds bounds = col.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y);
        float width = bounds.extents.x * 0.9f;

        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(origin, origin + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(origin + Vector2.left * width, origin + Vector2.left * width + Vector2.down * groundCheckDistance);
        Gizmos.DrawLine(origin + Vector2.right * width, origin + Vector2.right * width + Vector2.down * groundCheckDistance);
    }}
#endif
}}
";
        }

        #endregion

        #region Utilities

        private static string FormatFloat(float value)
        {
            // Format with 'f' suffix for C# float literals
            return value.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture) + "f";
        }

        #endregion
    }
}
