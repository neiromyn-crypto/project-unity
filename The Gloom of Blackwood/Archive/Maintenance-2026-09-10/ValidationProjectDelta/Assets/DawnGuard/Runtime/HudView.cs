using System;
using System.Collections.Generic;
using DawnGuard.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace DawnGuard.Unity
{
    public sealed class HudView : MonoBehaviour
    {
        private GameRoot root;
        private Font font;
        private Text stats,phase,message,selected,rocketText,boostText,modalTitle;
        private GameObject dayPanel,nightPanel,modal;
        private Button resume,retry;
        private RectTransform safe;
        private readonly Dictionary<string,Button> shop=new Dictionary<string,Button>();
        private readonly Color panelColor=new Color(.06f,.08f,.11f,.93f);
        public VirtualStick Stick { get; private set; }

        public void Initialize(GameRoot root)
        {
            this.root=root;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(EventSystem.current==null)
            {
                var es=new GameObject("EventSystem",typeof(EventSystem)); es.transform.SetParent(transform,false);
#if ENABLE_INPUT_SYSTEM
                var input=es.AddComponent<InputSystemUIInputModule>(); input.AssignDefaultActions();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
            var canvasObject=new GameObject("Game HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900); scaler.matchWidthOrHeight=.5f;
            var board=FullPanel(canvasObject.transform,"Board input",new Color(0,0,0,0),true);
            board.AddComponent<BoardPointer>().root=root;
            var safeObject=FullPanel(canvasObject.transform,"Safe area",Color.clear,false);
            safe=safeObject.GetComponent<RectTransform>(); safeObject.AddComponent<SafeAreaPanel>();
            stats=Label(safe,"Stats",new Vector2(0,1),new Vector2(18,-18),new Vector2(550,105),24,TextAnchor.UpperLeft);
            phase=Label(safe,"Phase",new Vector2(.5f,1),new Vector2(-190,-18),new Vector2(380,95),28,TextAnchor.UpperCenter);
            ButtonAt(safe,"ПАУЗА",new Vector2(1,1),new Vector2(-170,-18),new Vector2(152,60),root.TogglePause);
            message=Label(safe,"Message",new Vector2(.5f,1),new Vector2(-560,-118),new Vector2(1120,70),23,TextAnchor.UpperCenter);
            dayPanel=FullPanel(safe,"Day controls",Color.clear,false);
            string[] ids={"wall","gun","camp","generator","lab","tesla"};
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i]; var def=root.Game.Rules.Building(id);
                var button=ButtonAt(dayPanel.transform,def.title+"\n"+def.cost,new Vector2(0,0),new Vector2(18+i*151,112),new Vector2(142,90),()=>root.SelectBuild(id));
                shop.Add(id,button);
            }
            ButtonAt(dayPanel.transform,"ВЫБОР",new Vector2(0,0),new Vector2(924,112),new Vector2(142,90),root.ClearSelection);
            ButtonAt(dayPanel.transform,"НАЧАТЬ НОЧЬ",new Vector2(1,0),new Vector2(-240,112),new Vector2(220,90),root.StartNight,new Color(.8f,.34f,.05f));
            selected=Label(dayPanel.transform,"Selection details",new Vector2(0,0),new Vector2(18,235),new Vector2(1000,55),23,TextAnchor.UpperLeft);
            ButtonAt(dayPanel.transform,"УЛУЧШИТЬ",new Vector2(0,0),new Vector2(18,175),new Vector2(175,52),root.UpgradeSelected);
            ButtonAt(dayPanel.transform,"РЕМОНТ",new Vector2(0,0),new Vector2(205,175),new Vector2(150,52),root.RepairSelected);
            ButtonAt(dayPanel.transform,"ПЕРЕНЕСТИ",new Vector2(0,0),new Vector2(367,175),new Vector2(175,52),root.MoveSelected);
            ButtonAt(dayPanel.transform,"РАЗОБРАТЬ",new Vector2(0,0),new Vector2(554,175),new Vector2(175,52),root.SellSelected);
            ButtonAt(dayPanel.transform,"ТЕСЛА: ИССЛЕДОВАТЬ 120",new Vector2(1,1),new Vector2(-340,-180),new Vector2(320,60),root.Research);
            ButtonAt(dayPanel.transform,"УЛУЧШИТЬ ДРОН",new Vector2(1,1),new Vector2(-340,-250),new Vector2(320,60),root.UpgradeDrone);
            nightPanel=FullPanel(safe,"Night controls",Color.clear,false);
            var stickObject=Box(nightPanel.transform,"Joystick",new Vector2(0,0),new Vector2(24,244),new Vector2(220,220),new Color(.15f,.2f,.25f,.7f));
            Stick=stickObject.AddComponent<VirtualStick>();
            var handle=Box(stickObject.transform,"Handle",new Vector2(.5f,.5f),Vector2.zero,new Vector2(65,65),new Color(.65f,.75f,.8f,.8f));
            var hr=handle.GetComponent<RectTransform>(); hr.pivot=new Vector2(.5f,.5f); handle.GetComponent<Image>().raycastTarget=false; Stick.handle=hr;
            var rocket=ButtonAt(nightPanel.transform,"РАКЕТА",new Vector2(1,0),new Vector2(-345,164),new Vector2(155,140),root.Rocket);
            rocketText=rocket.GetComponentInChildren<Text>();
            var boost=ButtonAt(nightPanel.transform,"УСИЛЕНИЕ",new Vector2(1,0),new Vector2(-175,164),new Vector2(155,140),root.Boost);
            boostText=boost.GetComponentInChildren<Text>();
            modal=FullPanel(safe,"Pause and result",new Color(.01f,.02f,.04f,.92f),true);
            modalTitle=Label(modal.transform,"Result",new Vector2(.5f,.5f),new Vector2(-430,210),new Vector2(860,160),34,TextAnchor.MiddleCenter);
            resume=ButtonAt(modal.transform,"ПРОДОЛЖИТЬ",new Vector2(.5f,.5f),new Vector2(-170,20),new Vector2(340,65),root.TogglePause);
            retry=ButtonAt(modal.transform,"ПЕРЕИГРАТЬ ЭТОТ ДЕНЬ",new Vector2(.5f,.5f),new Vector2(-170,-60),new Vector2(340,65),root.Retry);
            ButtonAt(modal.transform,"НОВАЯ ИГРА С ДНЯ 1",new Vector2(.5f,.5f),new Vector2(-170,-140),new Vector2(340,65),root.NewGame);
            modal.SetActive(false);
        }

        public void Refresh()
        {
            var game=root.Game;
            var shelter=game.Shelter;
            stats.text="КРЕДИТЫ  "+game.Wallet.Credits+"     ЭНЕРГИЯ  "+game.PowerUsed+"/"+game.PowerCapacity+
                "\nБАЗА  "+(shelter==null ? 0 : Mathf.CeilToInt(shelter.health))+"     ДРОН ур. "+game.DroneLevel;
            string time=game.Day==1 && game.Phase==GamePhase.Day ? "БЕЗ ТАЙМЕРА" : Mathf.CeilToInt(game.Remaining)+" сек";
            phase.text=(game.Phase==GamePhase.Night ? "НОЧЬ " : "ДЕНЬ ")+game.Day+" / "+game.Rules.waves.Length+"\n"+time;
            message.text=root.Message;
            bool day=game.Phase==GamePhase.Day && !root.Completed;
            dayPanel.SetActive(day); nightPanel.SetActive(game.Phase==GamePhase.Night && !root.Completed);
            foreach(var item in shop)
            {
                var def=game.Rules.Building(item.Key);
                item.Value.interactable=game.Day>=def.requiredDay && game.Tech>=def.requiredTech &&
                    game.Wallet.Credits>=def.cost && game.PowerAvailable>=def.powerUse;
            }
            var b=game.FindBuilding(root.SelectedBuilding);
            if(b==null) selected.text="Северный проход: клетки (6–8, 9). Стоимость: стена 40 / пулемёт 120.";
            else
            {
                var def=game.Rules.Building(b.definitionId);
                int repair=Mathf.CeilToInt((game.MaxHealth(b)-b.health)*.15f);
                selected.text=def.title+" ур."+b.level+" | HP "+Mathf.CeilToInt(b.health)+" | улучшение "+
                    (b.level>=def.maxLevel ? "MAX" : (def.upgradeCost*b.level).ToString())+" | ремонт "+repair+(b.powered ? "" : " | НЕТ ПИТАНИЯ");
            }
            rocketText.text="РАКЕТА\n"+(game.Combat.RocketCooldown>0 ? Mathf.CeilToInt(game.Combat.RocketCooldown).ToString() : "ГОТОВО");
            boostText.text="УСИЛЕНИЕ\n"+(game.Combat.BoostCooldown>0 ? Mathf.CeilToInt(game.Combat.BoostCooldown).ToString() : "ГОТОВО");
            bool defeated=game.Phase==GamePhase.Defeat;
            modal.SetActive(root.Paused || defeated || root.Completed);
            if(modal.activeSelf)
            {
                modalTitle.text=root.Completed ? "ПОБЕДА\nПять ночей пройдены" : defeated ? "ПОРАЖЕНИЕ\nИзмените подготовку и попробуйте снова" : "ПАУЗА";
                resume.gameObject.SetActive(root.Paused && !defeated && !root.Completed);
                retry.gameObject.SetActive(!root.Completed);
            }
        }

        private GameObject FullPanel(Transform parent,string name,Color color,bool block)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            go.GetComponent<Image>().color=color; go.GetComponent<Image>().raycastTarget=block; return go;
        }
        private GameObject Box(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=anchor; rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=position; rect.sizeDelta=size; go.GetComponent<Image>().color=color; return go;
        }
        private Text Label(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,int fontSize,TextAnchor align)
        {
            var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=anchor;
            rect.pivot=new Vector2(0,1); rect.anchoredPosition=position; rect.sizeDelta=size;
            var text=go.AddComponent<Text>(); text.font=font; text.fontSize=fontSize; text.color=new Color(.94f,.96f,1);
            text.alignment=align; text.raycastTarget=false; text.horizontalOverflow=HorizontalWrapMode.Wrap;
            text.verticalOverflow=VerticalWrapMode.Truncate;
            return text;
        }
        private Button ButtonAt(Transform parent,string title,Vector2 anchor,Vector2 position,Vector2 size,Action callback,Color? color=null)
        {
            var go=Box(parent,title,anchor,position,size,color ?? panelColor);
            var button=go.AddComponent<Button>(); button.targetGraphic=go.GetComponent<Image>();
            button.onClick.AddListener(()=>callback());
            var label=Label(go.transform,"Text",new Vector2(0,1),new Vector2(5,-3),new Vector2(size.x-10,size.y-6),22,TextAnchor.MiddleCenter);
            label.text=title; return button;
        }
    }
}
