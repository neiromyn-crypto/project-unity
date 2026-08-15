using UnityEngine;

public class FogOfWar : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform player; // Ссылка на Игрока

    [Header("Настройки Карты")]
    public Vector2 mapSize = new Vector2(100f, 100f); // Размер карты в метрах
    public int textureSize = 256;                     // Разрешение маски тумана

    [Header("Настройки Разведки")]
    public float visionRadius = 8f;                   // Радиус открытия тумана
    [Range(0.1f, 1f)]
    public float featherRatio = 0.35f;                // Мягкость краев стирания дыма

    private Texture2D _fogTexture;
    private Color[] _pixels;
    private Material _fogMaterial;
    private Vector2 _centerOffset;

    void Start()
    {
        // Создаем динамическую текстуру с мягким сглаживанием
        _fogTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        _fogTexture.filterMode = FilterMode.Bilinear;
        _fogTexture.wrapMode = TextureWrapMode.Clamp;

        _pixels = new Color[textureSize * textureSize];

        // Заполняем всю карту непроницаемым густым дымом (Alpha = 1)
        for (int i = 0; i < _pixels.Length; i++)
        {
            _pixels[i] = new Color(0f, 0f, 0f, 1f);
        }

        _fogTexture.SetPixels(_pixels);
        _fogTexture.Apply();

        // Привязываем маску к материалу
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            _fogMaterial = rend.material;

            if (_fogMaterial.HasProperty("_MainTex"))
                _fogMaterial.SetTexture("_MainTex", _fogTexture);
            if (_fogMaterial.HasProperty("_BaseMap"))
                _fogMaterial.SetTexture("_BaseMap", _fogTexture);
        }

        _centerOffset = mapSize / 2f;
    }

    void Update()
    {
        if (player == null) return;

        // Переводим 3D-позицию игрока в координаты 2D-текстуры
        Vector3 pPos = player.position - transform.position;
        int px = Mathf.RoundToInt(((pPos.x + _centerOffset.x) / mapSize.x) * textureSize);
        int py = Mathf.RoundToInt(((pPos.z + _centerOffset.y) / mapSize.y) * textureSize);

        int radiusInPixels = Mathf.RoundToInt((visionRadius / mapSize.x) * textureSize);
        int radiusSqr = radiusInPixels * radiusInPixels;

        bool updated = false;

        // Плавное рассеивание дыма вокруг игрока
        for (int x = -radiusInPixels; x <= radiusInPixels; x++)
        {
            for (int y = -radiusInPixels; y <= radiusInPixels; y++)
            {
                int distSqr = x * x + y * y;
                if (distSqr <= radiusSqr)
                {
                    int targetX = px + x;
                    int targetY = py + y;

                    if (targetX >= 0 && targetX < textureSize && targetY >= 0 && targetY < textureSize)
                    {
                        int index = targetY * textureSize + targetX;

                        // Расчет плавного затухания дыма от центра к краю зоны обзора
                        float dist = Mathf.Sqrt(distSqr);
                        float normDist = dist / radiusInPixels;

                        float targetAlpha = Mathf.Clamp01((normDist - (1f - featherRatio)) / featherRatio);

                        if (_pixels[index].a > targetAlpha)
                        {
                            _pixels[index].a = targetAlpha;
                            updated = true;
                        }
                    }
                }
            }
        }

        // Обновляем текстуру только при открытии новых участков
        if (updated)
        {
            _fogTexture.SetPixels(_pixels);
            _fogTexture.Apply();
        }
    }
}