using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DawnGuard.BlackwoodLTD.Editor
{
    public static class CoastOperatorSetup
    {
        public const string Evidence="IntegrationEvidence/OperatorCore";
        public const string PlatformPath="Assets/GameArt/comandpunktforoperator/comandpunktforoperator.fbx";
        const string Output="Assets/DawnGuard/BlackwoodLTD/Art/OperatorCore";
        const string OperatorPath="Assets/GameArt/tactical_operator_rig_biped/tactical_operator_rig_biped_Animation_all_frame_rate_60.fbx";
        public static void Poses()
        {
            var report=new StringBuilder();
            foreach(string role in new[]{"Operator","Walker","Runner","Brute","Special"})
            {var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/"+role+".prefab"));try{var a=go.GetComponentInChildren<Animator>();a.Rebind();
                foreach(float step in new[]{0f,.1f,.5f}){a.Update(step);Bounds b=new Bounds();bool first=true;foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()){var mesh=new Mesh();r.BakeMesh(mesh);foreach(var v in mesh.vertices){var p=r.transform.TransformPoint(v);if(first){first=false;b=new Bounds(p,Vector3.zero);}else b.Encapsulate(p);}UnityEngine.Object.DestroyImmediate(mesh);}report.AppendLine(role+" t+"+step+" actual="+b+" root="+a.transform.localEulerAngles+" scale="+a.transform.localScale);}
                foreach(var t in go.GetComponentsInChildren<Transform>().Take(6))report.AppendLine("  "+t.name+" "+t.localPosition+" rot="+t.localEulerAngles+" scale="+t.localScale);
            }finally{UnityEngine.Object.DestroyImmediate(go);}}
            File.WriteAllText(Evidence+"/poses.txt",report.ToString());
        }
        public static void Build()
        {
            CoastScenePreview.Clear();Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var c=AssetDatabase.LoadAssetAtPath<CoastCatalog>(CoastEditor.Folder+"/Data/BlackwoodCoastCatalog.asset");
            var platform=new GameObject("OperatorPlatform");UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlatformPath),platform.transform);
            try{var mf=platform.GetComponentInChildren<MeshFilter>();var reduced=CoastOptimization.Read(Evidence+"/MeshWork/Output/OperatorCore.bwm",mf.sharedMesh);reduced.name="OperatorCore_GameMesh";mf.sharedMesh=Save(reduced,"OperatorCore_GameMesh.asset");Materials(platform,"Core",PlatformPath);c.operatorPlatform=PrefabUtility.SaveAsPrefabAsset(platform,Output+"/OperatorPlatform.prefab");}finally{UnityEngine.Object.DestroyImmediate(platform);}
            c.operatorPrefab=Character("Operator",OperatorPath,"Short_Breathe_and_Look_Around","Walking",null,"dying_backwards");
            string zombie="Assets/Enemy/Zombie/FBXs/Zombie3.FBX";
            c.enemyVisuals=new[]{
                new CoastEnemyVisual{id="walker",prefab=Character("Walker",zombie,"Z_Idle","Z_Walk_InPlace","Z_Attack","Z_FallingBack","Assets/Enemy/Zombie/Animations"),deathSeconds=1.6f},
                new CoastEnemyVisual{id="runner",prefab=Character("Runner","Assets/Enemy/Cyber_Demon/Cyber_Demonn.fbx",null,"Running","Right_Hand_Sword_Slash","Dead"),deathSeconds=3.1f},
                new CoastEnemyVisual{id="brute",prefab=Character("Brute","Assets/Enemy/TOLSTYAK/Meshy_AI_Mawbound_Behemoth_biped_Meshy_AI_Meshy_Merged_Animations.fbx","Idle_9","Walking","Step_in_High_Kick","Fall_Dead_from_Abdominal_Injury"),deathSeconds=3.7f},
                new CoastEnemyVisual{id="special",prefab=Character("Special","Assets/Enemy/Skeletal_Witch_Rig_biped/Skeletal_Witch_Rig_biped_Meshy_AI_Meshy_Merged_Animations.fbx",null,"Walking","Right_Hand_Sword_Slash","Shot_and_Fall_Forward"),deathSeconds=2.4f}};
            c.rules=CoastRules.Create();EditorUtility.SetDirty(c);AssetDatabase.SaveAssets();CoastScenePreview.Clear();
            File.WriteAllText(Evidence+"/setup.txt","PASS — existing platform and operator, four enemy roles, controllers and three foundation nights installed.\n");
        }
        static T Save<T>(T value,string name)where T:UnityEngine.Object
        {string path=Output+"/"+name;var existing=AssetDatabase.LoadAssetAtPath<T>(path);if(existing!=null){EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);return existing;}AssetDatabase.CreateAsset(value,path);return value;}
        static void Materials(GameObject go,string prefix,string sourcePath)
        {
            var cache=new System.Collections.Generic.Dictionary<Material,Material>();int index=0;
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(original=>
            {if(original==null)return null;if(cache.TryGetValue(original,out var existing))return existing;var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=prefix+"_"+index};var texture=original.mainTexture;if(texture==null){var file=Directory.GetFiles(Path.GetDirectoryName(sourcePath),"*.png").FirstOrDefault(f=>!f.Contains("metallic")&&!f.Contains("roughness")&&!f.Contains("normal"));if(file!=null)texture=AssetDatabase.LoadAssetAtPath<Texture2D>(file.Replace('\\','/'));}mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",original.HasProperty("_Color")?original.color:Color.white);mat.SetFloat("_Smoothness",.2f);mat.enableInstancing=true;cache[original]=Save(mat,mat.name+".mat");index++;return cache[original];}).ToArray();
        }
        static GameObject Character(string role,string path,string idleName,string moveName,string attackName,string deathName,string clipsFolder=null)
        {
            var clips=clipsFolder==null?AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray():AssetDatabase.FindAssets("t:Model",new[]{clipsFolder}).SelectMany(g=>AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(g)).OfType<AnimationClip>()).ToArray();
            Func<string,bool,AnimationClip> clip=(name,loop)=>{var source=clips.FirstOrDefault(a=>!a.name.StartsWith("__preview__")&&(a.name==name||a.name.EndsWith("|"+name)));if(source==null)throw new Exception(role+" missing clip "+name);var copy=UnityEngine.Object.Instantiate(source);copy.name=role+"_"+name;var settings=AnimationUtility.GetAnimationClipSettings(copy);settings.loopTime=loop;settings.keepOriginalPositionXZ=false;settings.keepOriginalPositionY=false;AnimationUtility.SetAnimationClipSettings(copy,settings);return Save(copy,copy.name+".anim");};
            var move=clip(moveName,true);var idle=idleName==null?move:clip(idleName,true);var death=clip(deathName,false);var attack=attackName==null?idle:clip(attackName,true);
            string controllerPath=Output+"/"+role+".controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm=controller.layers[0].stateMachine;foreach(var state in sm.states)sm.RemoveState(state.state);controller.parameters=new AnimatorControllerParameter[0];
            controller.AddParameter("IsMoving",AnimatorControllerParameterType.Bool);controller.AddParameter("IsAttacking",AnimatorControllerParameterType.Bool);controller.AddParameter("Speed",AnimatorControllerParameterType.Float);controller.AddParameter("Die",AnimatorControllerParameterType.Trigger);
            var rest=sm.AddState("Idle");rest.motion=idle;if(idleName==null)rest.speed=0;
            var walking=sm.AddState("Move");walking.motion=move;var hit=sm.AddState("Attack");hit.motion=attack;var dead=sm.AddState("Death");dead.motion=death;sm.defaultState=rest;
            foreach(var from in new[]{rest,walking,hit}){var die=from.AddTransition(dead);die.hasExitTime=false;die.duration=.08f;die.AddCondition(AnimatorConditionMode.If,0,"Die");}
            foreach(var from in new[]{rest,walking}){var tr=from.AddTransition(hit);tr.hasExitTime=false;tr.duration=.08f;tr.AddCondition(AnimatorConditionMode.If,0,"IsAttacking");}
            foreach(var from in new[]{rest,hit}){var tr=from.AddTransition(walking);tr.hasExitTime=false;tr.duration=.1f;tr.AddCondition(AnimatorConditionMode.If,0,"IsMoving");tr.AddCondition(AnimatorConditionMode.IfNot,0,"IsAttacking");}
            foreach(var from in new[]{walking,hit}){var tr=from.AddTransition(rest);tr.hasExitTime=false;tr.duration=.1f;tr.AddCondition(AnimatorConditionMode.IfNot,0,"IsMoving");tr.AddCondition(AnimatorConditionMode.IfNot,0,"IsAttacking");}
            EditorUtility.SetDirty(controller);
            var wrapper=new GameObject(role);var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),wrapper.transform);model.name="Visual";
            try
            {
                // Merged Meshy generic clips use a Z-up skeleton. Preserve its hierarchy.
                // Imported hierarchy owns its axis conversion; do not apply another rotation.
                var animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.CullUpdateTransforms;
                var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(v=>v.isValid&&(!move.isHumanMotion||v.isHuman));
                if(move.isHumanMotion&&(avatar==null||!avatar.isHuman))
                {
                    // Some supplied FBXs contain an invalid embedded avatar despite Human import.
                    // Their animation FBXs carry the matching valid rig description.
                    var source=clipsFolder==null?null:AssetDatabase.FindAssets("t:Model",new[]{clipsFolder}).Select(g=>AssetDatabase.GUIDToAssetPath(g)).FirstOrDefault(f=>f.Contains("Z_Walk_InPlace"));
                    if(source!=null){var importer=(ModelImporter)AssetImporter.GetAtPath(source);var built=AvatarBuilder.BuildHumanAvatar(model,importer.humanDescription);if(built.isValid&&built.isHuman)avatar=Save(built,role+"_Avatar.asset");else UnityEngine.Object.DestroyImmediate(built);}
                }
                if(avatar!=null)animator.avatar=avatar;
                if(move.isHumanMotion&&(animator.avatar==null||!animator.avatar.isValid||!animator.avatar.isHuman))throw new Exception(role+" requires a valid humanoid avatar");
                foreach(var collider in wrapper.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(collider);
                Materials(wrapper,role,path);
                animator.Rebind();animator.Update(0);
                Bounds bounds=new Bounds();bool first=true;
                foreach(var renderer in wrapper.GetComponentsInChildren<SkinnedMeshRenderer>())
                {var mesh=new Mesh();renderer.BakeMesh(mesh);foreach(var v in mesh.vertices){var point=renderer.transform.TransformPoint(v);if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}UnityEngine.Object.DestroyImmediate(mesh);}
                var pivot=new GameObject("PoseNormalization").transform;pivot.SetParent(wrapper.transform,false);model.transform.SetParent(pivot,true);
                if(!first&&bounds.size.y>.01f){float scale=1.6f/bounds.size.y;pivot.localScale=Vector3.one*scale;pivot.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;}
                return PrefabUtility.SaveAsPrefabAsset(wrapper,Output+"/"+role+".prefab");
            }finally{UnityEngine.Object.DestroyImmediate(wrapper);}
        }
        public static void ExportPlatform()
        {
            Directory.CreateDirectory(Evidence+"/MeshWork/Input");Directory.CreateDirectory(Evidence+"/MeshWork/Output");
            var mesh=AssetDatabase.LoadAllAssetsAtPath(PlatformPath).OfType<Mesh>().Single();
            CoastOptimization.Write(mesh,Evidence+"/MeshWork/Input/OperatorCore.bwm",false);
            var jobs=new CoastOptimization.Jobs();jobs.meshes.Add(new CoastOptimization.Job{id="OperatorCore",name="Existing command platform",vertices=mesh.vertexCount,triangles=mesh.triangles.Length/3,target=14000});
            File.WriteAllText(Evidence+"/MeshWork/jobs.json",JsonUtility.ToJson(jobs,true));
        }
        public static void Inventory()
        {
            Directory.CreateDirectory(Evidence);var report=new StringBuilder();
            foreach(var folder in new[]{"Assets/Enemy","Assets/GameArt/tactical_operator_rig_biped","Assets/GameArt/comandpunktforoperator"})
            foreach(var guid in AssetDatabase.FindAssets("t:Model",new[]{folder}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;report.AppendLine(path+" | rig="+importer.animationType+" | root="+model.transform.localScale);
                foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")))report.AppendLine("  CLIP "+clip.name+" seconds="+clip.length+" rate="+clip.frameRate+" loop="+clip.isLooping);
                foreach(var clip in importer.defaultClipAnimations)report.AppendLine("  TAKE "+clip.name+" "+clip.firstFrame+".."+clip.lastFrame);
                foreach(var mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())report.AppendLine("  MESH "+mesh.name+" vertices="+mesh.vertexCount+" bounds="+mesh.bounds);
                foreach(var avatar in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>())report.AppendLine("  AVATAR "+avatar.name+" valid="+avatar.isValid+" human="+avatar.isHuman);
            }
            File.WriteAllText(Evidence+"/assets.txt",report.ToString());
        }
    }
}
