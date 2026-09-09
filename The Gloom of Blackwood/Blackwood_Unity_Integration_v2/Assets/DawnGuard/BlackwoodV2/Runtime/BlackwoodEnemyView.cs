using System.Collections.Generic;
using DawnGuard.Core;
using UnityEngine;

namespace DawnGuard.BlackwoodV2
{
    // Visual adapter only. Damage, health, movement and rewards stay in GameSession.
    public sealed class BlackwoodEnemyView : MonoBehaviour
    {
        public Animator animator;
        public string speedParameter="Speed",movingParameter="IsMoving",attackParameter="IsAttacking",deathTrigger="Die";
        [Min(0)] public float deathSeconds=0;
        private readonly Dictionary<string,AnimatorControllerParameterType> parameters=new Dictionary<string,AnimatorControllerParameterType>();
        private Vector3 previous;
        private float stationary;
        private bool initialized;
        private void Initialize()
        {
            if(initialized) return; initialized=true;
            if(animator==null) animator=GetComponentInChildren<Animator>(true);
            if(animator==null) return;
            animator.applyRootMotion=false;
            if(animator.runtimeAnimatorController!=null) foreach(var p in animator.parameters) parameters[p.name]=p.type;
        }
        private bool Has(string name,AnimatorControllerParameterType type)
        { AnimatorControllerParameterType actual; return !string.IsNullOrEmpty(name) && parameters.TryGetValue(name,out actual) && actual==type; }
        public void ResetView()
        {
            initialized=false; parameters.Clear(); Initialize(); stationary=0; previous=transform.position;
            if(animator!=null && animator.runtimeAnimatorController!=null) { animator.speed=1; animator.Rebind(); animator.Update(0); }
        }
        public void SetPaused(bool paused) { Initialize(); if(animator!=null) animator.speed=paused ? 0 : 1; }
        public void Refresh(EnemyState enemy,GameSession game,bool paused)
        {
            Initialize(); SetPaused(paused);
            if(paused) { previous=transform.position; return; }
            float speed=(transform.position-previous).magnitude/Mathf.Max(.001f,Time.unscaledDeltaTime); previous=transform.position;
            stationary=speed<.04f ? stationary+Time.unscaledDeltaTime : 0;
            bool adjacent=false;
            foreach(var b in game.Buildings)
            {
                var d=game.Rules.Building(b.definitionId);
                float x=Mathf.Clamp(enemy.x,b.cell.x,b.cell.x+d.width),z=Mathf.Clamp(enemy.z,b.cell.z,b.cell.z+d.depth);
                if((x-enemy.x)*(x-enemy.x)+(z-enemy.z)*(z-enemy.z)<.8f*.8f) { adjacent=true; break; }
            }
            // The starter has no attack event. This is explicitly a visual approximation.
            bool attacking=stationary>.15f && adjacent && !enemy.travelling;
            if(animator==null || animator.runtimeAnimatorController==null) return;
            if(Has(speedParameter,AnimatorControllerParameterType.Float)) animator.SetFloat(speedParameter,speed);
            if(Has(movingParameter,AnimatorControllerParameterType.Bool)) animator.SetBool(movingParameter,speed>=.04f);
            if(Has(attackParameter,AnimatorControllerParameterType.Bool)) animator.SetBool(attackParameter,attacking);
        }
        public float BeginDeath()
        {
            Initialize();
            if(animator==null || animator.runtimeAnimatorController==null || deathSeconds<=0 || !Has(deathTrigger,AnimatorControllerParameterType.Trigger)) return 0;
            animator.speed=1; animator.SetTrigger(deathTrigger); return deathSeconds;
        }
    }
}
