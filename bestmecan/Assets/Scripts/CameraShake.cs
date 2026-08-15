using UnityEngine;

public class CameraShake : MonoBehaviour
{
    private Vector3 _originalPos;
    private float _shakeDuration = 0f;
    private float _shakeMagnitude = 0.7f;
    private float _dampingSpeed = 1.0f;
    private bool _isShaking = false;

    void Update()
    {
        if (_shakeDuration > 0)
        {
            // Трясем камеру, добавляя случайное смещение
            transform.localPosition = _originalPos + Random.insideUnitSphere * _shakeMagnitude;
            _shakeDuration -= Time.deltaTime * _dampingSpeed;
        }
        else if (_isShaking)
        {
            // Возвращаем камеру на место
            _shakeDuration = 0f;
            transform.localPosition = _originalPos;
            _isShaking = false;
        }
    }

    public void Shake(float duration, float magnitude)
    {
        if (!_isShaking)
        {
            _originalPos = transform.localPosition;
            _isShaking = true;
        }
        _shakeDuration = duration;
        _shakeMagnitude = magnitude;
    }
}