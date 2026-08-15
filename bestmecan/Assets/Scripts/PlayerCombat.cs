using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerCombat : MonoBehaviour
{
    [Header("Спец-способность (Отталкивание)")]
    public float abilityCooldown = 5f;
    public float pushRadius = 6f;
    public float pushForce = 12f;
    public float abilityDuration = 1.5f;
    public LayerMask enemyLayer; // ОПТИМИЗАЦИЯ: Ищем врагов только на этом слое, игнорируя стены и пол

    [Header("Текущее Оружие (Фаербол)")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("Базовые Характеристики")]
    public float baseDamage = 15f;
    public float baseCooldown = 1.2f;

    [Header("Прокачка (Множители)")]
    public float damageMultiplier = 1f;      // 1 = 100% урона, 1.5 = 150% урона
    public float attackSpeedMultiplier = 1f; // 1 = обычная скорость, 2 = в два раза быстрее

    [HideInInspector] public bool isUsingAbility = false;

    private Animator _animator;
    private float _nextAbilityTime = 0f;
    private float _nextAutoAttackTime = 0f;

    private readonly int _animAbility = Animator.StringToHash("SpecialAbility");

    void Start()
    {
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (isUsingAbility)
        {
            PushEnemiesInRadius();
            return;
        }

        HandleAutoAttack();
    }

    public void MobileSpecialAbility()
    {
        if (Time.time >= _nextAbilityTime && !isUsingAbility)
        {
            isUsingAbility = true;
            _nextAbilityTime = Time.time + abilityCooldown;
            _animator.SetTrigger(_animAbility);

            Invoke(nameof(EndAbility), abilityDuration);
        }
    }

    private void PushEnemiesInRadius()
    {
        // ОПТИМИЗАЦИЯ: Physics.OverlapSphere теперь ищет ТОЛЬКО объекты на слое enemyLayer
        Collider[] colliders = Physics.OverlapSphere(transform.position, pushRadius, enemyLayer);
        foreach (Collider col in colliders)
        {
            Rigidbody enemyRb = col.GetComponent<Rigidbody>();
            if (enemyRb != null)
            {
                Vector3 pushDirection = (col.transform.position - transform.position).normalized;
                pushDirection.y = 0;
                enemyRb.AddForce(pushDirection * pushForce, ForceMode.Impulse);
            }
        }
    }

    private void EndAbility()
    {
        isUsingAbility = false;
    }

    private void HandleAutoAttack()
    {
        // Высчитываем реальную скорость атаки с учетом прокачки
        float actualCooldown = baseCooldown / Mathf.Max(0.1f, attackSpeedMultiplier);

        if (Time.time < _nextAutoAttackTime) return;

        _nextAutoAttackTime = Time.time + actualCooldown;
        ExecuteAttack();
    }

    private void ExecuteAttack()
    {
        if (projectilePrefab == null || firePoint == null) return;

        // Высчитываем реальный урон с учетом прокачки
        float finalDamage = baseDamage * damageMultiplier;

        // TODO в будущем: заменить Instantiate на Object Pool для максимальной оптимизации памяти
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

        Fireball fbScript = projectile.GetComponent<Fireball>();
        if (fbScript != null)
        {
            fbScript.Setup(finalDamage);
        }
    }

    // МЕТОД ДЛЯ БУДУЩЕЙ СИСТЕМЫ ПРОКАЧКИ
    // Вызывается, когда игрок подбирает бонус или покупает улучшение
    public void UpgradeStats(float extraDamage, float extraSpeed)
    {
        damageMultiplier += extraDamage;
        attackSpeedMultiplier += extraSpeed;
        Debug.Log($"Прокачка! Урон: x{damageMultiplier}, Скорость: x{attackSpeedMultiplier}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, pushRadius);
    }
}