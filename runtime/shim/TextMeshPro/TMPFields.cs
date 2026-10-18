// Tier UI-1 — fields serializados do TMP_Text/TextMeshProUGUI/TMP_Dropdown
// (nomes m_* p/ FieldApplier) + tipos aninhados do textInfo.
using UnityEngine;

namespace TMPro
{
    public struct VertexGradient
    {
        public Color topLeft;
        public Color topRight;
        public Color bottomLeft;
        public Color bottomRight;
    }

    public struct TMP_Vertex
    {
        public Vector3 position;
        public Vector2 uv;
        public Vector2 uv2;
        public Vector2 uv4;
        public Color32 color;
    }

    public class TMP_TextElement
    {
        public int id;
        public float x;
        public float y;
        public float width;
        public float height;
        public float xOffset;
        public float yOffset;
        public float xAdvance;
        public float scale;
    }

    public struct Extents
    {
        public Vector2 min;
        public Vector2 max;
    }

    public partial struct TMP_CharacterInfo
    {
        public char character;
        public int index;
        public int elementType;
        public TMP_TextElement textElement;
        public TMP_FontAsset fontAsset;
        public Object spriteAsset;
        public int spriteIndex;
        public Material material;
        public int materialReferenceIndex;
        public bool isUsingAlternateTypeface;
        public float pointSize;
        public int lineNumber;
        public int pageNumber;
        public int vertexIndex;
        public TMP_Vertex vertex_TL;
        public TMP_Vertex vertex_BL;
        public TMP_Vertex vertex_TR;
        public TMP_Vertex vertex_BR;
        public Vector3 topLeft;
        public Vector3 bottomLeft;
        public Vector3 topRight;
        public Vector3 bottomRight;
        public float origin;
        public float ascender;
        public float baseLine;
        public float descender;
        public float xAdvance;
        public float aspectRatio;
        public float scale;
        public Color32 color;
        public Color32 underlineColor;
        public Color32 strikethroughColor;
        public Color32 highlightColor;
        public int style;
        public bool isVisible;
    }

    public partial struct TMP_WordInfo
    {
        public TMP_Text textComponent;
        public int firstCharacterIndex;
        public int lastCharacterIndex;
        public int characterCount;
    }

    public partial struct TMP_LinkInfo
    {
        public TMP_Text textComponent;
        public int hashCode;
        public int linkIdFirstCharacterIndex;
        public int linkIdLength;
        public int linkTextfirstCharacterIndex;
        public int linkTextLength;
    }

    public partial struct TMP_LineInfo
    {
        public int characterCount;
        public int visibleCharacterCount;
        public int spaceCount;
        public int wordCount;
        public int firstCharacterIndex;
        public int firstVisibleCharacterIndex;
        public int lastCharacterIndex;
        public int lastVisibleCharacterIndex;
        public float length;
        public float lineHeight;
        public float ascender;
        public float baseline;
        public float descender;
        public float maxAdvance;
        public float width;
        public float marginLeft;
        public float marginRight;
        public int alignment;
        public Extents lineExtents;
    }

    public struct TMP_PageInfo
    {
        public int firstCharacterIndex;
        public int lastCharacterIndex;
        public float ascender;
        public float baseLine;
        public float descender;
    }

    public partial struct TMP_MeshInfo
    {
        public Mesh mesh;
        public int vertexCount;
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector4[] tangents;
        public Vector2[] uvs0;
        public Vector2[] uvs2;
        public Color32[] colors32;
        public int[] triangles;
    }

    public partial class TMP_TextInfo
    {
        public TMP_Text textComponent;
        public int characterCount;
        public int spriteCount;
        public int spaceCount;
        public int wordCount;
        public int linkCount;
        public int lineCount;
        public int pageCount;
        public int materialCount;
        public TMP_CharacterInfo[] characterInfo;
        public TMP_WordInfo[] wordInfo;
        public TMP_LinkInfo[] linkInfo;
        public TMP_LineInfo[] lineInfo;
        public TMP_PageInfo[] pageInfo;
        public TMP_MeshInfo[] meshInfo;
    }

