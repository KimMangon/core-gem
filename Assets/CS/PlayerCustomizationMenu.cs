using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>Local wardrobe with an isolated, live 3D preview.</summary>
public sealed class PlayerCustomizationMenu : MonoBehaviour
{
    private GameObject mainMenu, lobby, overlay, studio;
    private Camera previewCamera;
    private RenderTexture texture;
    private Transform avatar;
    private PlayerCosmeticVisual cosmetics;
    private Material bodyMaterial, eyeMaterial, pupilMaterial;
    private Font font;
    private readonly UnityEngine.UI.Text[] itemLabels = new UnityEngine.UI.Text[2];
    private readonly UnityEngine.UI.Text[] categoryLabels = new UnityEngine.UI.Text[2];
    private readonly UnityEngine.UI.Text[] colorButtons = new UnityEngine.UI.Text[2];
    private GameObject colorDialog;
    private UnityEngine.UI.Text paletteTitle,paletteDone,paletteReset,paletteHint;
    private UnityEngine.UI.Image colorSample;
    private UnityEngine.UI.InputField hexInput;
    private Texture2D saturationTexture,hueTexture;
    private RectTransform svMarker,hueMarker;
    private int editingCategory;
    private float hue,saturation=1,value=1;
    private bool syncPending;
    private float syncAfter;
    private UnityEngine.UI.Text title, closeLabel, resetLabel, hint, mainLabel, lobbyLabel;

