using DawnGuard.Core;
using UnityEngine;

namespace DawnGuard.Unity
{
    [CreateAssetMenu(menuName="Dawn Guard/Game Catalog")]
    public sealed class GameCatalog : ScriptableObject
    {
        public GameRules rules;
        public VisualBinding[] buildings;
        public VisualBinding[] enemies;
        public GameObject dronePrefab;
        public Material surfaceTemplate;
        public Material tracerMaterial;
    }

    [System.Serializable]
    public sealed class VisualBinding
    {
        public string definitionId;
        public GameObject prefab;
    }
}
