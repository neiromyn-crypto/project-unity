using DawnGuard.Core;
using UnityEngine;

namespace DawnGuard.BlackwoodV2
{
    // Attach to the replacement Blender prefab too; preserve these references.
    public sealed class BlackwoodModel : MonoBehaviour
    {
        public Transform yaw;
        public Transform[] rotors;
        public GameObject[] levelMarkers;
        public void RefreshBuilding(BuildingState b,GameSession game,bool paused)
        {
            if(levelMarkers!=null) for(int i=0;i<levelMarkers.Length;i++)
                if(levelMarkers[i]!=null) levelMarkers[i].SetActive(b.level>i+1);
            if(yaw==null || paused || !b.powered || game.Phase!=GamePhase.Night) return;
            var def=game.Rules.Building(b.definitionId);
            float best=def.range*def.range; EnemyState target=null;
            foreach(var e in game.Enemies)
            {
                float dx=e.x-transform.position.x,dz=e.z-transform.position.z,ds=dx*dx+dz*dz;
                if(ds<best) { best=ds; target=e; }
            }
            if(target==null) return;
            Vector3 d=new Vector3(target.x-transform.position.x,0,target.z-transform.position.z);
            if(d.sqrMagnitude>.001f) yaw.rotation=Quaternion.RotateTowards(yaw.rotation,Quaternion.LookRotation(d),360*Time.unscaledDeltaTime);
        }
        private void Update()
        {
            if(rotors==null) return;
            foreach(var rotor in rotors) if(rotor!=null) rotor.Rotate(0,850*Time.unscaledDeltaTime,0,Space.Self);
        }
    }
}
