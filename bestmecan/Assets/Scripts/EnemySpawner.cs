using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Префаб твоего мутанта (из папки Prefabs)")]
    public GameObject enemyPrefab;
    public Transform player;
    public LightRadiusController lightController;

    [Header("Настройки Спавна")]
    [Tooltip("Как часто спавнить врага (в секундах)")]
    public float spawnInterval = 2f;

    [Tooltip("Максимальное количество врагов на карте одновременно (для FPS)")]
    public int maxEnemies = 20;

    [Tooltip("На сколько метров ДАЛЬШЕ круга света будут появляться враги")]
    public float spawnDistanceMargin = 3f;

    private float _timer;

    void Start()
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }
    }

    void Update()
    {
        // Если нет префаба, игрока или света — не работаем
        if (player == null || lightController == null || enemyPrefab == null) return;

        _timer += Time.deltaTime;

        // Если пришло время спавнить
        if (_timer >= spawnInterval)
        {
            _timer = 0f;
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        // 1. Проверяем лимит врагов по тегу "Enemy"
        GameObject[] currentEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (currentEnemies.Length >= maxEnemies)
        {
            return; // Врагов слишком много, отменяем спавн
        }

        // 2. Вычисляем безопасный радиус (чтобы они спавнились в темноте)
        float spawnRadius = lightController.CurrentRadius + spawnDistanceMargin;

        // 3. Выбираем случайную точку на окружности (случайный угол)
        float randomAngle = Random.Range(0f, Mathf.PI * 2f); // 360 градусов в радианах

        // 4. Математика: вычисляем позицию X и Z
        Vector3 spawnOffset = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle)) * spawnRadius;

        Vector3 spawnPosition = player.position + spawnOffset;
        spawnPosition.y = 0f; // Спавним строго на земле (Y = 0)

        // 5. Создаем врага!
        Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
    }
}