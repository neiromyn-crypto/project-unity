using System;
using System.Collections.Generic;
using DawnGuard.BlackwoodV2;
using DawnGuard.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace DawnGuard.BlackwoodLTD
{
    public sealed class CoastHud : MonoBehaviour
    {
        static readonly Color Ink=new Color(.035f,.085f,.11f,.96f),Panel=new Color(.075f,.16f,.20f,.97f),Cream=new Color(.92f,.92f,.84f),Mint=new Color(.40f,.89f,.78f),Orange=new Color(.98f,.59f,.23f),Muted=new Color(.56f,.68f,.68f);
        CoastRoot root;Font font;RectTransform safe;
        RectTransform droneEdge,droneArrow;Camera droneCamera;CoastDronePresentation drone;
        public bool DroneIndicatorVisible => droneEdge!=null&&droneEdge.gameObject.activeSelf;
        public Canvas Canvas {get;private set;}
        public bool ServiceOpen {get{return service!=null&&service.activeSelf;}}
        GameObject gameUi,menu,dayBar,nightBar,service,settings,pause,results,confirmation,selectionBox;
        Text credits,stone,power,workers,camp,phase,threat,notice,selected,continueText,roundTitle,roundText,serviceTitle,serviceBody,support,selectedStats;
        Image hpFill;Button startNight;Text nextLabel;
        readonly Dictionary<string,Button> buildButtons=new Dictionary<string,Button>();
        readonly List<GameObject> serviceActions=new List<GameObject>();string serviceId;float refreshAt;
        Text[] floats=new Text[8];float[] floatLife=new float[8];Vector3[] floatPoints=new Vector3[8];int nextFloat;
        public void Initialize(CoastRoot owner)
        {
            root=owner;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(EventSystem.current==null){var es=new GameObject("Coast EventSystem",typeof(EventSystem));es.transform.SetParent(transform);
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
            var go=new GameObject("Coast interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);Canvas=go.GetComponent<Canvas>();Canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=1;
            safe=Full(go.transform,"Safe area",Color.clear,false).GetComponent<RectTransform>();safe.gameObject.AddComponent<SafeAreaPanel>();
            gameUi=Full(safe,"Gameplay",Color.clear,false);Full(gameUi.transform,"Board input",Color.clear,true).AddComponent<CoastPointer>().root=root;
            BuildTop();BuildBars();BuildService();BuildMenu();BuildSettings();BuildResults();
            BuildDroneIndicator();
            pause=Full(safe,"Pause",new Color(0,.025f,.04f,.55f),true);var p=Box(pause.transform,"Pause panel",new Vector2(.5f,.5f),new Vector2(-230,170),new Vector2(460,340),Ink);
            Text(p.transform,"ПАУЗА",20,-24,420,55,38,Cream);Text(p.transform,"Время, добыча и бой остановлены",20,-83,420,45,20,Muted);
            Button(p.transform,"ПРОДОЛЖИТЬ",20,-145,420,68,()=>root.TogglePause(),Orange);
            Button(p.transform,"В МЕНЮ",20,-228,420,65,()=>root.OpenMenu());pause.SetActive(false);
            confirmation=Full(safe,"Confirm expedition",new Color(0,.025f,.04f,.7f),true);var c=Box(confirmation.transform,"Confirmation",new Vector2(.5f,.5f),new Vector2(-265,190),new Vector2(530,380),Ink);
            Text(c.transform,"НОВАЯ ЭКСПЕДИЦИЯ",26,-30,480,58,30,Cream);Text(c.transform,"Текущее прохождение будет заменено.\nЭкспедиция начнётся с первого дня.",26,-103,480,84,22,Muted);
            Button(c.transform,"НАЧАТЬ ЗАНОВО",26,-206,478,62,()=>{confirmation.SetActive(false);root.NewGame();},Orange);Button(c.transform,"НАЗАД",26,-286,478,62,()=>confirmation.SetActive(false));confirmation.SetActive(false);
            for(int i=0;i<floats.Length;i++){floats[i]=Text(gameUi.transform,"",0,0,160,32,23,Mint);floats[i].gameObject.SetActive(false);}
            Refresh(true);
        }
        void BuildTop()
        {
            var top=Box(gameUi.transform,"Top HUD",new Vector2(.5f,1),new Vector2(-782,-14),new Vector2(1564,78),Ink);
            Text(top.transform,"BLACKWOOD",18,-12,215,38,29,Cream);Text(top.transform,"БЕРЕГОВОЙ РУБЕЖ",19,-48,210,22,14,Mint);
            credits=Resource(top.transform,240,"КРЕДИТЫ",Orange);stone=Resource(top.transform,390,"КАМЕНЬ",Cream);power=Resource(top.transform,540,"ЭНЕРГИЯ",Mint);workers=Resource(top.transform,690,"РАБОЧИЕ",Cream);
            camp=Text(top.transform,"",853,-9,230,33,25,Cream);Text(top.transform,"ЛАГЕРЬ",853,-44,210,22,14,Muted);
            var hp=Box(top.transform,"Camp health",new Vector2(0,1),new Vector2(934,-48),new Vector2(146,9),Panel);var fill=new GameObject("Health fill",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));fill.transform.SetParent(hp.transform,false);hpFill=fill.GetComponent<Image>();hpFill.color=Mint;var r=fill.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
            phase=Text(top.transform,"",1120,-7,257,68,23,Cream);
            Button(top.transform,"МЕНЮ",1395,-13,151,52,()=>root.OpenMenu());
            var forecast=Box(gameUi.transform,"Wave forecast",new Vector2(1,1),new Vector2(-270,-111),new Vector2(250,153),Ink);
            Text(forecast.transform,"БЛИЖАЙШАЯ УГРОЗА",16,-14,220,27,18,Muted);threat=Text(forecast.transform,"",16,-47,220,91,20,Cream);
            notice=Text(gameUi.transform,"",0,0,990,46,20,Cream);var nr=notice.rectTransform;nr.anchorMin=nr.anchorMax=new Vector2(.5f,0);nr.anchoredPosition=new Vector2(-495,204);notice.alignment=TextAnchor.MiddleCenter;
            var cameraHint=Text(gameUi.transform,"КАМЕРА: WASD / стрелки / перетаскивание · Колесо: масштаб · Home: лагерь · Space: дрон",0,0,1050,24,14,Muted);var cr=cameraHint.rectTransform;cr.anchorMin=cr.anchorMax=new Vector2(.5f,0);cr.anchoredPosition=new Vector2(-525,197);cameraHint.alignment=TextAnchor.MiddleCenter;
        }
        void BuildDroneIndicator()
        {
            droneCamera=root.World.Camera;drone=root.World.Drone;
            var go=Box(gameUi.transform,"Offscreen drone",new Vector2(.5f,.5f),Vector2.zero,new Vector2(104,52),Ink);
            go.GetComponent<BlackwoodPanel>().raycastTarget=false;
            droneEdge=go.GetComponent<RectTransform>();droneEdge.pivot=new Vector2(.5f,.5f);
            var label=Text(go.transform,"ДРОН\nSpace",29,-4,70,44,15,Mint);label.alignment=TextAnchor.MiddleCenter;
            var arrow=Text(go.transform,"›",2,-11,28,30,30,Orange);arrow.alignment=TextAnchor.MiddleCenter;
            droneArrow=arrow.rectTransform;droneArrow.pivot=new Vector2(.5f,.5f);droneArrow.anchoredPosition=new Vector2(16,-26);
            go.SetActive(false);
        }
        void UpdateDroneIndicator()
        {
            Vector3 viewport=droneCamera.WorldToViewportPoint(drone.Position);
            bool visible=!root.MenuOpen&&!root.ManualPause&&!ServiceOpen&&
                (root.Game.S.phase==CoastPhase.Day||root.Game.S.phase==CoastPhase.Night)&&
                (viewport.z<=0||viewport.x<0||viewport.x>1||viewport.y<0||viewport.y>1);
            if(droneEdge.gameObject.activeSelf!=visible)droneEdge.gameObject.SetActive(visible);
            if(!visible)return;
            Vector2 local;Vector3 screen=droneCamera.ViewportToScreenPoint(viewport);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,screen,Canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:Canvas.worldCamera,out local);
            var rect=safe.rect;
            // Leave the top status row and bottom construction/support bar clear.
            var bounds=Rect.MinMaxRect(rect.xMin+62,rect.yMin+236,rect.xMax-62,rect.yMax-124);
            Vector2 center=bounds.center,direction=local-center;
            if(viewport.z<=0)direction=-direction;
            if(direction.sqrMagnitude<.001f)direction=Vector2.up;
            float t=1/Mathf.Max(Mathf.Abs(direction.x)/(bounds.width*.5f),Mathf.Abs(direction.y)/(bounds.height*.5f));
            // Both safe and Gameplay stretch to the same rectangle; anchoredPosition uses its center.
            droneEdge.anchoredPosition=center+direction*t-rect.center;
            droneArrow.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
        }
        Text Resource(Transform parent,float x,string label,Color color){var t=Text(parent,"",x,-8,145,37,29,color);Text(parent,label,x,-47,145,22,14,Muted);return t;}
        void BuildBars()
        {
            dayBar=Box(gameUi.transform,"Build toolbar",new Vector2(.5f,0),new Vector2(-782,175),new Vector2(1564,157),Ink);
            string[] ids={"wall","gun","arcane","tesla","cryo"};for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];var d=root.catalog.rules.Defense(id);var b=Button(dayBar.transform,"",15+i*145,-14,135,128,()=>root.ChooseTool(id));buildButtons[id]=b;
                var icon=root.catalog.defenseIcons!=null&&i<root.catalog.defenseIcons.Length?root.catalog.defenseIcons[i]:null;if(icon!=null)Icon(b.transform,icon,40,-7,57,55);
                Text(b.transform,d.title,6,-59,123,38,14,Cream).alignment=TextAnchor.MiddleCenter;
                string price=id=="wall"?"12 КАМ.":d.credits+" КР.  "+d.stone+" КАМ.";
                Text(b.transform,price,5,-95,125,23,15,Mint).alignment=TextAnchor.MiddleCenter;
            }
            Button(dayBar.transform,"КАЗАРМА",758,-15,173,52,()=>OpenService("barracks"));Button(dayBar.transform,"ОРУЖЕЙНАЯ",758,-82,173,52,()=>OpenService("armory"));
            Button(dayBar.transform,"ЛАБОРАТОРИЯ",944,-15,190,52,()=>OpenService("lab"));Button(dayBar.transform,"ЭНЕРГИЯ",944,-82,190,52,()=>OpenService("power"));
            startNight=Button(dayBar.transform,"НАЧАТЬ НОЧЬ",1157,-15,391,74,()=>{root.Act(()=>root.Game.StartNight());root.ClearSelection();},Orange);
            Text(dayBar.transform,"Ранняя ночь сокращает дневную добычу",1165,-105,377,26,16,Muted).alignment=TextAnchor.MiddleCenter;
            nightBar=Box(gameUi.transform,"Night support",new Vector2(.5f,0),new Vector2(-580,131),new Vector2(1160,113),Ink);
            support=Text(nightBar.transform,"",20,-14,275,82,21,Cream);
            Button(nightBar.transform,"РЕМОНТ\n10 КАМ.",309,-14,250,84,()=>root.Act(()=>root.Game.Support(root.SelectedBuilding,true)));
            Button(nightBar.transform,"ПЕРЕГРУЗКА\n+25% СКОРОСТИ",578,-14,270,84,()=>root.Act(()=>root.Game.Support(root.SelectedBuilding,false)));
            Button(nightBar.transform,"ПРИОРИТЕТ ЦЕЛИ",867,-14,275,84,()=>{root.Game.Prioritize(root.SelectedBuilding);root.Save();},Orange);
            selectionBox=Box(gameUi.transform,"Selection details",new Vector2(0,1),new Vector2(20,-111),new Vector2(286,288),Ink);
            selected=Text(selectionBox.transform,"",16,-17,257,37,22,Cream);selectedStats=Text(selectionBox.transform,"",16,-62,257,74,18,Muted);
            Button(selectionBox.transform,"ПЕРЕНЕСТИ",16,-149,254,45,()=>root.MoveSelected());
            Button(selectionBox.transform,"РЕМОНТ",16,-206,121,55,()=>root.Act(()=>root.Game.Repair(root.SelectedBuilding)));
            Button(selectionBox.transform,"ПРОДАТЬ",148,-206,122,55,()=>{int id=root.SelectedBuilding;root.Act(()=>root.Game.Sell(id));root.ClearSelection();});selectionBox.SetActive(false);
        }
        void BuildService()
        {
            service=Full(gameUi.transform,"Management shade",new Color(.015f,.04f,.055f,.25f),true);
            var p=Box(service.transform,"Management panel",new Vector2(1,1),new Vector2(-545,-111),new Vector2(525,635),Ink);
            serviceTitle=Text(p.transform,"",28,-25,405,52,34,Cream);Button(p.transform,"×",444,-22,54,50,CloseService);
            serviceBody=Text(p.transform,"",28,-96,470,151,21,Muted);
            service.SetActive(false);
        }
        public void OpenService(string id)
        {
            if(root.MenuOpen)return;serviceId=id;root.ClearSelection();service.SetActive(true);BuildServiceActions();Refresh(true);
        }
        public void CloseService(){if(service!=null)service.SetActive(false);}
        public void ShowSelection(){CloseService();Refresh(true);}
        void BuildServiceActions()
        {
            foreach(var a in serviceActions)Destroy(a);serviceActions.Clear();Transform p=serviceTitle.transform.parent;float y=-275;
            Action<string,Action,Color?> add=(label,action,color)=>{var b=Button(p,label,28,y,469,65,action,color);serviceActions.Add(b.gameObject);y-=79;};
            var g=root.Game;var s=g.S;
            if(serviceId=="barracks")
            {
                add("НАНЯТЬ • "+g.WorkerPrice+" КР. + 20 КАМ.",()=>{root.Act(g.Hire);BuildServiceActions();},Orange);
                add("ГРУЗ 8 → 10 • 100 КР. + 50 КАМ.",()=>root.Act(g.UpgradeHarvest),null);
            }
            else if(serviceId=="power")add("+4 МОЩНОСТИ • 80 КР. + 60 КАМ.",()=>root.Act(g.UpgradePower),Orange);
            else if(serviceId=="camp")add("ВОССТАНОВИТЬ ЛАГЕРЬ • "+Mathf.CeilToInt((g.Rules.campHP-s.campHP)/8)+" КАМ.",()=>root.Act(()=>g.Repair(0)),Orange);
            else if(serviceId=="lab")
            {
                if(!s.lab)add("ВКЛЮЧИТЬ • 60 КР. + 30 КАМ.",()=>{root.Act(g.ActivateLab);BuildServiceActions();},Orange);
                else{add("АРКАННЫЙ СТРАЖ • 40 КР. + 20 КАМ.",()=>root.Act(()=>g.Research(1)),null);add("КРИО-СТРАЖ • 40 КР. + 20 КАМ.",()=>root.Act(()=>g.Research(2)),null);add("ТЕСЛА • 50 КР. + 30 КАМ.",()=>root.Act(()=>g.Research(4)),null);}
            }
            else if(serviceId=="armory")
            {
                if(!s.armory)add("ВКЛЮЧИТЬ • 50 КР. + 25 КАМ.",()=>{root.Act(g.ActivateArmory);BuildServiceActions();},Orange);
                else{string[] names={"ПУЛЕМЁТ","АРКАННЫЙ СТРАЖ","ТЕСЛА","КРИО-СТРАЖ"};for(int i=0;i<4;i++){int n=i;int level=s.weaponLevels[i];add(names[i]+" • "+(level>=2?"МАКС.":level==0?"60 КР. + 30 КАМ.":"110 КР. + 60 КАМ."),()=>{root.Act(()=>g.UpgradeWeapon(n));},null);}}
            }
        }
        void BuildMenu()
        {
            menu=Full(safe,"Live main menu",Color.clear,false);
            var p=Box(menu.transform,"Menu panel",new Vector2(0,.5f),new Vector2(52,330),new Vector2(568,660),Ink);
            Text(p.transform,"THE GLOOM OF",34,-34,500,32,22,Mint);Text(p.transform,"BLACKWOOD",32,-83,520,85,60,Cream);
            Text(p.transform,"БЕРЕГОВОЙ РУБЕЖ",35,-181,490,30,20,Orange);
            Text(p.transform,"Построй оборону. Удержи три фронта.\nВстреть рассвет у моря.",35,-234,490,70,23,Cream);
            var c=Button(p.transform,"ПРОДОЛЖИТЬ",34,-344,500,73,()=>root.EnterGame(),Orange);continueText=c.GetComponentInChildren<Text>();
            Button(p.transform,"НОВАЯ ЭКСПЕДИЦИЯ",34,-435,500,64,()=>{if(root.HasSave)confirmation.SetActive(true);else root.NewGame();});
            Button(p.transform,"НАСТРОЙКИ",34,-516,500,60,()=>settings.SetActive(true));
            Text(p.transform,"8 НОЧЕЙ  /  СТРОИТЕЛЬСТВО  /  ВЫЖИВАНИЕ",35,-606,500,28,16,Muted);
            var badge=Box(menu.transform,"Living world badge",new Vector2(1,0),new Vector2(-437,118),new Vector2(410,82),Ink);
            Text(badge.transform,"ЛЕС БОЛЬШЕ НЕ СПИТ",20,-16,380,29,21,Cream);Text(badge.transform,"Заражённые окружают береговой лагерь",20,-48,380,25,16,Mint);
        }
        void BuildSettings()
        {
            settings=Full(safe,"Settings",new Color(.02f,.04f,.05f,.82f),true);var p=Box(settings.transform,"Settings panel",new Vector2(.5f,.5f),new Vector2(-310,275),new Vector2(620,550),Ink);
            Text(p.transform,"НАСТРОЙКИ",32,-27,555,57,35,Cream);Text(p.transform,"ГРОМКОСТЬ",32,-117,400,40,23,Mint);
            Button(p.transform,"ТИШЕ",32,-175,170,58,()=>root.Audio.SetVolume(root.Audio.Volume-.15f));Button(p.transform,"ГРОМЧЕ",219,-175,170,58,()=>root.Audio.SetVolume(root.Audio.Volume+.15f));
            Button(p.transform,"30 FPS",32,-263,170,58,()=>Application.targetFrameRate=30);Button(p.transform,"60 FPS",219,-263,170,58,()=>Application.targetFrameRate=60);
            Text(p.transform,"Колесо / жест: масштаб. Перетаскивание: камера.\nEsc: отмена выбора или пауза.\nВо время паузы добыча и бой остановлены.",32,-349,555,96,20,Muted);
            Button(p.transform,"НАЗАД",32,-461,555,59,()=>settings.SetActive(false),Orange);settings.SetActive(false);
        }
        void BuildResults()
        {
            results=Full(gameUi.transform,"Night report",new Color(.015f,.03f,.045f,.80f),true);var p=Box(results.transform,"Report",new Vector2(.5f,.5f),new Vector2(-355,280),new Vector2(710,560),Ink);
            roundTitle=Text(p.transform,"",35,-34,645,70,39,Cream);roundText=Text(p.transform,"",35,-140,645,248,23,Muted);
            var b=Button(p.transform,"К НОВОМУ ДНЮ",35,-432,645,85,()=>{if(root.Game.S.phase==CoastPhase.Debrief)root.NextDay();else if(root.Game.S.phase==CoastPhase.Defeat)root.RetryDay();else root.OpenMenu();},Orange);nextLabel=b.GetComponentInChildren<Text>();results.SetActive(false);
        }
        public void Refresh(){Refresh(false);}
        public void RefreshNow(){Refresh(true);}
        void Refresh(bool force)
        {
            if(root.Game==null)return;UpdateFloats();UpdateDroneIndicator();if(!force&&Time.unscaledTime<refreshAt)return;refreshAt=Time.unscaledTime+.12f;
            var g=root.Game;var s=g.S;bool day=s.phase==CoastPhase.Day;
            gameUi.SetActive(!root.MenuOpen);menu.SetActive(root.MenuOpen);pause.SetActive(root.ManualPause&&!root.MenuOpen);continueText.text=root.HasSave?"ПРОДОЛЖИТЬ • ДЕНЬ "+s.day:"В ЛАГЕРЬ";
            credits.text=s.credits.ToString();stone.text=s.stone.ToString();power.text=g.PowerUsed+" / "+g.Capacity;workers.text=s.workers.Count+" / 6";camp.text=Mathf.CeilToInt(s.campHP)+" / 600";
            hpFill.rectTransform.anchorMax=new Vector2(s.campHP/g.Rules.campHP,1);
            phase.text=(day?"ДЕНЬ ":"НОЧЬ ")+s.day+" / 8\n"+(day?TimeString(s.remaining):g.Unspawned>0?"ВРАГОВ: "+g.ThreatCount:"ЗАЧИСТКА: "+s.enemies.Count);
            var sides=new bool[3];var counts=new Dictionary<string,int>();foreach(var group in g.Wave.groups){sides[group.front]=true;if(!counts.ContainsKey(group.kind))counts[group.kind]=0;counts[group.kind]+=group.count;}
            string sidesText="";for(int i=0;i<3;i++)if(sides[i])sidesText+=(sidesText.Length==0?"":" • ")+CoastSession.FrontName(i);
            string enemiesText="";foreach(var pair in counts){enemiesText+=(enemiesText.Length==0?"":"  ")+g.Rules.Enemy(pair.Key).title+" ×"+pair.Value;}
            threat.text=sidesText+"\n\n"+enemiesText;threat.fontSize=counts.Count>3?15:18;
            notice.text=root.Notice;dayBar.SetActive(day);nightBar.SetActive(s.phase==CoastPhase.Night);startNight.interactable=day&&!root.Paused;
            support.text="ДРОН — ПОДДЕРЖКА\nЗаряды: "+s.supportCharges+" / 2"+(s.supportCooldown>0?" • "+Mathf.CeilToInt(s.supportCooldown)+" с":"");
            foreach(var pair in buildButtons){var d=g.Rules.Defense(pair.Key);bool unlocked=(s.tech&d.unlock)==d.unlock;pair.Value.interactable=day&&!root.Paused;var graphic=pair.Value.GetComponent<BlackwoodPanel>();graphic.color=root.SelectedTool==pair.Key?new Color(.16f,.37f,.37f):unlocked?Panel:Ink;}
            var b=g.Building(root.SelectedBuilding);selectionBox.SetActive(b!=null&&!ServiceOpen&&day&&!root.MenuOpen);if(b!=null){var d=g.Rules.Defense(b.kind);selected.text=d.title;selectedStats.text="ПРОЧНОСТЬ "+Mathf.CeilToInt(b.hp)+" / "+d.hp+"\n"+(d.damage>0?"УРОН "+(d.damage*(1+.2f*s.weaponLevels[CoastSession.WeaponIndex(b.kind)])).ToString("0.#")+" • КД "+d.cooldown+" с\nЦЕЛЬ: "+b.order:"ПЕРЕНОС / РЕМОНТ / ПРОДАЖА");}
            if(ServiceOpen)UpdateService();
            bool end=s.phase==CoastPhase.Debrief||s.phase==CoastPhase.Victory||s.phase==CoastPhase.Defeat;results.SetActive(end);
            if(end){roundTitle.text=s.phase==CoastPhase.Victory?"РАССВЕТ НАД BLACKWOOD":s.phase==CoastPhase.Defeat?"ЛАГЕРЬ ПАЛ":"НОЧЬ ВЫДЕРЖАНА";nextLabel.text=s.phase==CoastPhase.Debrief?"К НОВОМУ ДНЮ":s.phase==CoastPhase.Defeat?"ПОВТОРИТЬ ДЕНЬ":"В МЕНЮ";
                roundText.text="Лагерь: "+Mathf.CeilToInt(s.campHP)+" / 600\nУничтожено: "+s.killed+"\nКамня доставлено: "+s.delivered+"\nПрорывы: север "+s.leaks[0]+", запад "+s.leaks[1]+", восток "+s.leaks[2]+"\n\n"+(s.phase==CoastPhase.Debrief?"Следующая ночь: "+g.Wave.title+"\n"+g.Wave.advice:s.phase==CoastPhase.Defeat?"Удлините путь в радиусе защиты.\nПрямые проходы слишком быстро выпускают врагов.":"Все восемь волн уничтожены. Море снова спокойно.");}
        }
        void UpdateService()
        {
            var g=root.Game;var s=g.S;
            if(serviceId=="barracks"){serviceTitle.text="КАЗАРМА";serviceBody.text="РАБОЧИЕ "+s.workers.Count+" / 6\nГРУЗ "+(8+s.harvestLevel*2)+" КАМ. • РЕЙС ≈20 СЕК.\nЗа день доставлено: "+s.delivered+" КАМ.\n"+(s.queuedWorker!=0?"Обучение: "+Mathf.CeilToInt(s.hireRemaining)+" сек.":"Камень начисляется при разгрузке.");}
            else if(serviceId=="power"){serviceTitle.text="ЭНЕРГИЯ";serviceBody.text="ЗАНЯТО "+g.PowerUsed+" / "+g.Capacity+"\nУлучшений: "+s.generatorLevel+" / 2\nРабочие не занимают мощность.\nЗащита требует 2–3 энергии.";}
            else if(serviceId=="camp"){serviceTitle.text="ЛАГЕРЬ";serviceBody.text="ПРОЧНОСТЬ "+Mathf.CeilToInt(s.campHP)+" / 600\nРемонт доступен днём.\n1 камень восстанавливает до 8 HP.\nРассвет даёт 100 кредитов.";}
            else if(serviceId=="lab"){serviceTitle.text="ЛАБОРАТОРИЯ";serviceBody.text=!s.lab?"Введите лабораторию в работу со дня 2.\nЗатем исследуйте новый вид защиты.\nИсследование открывает покупку,\nно не дарит башню.":"ОТКРЫТО: "+((s.tech&1)>0?"АРКАН ":"")+((s.tech&2)>0?"КРИО ":"")+((s.tech&4)>0?"ТЕСЛА":"")+"\n"+(s.queuedTech!=0?"Исследование: "+Mathf.CeilToInt(s.labRemaining)+" сек.":"Выберите технологию. Длительность: 20 сек.")+"\nТесла доступна со дня 3.";}
            else{serviceTitle.text="ОРУЖЕЙНАЯ";serviceBody.text=!s.armory?"Введите оружейную в работу со дня 2.\nУлучшения действуют на всё семейство\nсуществующей и будущей защиты.":"Два уровня: +20% / +40% базового урона.\nПулемёт "+s.weaponLevels[0]+" • Аркан "+s.weaponLevels[1]+"\nТесла "+s.weaponLevels[2]+" • Крио "+s.weaponLevels[3]+"\n"+(s.queuedWeapon>=0?"Улучшение: "+Mathf.CeilToInt(s.armoryRemaining)+" сек.":"Выберите семейство.");}
        }
        public void Float(CoastEvent e){int n=nextFloat++%floats.Length;floats[n].text="+"+e.amount+" КАМ.";floatLife[n]=1.8f;floatPoints[n]=new Vector3(e.x,1.1f,e.z);floats[n].gameObject.SetActive(true);}
        void UpdateFloats(){for(int i=0;i<floats.Length;i++)if(floatLife[i]>0){if(!root.Paused)floatLife[i]-=Time.unscaledDeltaTime;Vector3 screen=root.World.Camera.WorldToScreenPoint(floatPoints[i]+Vector3.up*(1.8f-floatLife[i])*.4f);Vector2 local;RectTransformUtility.ScreenPointToLocalPointInRectangle(safe,screen,null,out local);floats[i].rectTransform.anchorMin=floats[i].rectTransform.anchorMax=new Vector2(.5f,.5f);floats[i].rectTransform.anchoredPosition=local;floats[i].gameObject.SetActive(floatLife[i]>0);}}
        static string TimeString(float time){int t=Mathf.CeilToInt(time);return(t/60).ToString("00")+":"+(t%60).ToString("00");}
        GameObject Full(Transform parent,string name,Color color,bool raycast)
        {var g=Box(parent,name,Vector2.zero,Vector2.zero,Vector2.zero,color);var r=g.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;g.GetComponent<BlackwoodPanel>().raycastTarget=raycast;return g;}
        GameObject Box(Transform parent,string name,Vector2 anchor,Vector2 at,Vector2 size,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(BlackwoodPanel));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.pivot=new Vector2(0,1);r.anchoredPosition=at;r.sizeDelta=size;var p=go.GetComponent<BlackwoodPanel>();p.color=color;p.corner=10;return go;}
        Text Text(Transform parent,string content,float x,float y,float width,float height,int size,Color color)
        {var g=new GameObject(content.Length>20?content.Substring(0,20):content,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));g.transform.SetParent(parent,false);var r=g.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);var t=g.GetComponent<Text>();t.font=font;t.text=content;t.fontSize=size;t.color=color;t.raycastTarget=false;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;}
        Button Button(Transform parent,string label,float x,float y,float width,float height,Action callback,Color? color=null)
        {var bg=color??Panel;var go=Box(parent,label,new Vector2(0,1),new Vector2(x,y),new Vector2(width,height),bg);var b=go.AddComponent<Button>();b.targetGraphic=go.GetComponent<BlackwoodPanel>();var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.15f,1.15f,1.15f);colors.pressedColor=new Color(.75f,.9f,.9f);colors.disabledColor=new Color(.4f,.45f,.45f);b.colors=colors;b.onClick.AddListener(()=>{if(root.Audio!=null)root.Audio.Click();callback();});var t=Text(go.transform,label,8,-4,width-16,height-8,19,color.HasValue?Ink:Cream);t.alignment=TextAnchor.MiddleCenter;return b;}
        void Icon(Transform parent,Sprite sprite,float x,float y,float width,float height)
        {var go=new GameObject("Icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,y);r.sizeDelta=new Vector2(width,height);var image=go.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;}
    }
}
