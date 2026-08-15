using UnityEngine;

public class DroneFollow : MonoBehaviour
{
    [Header("За кем летим")]
    public Transform target; // Ссылка на Игрока

    [Header("Настройки полета и задержки")]
    public Vector3 offset = new Vector3(0f, 3.5f, 0f); // Позиция над головой игрока

    [Tooltip("Чем МЕНЬШЕ число, тем больше задержка (время реакции) дрона")]
    [Range(0.5f, 10f)]
    public float smoothSpeed = 2f; // Попробуй значение 1.5 - 2.0 для мягкого отставания

    [Header("Эффект парения (Живой дрон)")]
    public float bobAmplitude = 0.25f; // Амплитуда покачивания вверх-вниз
    public float bobFrequency = 2.5f;  // Скорость покачивания

    private float _randomOffset;

    void Start()
    {
        _randomOffset = Random.Range(0f, 2f * Mathf.PI);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Вычисляем целевую точку над игроком
        Vector3 desiredPosition = target.position + offset;

        // 2. Добавляем мягкое живое покачивание
        float bobbing = Mathf.Sin(Time.time * bobFrequency + _randomOffset) * bobAmplitude;
        desiredPosition.y += bobbing;

        // 3. Плавно летим в эту точку (небольшое значение smoothSpeed дает нужную задержку)
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // 4. Плавно поворачиваемся вслед за игроком
        transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, smoothSpeed * Time.deltaTime);
    }
}