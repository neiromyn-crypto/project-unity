using UnityEngine;

[RequireComponent(typeof(Light))]
public class LightRadiusController : MonoBehaviour
{
    [Header("Текущий радиус Света")]
    [Tooltip("Радиус круга света на земле (в метрах)")]
    public float groundRadius = 4f;

    [Header("Настройки перехода")]
    [Tooltip("Насколько резким будет край света (от 0 до 1)")]
    [Range(0.1f, 1f)] public float edgeSharpness = 0.9f;

    [Tooltip("Скорость расширения круга при прокачке")]
    public float transitionSpeed = 3f;

    private Light _spotLight;
    private Transform _transform;
    private float _currentRadius;

    // Свойство для получения точного радиуса (используется Менеджером Тьмы)
    public float CurrentRadius => _currentRadius;

    void Start()
    {
        _spotLight = GetComponent<Light>();
        _transform = transform; // Кэшируем transform для производительности
        _currentRadius = groundRadius;

        UpdateLightGeometry(); // Первичное вычисление геометрии
    }

    void Update()
    {
        if (_spotLight == null || _spotLight.type != LightType.Spot) return;

        // ОПТИМИЗАЦИЯ: Выполняем логику и математику ТОЛЬКО если радиус меняется
        if (!Mathf.Approximately(_currentRadius, groundRadius))
        {
            // Используем MoveTowards вместо Lerp — он точно достигает цели и останавливает расчеты
            _currentRadius = Mathf.MoveTowards(_currentRadius, groundRadius, Time.deltaTime * transitionSpeed);
            UpdateLightGeometry();
        }
    }

    private void UpdateLightGeometry()
    {
        float height = _transform.position.y;
        if (height < 0.5f) height = 0.5f; // Защита от деления на ноль

        // Вычисляем углы конуса
        float calculatedAngle = Mathf.Atan(_currentRadius / height) * Mathf.Rad2Deg * 2f;
        _spotLight.spotAngle = calculatedAngle;
        _spotLight.innerSpotAngle = calculatedAngle * edgeSharpness;

        // Удлиняем дальность луча по теореме Пифагора, чтобы край всегда был ярким
        float requiredRange = Mathf.Sqrt((height * height) + (_currentRadius * _currentRadius));
        _spotLight.range = requiredRange + 2f;
    }

    // Метод для вызова при прокачке персонажа
    public void UpgradeRadius(float bonusRadius)
    {
        groundRadius += bonusRadius;
        Debug.Log($"<color=yellow>Радиус света увеличен! Текущий: {groundRadius}м</color>");
    }
}