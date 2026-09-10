using DawnGuard.Unity;
using UnityEngine;

namespace DawnGuard.BlackwoodV2
{
    [CreateAssetMenu(menuName="Dawn Guard/Blackwood user assets")]
    public sealed class BlackwoodAssetSet : ScriptableObject
    {
        public GameObject shelter,camp,generator,wall,gun,lab,tesla,drone;
        public GameObject zombie,runner,brute,pine,rubble,pad;
        public Material groundMaterial;
        public Texture2D menuBackdrop;
        public GameCatalog sourceCatalog;
        public bool fitModelsToGrid=true;
        public GameObject Building(string id)
        {
            switch(id) { case "shelter":return shelter; case "camp":return camp;
                case "generator":return generator; case "wall":return wall; case "gun":return gun;
                case "lab":return lab; case "tesla":return tesla; default:return null; }
        }
    }
}
