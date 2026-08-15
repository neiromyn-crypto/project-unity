using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Движение")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;

    [Header("Управление")]
    public MobileJoystick joystick;

    private CharacterController _controller;
    private Animator _animator;
    private Camera _mainCamera;
    private Vector3 _velocity;

    // Ссылка на боевку, чтобы блокировать шаги во время спец-атаки
    private PlayerCombat _combat;

    private readonly int _animMoveSpeed = Animator.StringToHash("MoveSpeed");

    void Start()
    {
        _controller = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _combat = GetComponent<PlayerCombat>();
        _mainCamera = Camera.main;
    }

    void Update()
    {
        ApplyGravity();

        // Если используем способность - стоим на месте
        if (_combat != null && _combat.isUsingAbility) return;

        HandleMovement();
    }

    private void HandleMovement()
    {
        float horizontal = (joystick != null && joystick.inputVector.x != 0) ? joystick.inputVector.x : Input.GetAxisRaw("Horizontal");
        float vertical = (joystick != null && joystick.inputVector.y != 0) ? joystick.inputVector.y : Input.GetAxisRaw("Vertical");

        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
            float angle = Mathf.LerpAngle(transform.eulerAngles.y, targetAngle, rotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
            _controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);

            _animator.SetFloat(_animMoveSpeed, 1f, 0.1f, Time.deltaTime);
        }
        else
        {
            _animator.SetFloat(_animMoveSpeed, 0f, 0.1f, Time.deltaTime);
        }
    }

    private void ApplyGravity()
    {
        if (_controller.isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}