    public void Initialize(GameObject menu, GameObject waitingRoom, Font uiFont)
    {
        mainMenu=menu; lobby=waitingRoom; font=uiFont;
        var canvas=menu.GetComponentInParent<Canvas>();
        if(canvas==null) return;
        var mainButton=Button(menu.transform,"CustomizeButton",new Vector2(175,-130),new Vector2(290,66),Open);
        mainButton.GetComponent<RectTransform>().anchorMin=mainButton.GetComponent<RectTransform>().anchorMax=new Vector2(0,.5f);
        mainLabel=mainButton.GetComponentInChildren<UnityEngine.UI.Text>();
        if(lobby!=null)
        {
            var button=Button(lobby.transform,"LobbyCustomizeButton",new Vector2(0,365),new Vector2(330,60),Open);
            lobbyLabel=button.GetComponentInChildren<UnityEngine.UI.Text>();
        }
        var root=Rect(canvas.transform,"CustomizationOverlay",Vector2.zero,Vector2.zero);
        root.anchorMin=Vector2.zero; root.anchorMax=Vector2.one; root.offsetMin=root.offsetMax=Vector2.zero;
        root.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.025f,.03f,.04f,.98f);
        overlay=root.gameObject;
        var card=Rect(root,"Wardrobe",Vector2.zero,new Vector2(1120,820));
        title=Text(card,"Title",new Vector2(0,350),new Vector2(1000,70),42);
        var view=Rect(card,"AvatarPreview",new Vector2(-260,15),new Vector2(470,550));
        var raw=view.gameObject.AddComponent<UnityEngine.UI.RawImage>(); raw.raycastTarget=true;
        var drag=view.gameObject.AddComponent<CustomizationPreviewDrag>();
        for(int i=0;i<2;i++)
        {
            int category=i;
            float y=i==1?180:-40;
            categoryLabels[i]=Text(card,"Category"+i,new Vector2(270,y),new Vector2(260,60),34);
            Button(card,"PreviousItem"+i,new Vector2(85,y),new Vector2(76,66),()=>Cycle(category,-1)).GetComponentInChildren<UnityEngine.UI.Text>().text="<";
            Button(card,"NextItem"+i,new Vector2(455,y),new Vector2(76,66),()=>Cycle(category,1)).GetComponentInChildren<UnityEngine.UI.Text>().text=">";
            itemLabels[i]=Text(card,"SelectedItem"+i,new Vector2(270,y-75),new Vector2(440,60),29);
            colorButtons[i]=Button(card,"CustomizeColor"+i,new Vector2(270,y-145),new Vector2(330,58),()=>OpenPalette(category)).GetComponentInChildren<UnityEngine.UI.Text>();
        }
        resetLabel=Button(card,"RemoveAll",new Vector2(270,-295),new Vector2(460,55),()=>{PlayerCosmetics.Selected=0;Apply();}).GetComponentInChildren<UnityEngine.UI.Text>();
        hint=Text(card,"SavedHint",new Vector2(0,-360),new Vector2(1080,40),23);
        closeLabel=Button(card,"Done",new Vector2(450,350),new Vector2(180,60),Close).GetComponentInChildren<UnityEngine.UI.Text>();
        BuildPreview();raw.texture=texture;drag.Target=avatar;
        BuildPalette();
        GameLocalization.Changed+=Refresh;
        overlay.SetActive(false);studio.SetActive(false);Refresh();
    }

    private void Open()
    {
        overlay.SetActive(true);overlay.transform.SetAsLastSibling();studio.SetActive(true);
        avatar.localRotation=Quaternion.Euler(0,-12,0);Refresh();
    }

    private void Close()
    {
        colorDialog.SetActive(false);
        overlay.SetActive(false);studio.SetActive(false);
    }

    private void Cycle(int category,int direction) { PlayerCosmetics.Cycle(category,direction);Apply(); }

    private void BuildPalette()
    {
        var root=Rect(overlay.transform,"ColorPaletteOverlay",Vector2.zero,Vector2.zero);
        root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
        root.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.35f);
        colorDialog=root.gameObject;
        var card=Rect(root,"PaletteCard",new Vector2(270,0),new Vector2(560,740));
        card.gameObject.AddComponent<UnityEngine.UI.Image>().color=new Color(.09f,.11f,.14f,.99f);
        paletteTitle=Text(card,"PaletteTitle",new Vector2(0,320),new Vector2(520,60),29);
        paletteHint=Text(card,"PaletteHint",new Vector2(0,255),new Vector2(500,60),19);
        saturationTexture=new Texture2D(128,128,TextureFormat.RGB24,false) {name="Saturation brightness palette",wrapMode=TextureWrapMode.Clamp};
        var square=Rect(card,"SaturationBrightness",new Vector2(0,65),new Vector2(360,280));
        square.gameObject.AddComponent<UnityEngine.UI.RawImage>().texture=saturationTexture;
        square.gameObject.AddComponent<ColorPaletteDrag>().Changed=point=>{saturation=point.x;value=point.y;PickPaletteColor();};
        svMarker=Rect(square,"Selection",Vector2.zero,new Vector2(12,12));
        var dot=svMarker.gameObject.AddComponent<UnityEngine.UI.Image>();dot.color=Color.white;dot.raycastTarget=false;
        var edge=svMarker.gameObject.AddComponent<UnityEngine.UI.Outline>();edge.effectColor=Color.black;edge.effectDistance=new Vector2(2,-2);
        hueTexture=new Texture2D(256,1,TextureFormat.RGB24,false) {name="Hue spectrum",wrapMode=TextureWrapMode.Clamp};
        for(int x=0;x<256;x++)hueTexture.SetPixel(x,0,Color.HSVToRGB(x/255f,1,1));hueTexture.Apply();
        var strip=Rect(card,"Hue",new Vector2(0,-110),new Vector2(360,32));
        strip.gameObject.AddComponent<UnityEngine.UI.RawImage>().texture=hueTexture;
        strip.gameObject.AddComponent<ColorPaletteDrag>().Changed=point=>{hue=point.x;RedrawSaturation();PickPaletteColor();};
        hueMarker=Rect(strip,"HueSelection",Vector2.zero,new Vector2(5,40));
        var hueDot=hueMarker.gameObject.AddComponent<UnityEngine.UI.Image>();hueDot.raycastTarget=false;hueDot.color=Color.white;
        var hueEdge=hueMarker.gameObject.AddComponent<UnityEngine.UI.Outline>();hueEdge.effectColor=Color.black;hueEdge.effectDistance=new Vector2(1,-1);
        colorSample=Rect(card,"SelectedColor",new Vector2(-135,-180),new Vector2(80,56)).gameObject.AddComponent<UnityEngine.UI.Image>();
        var inputRect=Rect(card,"HexColor",new Vector2(45,-180),new Vector2(230,56));
        var inputBackground=inputRect.gameObject.AddComponent<UnityEngine.UI.Image>();inputBackground.color=Color.white;
        hexInput=inputRect.gameObject.AddComponent<UnityEngine.UI.InputField>();hexInput.targetGraphic=inputBackground;
        var inputText=Text(inputRect,"HexText",Vector2.zero,new Vector2(210,46),26);inputText.color=Color.black;
        hexInput.textComponent=inputText;hexInput.characterLimit=7;hexInput.lineType=UnityEngine.UI.InputField.LineType.SingleLine;
        hexInput.onEndEdit.AddListener(text=>
        {
            if(ColorUtility.TryParseHtmlString(text.StartsWith("#")?text:"#"+text,out var parsed))
            {Color.RGBToHSV(parsed,out hue,out saturation,out value);RedrawSaturation();PickPaletteColor();}
            else RefreshPaletteSample();
        });
        paletteReset=Button(card,"OriginalColor",new Vector2(-125,-280),new Vector2(220,66),()=>
        {
            PlayerCosmetics.SetCustomColor(editingCategory,0);PlayerCosmetics.SetColor(editingCategory,0);
            Apply();OpenPalette(editingCategory);
        }).GetComponentInChildren<UnityEngine.UI.Text>();
        paletteDone=Button(card,"PaletteDone",new Vector2(125,-280),new Vector2(220,66),()=>colorDialog.SetActive(false)).GetComponentInChildren<UnityEngine.UI.Text>();
        colorDialog.SetActive(false);
    }

    private void OpenPalette(int category)
    {
        editingCategory=category;
        uint packed=PlayerCosmetics.CustomColor(category);
        int oldColor=PlayerCosmetics.GetColor(PlayerCosmetics.Selected,category);
        Color color=packed!=0?PlayerColorPalette.Unpack(packed):oldColor!=0?PlayerCosmetics.Tint(oldColor):cosmetics.OriginalColor(PlayerCosmetics.Selected,category);
        Color.RGBToHSV(color,out hue,out saturation,out value);
        colorDialog.SetActive(true);colorDialog.transform.SetAsLastSibling();RedrawSaturation();RefreshPaletteSample();
        Refresh();
    }

    private void RedrawSaturation()
    {
        var pixels=new Color[128*128];
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)pixels[y*128+x]=Color.HSVToRGB(hue,x/127f,y/127f);
        saturationTexture.SetPixels(pixels);saturationTexture.Apply();
    }

    private void PickPaletteColor()
    {
        Color32 color=Color.HSVToRGB(hue,saturation,value);color.a=255;
        PlayerCosmetics.SetCustomColor(editingCategory,PlayerColorPalette.Pack(color));
        RefreshPaletteSample();Apply();
    }

    private void RefreshPaletteSample()
    {
        Color color=Color.HSVToRGB(hue,saturation,value);
        colorSample.color=color;hexInput.SetTextWithoutNotify("#"+ColorUtility.ToHtmlStringRGB(color));
        svMarker.anchoredPosition=new Vector2((saturation-.5f)*360,(value-.5f)*280);
        hueMarker.anchoredPosition=new Vector2((hue-.5f)*360,0);
    }

    private void Apply()
    {
        Refresh();
        syncPending=true;syncAfter=Time.unscaledTime+.2f;
    }

    private void SyncCosmetics()
    {
        syncPending=false;
        var network=NetworkManager.Singleton;
        if(network!=null && network.IsClient && network.LocalClient?.PlayerObject!=null)
            network.LocalClient.PlayerObject.GetComponent<PlayerMovement>()?.RefreshOwnerCosmetics();
    }

    private void Refresh()
    {
        if(title==null) return;
        bool ko=GameLocalization.Korean;
        title.text=ko?"캐릭터 꾸미기":"CUSTOMIZE";
        mainLabel.text=ko?"캐릭터 꾸미기":"CUSTOMIZE";
        if(lobbyLabel!=null) lobbyLabel.text=mainLabel.text;
        closeLabel.text=ko?"완료":"DONE";
        resetLabel.text=ko?"모두 해제":"REMOVE ALL";
        hint.text=ko?"캐릭터를 드래그해 회전 · 선택한 모습은 자동 저장됩니다":"DRAG THE CHARACTER TO ROTATE  /  YOUR LOOK IS SAVED AUTOMATICALLY";
        for(int i=0;i<2;i++)
        {
            categoryLabels[i].text=i==0?(ko?"몸통":"BODY"):(ko?"머리":"HEAD");
            int item=PlayerCosmetics.GetItem(PlayerCosmetics.Selected,i);
            itemLabels[i].text=(ko?PlayerCosmetics.Korean:PlayerCosmetics.English)[i][item];
            itemLabels[i].resizeTextForBestFit=true;itemLabels[i].resizeTextMinSize=20;itemLabels[i].resizeTextMaxSize=29;
            colorButtons[i].text=ko?"색상 변경":"CUSTOMIZE COLOR";
        }
        paletteTitle.text=ko?"장식 색상 팔레트":"ACCESSORY COLOR PALETTE";
        paletteDone.text=ko?"완료":"DONE";paletteReset.text=ko?"기본 색":"ORIGINAL";
        paletteHint.text=ko?"아래 띠로 색상 선택 · 사각형에서 채도와 밝기 조절":"CHOOSE HUE BELOW, THEN SATURATION AND BRIGHTNESS IN THE SQUARE";
        cosmetics.Apply(PlayerCosmetics.Selected,PlayerCosmetics.CustomColor(0),PlayerCosmetics.CustomColor(1));
        bodyMaterial.color=PlayerColorPalette.Colors[PlayerColorPalette.SelectedIndex];
        foreach(var label in overlay.GetComponentsInChildren<UnityEngine.UI.Text>(true)) GameLocalization.Style(label);
        GameLocalization.Style(mainLabel);if(lobbyLabel!=null)GameLocalization.Style(lobbyLabel);
        foreach(var button in overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            FitButtonLabel(button.GetComponentInChildren<UnityEngine.UI.Text>(true));
        FitButtonLabel(mainLabel);FitButtonLabel(lobbyLabel);
        foreach(var label in colorButtons)FitColorButton(label);
    }

    private static void FitColorButton(UnityEngine.UI.Text label)
    {
        if(label==null)return;
        label.resizeTextForBestFit=false;
        label.fontSize=28;
        var button=label.GetComponentInParent<UnityEngine.UI.Button>(true);
        if(button==null)return;
        var rect=button.GetComponent<RectTransform>();
        rect.sizeDelta=new Vector2(Mathf.Ceil(label.preferredWidth)+64f,58f);
        label.horizontalOverflow=HorizontalWrapMode.Wrap;
        label.verticalOverflow=VerticalWrapMode.Truncate;
        label.resizeTextMinSize=28;label.resizeTextMaxSize=28;
    }

    private static void FitButtonLabel(UnityEngine.UI.Text label)
    {
        if(label==null) return;
        var button=label.GetComponentInParent<UnityEngine.UI.Button>(true);
        if(button==null) return;
        Vector2 size=button.GetComponent<RectTransform>().rect.size;
        label.resizeTextForBestFit=false;
        label.fontSize=100;
        float width=Mathf.Max(1f,label.preferredWidth);
        var settings=label.GetGenerationSettings(Vector2.zero);
        settings.horizontalOverflow=HorizontalWrapMode.Overflow;
        settings.verticalOverflow=VerticalWrapMode.Overflow;
        float height=Mathf.Max(1f,label.cachedTextGeneratorForLayout.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit);
        // Use the same visual margin for short labels, arrows and translated labels.
        int fitted=Mathf.Max(1,Mathf.FloorToInt(100f*Mathf.Min(size.x*.76f/width,size.y*.64f/height)));
        label.fontSize=fitted;
        label.horizontalOverflow=HorizontalWrapMode.Wrap;
        label.verticalOverflow=VerticalWrapMode.Truncate;
        label.resizeTextMinSize=1;label.resizeTextMaxSize=fitted;label.resizeTextForBestFit=true;
    }

    private void Update()
    {
        if(syncPending && Time.unscaledTime>=syncAfter)SyncCosmetics();
        if(overlay==null || !overlay.activeSelf) return;
        if(!mainMenu.activeInHierarchy && (lobby==null || !lobby.activeInHierarchy))Close();
        else if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if(colorDialog.activeSelf)colorDialog.SetActive(false);else Close();
        }
    }

    private void BuildPreview()
    {
        // A remote studio prevents this camera and its lighting from affecting the course.
        studio=new GameObject("CustomizationPreviewStudio");
        studio.transform.SetParent(transform,false);studio.transform.position=new Vector3(0,-10000,0);
        avatar=new GameObject("PreviewAvatar").transform;avatar.SetParent(studio.transform,false);
        bodyMaterial=MakeMaterial(PlayerColorPalette.Colors[PlayerColorPalette.SelectedIndex]);
        eyeMaterial=MakeMaterial(Color.white);pupilMaterial=MakeMaterial(Color.black);
        PreviewPart("Body",PrimitiveType.Capsule,avatar,Vector3.zero,Vector3.one,bodyMaterial);
        PreviewPart("Hand",PrimitiveType.Cube,avatar,new Vector3(.66f,0,.15f),Vector3.one*.2f,bodyMaterial);
        for(int side=-1;side<=1;side+=2)
        {
            PreviewPart("Eye",PrimitiveType.Sphere,avatar,new Vector3(side*.18f,.665f,.435f),new Vector3(.31f,.33f,.25f),eyeMaterial);
            PreviewPart("Pupil",PrimitiveType.Sphere,avatar,new Vector3(side*.18f,.665f,.56f),new Vector3(.105f,.112f,.06f),pupilMaterial);
        }
        var wardrobe=new GameObject("Accessories");wardrobe.transform.SetParent(avatar,false);
        cosmetics=wardrobe.AddComponent<PlayerCosmeticVisual>();cosmetics.Build();
        foreach(var child in studio.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
        var cameraObject=new GameObject("PreviewCamera",typeof(Camera));cameraObject.transform.SetParent(studio.transform,false);
        previewCamera=cameraObject.GetComponent<Camera>();previewCamera.orthographic=true;previewCamera.orthographicSize=1.65f;
        previewCamera.transform.localPosition=new Vector3(0,.2f,5);previewCamera.transform.localRotation=Quaternion.Euler(0,180,0);
        previewCamera.cullingMask=1<<31;previewCamera.nearClipPlane=.1f;previewCamera.farClipPlane=10;
        previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.10f,.13f,.17f);
        previewCamera.allowHDR=false;previewCamera.allowMSAA=false;
        texture=new RenderTexture(512,600,24) { name="Customization preview" };texture.Create();previewCamera.targetTexture=texture;
        var lightObject=new GameObject("PreviewLight",typeof(Light));lightObject.transform.SetParent(studio.transform,false);
        var light=lightObject.GetComponent<Light>();light.type=LightType.Point;light.range=9;light.intensity=5;
        light.cullingMask=1<<31;light.shadows=LightShadows.None;lightObject.transform.localPosition=new Vector3(-2,3,3);
    }

    private static Material MakeMaterial(Color color)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard"));
        material.color=color;return material;
    }

    private static void PreviewPart(string name,PrimitiveType type,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=position;go.transform.localScale=scale;
        var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        go.GetComponent<MeshRenderer>().sharedMaterial=material;
    }

    private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
        rect.sizeDelta=size;rect.anchoredPosition=position;return rect;
    }

    private UnityEngine.UI.Text Text(Transform parent,string name,Vector2 position,Vector2 size,int fontSize)
    {
        var label=Rect(parent,name,position,size).gameObject.AddComponent<UnityEngine.UI.Text>();
        label.font=font;label.fontSize=fontSize;label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;label.raycastTarget=false;
        return label;
    }

    private UnityEngine.UI.Button Button(Transform parent,string name,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var template=mainMenu.transform.Find("Host Button");
        var go=Instantiate(template.gameObject,parent,false);go.name=name;
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
        rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;
        var button=go.GetComponent<UnityEngine.UI.Button>();button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();button.onClick.AddListener(action);
        var label=go.GetComponentInChildren<UnityEngine.UI.Text>(true);
        foreach(var localized in go.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true))
        {
            localized.OnUpdateString.RemoveAllListeners();localized.enabled=false;Destroy(localized);
        }
        var labelRect=label.rectTransform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;
        labelRect.offsetMin=new Vector2(10,5);labelRect.offsetMax=new Vector2(-10,-5);labelRect.localScale=Vector3.one;
        label.text="";label.font=font;label.fontSize=28;label.alignment=TextAnchor.MiddleCenter;label.color=new Color(.18f,.18f,.18f);
        label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
        label.resizeTextForBestFit=true;label.resizeTextMinSize=18;label.resizeTextMaxSize=28;
        go.SetActive(true);
        return button;
    }

    private void OnDestroy()
    {
        GameLocalization.Changed-=Refresh;
        if(previewCamera!=null)previewCamera.targetTexture=null;
        if(texture!=null) { texture.Release();Destroy(texture); }
        Destroy(studio);Destroy(overlay);Destroy(bodyMaterial);Destroy(eyeMaterial);Destroy(pupilMaterial);
        Destroy(saturationTexture);Destroy(hueTexture);
    }
}
