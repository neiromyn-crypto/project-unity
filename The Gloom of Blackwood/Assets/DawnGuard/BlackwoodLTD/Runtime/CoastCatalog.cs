using UnityEngine;
using DawnGuard.Unity;
namespace DawnGuard.BlackwoodLTD
{
    [CreateAssetMenu(menuName="Dawn Guard/Coastal campaign")]
    public sealed class CoastCatalog : ScriptableObject
    {
        public CoastRules rules=CoastRules.Create();
        public GameCatalog source;
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
            string key=id=="sapper"||id=="armored"||id=="boss"?"brute":id;
            if(source!=null)foreach(var binding in source.enemies)if(binding.definitionId==key)return binding.prefab;return null;
        }
    }
}
