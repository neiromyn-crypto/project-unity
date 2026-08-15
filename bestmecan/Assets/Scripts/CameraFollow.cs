using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("За кем следить")]
    public Transform target;

    [Header("Настройки")]
    public Vector3 offset = new Vector3(0, 6f, -8f); // Насколько высоко и далеко висит камера
    public float smoothSpeed = 5f; // Плавность полета

    [Header("Угол обзора")]
    public Vector3 cameraAngle = new Vector3(45f, 0f, 0f); // Поворот камеры (ось X наклоняет вниз)

    void LateUpdate()
    {
        if (target == null) return;

        // Вычисляем идеальную позицию
        Vector3 desiredPosition = target.position + offset;

        // Плавно летим к ней
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // Устанавливаем заданный угол обзора
        transform.rotation = Quaternion.Euler(cameraAngle);
    }
}