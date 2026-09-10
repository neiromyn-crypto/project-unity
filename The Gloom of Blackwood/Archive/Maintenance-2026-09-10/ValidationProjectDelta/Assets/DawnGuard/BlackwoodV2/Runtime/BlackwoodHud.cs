using System;
using System.Collections.Generic;
using DawnGuard.Core;
using DawnGuard.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace DawnGuard.BlackwoodV2
{
    public sealed class BlackwoodHud : MonoBehaviour
    {
        private static readonly Color Ink=new Color(.065f,.105f,.13f,.97f);
        private static readonly Color Panel=new Color(.12f,.2f,.23f,.97f);
        private static readonly Color Cream=new Color(.91f,.89f,.81f);
        private static readonly Color Mint=new Color(.45f,.84f,.75f);
        private static readonly Color Orange=new Color(.91f,.57f,.26f);
        private BlackwoodRoot root;
        private Font font;
        private RectTransform safe;
        private GameObject gameUi,dayUi,nightUi,selectionUi,menu,settings,confirm,modal,techUi;
        private Text credits,energy,health,phase,objective,message,selection,rocket,boost,result,playText,fpsText,soundText;
        private Button resume,retry,upgrade,repair,move,sell,research,drone;
        private Text upgradeLabel,repairLabel,researchLabel,droneLabel;
        private BlackwoodPanel healthFill;
        private bool menuVisible=true,techVisible,soundEnabled;
        private int lastDay;
        private GamePhase lastPhase;
        private float noticeUntil,nextRefresh;
        private string notice="";
        private AudioSource audioSource;
        private AudioClip clickSound;
        private readonly Dictionary<string,Button> shop=new Dictionary<string,Button>();
        private readonly Dictionary<string,Text> shopLabels=new Dictionary<string,Text>();
        public VirtualStick Stick { get; private set; }

        public void Initialize(BlackwoodRoot gameRoot)
        {
            root=gameRoot; font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(EventSystem.current==null)
            {
                var es=new GameObject("Blackwood EventSystem",typeof(EventSystem)); es.transform.SetParent(transform,false);
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
            var canvasGo=new GameObject("Blackwood UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform,false); canvasGo.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=1;
            safe=Full(canvasGo.transform,"Safe area",Color.clear,false).GetComponent<RectTransform>(); safe.gameObject.AddComponent<SafeAreaPanel>();
            SetupSound();
            gameUi=Full(safe,"Gameplay",Color.clear,false);
            Full(gameUi.transform,"Board",Color.clear,true).AddComponent<BlackwoodPointer>().root=root;
            BuildGameplay();
            BuildMenu(canvasGo.transform);
            BuildModal();
            lastDay=root.Game.Day; lastPhase=root.Game.Phase;
            Refresh();
        }

        private void BuildGameplay()
        {
            var top=Box(gameUi.transform,"Resources",new Vector2(0,1),new Vector2(22,-20),new Vector2(428,98),Ink);
            credits=Label(top.transform,"Credits",new Vector2(0,1),new Vector2(18,-12),new Vector2(205,40),30,Orange);
            energy=Label(top.transform,"Energy",new Vector2(0,1),new Vector2(236,-12),new Vector2(182,40),24,Mint);
            health=Label(top.transform,"Health",new Vector2(0,1),new Vector2(18,-56),new Vector2(285,28),19,Cream);
            var track=Box(top.transform,"Health track",new Vector2(0,1),new Vector2(248,-66),new Vector2(158,10),Panel);
            healthFill=Full(track.transform,"Health fill",Mint,false).GetComponent<BlackwoodPanel>();
            phase=Label(gameUi.transform,"Phase",new Vector2(.5f,1),new Vector2(-180,-23),new Vector2(360,85),28,Cream,TextAnchor.UpperCenter);
            Btn(gameUi.transform,"МЕНЮ",new Vector2(1,1),new Vector2(-166,-24),new Vector2(144,66),ShowMenu);
            objective=Label(gameUi.transform,"Objective",new Vector2(.5f,1),new Vector2(-535,-128),new Vector2(1070,58),23,Cream,TextAnchor.UpperCenter);
            message=Label(gameUi.transform,"Feedback",new Vector2(.5f,0),new Vector2(-515,184),new Vector2(1030,48),21,Orange,TextAnchor.MiddleCenter);
            dayUi=Full(gameUi.transform,"Build controls",Color.clear,false);
            string[] ids={"wall","gun","camp","generator","lab","tesla"};
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i]; Text text;
                var button=Btn(dayUi.transform,id,new Vector2(0,0),new Vector2(22+i*136,128),new Vector2(128,104),()=>root.SelectBuild(id),out text);
                text.fontSize=20; shop.Add(id,button); shopLabels.Add(id,text);
            }
            Btn(dayUi.transform,"ВЫБОР",new Vector2(0,0),new Vector2(838,128),new Vector2(110,104),root.ClearSelection);
            Btn(dayUi.transform,"НАЧАТЬ НОЧЬ",new Vector2(1,0),new Vector2(-310,128),new Vector2(288,104),root.StartNight,null,Orange);
            Btn(dayUi.transform,"ТЕХНОЛОГИИ",new Vector2(1,1),new Vector2(-278,-114),new Vector2(256,64),()=>techVisible=!techVisible);
            techUi=Box(dayUi.transform,"Technology panel",new Vector2(1,1),new Vector2(-340,-192),new Vector2(318,198),Ink);
            research=Btn(techUi.transform,"",new Vector2(0,1),new Vector2(14,-14),new Vector2(290,78),root.Research,out researchLabel);
            drone=Btn(techUi.transform,"",new Vector2(0,1),new Vector2(14,-104),new Vector2(290,78),root.UpgradeDrone,out droneLabel);
            selectionUi=Box(dayUi.transform,"Building detail",new Vector2(.5f,0),new Vector2(-498,300),new Vector2(996,112),Ink);
            selection=Label(selectionUi.transform,"Detail",new Vector2(0,1),new Vector2(15,-8),new Vector2(965,36),22,Cream);
            upgrade=Btn(selectionUi.transform,"",new Vector2(0,1),new Vector2(14,-50),new Vector2(260,52),root.UpgradeSelected,out upgradeLabel);
            repair=Btn(selectionUi.transform,"",new Vector2(0,1),new Vector2(286,-50),new Vector2(246,52),root.RepairSelected,out repairLabel);
            move=Btn(selectionUi.transform,"ПЕРЕНЕСТИ",new Vector2(0,1),new Vector2(544,-50),new Vector2(212,52),root.MoveSelected);
            sell=Btn(selectionUi.transform,"РАЗОБРАТЬ",new Vector2(0,1),new Vector2(768,-50),new Vector2(212,52),root.SellSelected);
            nightUi=Full(gameUi.transform,"Drone controls",Color.clear,false);
            var stick=Box(nightUi.transform,"Flight joystick",new Vector2(0,0),new Vector2(40,258),new Vector2(214,214),new Color(.12f,.22f,.25f,.8f));
            stick.GetComponent<RectTransform>().pivot=new Vector2(.5f,.5f); stick.GetComponent<RectTransform>().anchoredPosition=new Vector2(147,151);
            Stick=stick.AddComponent<VirtualStick>();
            var handle=Box(stick.transform,"Handle",new Vector2(.5f,.5f),Vector2.zero,new Vector2(70,70),Mint);
            handle.GetComponent<RectTransform>().pivot=new Vector2(.5f,.5f); handle.GetComponent<BlackwoodPanel>().raycastTarget=false;
            Stick.handle=handle.GetComponent<RectTransform>();
            Btn(nightUi.transform,"",new Vector2(1,0),new Vector2(-374,204),new Vector2(164,158),root.Rocket,out rocket,Orange);
            Btn(nightUi.transform,"",new Vector2(1,0),new Vector2(-194,204),new Vector2(164,158),root.Boost,out boost);
        }

        private void BuildMenu(Transform canvas)
        {
            menu=Full(canvas,"Main menu",Ink,true);
            if(root.menuBackdrop!=null)
            {
                var art=new GameObject("Background art",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));
                art.transform.SetParent(menu.transform,false); var raw=art.GetComponent<RawImage>(); raw.texture=root.menuBackdrop; raw.raycastTarget=false;
                var fit=art.GetComponent<AspectRatioFitter>(); fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio=(float)root.menuBackdrop.width/root.menuBackdrop.height;
            }
            var menuSafe=Full(menu.transform,"Menu safe area",Color.clear,false); menuSafe.AddComponent<SafeAreaPanel>();
            var slab=Box(menuSafe.transform,"Menu panel",new Vector2(0,.5f),new Vector2(42,334),new Vector2(566,668),new Color(.055f,.10f,.12f,.94f));
            Label(slab.transform,"Overline",new Vector2(0,1),new Vector2(34,-32),new Vector2(480,40),20,Mint).text="THE GLOOM OF";
            Label(slab.transform,"Title",new Vector2(0,1),new Vector2(29,-78),new Vector2(510,96),65,Cream).text="BLACKWOOD";
            Label(slab.transform,"Description",new Vector2(0,1),new Vector2(34,-186),new Vector2(482,95),25,Cream).text="Построй убежище.\nУдержи ночь. Встреть рассвет.";
            Btn(slab.transform,"",new Vector2(0,1),new Vector2(34,-308),new Vector2(498,86),EnterGame,out playText,Orange);
            Btn(slab.transform,"НОВАЯ ЭКСПЕДИЦИЯ",new Vector2(0,1),new Vector2(34,-410),new Vector2(498,72),()=>confirm.SetActive(true));
            Btn(slab.transform,"НАСТРОЙКИ",new Vector2(0,1),new Vector2(34,-498),new Vector2(498,72),()=>settings.SetActive(true));
            Label(slab.transform,"Footer",new Vector2(0,1),new Vector2(34,-601),new Vector2(498,40),18,Mint).text="АВАНПОСТ 01    /    8 НОЧЕЙ";
            settings=Full(menuSafe.transform,"Settings",Ink,true);
            Label(settings.transform,"Title",new Vector2(.5f,.5f),new Vector2(-310,240),new Vector2(620,72),36,Cream,TextAnchor.MiddleCenter).text="НАСТРОЙКИ";
            Btn(settings.transform,"",new Vector2(.5f,.5f),new Vector2(-260,125),new Vector2(520,76),ToggleFps,out fpsText);
            Btn(settings.transform,"",new Vector2(.5f,.5f),new Vector2(-260,31),new Vector2(520,76),ToggleSound,out soundText);
            Btn(settings.transform,"НАЗАД",new Vector2(.5f,.5f),new Vector2(-260,-83),new Vector2(520,76),()=>settings.SetActive(false));
            settings.SetActive(false);
            confirm=Full(menuSafe.transform,"New expedition confirmation",Ink,true);
            Label(confirm.transform,"Title",new Vector2(.5f,.5f),new Vector2(-400,210),new Vector2(800,150),29,Cream,TextAnchor.MiddleCenter).text="Начать с первого дня?\nТекущая экспедиция будет заменена.\nЕё сохранение останется в архиве.";
            Btn(confirm.transform,"НАЧАТЬ ЗАНОВО",new Vector2(.5f,.5f),new Vector2(-270,12),new Vector2(540,78),()=>{root.NewGame(); confirm.SetActive(false); EnterGame();},null,Orange);
            Btn(confirm.transform,"НАЗАД",new Vector2(.5f,.5f),new Vector2(-270,-86),new Vector2(540,70),()=>confirm.SetActive(false));
            confirm.SetActive(false);
        }

        private void BuildModal()
        {
            modal=Full(safe,"Pause and result",Ink,true);
            result=Label(modal.transform,"Result",new Vector2(.5f,.5f),new Vector2(-440,256),new Vector2(880,180),32,Cream,TextAnchor.MiddleCenter);
            resume=Btn(modal.transform,"ПРОДОЛЖИТЬ",new Vector2(.5f,.5f),new Vector2(-270,24),new Vector2(540,74),root.TogglePause,null,Orange);
            retry=Btn(modal.transform,"ПЕРЕИГРАТЬ ПОДГОТОВКУ",new Vector2(.5f,.5f),new Vector2(-270,-68),new Vector2(540,74),root.Retry);
            Btn(modal.transform,"ГЛАВНОЕ МЕНЮ",new Vector2(.5f,.5f),new Vector2(-270,-160),new Vector2(540,74),ShowMenu);
        }
        private void ShowMenu()
        { if(!root.Paused) root.TogglePause(); menuVisible=true; Stick.ResetInput(); }
        private void EnterGame()
        { menuVisible=false; if(root.Paused) root.TogglePause(); Stick.ResetInput(); }

        public void Refresh()
        {
            if(Time.unscaledTime<nextRefresh) return; nextRefresh=Time.unscaledTime+.1f;
            var g=root.Game; bool day=g.Phase==GamePhase.Day;
            if(g.Day!=lastDay || g.Phase!=lastPhase)
            {
                if(day && g.Day>lastDay) notice="РАССВЕТ  •  Доход получен. Следующая ночь: "+g.Day;
                else if(g.Phase==GamePhase.Night) notice="НОЧЬ "+g.Day+"  •  "+g.Rules.waves[g.Day-1].title;
                else notice="";
                noticeUntil=Time.unscaledTime+5; lastDay=g.Day; lastPhase=g.Phase;
            }
            credits.text=g.Wallet.Credits+"  КРЕД."; energy.text="ЭНЕРГИЯ "+g.PowerUsed+" / "+g.PowerCapacity;
            float hp=g.Shelter==null ? 0 : g.Shelter.health;
            float max=g.Shelter==null ? g.Rules.Building("shelter").maxHealth : g.MaxHealth(g.Shelter);
            health.text="УБЕЖИЩЕ  "+Mathf.CeilToInt(hp)+" / "+Mathf.CeilToInt(max);
            healthFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(hp/max),1);
            healthFill.color=hp/max<.3f ? Orange : Mint;
            phase.text=(day ? "ДЕНЬ " : "НОЧЬ ")+g.Day+" / "+g.Rules.waves.Length+"\n"+(day && g.Day==1 ? "ПОДГОТОВКА БЕЗ СПЕШКИ" : g.Phase==GamePhase.Night && g.Day==g.Rules.waves.Length && g.Remaining<=0 ? "ЗАЧИСТКА" : Mathf.CeilToInt(g.Remaining)+" СЕК");
            objective.text=Time.unscaledTime<noticeUntil ? notice : Objective(g);
            message.text=root.Message;
            menu.SetActive(menuVisible); gameUi.SetActive(!menuVisible);
            dayUi.SetActive(day && !root.Completed); nightUi.SetActive(g.Phase==GamePhase.Night && !root.Completed);
            techUi.SetActive(techVisible && g.Day>=2);
            foreach(var kv in shop)
            {
                var d=g.Rules.Building(kv.Key); string why="";
                if(g.Day<d.requiredDay) why="С ДНЯ "+d.requiredDay;
                else if(g.Tech<d.requiredTech) why="НУЖНА ТЕХНОЛОГИЯ";
                else if(g.PowerAvailable<d.powerUse) why="НЕТ ЭНЕРГИИ";
                else if(g.Wallet.Credits<d.cost) why="НУЖНО "+d.cost;
                kv.Value.interactable=why.Length==0 && !root.Paused;
                kv.Value.GetComponent<BlackwoodPanel>().color=root.SelectedBuild==kv.Key ? new Color(.25f,.42f,.4f) : Panel;
                shopLabels[kv.Key].text=d.title+"\n"+d.cost+" КР.  "+(d.powerUse>0 ? " / "+d.powerUse+" Э" : "")+(why.Length>0 ? "\n"+why : "");
                shopLabels[kv.Key].fontSize=kv.Key=="lab" ? 17 : 19;
            }
            var b=g.FindBuilding(root.SelectedBuilding); selectionUi.SetActive(b!=null && !root.MovingBuilding);
            if(b!=null)
            {
                var d=g.Rules.Building(b.definitionId); int fix=Mathf.CeilToInt((g.MaxHealth(b)-b.health)*g.Rules.repairCreditPerHealth),up=d.upgradeCost*b.level;
                selection.text=d.title+"  /  УР. "+b.level+"  /  "+Mathf.CeilToInt(b.health)+" HP"+(b.powered ? "" : "  /  НЕТ ПИТАНИЯ");
                upgradeLabel.text=b.level>=d.maxLevel ? "МАКС. УРОВЕНЬ" : "УЛУЧШИТЬ  "+up;
                repairLabel.text=fix>0 ? "РЕМОНТ  "+fix : "ПОВРЕЖДЕНИЙ НЕТ";
                upgrade.interactable=b.level<d.maxLevel && g.Wallet.Credits>=up;
                repair.interactable=fix>0 && g.Wallet.Credits>=fix; move.interactable=sell.interactable=!b.permanent;
            }
            researchLabel.text=g.Tech>0 ? "ТЕСЛА ОТКРЫТА" : "ОТКРЫТЬ ТЕСЛУ  /  "+g.Rules.researchCost;
            research.interactable=g.Tech==0 && g.HasPoweredLab() && g.Wallet.Credits>=g.Rules.researchCost;
            droneLabel.text=g.DroneLevel>=3 ? "ДРОН: МАКС. УРОВЕНЬ" : "ДРОН УР. "+(g.DroneLevel+1)+"  /  "+(g.Rules.droneUpgradeBaseCost*g.DroneLevel);
            drone.interactable=g.DroneLevel<3 && g.HasPoweredLab() && g.Wallet.Credits>=g.Rules.droneUpgradeBaseCost*g.DroneLevel;
            rocket.text="РАКЕТА\n"+(g.Combat.RocketCooldown>0 ? Mathf.CeilToInt(g.Combat.RocketCooldown)+" СЕК" : "ГОТОВО");
            boost.text="УСИЛЕНИЕ\n"+(g.Combat.BoostCooldown>0 ? Mathf.CeilToInt(g.Combat.BoostCooldown)+" СЕК" : "ГОТОВО");
            bool dead=g.Phase==GamePhase.Defeat;
            modal.SetActive(!menuVisible && (root.Paused || dead || root.Completed));
            result.text=root.Completed ? "РАССВЕТ НАД BLACKWOOD\nВы удержали аванпост "+g.Rules.waves.Length+" ночей." : dead ? "АВАНПОСТ ПОТЕРЯН\nВернитесь к подготовке этого дня\nи попробуйте другую оборону." : "ПАУЗА\nАванпост ждёт вашего возвращения.";
            resume.gameObject.SetActive(root.Paused && !dead && !root.Completed); retry.gameObject.SetActive(!root.Completed);
            playText.text=root.Completed ? "ИТОГ ЭКСПЕДИЦИИ" : root.HasSavedCampaign || g.Day>1 ? "ПРОДОЛЖИТЬ  /  ДЕНЬ "+g.Day : "В ЛАГЕРЬ";
            fpsText.text="ЧАСТОТА КАДРОВ  /  "+Application.targetFrameRate+" FPS";
            soundText.text="ЗВУК КНОПОК  /  "+(soundEnabled ? "ВКЛ" : "ВЫКЛ");
        }
        private string Objective(GameSession g)
        {
            if(g.Phase==GamePhase.Night) return "Левый стик — полёт. Дрон стреляет сам. Нажмите врага, чтобы выбрать цель.";
            if(g.Day==1)
            {
                bool defense=false; foreach(var b in g.Buildings) if(b.definitionId=="wall" || b.definitionId=="gun") defense=true;
                return defense ? "Оборона поставлена. Можно добавить стены или начать первую ночь." : "120 кредитов: пулемёт или три стены. Закройте северный проход — разрыв в камнях.";
            }
            if(g.Day>=g.Rules.waves.Length) return "Последняя ночь: уничтожьте всех врагов. Таймер не завершает финальную зачистку.";
            int income=g.Rules.dawnReward; foreach(var b in g.Buildings) income+=g.Rules.Building(b.definitionId).dailyIncome*b.level;
            return "Следующий рассвет: +"+income+" кредитов при сохранении лагерей. Следите за мощностью.";
        }

        private void SetupSound()
        {
            soundEnabled=PlayerPrefs.GetInt("BlackwoodV2.Sound",1)==1;
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake=false; audioSource.volume=.2f;
            int n=3528; var data=new float[n];
            for(int i=0;i<n;i++) { float t=(float)i/44100; data[i]=Mathf.Sin(t*2*Mathf.PI*660)*Mathf.Sin(Mathf.PI*i/n)*Mathf.Exp(-t*38); }
            clickSound=AudioClip.Create("Blackwood soft UI click",n,1,44100,false); clickSound.SetData(data,0);
        }
        private void ToggleFps() { Application.targetFrameRate=Application.targetFrameRate==60 ? 30 : 60; PlayerPrefs.SetInt("BlackwoodV2.FPS",Application.targetFrameRate); PlayerPrefs.Save(); }
        private void ToggleSound() { soundEnabled=!soundEnabled; PlayerPrefs.SetInt("BlackwoodV2.Sound",soundEnabled ? 1 : 0); PlayerPrefs.Save(); }
        private void OnDestroy() { if(clickSound!=null) Destroy(clickSound); }
        private GameObject Full(Transform parent,string name,Color color,bool block)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(BlackwoodPanel)); go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>(); r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero;
            var p=go.GetComponent<BlackwoodPanel>(); p.color=color; p.corner=0; p.raycastTarget=block; return go;
        }
        private GameObject Box(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(BlackwoodPanel)); go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=anchor; r.pivot=new Vector2(0,1); r.anchoredPosition=position; r.sizeDelta=size;
            go.GetComponent<BlackwoodPanel>().color=color; return go;
        }
        private Text Label(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,int fontSize,Color color,TextAnchor align=TextAnchor.UpperLeft)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            var r=go.GetComponent<RectTransform>(); r.anchorMin=r.anchorMax=anchor; r.pivot=new Vector2(0,1); r.anchoredPosition=position; r.sizeDelta=size;
            var t=go.GetComponent<Text>(); t.font=font; t.fontSize=fontSize; t.color=color; t.alignment=align; t.raycastTarget=false;
            if(name=="Title") t.fontStyle=FontStyle.Bold;
            t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Truncate; return t;
        }
        private Button Btn(Transform parent,string title,Vector2 anchor,Vector2 position,Vector2 size,Action action,Text unused=null,Color? color=null)
        { Text label; return Btn(parent,title,anchor,position,size,action,out label,color); }
        private Button Btn(Transform parent,string title,Vector2 anchor,Vector2 position,Vector2 size,Action action,out Text label,Color? color=null)
        {
            var go=Box(parent,title,anchor,position,size,color ?? Panel); var button=go.AddComponent<Button>(); button.targetGraphic=go.GetComponent<BlackwoodPanel>();
            var colors=button.colors; colors.normalColor=Color.white; colors.highlightedColor=new Color(1.1f,1.1f,1.1f); colors.pressedColor=new Color(.75f,.9f,.9f); colors.disabledColor=new Color(.55f,.57f,.57f,.8f); button.colors=colors;
            button.navigation=new Navigation {mode=Navigation.Mode.None};
            label=Label(go.transform,"Label",new Vector2(0,1),new Vector2(7,-5),new Vector2(size.x-14,size.y-10),22,color.HasValue ? Ink : Cream,TextAnchor.MiddleCenter); label.text=title; label.fontStyle=FontStyle.Bold;
            button.onClick.AddListener(()=>{if(soundEnabled && clickSound!=null) audioSource.PlayOneShot(clickSound); action();}); return button;
        }
    }
}

