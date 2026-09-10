using UnityEngine;
namespace DawnGuard.BlackwoodLTD
{
    // Cached presentation of the single logical defend target. No independent HP simulation.
    public sealed class CoastOperatorCore : MonoBehaviour
    {
        CoastRoot root;Animator operatorAnimator;Renderer[] platform;Color[] original;
        MaterialPropertyBlock block;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),Die=Animator.StringToHash("Die");
        float lastCore=-1,lastOperator=-1,flash;bool dead;
        public bool DeathStarted {get{return dead;}}
        public float DeathElapsed {get;private set;}
        public Animator OperatorAnimator {get{return operatorAnimator;}}
        public float Integrity {get{return root.Game.S.campHP;}}
        public float OperatorHP {get{return root.Game.S.operatorHP;}}
        public void Initialize(CoastRoot owner,Animator animator,Renderer[] renderers)
        {block=new MaterialPropertyBlock();root=owner;operatorAnimator=animator;platform=renderers;original=new Color[platform.Length];for(int i=0;i<platform.Length;i++)original[i]=platform[i].sharedMaterial.GetColor(BaseColor);}
        public void Refresh(float dt)
        {
            var s=root.Game.S;if(lastCore>=0&&(s.campHP<lastCore||s.operatorHP<lastOperator))flash=.18f;
            if(dead&&s.operatorHP>0){dead=false;DeathElapsed=0;if(operatorAnimator!=null){operatorAnimator.Rebind();operatorAnimator.Update(0);}}
            if(s.operatorHP<=0&&!dead){dead=true;if(operatorAnimator!=null){operatorAnimator.speed=1;operatorAnimator.SetTrigger(Die);}}
            if(operatorAnimator!=null)operatorAnimator.speed=dead?1:root.Paused?0:1;
            if(dead)DeathElapsed+=dt;
            flash=Mathf.Max(0,flash-dt);Color tint=flash>0?new Color(1,.32f,.12f):s.campHP<=0?new Color(.35f,.38f,.4f):s.campHP<root.Game.Rules.campHP*.25f?new Color(1,.58f,.3f):Color.white;
            for(int i=0;i<platform.Length;i++){block.SetColor(BaseColor,original[i]*tint);platform[i].SetPropertyBlock(block);}
            lastCore=s.campHP;lastOperator=s.operatorHP;
        }
    }
}
