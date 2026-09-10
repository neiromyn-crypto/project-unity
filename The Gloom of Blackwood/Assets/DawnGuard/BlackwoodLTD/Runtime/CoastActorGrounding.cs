using UnityEngine;
namespace DawnGuard.BlackwoodLTD
{
    // Flat coast presentation only. References resolved once; preserve authored death motion.
    public sealed class CoastActorGrounding : MonoBehaviour
    {
        Animator actor;Transform offset,leftFoot,rightFoot;
        public void Initialize(Animator animator,Transform pivot)
        {
            actor=animator;offset=pivot;
            if(actor.isHuman){leftFoot=actor.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=actor.GetBoneTransform(HumanBodyBones.RightFoot);}
            else foreach(var bone in actor.GetComponentsInChildren<Transform>())
            {if(bone.name=="LeftFoot")leftFoot=bone;else if(bone.name=="RightFoot")rightFoot=bone;}
        }
        void LateUpdate()
        {
            if(actor==null||offset==null||leftFoot==null||rightFoot==null||actor.GetCurrentAnimatorStateInfo(0).IsName("Death"))return;
            float sole=Mathf.Min(leftFoot.position.y,rightFoot.position.y)-.08f*transform.localScale.y;
            offset.position+=Vector3.up*(transform.position.y-sole);
        }
    }
}
