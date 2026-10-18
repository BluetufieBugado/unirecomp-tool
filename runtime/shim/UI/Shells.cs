// GERADO por tools/genshells.py a partir de docs/apicheck_stardust.json — nao editar a mao.
// Cascas para o assembly do jogo carregar (Fase 1). Membros reais vêm nos Tiers 1-2.
using UnityEngine;

namespace UnityEngine.EventSystems
{
    public partial class EventSystem : Behaviour { }
    public partial interface IEventSystemHandler { }
    public partial interface IPointerClickHandler : IEventSystemHandler { }
    public partial interface IPointerEnterHandler : IEventSystemHandler { }
    public partial interface IPointerExitHandler : IEventSystemHandler { }
    public partial interface IPointerUpHandler : IEventSystemHandler { }
    public partial class PointerEventData { }
    public partial class StandaloneInputModule : Behaviour { }
}

namespace UnityEngine.UI
{
    public partial class Button : Selectable { }
    public partial class CanvasScaler : Behaviour { }
    public partial class Dropdown : Selectable { }
    public partial class EventTrigger : Behaviour { }
    public partial class Graphic : Behaviour { }
    public partial class GraphicRaycaster : Behaviour { }
    public partial class Image : Graphic { }
    public partial class Mask : Behaviour { }
    public partial class Outline : Behaviour { }
    public partial class ScrollRect : Behaviour { }
    public partial class Scrollbar : Selectable { }
    public partial class Shadow : Behaviour { }
    public partial class Slider : Selectable { }
    public partial class Text : Graphic { }
    public partial class Toggle : Selectable { }
    public partial class Selectable : Behaviour { }
    public partial class ToggleGroup : Behaviour { }
}
