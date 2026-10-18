// Tier UI-1 — fields serializados engine (nomes m_* p/ FieldApplier) + tipos
// aninhados (FontData, Navigation, ColorBlock, SpriteState, AnimationTriggers,
// OptionData, Entry). Comportamento (props) fica em UIImpl.cs.
using System.Collections.Generic;
using UnityEngine.Events;

namespace UnityEngine.UI
{
    public partial class Graphic
    {
        public Material m_Material;
        public Color m_Color = new Color(1, 1, 1, 1);
        public bool m_RaycastTarget = true;
    }

    public partial class Image
    {
        public Sprite m_Sprite;
        public int m_Type;
        public bool m_PreserveAspect;
        public bool m_FillCenter = true;
        public int m_FillMethod;
        public float m_FillAmount = 1f;
        public bool m_FillClockwise = true;
        public int m_FillOrigin;
    }

    public class FontData
    {
        public Font m_Font;
        public int m_FontSize = 14;
        public int m_FontStyle;
        public bool m_BestFit;
        public int m_MinSize = 10;
        public int m_MaxSize = 40;
        public int m_Alignment;
        public bool m_AlignByGeometry;
        public bool m_RichText = true;
        public int m_HorizontalOverflow;
        public int m_VerticalOverflow;
        public float m_LineSpacing = 1f;
    }

    public partial class Text
    {
        public FontData m_FontData = new FontData();
        public string m_Text = "";
    }

    public struct Navigation
    {
        public int m_Mode;
        public Selectable m_SelectOnUp;
        public Selectable m_SelectOnDown;
        public Selectable m_SelectOnLeft;
        public Selectable m_SelectOnRight;
    }

    public struct ColorBlock
    {
        public Color m_NormalColor;
        public Color m_HighlightedColor;
        public Color m_PressedColor;
        public Color m_DisabledColor;
        public float m_ColorMultiplier;
        public float m_FadeDuration;
    }

    public struct SpriteState
    {
        public Sprite m_HighlightedSprite;
        public Sprite m_PressedSprite;
        public Sprite m_DisabledSprite;
    }

    public class AnimationTriggers
    {
        public string m_NormalTrigger = "Normal";
        public string m_HighlightedTrigger = "Highlighted";
        public string m_PressedTrigger = "Pressed";
        public string m_DisabledTrigger = "Disabled";
    }

    public partial class Selectable
    {
        public Navigation m_Navigation;
        public int m_Transition;
        public ColorBlock m_Colors;
        public SpriteState m_SpriteState;
        public AnimationTriggers m_AnimationTriggers = new AnimationTriggers();
        public bool m_Interactable = true;
        public Graphic m_TargetGraphic;
    }

    public partial class Button
    {
        public UnityEventData m_OnClick;
    }

    public class OptionData
    {
        public string m_Text = "";
        public Sprite m_Image;
    }

    public class OptionDataList
    {
        public List<OptionData> m_Options = new List<OptionData>();
    }

    public partial class Dropdown
    {
        public RectTransform m_Template;
        public Text m_CaptionText;
        public Image m_CaptionImage;
        public Text m_ItemText;
        public Image m_ItemImage;
        public int m_Value;
        public OptionDataList m_Options = new OptionDataList();
        public UnityEventData m_OnValueChanged;
    }

    public partial class Toggle
    {
        public int toggleTransition;
        public Graphic graphic;
        public ToggleGroup m_Group;
        public UnityEventData onValueChanged;
        public bool m_IsOn;
    }

    public partial class ToggleGroup
    {
        public bool m_AllowSwitchOff;
    }

    public partial class Slider
    {
        public RectTransform m_FillRect;
        public RectTransform m_HandleRect;
        public int m_Direction;
        public float m_MinValue;
        public float m_MaxValue = 1f;
        public bool m_WholeNumbers;
        public float m_Value = 1f;
        public UnityEventData m_OnValueChanged;
    }

    public partial class Scrollbar
    {
        public RectTransform m_HandleRect;
        public int m_Direction;
        public float m_Value;
        public float m_Size = 0.2f;
        public int m_NumberOfSteps;
        public UnityEventData m_OnValueChanged;
    }

    public partial class ScrollRect
    {
        public RectTransform m_Content;
        public bool m_Horizontal = true;
        public bool m_Vertical = true;
        public int m_MovementType = 1;
        public float m_Elasticity = 0.1f;
        public bool m_Inertia = true;
        public float m_DecelerationRate = 0.135f;
        public float m_ScrollSensitivity = 1f;
        public RectTransform m_Viewport;
        public Scrollbar m_HorizontalScrollbar;
        public Scrollbar m_VerticalScrollbar;
        public int m_HorizontalScrollbarVisibility;
        public int m_VerticalScrollbarVisibility;
        public float m_HorizontalScrollbarSpacing;
        public float m_VerticalScrollbarSpacing;
    }

    public partial class Mask
    {
        public bool m_ShowMaskGraphic = true;
    }

    public partial class GraphicRaycaster
    {
        public bool m_IgnoreReversedGraphics = true;
        public int m_BlockingObjects;
        public int m_BlockingMask = -1;
    }

    public partial class CanvasScaler
    {
        public int m_UiScaleMode;
        public float m_ReferencePixelsPerUnit = 100f;
        public float m_ScaleFactor = 1f;
        public Vector2 m_ReferenceResolution = new Vector2(800, 600);
        public int m_ScreenMatchMode;
        public float m_MatchWidthOrHeight;
        public int m_PhysicalUnit = 3;
        public float m_FallbackScreenDPI = 96f;
        public float m_DefaultSpriteDPI = 96f;
        public float m_DynamicPixelsPerUnit = 1f;
    }

    public partial class EventTrigger
    {
        public class Entry
        {
            public int eventID;
            public UnityEventData callback;
        }
        public List<Entry> m_Delegates = new List<Entry>();
        public List<Entry> delegates = new List<Entry>();
    }
}

namespace UnityEngine.EventSystems
{
    public partial class EventSystem
    {
        public GameObject m_FirstSelected;
        public bool m_sendNavigationEvents = true;
        public int m_DragThreshold = 5;
    }

    public partial class StandaloneInputModule
    {
        public string m_HorizontalAxis = "Horizontal";
        public string m_VerticalAxis = "Vertical";
        public string m_SubmitButton = "Submit";
        public string m_CancelButton = "Cancel";
        public float m_InputActionsPerSecond = 10f;
        public float m_RepeatDelay = 0.5f;
        public bool m_ForceModuleActive;
    }
}
