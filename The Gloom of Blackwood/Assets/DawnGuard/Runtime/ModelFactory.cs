using System.Collections.Generic;
using DawnGuard.Core;
using UnityEngine;

namespace DawnGuard.Unity
{
    public sealed class ModelFactory
    {
        private readonly Dictionary<Color,Material> materials=new Dictionary<Color,Material>();
        private readonly Material template;
        public ModelFactory(Material template) { this.template=template; }
        public Material Material(Color color)
        {
            Material value;
            if(materials.TryGetValue(color,out value)) return value;
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null) shader=Shader.Find("Standard");
            value=template!=null ? new Material(template) : new Material(shader); value.color=color;
            materials.Add(color,value); return value;
        }
        public GameObject Part(Transform parent,string name,Vector3 position,Vector3 scale,Color color,PrimitiveType type=PrimitiveType.Cube)
        {
            var part=GameObject.CreatePrimitive(type); part.name=name;
            part.transform.SetParent(parent,false); part.transform.localPosition=position; part.transform.localScale=scale;
            var collider=part.GetComponent<Collider>(); if(collider!=null) Object.Destroy(collider);
            part.GetComponent<Renderer>().sharedMaterial=Material(color);
            return part;
        }
        public GameObject Building(BuildingDefinition def,Transform parent)
        {
            var root=new GameObject(def.id); root.transform.SetParent(parent,false);
            Color steel=new Color(0.28f,0.33f,0.36f), orange=new Color(0.95f,0.48f,0.12f);
            float w=def.width*0.85f,d=def.depth*0.85f;
            if(def.role==BuildingRole.Wall)
            {
                Part(root.transform,"Concrete",new Vector3(0,0.3f,0),new Vector3(.96f,.6f,.8f),steel);
                Part(root.transform,"Plate",new Vector3(0,.75f,0),new Vector3(.9f,.5f,.3f),orange);
            }
            else if(def.role==BuildingRole.Turret)
            {
                Part(root.transform,"Pedestal",new Vector3(0,.25f,0),new Vector3(.75f,.5f,.75f),steel);
                if(def.id=="tesla")
                {
                    for(int i=0;i<3;i++) Part(root.transform,"Coil",new Vector3(0,.65f+i*.2f,0),new Vector3(.5f,.06f,.5f),Color.cyan,PrimitiveType.Cylinder);
                }
                else
                {
                    Part(root.transform,"Head",new Vector3(0,.7f,0),new Vector3(.65f,.35f,.5f),new Color(.3f,.4f,.22f));
                    Part(root.transform,"Barrel",new Vector3(-.15f,.75f,.45f),new Vector3(.1f,.1f,.7f),steel);
                    Part(root.transform,"Barrel",new Vector3(.15f,.75f,.45f),new Vector3(.1f,.1f,.7f),steel);
                }
            }
            else
            {
                Color color=def.role==BuildingRole.Generator ? orange : def.role==BuildingRole.Camp ? new Color(.24f,.43f,.6f) : new Color(.8f,.8f,.72f);
                Part(root.transform,"Body",new Vector3(0,.5f,0),new Vector3(w,1,d),color);
                Part(root.transform,"Roof",new Vector3(0,1.07f,0),new Vector3(w+.1f,.15f,d+.1f),steel);
                Part(root.transform,"Door",new Vector3(0,.4f,-d/2-.01f),new Vector3(.4f,.8f,.06f),orange);
                for(int i=-1;i<=1;i+=2) Part(root.transform,"Window",new Vector3(i*.52f,.6f,-d/2-.04f),new Vector3(.3f,.35f,.06f),Color.cyan);
                if(def.role==BuildingRole.Generator)
                    for(int i=-1;i<=1;i++) Part(root.transform,"Cell",new Vector3(i*.5f,1.35f,0),new Vector3(.22f,.3f,.22f),Color.cyan,PrimitiveType.Cylinder);
                if(def.role==BuildingRole.Laboratory)
                {
                    Part(root.transform,"Mark",new Vector3(0,1.17f,0),new Vector3(.8f,.05f,.2f),Color.cyan);
                    Part(root.transform,"Mark",new Vector3(0,1.17f,0),new Vector3(.2f,.05f,.8f),Color.cyan);
                }
            }
            return root;
        }
        public GameObject Enemy(EnemyDefinition def,Transform parent)
        {
            var root=new GameObject(def.id); root.transform.SetParent(parent,false);
            Color skin=def.id=="brute" ? new Color(.55f,.4f,.32f) : new Color(.38f,.58f,.29f);
            Part(root.transform,"Torso",new Vector3(0,.65f,0),new Vector3(.4f,.65f,.3f),skin);
            Part(root.transform,"Head",new Vector3(0,1.12f,.04f),new Vector3(.3f,.3f,.3f),skin,PrimitiveType.Sphere);
            foreach(int side in new[] {-1,1})
            {
                Part(root.transform,"Leg",new Vector3(side*.12f,.2f,0),new Vector3(.15f,.4f,.2f),Color.gray);
                Part(root.transform,"Arm",new Vector3(side*.3f,.7f,.16f),new Vector3(.13f,.17f,.5f),skin);
            }
            root.transform.localScale=Vector3.one*(def.size/.65f);
            return root;
        }
        public GameObject Drone(Transform parent)
        {
            var root=new GameObject("Drone"); root.transform.SetParent(parent,false);
            Part(root.transform,"Body",Vector3.zero,new Vector3(.55f,.2f,.5f),Color.white);
            Part(root.transform,"Stripe",new Vector3(0,.12f,0),new Vector3(.15f,.05f,.5f),new Color(1,.45f,.1f));
            foreach(int x in new[] {-1,1}) foreach(int z in new[] {-1,1})
                Part(root.transform,"Rotor",new Vector3(x*.45f,0,z*.45f),new Vector3(.45f,.03f,.45f),Color.gray,PrimitiveType.Cylinder);
            return root;
        }
        public void Dispose() { foreach(var material in materials.Values) Object.Destroy(material); materials.Clear(); }
    }
}
