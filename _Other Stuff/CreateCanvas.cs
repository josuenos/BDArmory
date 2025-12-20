using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateCanvas : MonoBehaviour
{
    public bool addCanvas = false;
    private bool addedCanvas = false;

    private GameObject canvasObject;
    private GridLayoutGroup grid;
    private GameObject groupObject;
    private GameObject resourcesPanel;
    private List<TextMeshProUGUI> textObjects;

    private static Material textMat;

    public Sprite backgroundSprite;
    public float panelSpacing = 7f;
    public float cornerPadding = 12f;

    public float resourceMultiplier = 0.01f;

    //public struct Text
    //{
    //    public string text;
    //    public Color colour;
    //    public TextAlignmentOptions alignment;
    //    public int fontSize;
    //}

    // Ship info.

    public string competitionText = "Namae Y-21 24-G was shot down by Nagai X-12 42-F";
    public string vesselName = "Nagai X-12 42-F";
    public float altitude = 15000;
    public Vector3 velocity = new Vector3(0,1200f,0);
    public string action = "is shooting at Namae Y-21 24-G";
    public Resource[] resources = new Resource[6];
    public Color teamColour = Desaturate(Color.cyan, 0.5f);

    public static Color Desaturate(Color colour, float sat)
    {
        Color.RGBToHSV(colour, out float h, out float s, out float v);
        return Color.HSVToRGB(h, sat * s, v);
    }

    public struct Resource
    {
        public string name;
        public float amount;
        public float maxAmount;

        public static Color full = Color.white;
        public static Color good = Desaturate(Color.green, 0.7f);
        public static Color half = Desaturate(Color.yellow, 0.7f);
        public static Color low = Desaturate(Color.red, 0.7f);
        public static Color empty = Color.grey;

        public float Percentage
        {
            get { return amount / maxAmount * 100; }
        }

        public string Text
        {
            get {
                return $"{name}: <color=#{ColorUtility.ToHtmlStringRGB(Colour)}>{amount:N0}/{maxAmount:N0}</color>"; 
            }
        }

        public string Amount
        {
            get
            {
                return $"<color=#{ColorUtility.ToHtmlStringRGB(Colour)}>{amount:N0}/{maxAmount:N0}</color>";
            }
        }

        public Color Colour
        {
            get
            {
                switch (Percentage)
                {
                    case float n when (n >= 100):
                        return full;
                    case float n when (n >= 50):
                        return good;
                    case float n when (n >= 25):
                        return half;
                    case float n when (n > 0):
                        return low;
                    default:
                        return empty;
                }
            }
        }

        public Resource(string name, float amount, float maxAmount)
        {
            this.name = name;
            this.amount = amount;
            this.maxAmount = maxAmount;
        }
    }



    //private Vector2 _gridSize = new Vector2(200, 20);
    //public Vector2 GridSize
    //{
    //    get { return _gridSize; }
    //    set
    //    {
    //        _gridSize = value;
    //        grid.cellSize = _gridSize;
    //    }
    //}

    // Start is called before the first frame update
    void Start()
    {
        textMat = new Material(Shader.Find("TextMeshPro/Distance Field"));
        textMat.EnableKeyword("UNDERLAY_ON");
        textMat.SetFloat("_UnderlaySoftness", 0.15f);
        textMat.SetFloat("_UnderlayOffsetX", 1);
        textMat.SetFloat("_UnderlayOffsetY", -1);

        resources[0] = new Resource("CMFlare", 252, 264);
        resources[1] = new Resource("Electric Charge", 200, 200);
        resources[2] = new Resource("CMChaff", 132, 162);
        resources[3] = new Resource("25x137 Ammo", 500, 625);
        resources[4] = new Resource("Intake Atm", 5.7f, 5.7f);
        resources[5] = new Resource("Liquid Fuel", 193, 270);
    }

    // Update is called once per frame
    void Update()
    {
        if (addCanvas != addedCanvas)
        {
            if (addCanvas)
                AddTestCanvas(Camera.main);
            else
                Destroy(canvasObject);

            addedCanvas = addCanvas;
        }

        if (addedCanvas)
        {
            // Update the canvas.
            //UpdateValues();

            if (resources.Length != textObjects.Count / 2)
                RefreshResources();

            UpdateResources();
        }

        for (int i = 0; i < resources.Length; i++)
        {
            resources[i].amount -= Time.deltaTime * resourceMultiplier * (resources[i].maxAmount / 10);
            resources[i].amount = Mathf.Clamp(resources[i].amount, 0, resources[i].maxAmount);
        }
    }

    //public void UpdateValues()
    //{
    //    groupObject.GetComponent<VerticalLayoutGroup>().spacing = panelSpacing;
    //    groupObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(cornerPadding, -cornerPadding);
    //}

    public void AddTestCanvas(Camera camera)
    {
        // Add a canvas to the individual camera.
        canvasObject = new GameObject("Capture Tools BD Canvas", typeof(Canvas), typeof(CanvasScaler));

        // Set the canvas to render to the camera.
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        canvas.transform.SetParent(camera.transform, false);

        // Scale canvas with screen size.
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        // Create the vessel group.
        var vesselGroup = CreateGroup("Vessel", canvasObject, Vector2.up, 
            new Vector2(cornerPadding, -cornerPadding), TextAnchor.UpperLeft, Vector2.up);

        // Create the pilot panel.
        var pilotPanel = CreatePanel(vesselGroup);
        var pilotLayout = pilotPanel.GetComponent<VerticalLayoutGroup>();
        pilotLayout.childAlignment = TextAnchor.MiddleCenter;

        var vesselNameT = CreateText(pilotPanel, vesselName, teamColour, 20);
        vesselNameT.overflowMode = TextOverflowModes.Ellipsis;
        vesselNameT.alignment = TextAlignmentOptions.Center;

        var actionText = CreateText(pilotPanel, action, Color.white, 14);
        actionText.overflowMode = TextOverflowModes.Ellipsis;
        actionText.alignment = TextAlignmentOptions.Center;

        // Create the stats panel.
        var statsPanel = CreatePanel(vesselGroup);

        CreateText(statsPanel, $"Speed: {velocity.magnitude:N0} m/s", Color.white, 14);
        CreateText(statsPanel, $"Altitude: {altitude:N0} m", Color.white, 14);


        // Create the group situated in the upper right.
        var compGroup = CreateGroup("Competition", canvasObject, Vector2.one, 
            new Vector2(-cornerPadding, -cornerPadding), TextAnchor.UpperRight, Vector2.up);

        // Create the competition panel.
        var compPanel = CreatePanel(compGroup);

        // Create comp text.
        var compText = CreateText(compPanel, competitionText, Color.white, 16);
        compText.overflowMode = TextOverflowModes.Overflow;
        compText.alignment = TextAlignmentOptions.Center;


        // Create the resources panel.
        var resourcesGroup = CreateGroup("Resources", canvasObject, Vector2.zero,
                       new Vector2(cornerPadding, cornerPadding), TextAnchor.LowerLeft, Vector2.zero);

        var resFitter = resourcesGroup.AddComponent<ContentSizeFitter>();
        resFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        resFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        resourcesPanel = CreatePanel(resourcesGroup);
        //var resRect = resourcesPanel.GetComponent<RectTransform>();
        //resRect.anchorMin = Vector2.zero;
        //resRect.anchorMax = Vector2.zero;
        //resRect.pivot = Vector2.zero;
        //resRect.anchoredPosition = new Vector2(cornerPadding, cornerPadding);

        textObjects = new List<TextMeshProUGUI>();
        RefreshResources();
    }

    public void RefreshResources()
    {
        // Destroy all children.
        //foreach (var child in textObjects)
        //    Destroy(child.gameObject);

        // Destroy all children of the resources panel.
        foreach (Transform child in resourcesPanel.transform)
            Destroy(child.gameObject);

        textObjects.Clear();

        var horizontalGroup = CreateLayoutGroup("Horizontal", false, resourcesPanel, TextAnchor.MiddleLeft, false, 10);
        var nameColumn = CreateLayoutGroup("Name", true, horizontalGroup, TextAnchor.MiddleRight, false, 10);
        var amountColumn = CreateLayoutGroup("Amount", true, horizontalGroup, TextAnchor.MiddleLeft, false, 10);

        // Create the resources.
        foreach (var resource in resources)
        {
            //var text = CreateText(resourcesPanel, resource.text, resource.colour, 14);
            var text = CreateText(nameColumn, resource.name, Color.white, 14);
            text.alignment = TextAlignmentOptions.Right;
            text.gameObject.name = "Name";
            text.overflowMode = TextOverflowModes.Overflow;

            var amount = CreateText(amountColumn, resource.Amount, Color.white, 14);
            amount.gameObject.name = resource.name;
            amount.alignment = TextAlignmentOptions.Left;
            amount.overflowMode = TextOverflowModes.Overflow;

            textObjects.Add(text);
            textObjects.Add(amount);
        }
    }

    public static GameObject CreateLayoutGroup(string name, bool vertical, GameObject parent, TextAnchor alignment, bool controlChildren, float spacing = 0)
    {
        var go = new GameObject(name, typeof(RectTransform), vertical ? typeof(VerticalLayoutGroup) : typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent.transform, false);

        var layout = go.GetComponent(vertical ? "VerticalLayoutGroup" : "HorizontalLayoutGroup") as HorizontalOrVerticalLayoutGroup;
        layout.childAlignment = alignment;

        if (!controlChildren)
        {
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
        }

        layout.spacing = spacing;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return go;
    }

    void UpdateResources()
    {
        // Update resource text and colours.

        foreach (var child in textObjects)
        {
            var resource = resources.ToList().Find(r => r.name == child.gameObject.name);

            if (resource.name == null)
                continue;

            child.text = resource.Amount;
            //text.color = resource.colour;
        }
    }

    public GameObject CreateGroup(string name, GameObject parent, Vector2 anchor, Vector2 padding, TextAnchor alignment, Vector2 pivot)
    {
        // Create the group situated in the upper right.
        var groupObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        var groupRect = groupObject.GetComponent<RectTransform>();
        groupRect.SetParent(parent.transform, false);

        groupRect.anchorMin = anchor;
        groupRect.anchorMax = anchor;

        groupRect.pivot = pivot;
        groupRect.anchoredPosition = padding;

        groupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 0);
        groupRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 0);

        // Group spacing.
        var group = groupObject.GetComponent<VerticalLayoutGroup>();
        group.spacing = panelSpacing;
        group.childAlignment = alignment;

        return groupObject;
    }

    private GameObject CreatePanel(GameObject groupObject)
    {
        // Create a panel
        var panelObject = new GameObject("Panel", typeof(Image), typeof(CanvasGroup));
        panelObject.transform.SetParent(groupObject.transform, false);
        var panel = panelObject.GetComponent<Image>();
        panel.color = new Color(0, 0, 0, 0.3f);
        //panel.rectTransform.pivot = new Vector2(0, 1);

        //rect_round_down_dark_transparent
        //var sprite1 = Resources.GetBuiltinResource<Sprite>("unity_builtin_extra/Background");
        //var sprite2 = FindObjectsOfType<Sprite>().ToList().Find(x => x.name == "Background");

        var image = panelObject.GetComponent<Image>();
        image.sprite = backgroundSprite;
        image.type = Image.Type.Sliced;

        // Grid layout group.
        //grid = panelObject.AddComponent<GridLayoutGroup>();
        //grid.cellSize = new Vector2(340, 20);
        //grid.padding = new RectOffset(20, 20, 12, 12);

        // Vertical layout group.
        var vertical = panelObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(20, 20, 12, 12);
        vertical.childControlHeight = false;
        vertical.childControlWidth = false;
        vertical.childForceExpandHeight = false;
        vertical.childForceExpandWidth = false;
        vertical.spacing = 4f;

        // Fitter.
        var fitter = panelObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return panelObject;
    }

    private TextMeshProUGUI CreateText(GameObject panel, string text, Color colour, int size = 14)
    {
        var textObject = new GameObject("Text", typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);

        var textM = textObject.GetComponent<TextMeshProUGUI>();
        textM.fontSize = size;
        textM.alignment = TextAlignmentOptions.Left;
        textM.color = colour;
        textM.text = text;
        textM.overflowMode = TextOverflowModes.Ellipsis;
        textM.material = textMat;
        textM.richText = true;

        textM.rectTransform.sizeDelta = new Vector2(textM.preferredWidth, textM.preferredHeight);
        return textM;
    }
}
