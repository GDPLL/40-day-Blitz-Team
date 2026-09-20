using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Player MVC controller. This class coordinates only: Input -> PlayerModel -> Player/Camera/Animation/Weapon views.
/// It never moves a Rigidbody, rotates a Transform, edits an Animator, or renders weapon effects itself.
/// </summary>
public sealed class Player_Control : NetworkBehaviour
{
    [Header("Migration bindings (moved into views at Awake)")]
    public Transform Head;
    public Rigidbody rb;
    public GameObject Gun;
    public Gun_Control gun_Control;
    public Player_animation animationView;

    [Header("Player configuration")]
    public float turnSpeed = 360f;
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float squatSpeed = 1f;
    public float moveForce = 700f;
    public float jumpForce = 100f;
    public float groundCheckDistance = .2f;
    public float focusDistance = 10f;

    public PlayerModel Model { get; } = new PlayerModel();

    private PlayerInputController inputController;
    private PlayerView playerView;
    private Camera sceneCamera;
    private Player_camera cameraView;
    private RectTransform crosshair;
    private Vector3 smoothMoveDirection;

    private void Awake()
    {
        inputController = GetComponent<PlayerInputController>();
        if (inputController == null) inputController = gameObject.AddComponent<PlayerInputController>();
        playerView = GetComponent<PlayerView>();
        if (playerView == null) playerView = gameObject.AddComponent<PlayerView>();
        playerView.Configure(rb, Gun == null ? null : Gun.transform);
        if (animationView == null) animationView = GetComponent<Player_animation>();
    }

    private void Start()
    {
        // Non-networked test players never receive OnNetworkSpawn.
        if (!IsNetworkSessionActive) ConfigureOwnership(true);
    }

    public override void OnNetworkSpawn()
    {
        // IsOwner is valid here; it is not reliable in Start for a newly spawned NGO object.
        ConfigureOwnership(IsOwner);
    }

    public override void OnGainedOwnership()
    {
        ConfigureOwnership(true);
    }

    public override void OnLostOwnership()
    {
        ConfigureOwnership(false);
    }

    private void ConfigureOwnership(bool local)
    {
        playerView.SetLocallyControlled(local);
        if (!local) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        smoothMoveDirection = transform.forward;
    }

    private void Update()
    {
        if (!IsLocalPlayer || !TryResolveSceneViews()) return;

        PlayerInput input = inputController.Read();
        Vector3 move = GetCameraRelativeMove(input.Move);
        if (move.sqrMagnitude > .001f)
            smoothMoveDirection = Vector3.RotateTowards(smoothMoveDirection, move,
                turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 1f);

        Model.Apply(input, playerView.IsGrounded(groundCheckDistance), walkSpeed, runSpeed, squatSpeed, moveForce);
        animationView?.Render(Model);
        cameraView?.Rotate(inputController.ReadLookDelta());
        cameraView?.SetShoulderAim(input.Aim);
        gun_Control?.SetAimMode(input.Aim);
    }

    private void FixedUpdate()
    {
        if (!IsLocalPlayer || !TryResolveSceneViews() || !Model.IsGrounded) return;
        if (Model.IsMoving) playerView.Move(smoothMoveDirection, Model.MoveForce, Model.MoveSpeed);

        Ray aimRay = sceneCamera.ScreenPointToRay(AimScreenPosition);
        Vector3 aimPoint = FindAimPoint(aimRay);
        playerView.SetWeaponAim(aimPoint, Model.IsAiming);
        if (Model.IsAiming) playerView.Face(aimRay.direction, turnSpeed);
        else if (Model.IsMoving) playerView.Face(smoothMoveDirection, turnSpeed);

        if (Model.JumpRequested) playerView.Jump(jumpForce);
        if (Model.ReloadRequested) gun_Control?.Reload();
        if (Model.IsFiring && gun_Control != null && gun_Control.TryFire(aimPoint)) cameraView?.Shake();
        Model.ConsumeFrameActions();
    }

    private Vector3 GetCameraRelativeMove(Vector2 input)
    {
        Vector3 forward = Vector3.ProjectOnPlane(sceneCamera.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(sceneCamera.transform.right, Vector3.up).normalized;
        return (right * input.x + forward * input.y).normalized;
    }

    // GameManager performs the normal injection path. This fallback keeps the local player
    // controllable if scene loading and network spawning complete in a different order.
    private bool TryResolveSceneViews()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (cameraView == null && sceneCamera != null) cameraView = sceneCamera.GetComponent<Player_camera>();
        if (crosshair == null)
        {
            GameObject ui = GameObject.Find("uiPos");
            if (ui != null) crosshair = ui.GetComponent<RectTransform>();
        }
        if (cameraView != null && cameraView.Player == null) cameraView.SetFollowTarget(Head);
        return sceneCamera != null;
    }

    private Vector3 AimScreenPosition => crosshair != null
        ? crosshair.position
        : new Vector3(Screen.width * .5f, Screen.height * .5f, 0f);

    private Vector3 FindAimPoint(Ray ray)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(ray, 100f))
            if (!hit.collider.CompareTag("Player")) return hit.point;
        return ray.GetPoint(focusDistance);
    }

    private bool IsNetworkSessionActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    private bool IsLocalPlayer => !IsNetworkSessionActive || IsOwner;

    // Composition root called by GameManager when this client's player object is ready.
    public void SetupLocal(Camera camera, Player_camera cameraRig, RectTransform ui)
    {
        sceneCamera = camera;
        cameraView = cameraRig;
        crosshair = ui;
        if (cameraView != null) cameraView.SetFollowTarget(Head);
    }
}