    public partial class TMP_Text
    {
        public Material m_Material;
        public Color m_Color = new Color(1, 1, 1, 1);
        public bool m_RaycastTarget = true;
        public string m_text = "";
        public bool m_isRightToLeft;
        public TMP_FontAsset m_fontAsset;
        public Material m_sharedMaterial;
        public Material[] m_fontSharedMaterials;
        public Material m_fontMaterial;
        public Material[] m_fontMaterials;
        public Color32 m_fontColor32 = new Color32(255, 255, 255, 255);
        public Color m_fontColor = new Color(1, 1, 1, 1);
        public bool m_enableVertexGradient;
        public VertexGradient m_fontColorGradient;
        public Object m_fontColorGradientPreset;
        public Object m_spriteAsset;
        public bool m_tintAllSprites;
        public bool m_overrideHtmlColors;
        public Color32 m_faceColor;
        public Color32 m_outlineColor;
        public float m_fontSize = 36f;
        public float m_fontSizeBase = 36f;
        public int m_fontWeight = 400;
        public bool m_enableAutoSizing;
        public float m_fontSizeMin = 18f;
        public float m_fontSizeMax = 72f;
        public int m_fontStyle;
        public int m_textAlignment = 257;
        public bool m_isAlignmentEnumConverted;
        public float m_characterSpacing;
        public float m_wordSpacing;
        public float m_lineSpacing;
        public float m_lineSpacingMax;
        public float m_paragraphSpacing;
        public float m_charWidthMaxAdj = 0.1f;
        public bool m_enableWordWrapping = true;
        public float m_wordWrappingRatios = 0.4f;
        public int m_overflowMode;
        public int m_firstOverflowCharacterIndex = -1;
        public TMP_Text m_linkedTextComponent;
        public bool m_isLinkedTextComponent;
        public bool m_isTextTruncated;
        public bool m_enableKerning = true;
        public bool m_enableExtraPadding;
        public bool checkPaddingRequired;
        public bool m_isRichText = true;
        public bool m_parseCtrlCharacters = true;
        public bool m_isOrthographic;
        public bool m_isCullingEnabled;
        public bool m_ignoreRectMaskCulling;
        public bool m_ignoreCulling = true;
        public int m_horizontalMapping = 4;
        public int m_verticalMapping = 4;
        public float m_uvLineOffset;
        public int m_geometrySortingOrder;
        public int m_firstVisibleCharacter;
        public bool m_useMaxVisibleDescender;
        public int m_pageToDisplay = 1;
        public Vector4 m_margin;
        public TMP_TextInfo m_textInfo = new TMP_TextInfo();
        public bool m_havePropertiesChanged;
        public bool m_isUsingLegacyAnimationComponent;
        public bool m_isVolumetricText;
        public Object m_spriteAnimator;
        public bool m_isInputParsingRequired;
        public int m_inputSource;

        public string text { get => m_text; set => m_text = value; }
        public float fontSize { get => m_fontSize; set => m_fontSize = value; }
    }

    public partial class TextMeshProUGUI
    {
        public bool m_hasFontAssetChanged;
        public TMP_SubMeshUI[] m_subTextObjects;
        public Material m_baseMaterial;
        public Vector4 m_maskOffset;
    }

    public partial class TMP_Dropdown
    {
        public RectTransform m_Template;
        public TMP_Text m_CaptionText;
        public UnityEngine.UI.Image m_CaptionImage;
        public TMP_Text m_ItemText;
        public UnityEngine.UI.Image m_ItemImage;
        public int m_Value;
        public UnityEngine.UI.OptionDataList m_Options = new UnityEngine.UI.OptionDataList();
        public UnityEngine.Events.UnityEventData m_OnValueChanged;
    }
}
