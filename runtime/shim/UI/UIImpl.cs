// Tier UI-1 — uGUI + EventSystem: props de comportamento sobre os fields m_*
// (UIFields.cs). Seleção do menu funciona; render/clicks vêm nas fatias.
using System.Collections.Generic;

namespace UnityEngine.EventSystems
{
    public partial class EventSystem
    {
        static readonly List<EventSystem> all = new List<EventSystem>();
        public EventSystem() { all.Add(this); }
        static EventSystem First()
        {
            all.RemoveAll(e => e == null);
            return all.Count > 0 ? all[0] : null;
        }
        public static EventSystem current => First();
        public GameObject currentSelectedGameObject { get; private set; }
        public GameObject firstSelectedGameObject
        {
            get => m_FirstSelected;
            set => m_FirstSelected = value;
        }
        public void SetSelectedGameObject(GameObject go) => currentSelectedGameObject = go;
    }
}

namespace UnityEngine.UI
{
    public partial class Graphic
    {
        public Color color { get => m_Color; set => m_Color = value; }
    }

    public partial class Image
    {
        public Sprite sprite { get => m_Sprite; set => m_Sprite = value; }
    }

    public partial class Text
    {
        public string text { get => m_Text; set => m_Text = value; }
        public Font font { get => m_FontData.m_Font; set => m_FontData.m_Font = value; }
        public int fontSize { get => m_FontData.m_FontSize; set => m_FontData.m_FontSize = value; }
        public TextAnchor alignment
        {
            get => (TextAnchor)m_FontData.m_Alignment;
            set => m_FontData.m_Alignment = (int)value;
        }
    }

    public partial class Selectable
    {
        public bool interactable { get => m_Interactable; set => m_Interactable = value; }
    }

    public partial class Dropdown
    {
        public int value { get => m_Value; set => m_Value = value; }
        public void ClearOptions()
        {
            if (m_Options != null) m_Options.m_Options.Clear();
        }
        public void AddOptions(List<string> options)
        {
            if (m_Options == null) m_Options = new OptionDataList();
            foreach (var o in options) m_Options.m_Options.Add(new OptionData { m_Text = o });
        }
        public void RefreshShownValue() { }
    }

    public partial class Toggle
    {
        public bool isOn { get => m_IsOn; set => m_IsOn = value; }
    }

    public partial class Slider
    {
        public float value { get => m_Value; set => m_Value = value; }
    }

    public partial class Scrollbar
    {
        public float value { get => m_Value; set => m_Value = value; }
    }

    public partial class ScrollRect
    {
        public RectTransform content { get => m_Content; set => m_Content = value; }
    }
}
