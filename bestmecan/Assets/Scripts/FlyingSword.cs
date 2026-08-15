using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FlyingSword : MonoBehaviour
{
    [Header("Характеристики Снаряда")]
    public float speed = 15f;           // Скорость полета
    public float damage = 25f;          // Урон
    public float lifeTime = 3f;         // Время жизни (чтобы не засорять память)
    public int pierceAmount = 1;        // Сколько врагов может насквозь пробить меч (1 = урон 1 врагу и исчезает)

    [Header("Визуальные Эффекты")]
    public bool spinInFlight = true;    // Включает вращение меча в полете
    public float spinSpeed = 720f;      // Скорость вращения (градусов в секунду)
    public Transform visualModel;       // Ссылка на 3D-модель (для вращения)

    private int _currentHits = 0;

    void Start()
    {
        // Автоматически уничтожаем меч через X секунд, если он ни в кого не попал
        Destroy(gameObject, lifeTime);

        // Если модель не перетащили, берем первый дочерний объект
        if (visualModel == null && transform.childCount > 0)
        {
            visualModel = transform.GetChild(0);
        }
    }

    void Update()
    {
        // 1. Движение родительского объекта строго ВПРЕД
        transform.Translate(Vector3.forward * (speed * Time.deltaTime));

        // 2. Вращение только 3D-модели вокруг своей оси (для сочного визуала)
        if (spinInFlight && visualModel != null)
        {
            visualModel.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Проверяем, попал ли меч во врага (по компоненту IDamageable или тегу Enemy)
        IDamageable damageable = other.GetComponent<IDamageable>();

        if (damageable != null && !other.CompareTag("Player"))
        {
            damageable.TakeDamage(damage);
            _currentHits++;

            // Если меч исчерпал лимит пробитий — уничтожаем его
            if (_currentHits >= pierceAmount)
            {
                Destroy(gameObject);
            }
        }
    }
}