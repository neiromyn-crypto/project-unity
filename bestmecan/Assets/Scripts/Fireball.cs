using UnityEngine;

public class Fireball : MonoBehaviour
{
    public float speed = 12f;
    private float _damage;

    public void Setup(float damage)
    {
        _damage = damage;
        Destroy(gameObject, 4f); // Уничтожаем через 4 секунды, если улетел в пустоту
    }

    void Update()
    {
        // Летим строго вперед (туда, куда повернут снаряд)
        transform.Translate(Vector3.forward * speed * Time.deltaTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // Наносим урон, если попали во врага
        if (other.CompareTag("Enemy") || other.CompareTag("Boss"))
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            damageable?.TakeDamage(_damage);

            // Уничтожаем фаербол после попадания
            Destroy(gameObject);
        }
    }
}