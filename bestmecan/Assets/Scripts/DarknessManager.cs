using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DarknessManager : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform player;
    public LightRadiusController lightController;
    public Volume globalVolume;
    public CameraShake cameraShake;

    [Header("Настройки Урона и Эффектов")]
    public float darknessDamage = 10f;
    public float damageInterval = 1f;
    public bool enableVibration = true;

    [Header("Настройки Визуала")]
    public Color safeVignetteColor = Color.black;
    public Color dangerVignetteColor = new Color(0.8f, 0.1f, 0.1f);
    public float pulseSpeed = 6f;

    private float _timeInDarkness = 0f;
    private IDamageable _playerHealth;
    private Vignette _vignette;

    // Кэширование для оптимизации
    private Transform _lightTransform;
    private bool _isVignetteActive = false;

    void Start()
    {
        if (player != null) _playerHealth = player.GetComponent<IDamageable>();
        if (lightController != null) _lightTransform = lightController.transform;

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _vignette);
        }

        if (cameraShake == null) cameraShake = Object.FindAnyObjectByType<CameraShake>();
    }

    void Update()
    {
        if (player == null || lightController == null || _lightTransform == null) return;

        // ОПТИМИЗАЦИЯ 1: Исключаем ось Y без создания новых объектов Vector2 в памяти
        Vector3 offset = player.position - _lightTransform.position;
        offset.y = 0f;

        // ОПТИМИЗАЦИЯ 2: Используем sqrMagnitude вместо Distance (без Mathf.Sqrt)
        float sqrDistance = offset.sqrMagnitude;

        float currentRadius = lightController.CurrentRadius;
        float sqrRadius = currentRadius * currentRadius;

        if (sqrDistance > sqrRadius)
        {
            // ИГРОК ВО ТЬМЕ
            _timeInDarkness += Time.deltaTime;
            _isVignetteActive = true;

            if (_vignette != null)
            {
                float pingPong = Mathf.PingPong(Time.time * pulseSpeed, 1f);
                _vignette.color.value = Color.Lerp(safeVignetteColor, dangerVignetteColor, pingPong);
                _vignette.intensity.value = Mathf.Lerp(0.55f, 0.75f, pingPong);
            }

            if (_timeInDarkness >= damageInterval)
            {
                _playerHealth?.TakeDamage(darknessDamage);
                _timeInDarkness = 0f;

                if (cameraShake != null) cameraShake.Shake(0.2f, 0.3f);
                if (enableVibration) Handheld.Vibrate();
            }
        }
        else
        {
            // ИГРОК В БЕЗОПАСНОСТИ
            _timeInDarkness = 0f;

            // ОПТИМИЗАЦИЯ 3: Выполняем Lerp виньетки ТОЛЬКО пока она не вернется в норму
            if (_isVignetteActive && _vignette != null)
            {
                _vignette.color.value = Color.Lerp(_vignette.color.value, safeVignetteColor, Time.deltaTime * 5f);
                _vignette.intensity.value = Mathf.Lerp(_vignette.intensity.value, 0.4f, Time.deltaTime * 5f);

                // Если виньетка почти потухла - жестко фиксируем её и выключаем вычисления
                if (_vignette.intensity.value <= 0.41f)
                {
                    _vignette.color.value = safeVignetteColor;
                    _vignette.intensity.value = 0.4f;
                    _isVignetteActive = false;
                }
            }
        }
    }
}