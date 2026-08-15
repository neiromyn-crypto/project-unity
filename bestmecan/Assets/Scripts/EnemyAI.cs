using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class EnemyAI : MonoBehaviour, IDamageable
{
    [Header("Характеристики")]
    public float maxHealth = 30f;
    public float moveSpeed = 4f;

    [Header("Настройки Атаки (Удар ЛЕВОЙ ногой)")]
    public float attackDamage = 10f;
    public float attackRange = 1.8f;
    public float attackAnimationSpeed = 1.2f;
    public float attackTelegraphTime = 0.4f;
    public float attackRecoveryTime = 0.5f;

    // --- ЛОГИКА ТОЛПЫ ---
    public static readonly HashSet<EnemyAI> ActiveAttackers = new HashSet<EnemyAI>();
    private const int MaxAttackers = 5;
    private bool _isEngaged = false;

    // --- ЛОГИКА БЛУЖДАНИЯ ---
    private Vector3 _wanderDirection;
    private float _wanderTimer;
    private bool _isWandering;

    private float _currentHealth;
    private Transform _playerTransform;
    private IDamageable _playerHealth;

    private Animator _animator;
    private bool _hasAnimator;
    private Rigidbody _rb;

    private bool _isAttacking = false;
    private bool _isHit = false;
    private bool _isDead = false;

    // Сила притягивания левой ноги (IK Weight)
    private float _ikWeight = 0f;

    private Coroutine _currentAttack;
    private Coroutine _currentHit;

    // Кэшированные хэши параметров Аниматора для производительности
    private readonly int _animSpeed = Animator.StringToHash("Speed");
    private readonly int _animAttack = Animator.StringToHash("Attack");
    private readonly int _animHit = Animator.StringToHash("Hit");
    private readonly int _animDie = Animator.StringToHash("Die");
    private readonly int _animAttackSpeedMult = Animator.StringToHash("AttackSpeedMult");

    void Start()
    {
        _currentHealth = maxHealth;
        _rb = GetComponent<Rigidbody>();

        _animator = GetComponent<Animator>();
        _hasAnimator = _animator != null && _animator.runtimeAnimatorController != null;

        if (_hasAnimator)
        {
            _animator.SetFloat(_animAttackSpeedMult, attackAnimationSpeed);
        }

        _rb.isKinematic = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            _playerHealth = playerObj.GetComponent<IDamageable>();
        }
    }

    void FixedUpdate()
    {
        if (_isDead || _isHit || _isAttacking || _playerTransform == null)
        {
            StopMovement();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);

        ManageSwarmLogic(distanceToPlayer);

        if (_isEngaged)
        {
            if (distanceToPlayer <= attackRange)
            {
                StopMovement();
                _currentAttack = StartCoroutine(AttackRoutine());
            }
            else
            {
                ChasePlayer();
            }
        }
        else
        {
            WanderAround();
        }
    }

    private void ManageSwarmLogic(float distanceToPlayer)
    {
        if (_isEngaged)
        {
            if (distanceToPlayer > 15f)
            {
                ActiveAttackers.Remove(this);
                _isEngaged = false;
            }
        }
        else
        {
            if (ActiveAttackers.Count < MaxAttackers)
            {
                ActiveAttackers.Add(this);
                _isEngaged = true;
            }
        }
    }

    private void StopMovement()
    {
        // Исключаем ошибку с кинематическим Rigidbody
        if (!_rb.isKinematic)
        {
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
            _rb.angularVelocity = Vector3.zero;
        }

        if (_hasAnimator)
        {
            _animator.SetFloat(_animSpeed, 0f, 0.1f, Time.fixedDeltaTime);
        }
    }

    private void ChasePlayer()
    {
        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        direction.y = 0;

        _rb.linearVelocity = new Vector3(direction.x * moveSpeed, _rb.linearVelocity.y, direction.z * moveSpeed);

        if (direction != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(direction);
            _rb.MoveRotation(Quaternion.Slerp(transform.rotation, lookRot, Time.fixedDeltaTime * 10f));
        }

        if (_hasAnimator)
        {
            _animator.SetFloat(_animSpeed, 1f, 0.1f, Time.fixedDeltaTime);
        }
    }

    private void WanderAround()
    {
        _wanderTimer -= Time.fixedDeltaTime;

        if (_wanderTimer <= 0f)
        {
            _isWandering = !_isWandering;

            if (_isWandering)
            {
                float angle = Random.Range(0f, 360f);
                _wanderDirection = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                _wanderTimer = Random.Range(1.5f, 3f);
            }
            else
            {
                _wanderTimer = Random.Range(1f, 2f);
            }
        }

        if (_isWandering)
        {
            float walkSpeed = moveSpeed * 0.4f;
            _rb.linearVelocity = new Vector3(_wanderDirection.x * walkSpeed, _rb.linearVelocity.y, _wanderDirection.z * walkSpeed);

            if (_wanderDirection != Vector3.zero)
            {
                Quaternion lookRot = Quaternion.LookRotation(_wanderDirection);
                _rb.MoveRotation(Quaternion.Slerp(transform.rotation, lookRot, Time.fixedDeltaTime * 5f));
            }

            if (_hasAnimator)
            {
                _animator.SetFloat(_animSpeed, 0.4f, 0.1f, Time.fixedDeltaTime);
            }
        }
        else
        {
            StopMovement();
        }
    }

    private IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        StopMovement();

        if (_hasAnimator)
        {
            _animator.SetTrigger(_animAttack);
        }

        // Замах: повороты за игроком + натягивание левой ноги к цели через IK
        float timer = 0f;
        while (timer < attackTelegraphTime)
        {
            if (!_isDead && _playerTransform != null && !_isHit)
            {
                Vector3 dir = (_playerTransform.position - transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 12f);
                }

                _ikWeight = Mathf.Lerp(0f, 1f, timer / attackTelegraphTime);
            }
            timer += Time.deltaTime;
            yield return null;
        }

        _ikWeight = 1f; // Нога притянута к игроку на максимум

        if (!_isDead && _playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            if (dist <= attackRange + 0.6f)
            {
                _playerHealth?.TakeDamage(attackDamage);
            }
        }

        // Возврат ноги после удара
        timer = 0f;
        while (timer < attackRecoveryTime)
        {
            _ikWeight = Mathf.Lerp(1f, 0f, timer / attackRecoveryTime);
            timer += Time.deltaTime;
            yield return null;
        }

        _ikWeight = 0f;
        _isAttacking = false;
    }

    // ВСТРОЕННАЯ ИНВЕРСНАЯ КИНЕМАТИКА (IK) ДЛЯ ЛЕВОЙ НОГИ
    private void OnAnimatorIK(int layerIndex)
    {
        if (!_hasAnimator || _playerTransform == null || _isDead) return;

        // Настраиваем IK для ЛЕВОЙ ноги (LeftFoot)
        _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, _ikWeight);

        if (_ikWeight > 0f)
        {
            // Направляем левую стопу в торс/колено игрока
            Vector3 targetPosition = _playerTransform.position + Vector3.up * 0.8f;
            _animator.SetIKPosition(AvatarIKGoal.LeftFoot, targetPosition);
        }
    }

    public void TakeDamage(float amount)
    {
        if (_isDead) return;

        _currentHealth -= amount;

        if (_currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (_currentHit != null) StopCoroutine(_currentHit);
            _currentHit = StartCoroutine(HitRoutine());
        }
    }

    private IEnumerator HitRoutine()
    {
        if (_isAttacking && _currentAttack != null) StopCoroutine(_currentAttack);

        _isAttacking = false;
        _ikWeight = 0f;
        _isHit = true;

        StopMovement();

        if (_hasAnimator)
        {
            _animator.SetTrigger(_animHit);
        }

        yield return new WaitForSeconds(0.4f);
        _isHit = false;
    }

    private void Die()
    {
        _isDead = true;
        _ikWeight = 0f;

        if (ActiveAttackers.Contains(this))
        {
            ActiveAttackers.Remove(this);
        }

        GetComponent<Collider>().enabled = false;
        _rb.isKinematic = true;

        if (_hasAnimator)
        {
            _animator.SetTrigger(_animDie);
        }

        Destroy(gameObject, 3f);
    }

    void OnDestroy()
    {
        if (ActiveAttackers.Contains(this))
        {
            ActiveAttackers.Remove(this);
        }
    }
}