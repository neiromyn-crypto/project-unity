using UnityEngine;
using DawnGuard.Unity;
namespace DawnGuard.BlackwoodLTD
{
    [CreateAssetMenu(menuName="Dawn Guard/Coastal campaign")]
    public sealed class CoastCatalog : ScriptableObject
    {
        public CoastRules rules=CoastRules.Create();
        public GameCatalog source;
        public CoastForestProfile forest;
        public GameObject grenadePrefab;
        public GameObject operatorPlatform,operatorPrefab;
        public float operatorStandingHeight=.5f;
        public CoastEnemyVisual[] enemyVisuals;
        public GameObject pine,rock,pad,worker,guardian,dock,lantern,basalt,ore,fern;
        public Material ground,water,sand,path,ink,mint,orange;
        public Sprite[] defenseIcons;
        public Sprite workerIcon,enemyIcon;
        public GameObject Defense(string id)
        {
            if(id=="arcane"||id=="cryo")return guardian;
            if(source!=null)foreach(var binding in source.buildings)if(binding.definitionId==id)return binding.prefab;return null;
        }
        public GameObject Enemy(string id)
        {
            if(enemyVisuals!=null)foreach(var binding in enemyVisuals)if(binding.id==id)return binding.prefab;
            string key=id=="sapper"||id=="armored"||id=="boss"?"brute":id;
            if(source!=null)foreach(var binding in source.enemies)if(binding.definitionId==key)return binding.prefab;return null;
        }
    }
    [System.Serializable] public sealed class CoastEnemyVisual
    {public string id;public GameObject prefab;public float deathSeconds=3.5f;}
}
