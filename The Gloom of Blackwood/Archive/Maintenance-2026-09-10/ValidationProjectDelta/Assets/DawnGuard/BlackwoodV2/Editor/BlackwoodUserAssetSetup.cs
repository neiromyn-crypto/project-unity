using System;
using System.IO;
using System.Linq;
using DawnGuard.Unity;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DawnGuard.BlackwoodV2.Editor
{
    public static class BlackwoodUserAssetSetup
    {
        public const string Zombie="Assets/Enemy/TOLSTYAK/TOLSTYAK.fbx";
        public const string Clips="Assets/Enemy/TOLSTYAK/Meshy_AI_Mawbound_Behemoth_biped_Meshy_AI_Meshy_Merged_Animations.fbx";
        [MenuItem("Dawn Guard/7 - Integrate and validate Blackwood user map")]
        public static void Integrate()
        {
            Assign();
            BlackwoodUserAssetsBuilder.Build();
            var root=UnityEngine.Object.FindAnyObjectByType<BlackwoodRoot>();
            if(root==null) throw new InvalidOperationException("Map build cancelled.");
            File.WriteAllText("IntegrationEvidence/built-scene.txt",root.gameObject.scene.path);
        }
        public static void Assign()
        {
            BlackwoodUserAssetsBuilder.Scan();
            var set=AssetDatabase.LoadAssetAtPath<BlackwoodAssetSet>(BlackwoodUserAssetsBuilder.SetPath);
            // Explicit choices: complete textured exports, not the separate part-segmentation exports.
            set.shelter=Load("MainShelter/MainShelter"); set.camp=Load("scifi_bunker/scifi_bunker");
            set.generator=Load("PowerStation/PowerStation"); set.wall=Load("Barrier/Barrier");
            set.gun=Load("AutoTurret/AutoTurret"); set.lab=Load("ResearchLab/ResearchLab");
            set.tesla=Load("ElectricTurret/ElectricTurret"); set.drone=Load("Drone/Drone");
            set.pine=Load("pine_tree/pine_tree"); set.rubble=Load("RockPile_01/RockPile_01/RockPile_01");
            set.pad=Load("Landing_Platfor/Landing_Platfor");
            if(set.zombie==null) set.zombie=AssetDatabase.LoadAssetAtPath<GameObject>(Zombie);
            var ground=Load("GroundTile_01/GroundTile_01").GetComponentInChildren<Renderer>();
            set.groundMaterial=ground.sharedMaterial;
            EditorUtility.SetDirty(set); AssetDatabase.SaveAssets();
        }
        static GameObject Load(string name)
        {
            string path="Assets/GameArt/"+name+".fbx";
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(go==null) throw new InvalidOperationException("Missing mapped asset "+path);
            return go;
        }
        public static void ConfigureEnemy(BlackwoodEnemyView view,string folder,string id)
        {
            var animator=view.animator;
            if(animator==null || animator.avatar==null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Expected the existing valid TOLSTYAK humanoid Avatar.");
            var source=AssetDatabase.LoadAllAssetsAtPath(Clips).OfType<AnimationClip>().ToArray();
            AnimationClip Copy(string name,bool loop)
            {
                string path=folder+"/"+name+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(clip!=null) return clip;
                var original=source.Single(c=>c.name==name);
                clip=UnityEngine.Object.Instantiate(original); clip.name=name;
                // Imported clips remain untouched. Events cannot apply a second gameplay hit.
                AnimationUtility.SetAnimationEvents(clip,Array.Empty<AnimationEvent>());
                var settings=AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime=loop; settings.loopBlend=loop;
                settings.keepOriginalPositionXZ=true; settings.keepOriginalPositionY=true;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                AssetDatabase.CreateAsset(clip,path); return clip;
            }
            var idle=Copy("Idle_9",true); var walk=Copy(id=="runner" ? "Running" : "Walking",true);
            var attack=Copy("Step_in_High_Kick",true); var death=Copy("Fall_Dead_from_Abdominal_Injury",false);
            var controller=AnimatorController.CreateAnimatorControllerAtPath(folder+"/"+id+".controller");
            controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            controller.AddParameter("IsMoving",AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAttacking",AnimatorControllerParameterType.Bool);
            controller.AddParameter("Die",AnimatorControllerParameterType.Trigger);
            var machine=controller.layers[0].stateMachine;
            var idleState=machine.AddState("Idle"); idleState.motion=idle;
            var moveState=machine.AddState(id=="runner" ? "Run" : "Walk"); moveState.motion=walk;
            var attackState=machine.AddState("Attack"); attackState.motion=attack;
            var deathState=machine.AddState("Death"); deathState.motion=death;
            machine.defaultState=idleState;
            void Transition(AnimatorState from,AnimatorState to,string parameter,bool value)
            {
                var t=from.AddTransition(to); t.hasExitTime=false; t.duration=.1f;
                t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,0,parameter);
            }
            Transition(idleState,moveState,"IsMoving",true); Transition(moveState,idleState,"IsMoving",false);
            Transition(idleState,attackState,"IsAttacking",true); Transition(moveState,attackState,"IsAttacking",true);
            Transition(attackState,idleState,"IsAttacking",false);
            var die=machine.AddAnyStateTransition(deathState); die.hasExitTime=false; die.duration=.08f;
            die.canTransitionToSelf=false; die.AddCondition(AnimatorConditionMode.If,0,"Die");
            animator.runtimeAnimatorController=controller; animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            view.deathSeconds=death.length+.08f;
            // Pose the imported skeleton before fitting. SkinnedRenderer.bounds contains all animation poses.
            idle.SampleAnimation(animator.gameObject,0);
        }
        public static Bounds MeshBounds(Renderer[] renderers)
        {
            Bounds result=default; bool first=true;
            foreach(var renderer in renderers)
            {
                Bounds b=renderer.bounds;
                if(renderer is SkinnedMeshRenderer skin)
                {
                    var mesh=new Mesh(); skin.BakeMesh(mesh);
                    b=new Bounds(skin.transform.TransformPoint(mesh.bounds.center),Vector3.zero);
                    for(int i=0;i<8;i++)
                    {
                        var e=mesh.bounds.extents;
                        b.Encapsulate(skin.transform.TransformPoint(mesh.bounds.center+new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z)));
                    }
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                if(first) {result=b; first=false;} else result.Encapsulate(b);
            }
            return result;
        }
    }
}
