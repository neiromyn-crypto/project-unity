using UnityEngine;
namespace DawnGuard.BlackwoodLTD
{
    // Cached presentation of the single logical defend target. No independent HP simulation.
    public sealed class CoastOperatorCore : MonoBehaviour
    {
        CoastRoot root;Animator operatorAnimator;
        static readonly int Die=Animator.StringToHash("Die");
        bool dead;
        public bool DeathStarted {get{return dead;}}
        public float DeathElapsed {get;private set;}
        public Animator OperatorAnimator {get{return operatorAnimator;}}
        public float Integrity {get{return root.Game.S.campHP;}}
        public float OperatorHP {get{return root.Game.S.operatorHP;}}
        public void Initialize(CoastRoot owner,Animator animator)
        {root=owner;operatorAnimator=animator;}
        public void Refresh(float dt)
        {
            var s=root.Game.S;
            if(dead&&s.operatorHP>0){dead=false;DeathElapsed=0;if(operatorAnimator!=null){operatorAnimator.Rebind();operatorAnimator.Update(0);}}
            if(s.operatorHP<=0&&!dead){dead=true;if(operatorAnimator!=null){operatorAnimator.speed=1;operatorAnimator.SetTrigger(Die);}}
            if(operatorAnimator!=null)operatorAnimator.speed=dead?1:root.Paused?0:1;
            if(dead)DeathElapsed+=dt;
        }
    }
}
