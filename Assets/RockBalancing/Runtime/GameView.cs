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
        public Button RestartButton { get; private set; }
        public Button DiscardButton { get; private set; }
        public Button ReleaseButton { get; private set; }
        Text message;
        Text pendingEdge;
        GameObject feedbackPanel;
        GameObject controls, confirmation;
        RectTransform actionBar;
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
            // WASD/arrows belong to stone adjustment, not UI selection/submission.
            EventSystem.current.sendNavigationEvents = false;
            HeightLabel = Label(Root, "", new Vector2(.06f,.87f), new Vector2(.63f,.98f), 44, TextAnchor.MiddleLeft);
            var feedback = Panel(Root,"Temporary feedback",new Vector2(.06f,.27f),new Vector2(.94f,.34f),paper,false);
            feedbackPanel = feedback.gameObject;
            message = Label(feedback, "", Vector2.zero, Vector2.one, 13, TextAnchor.MiddleCenter);
            feedbackPanel.SetActive(false);
            var bar = Panel(Root, "Actions", new Vector2(.94f,.965f), new Vector2(.94f,.965f), Color.clear, false);
            actionBar = bar; bar.pivot = Vector2.one;
            RestartButton = ActionButton(bar, "重新开始", "rotate-ccw", () => ShowRestart(true));
            ShareButton = ActionButton(bar, "分享", "share-2", () => {});
            ShareButton.gameObject.SetActive(false);
            controls = Panel(Root, "Adjustments", new Vector2(.06f,.02f), new Vector2(.94f,.16f), Color.clear, false).gameObject;
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
            ReleaseButton = StoneButton("释放", new Vector2(.06f,.18f), new Vector2(.59f,.245f), false);
            DiscardButton = StoneButton("弃置这颗", new Vector2(.67f,.18f), new Vector2(.94f,.245f), true);
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
            bool outside=p.z<=0 || p.x<.12f || p.x>.88f || p.y<.38f || p.y>.83f;
            var rect=(RectTransform)label.transform.parent;
            rect.gameObject.SetActive(outside && !Input.Modal);
            if(!outside)return;
            string direction=p.x<.12f?"←":p.x>.88f?"→":p.y<.38f?"↓":"↑";
            label.text=title+" "+direction;
            float margin=Mathf.Max(.12f,rect.sizeDelta.x/(2*Root.rect.width)+.02f);
            rect.anchorMin=rect.anchorMax=new Vector2(Mathf.Clamp(p.x,margin,1-margin),Mathf.Clamp(p.y,.38f,.83f));
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
            rect.gameObject.AddComponent<IconButtonPresentation>().Initialize(this, button, discard ? "弃置这颗" : "释放", discard ? "trash-2" : "arrow-down-to-line", ink);
            return button;
        }
        void LateUpdate()
        {
            if (controls == null || Session == null) return;
            UpdateActionLayout();
            RestartButton.interactable = !Input.Modal && !Session.ShareBusy;
            controls.SetActive(!Session.Waiting);
            retryButton.gameObject.SetActive(Session.Waiting);
            DiscardButton.interactable = ReleaseButton.interactable = Input.CanAct;
            HeightLabel.text = Session.Stabilizing ? "stablizing" : Session.CurrentHeightMm + " mm";
            HeightLabel.fontSize = HeightLabel.resizeTextMaxSize = Session.Stabilizing ? 28 : 44;
            ShareButton.interactable = Session.CanShare && !Input.Modal;
            message.text = Session.Feedback;
            feedbackPanel.SetActive(message.text.Length > 0 && !Session.Waiting && !Input.Modal);
            if(Session.Pending!=null)
                UpdateEdge(pendingEdge,Session.Pending.transform.position,"待摆石头");
            else
                pendingEdge.transform.parent.gameObject.SetActive(false);
        }
        Button ActionButton(Transform parent, string title, string icon, UnityEngine.Events.UnityAction action)
        {
            var rect = Panel(parent, title, Vector2.zero, Vector2.zero, paper, true);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(action);
            rect.gameObject.AddComponent<IconButtonPresentation>().Initialize(this, button, title, icon, ink);
            return button;
        }
        void UpdateActionLayout()
        {
            // Keep at least 48 screen pixels even in short/narrow windows; the old
            // percentage-only header made Share less than 44 px wide on a phone.
            float scale = Mathf.Max(.01f, Root.GetComponent<Canvas>().scaleFactor);
            float size = Mathf.Max(48, 48 / scale), gap = Mathf.Max(8, 8 / scale);
            actionBar.sizeDelta = new Vector2(size * 2 + gap, size);
            var restart = (RectTransform)RestartButton.transform;
            var share = (RectTransform)ShareButton.transform;
            restart.pivot = share.pivot = Vector2.zero;
            restart.sizeDelta = share.sizeDelta = Vector2.one * size;
            restart.anchoredPosition = Vector2.zero;
            share.anchoredPosition = new Vector2(size + gap, 0);
            HeightLabel.rectTransform.anchorMax = new Vector2(
                Mathf.Min(.63f, .94f - (size * 2 + gap * 2) / Root.rect.width), .98f);
            float height = Mathf.Max(Root.rect.height * .065f, 48 / scale);
            LayoutStoneAction(ReleaseButton, height);
            LayoutStoneAction(DiscardButton, height);
            var feedback = (RectTransform)feedbackPanel.transform;
            float bottom = Mathf.Max(.27f, .18f + (height + gap) / Root.rect.height);
            feedback.anchorMin = new Vector2(.06f, bottom);
            feedback.anchorMax = new Vector2(.94f, bottom + .07f);
        }
        static void LayoutStoneAction(Button button, float height)
        {
                var rect = (RectTransform)button.transform;
                var min = rect.anchorMin; var max = rect.anchorMax;
                min.y = max.y = .18f;
                rect.anchorMin = min; rect.anchorMax = max;
                rect.pivot = new Vector2(.5f, 0);
                rect.sizeDelta = new Vector2(0, height);
                rect.anchoredPosition = Vector2.zero;
        }
        public Text Label(Transform parent, string text, Vector2 min, Vector2 max, int size, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rect = Panel(parent, text, min, max, Color.clear, false);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Typeface;
            label.text = text;
            label.fontSize = size;
            // Wide, short windows have less vertical space after canvas scaling.
            // Shrink captions within their cells instead of silently clipping them.
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(9, size / 2);
            label.resizeTextMaxSize = size;
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
