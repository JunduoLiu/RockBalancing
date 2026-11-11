using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace RockBalancing
{
    [DefaultExecutionOrder(300)]
    public sealed class GameView : MonoBehaviour
    {
        public GameSession Session;
        public GameInput Input;
        public Font Typeface;
        public RectTransform Root { get; private set; }
        public Text HeightLabel { get; private set; }
        public Button ShareButton { get; private set; }
        public Button DiscardButton { get; private set; }
        public Button ReleaseButton { get; private set; }
        Text message;
        Text pendingEdge, dropEdge;
        PlacementGuide guide;
        GameObject controls, confirmation;
        readonly Color ink = new Color(.16f, .22f, .23f);
        readonly Color paper = new Color(.96f, .95f, .91f, .96f);

        public void Build()
        {
            if (Typeface == null) Typeface = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(430, 900);
            scaler.matchWidthOrHeight = .5f;
            Root = canvasObject.GetComponent<RectTransform>();
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform);
            Label(Root, "ROCK / BALANCE", new Vector2(.06f,.925f), new Vector2(.8f,.98f), 22, TextAnchor.MiddleLeft);
            Label(Root, "一点耐心，一点重力。", new Vector2(.06f,.89f), new Vector2(.96f,.93f), 12, TextAnchor.MiddleLeft);
            HeightLabel = Label(Root, "", new Vector2(.06f,.79f), new Vector2(.9f,.89f), 44, TextAnchor.MiddleLeft);
            var hints=Panel(Root,"Instructions",new Vector2(.06f,.24f),new Vector2(.94f,.295f),new Color(.96f,.95f,.91f,.9f),false);
            message = Label(hints, "", Vector2.zero, Vector2.one, 13, TextAnchor.MiddleCenter);
            var bar = Panel(Root, "Actions", new Vector2(.06f,.16f), new Vector2(.94f,.225f), Color.clear, false);
            ButtonAt(bar, "查看全景", new Vector2(0,0), new Vector2(.31f,1), Input.Panorama);
            ButtonAt(bar, "重新开始", new Vector2(.345f,0), new Vector2(.655f,1), () => ShowRestart(true));
            ShareButton = ButtonAt(bar, "分享", new Vector2(.69f,0), new Vector2(1,1), () => {});
            ShareButton.gameObject.SetActive(false);
            controls = Panel(Root, "Adjustments", new Vector2(.06f,.02f), new Vector2(.94f,.15f), Color.clear, false).gameObject;
            var move = Panel(controls.transform, "Move", Vector2.zero, new Vector2(.56f,1), paper, true);
            Label(move, "前", new Vector2(.4f,.77f), new Vector2(.6f,1), 13);
            Label(move, "后", new Vector2(.4f,0), new Vector2(.6f,.23f), 13);
            Label(move, "左", new Vector2(.16f,.35f), new Vector2(.32f,.65f), 13);
            Label(move, "右", new Vector2(.68f,.35f), new Vector2(.84f,.65f), 13);
            var pad = move.gameObject.AddComponent<DragPad>();
            pad.Changed = Input.MoveStone;
            pad.Held = Input.HoldPlanar;
            pad.CanBegin = () => Input.CanAdjust;
            pad.Handle = Handle(move, "摇杆");
            var lift = Panel(controls.transform, "Lift", new Vector2(.61f,0), Vector2.one, paper, true);
            Label(lift, "升", new Vector2(.35f,.77f), new Vector2(.65f,1), 13);
            Label(lift, "降", new Vector2(.35f,0), new Vector2(.65f,.23f), 13);
            Label(lift, "高度", new Vector2(.68f,.35f), new Vector2(.98f,.65f), 12);
            var vertical = lift.gameObject.AddComponent<DragPad>();
            vertical.VerticalOnly = true;
            vertical.Changed = Input.RaiseStone;
            vertical.Held = Input.HoldHeight;
            vertical.CanBegin = () => Input.CanAdjust;
            vertical.Handle = Handle(lift, "升降");
            ReleaseButton = StoneButton("释放", new Vector2(.06f,.30f), new Vector2(.59f,.355f), false);
            DiscardButton = StoneButton("弃置这颗", new Vector2(.67f,.30f), new Vector2(.94f,.355f), true);
            var retry = ButtonAt(Root, "等待下一颗 · 点击重试", new Vector2(.22f,.07f), new Vector2(.78f,.13f), () => { _ = Session.RequestNextAsync(); });
            retry.gameObject.name = "Retry";
            retryButton = retry;
            confirmation = Panel(Root, "Restart confirmation", Vector2.zero, Vector2.one, new Color(.1f,.15f,.16f,.85f), true).gameObject;
            var dialog = Panel(confirmation.transform, "Dialog", new Vector2(.08f,.36f), new Vector2(.92f,.66f), paper, true);
            Label(dialog, "开始一座新石塔？", new Vector2(.05f,.6f), new Vector2(.95f,.95f), 24);
            Label(dialog, "当前场景将被清空。", new Vector2(.05f,.4f), new Vector2(.95f,.65f), 14);
            ButtonAt(dialog, "继续搭建", new Vector2(.05f,.1f), new Vector2(.48f,.35f), () => ShowRestart(false));
            ButtonAt(dialog, "重新开始", new Vector2(.52f,.1f), new Vector2(.95f,.35f), () => { Session.Restart(); ShowRestart(false); });
            confirmation.SetActive(false);
            pendingEdge=EdgeLabel("Pending direction");
            dropEdge=EdgeLabel("Landing direction");
        }
        Text EdgeLabel(string name)
        {
            var rect=Panel(Root,name,Vector2.one*.5f,Vector2.one*.5f,new Color(.96f,.95f,.91f,.96f),false);
            rect.sizeDelta=new Vector2(100,28);
            var label=Label(rect,"",Vector2.zero,Vector2.one,13);
            rect.gameObject.SetActive(false);
            return label;
        }
        void UpdateEdge(Text label,Vector3 point,string title)
        {
            var p=Input.ViewCamera.WorldToViewportPoint(point);
            if(p.z<0){p.x=1-p.x;p.y=1-p.y;}
            bool outside=p.z<=0 || p.x<.12f || p.x>.88f || p.y<.40f || p.y>.75f;
            var rect=(RectTransform)label.transform.parent;
            rect.gameObject.SetActive(outside && !Input.Modal);
            if(!outside)return;
            string direction=p.x<.12f?"←":p.x>.88f?"→":p.y<.40f?"↓":"↑";
            label.text=title+" "+direction;
            float margin=Mathf.Max(.12f,rect.sizeDelta.x/(2*Root.rect.width)+.02f);
            rect.anchorMin=rect.anchorMax=new Vector2(Mathf.Clamp(p.x,margin,1-margin),Mathf.Clamp(p.y,.40f,.75f));
            rect.anchoredPosition=Vector2.zero;
        }
        Button retryButton;
        void ShowRestart(bool show) { Input.CancelGesture(); Input.Modal = show; confirmation.SetActive(show); }
        RectTransform Handle(RectTransform parent, string name)
        {
            var handle = Panel(parent, name, Vector2.one * .5f, Vector2.one * .5f, new Color(.32f,.48f,.44f,.65f), false);
            handle.sizeDelta = new Vector2(24, 24);
            return handle;
        }
        Button StoneButton(string title, Vector2 min, Vector2 max, bool discard)
        {
            var rect = Panel(Root, title, min, max, discard ? paper : new Color(.74f,.85f,.78f), true);
            var button = rect.gameObject.AddComponent<StoneActionButton>();
            button.Input = Input; button.Discard = discard;
            button.targetGraphic = rect.GetComponent<Image>();
            Label(rect, title, Vector2.zero, Vector2.one, 17);
            return button;
        }
        void LateUpdate()
        {
            if (controls == null || Session == null) return;
            controls.SetActive(!Session.Waiting);
            retryButton.gameObject.SetActive(Session.Waiting);
            DiscardButton.interactable = ReleaseButton.interactable = Input.CanAct;
            HeightLabel.text = Session.Stabilizing ? "stablizing" : Session.CurrentHeightMm + " mm";
            HeightLabel.fontSize = Session.Stabilizing ? 28 : 44;
            ShareButton.interactable = Session.CanShare;
            message.text = Session.Feedback.Length > 0 ? Session.Feedback : "摇杆移动 · 升降调高 · 点击释放\n拖石头旋转 · 拖空白观察 · 双指缩放";
            if(guide==null)guide=GetComponent<PlacementGuide>();
            if(Session.Pending!=null)
            {
                UpdateEdge(pendingEdge,Session.Pending.transform.position,"待摆石头");
                if(guide!=null)UpdateEdge(dropEdge,guide.DropPosition,"落点");
                var a=(RectTransform)pendingEdge.transform.parent;
                var b=(RectTransform)dropEdge.transform.parent;
                if(a.gameObject.activeSelf && b.gameObject.activeSelf && Vector2.Distance(a.anchorMin,b.anchorMin)<.08f)
                    b.anchoredPosition=Vector2.down*30;
            }
            else
            {
                pendingEdge.transform.parent.gameObject.SetActive(false);
                dropEdge.transform.parent.gameObject.SetActive(false);
            }
        }
        public Text Label(Transform parent, string text, Vector2 min, Vector2 max, int size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rect = Panel(parent, text, min, max, Color.clear, false);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Typeface;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = ink;
            label.raycastTarget = false;
            return label;
        }
        public RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (color.a > 0 || raycast)
            {
                var image = go.AddComponent<Image>();
                image.color = color; image.raycastTarget = raycast;
            }
            return rect;
        }
        public Button ButtonAt(Transform parent, string title, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(parent, title, min, max, paper, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(action);
            Label(rect, title, Vector2.zero, Vector2.one, 12);
            return button;
        }
    }
}
