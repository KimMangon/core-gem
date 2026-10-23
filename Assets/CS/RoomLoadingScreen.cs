using UnityEngine;

/// <summary>Artwork while preparing resources and connecting; hidden once the room is ready.</summary>
public sealed class RoomLoadingScreen : MonoBehaviour
{
    private GameObject screen;
    private UnityEngine.UI.Text status;
    private float loadingStarted;
    private UnityEngine.UI.RawImage artworkImage;
    private UnityEngine.UI.AspectRatioFitter artworkFit;
    private Texture2D[] artworks;

    public void Initialize(Canvas canvas,Font font)
    {
        var root=Rect(canvas.transform,"RoomLoadingScreen",Vector2.zero,Vector2.zero);
        root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
        screen=root.gameObject;
        root.gameObject.AddComponent<UnityEngine.UI.Image>().color=Color.black;
        var picture=Rect(root,"LoadingArtwork",Vector2.zero,Vector2.one);
        var images=new System.Collections.Generic.List<Texture2D>();
        foreach(var path in new[]{"UI/RoomLoadingArtwork","UI/RoomLoadingStage2","UI/RoomLoadingCannon","UI/RoomLoadingBoulder"})
        {
            var texture=Resources.Load<Texture2D>(path);
            if(texture!=null)images.Add(texture);
        }
        artworks=images.ToArray();
        artworkImage=picture.gameObject.AddComponent<UnityEngine.UI.RawImage>();artworkImage.raycastTarget=false;
        artworkFit=picture.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
        artworkFit.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;

        var label=Rect(root,"LoadingStatus",new Vector2(-48,45),new Vector2(310,62));
        label.anchorMin=label.anchorMax=new Vector2(1,0);label.pivot=new Vector2(1,0);
        status=label.gameObject.AddComponent<UnityEngine.UI.Text>();status.font=font;status.fontSize=36;
        // Reserve space for all three dots so the word stays still during the animation.
        status.alignment=TextAnchor.MiddleLeft;status.color=Color.white;status.raycastTarget=false;
        var outline=label.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor=new Color(0,0,0,.85f);outline.effectDistance=new Vector2(2,-2);
        screen.SetActive(false);
    }

    public void SetLoading(bool active,RoomResourcePreloader preloader)
    {
        if(screen==null)return;
        if(screen.activeSelf!=active)
        {
            if(active)
            {
                loadingStarted=Time.unscaledTime;
                if(artworks.Length>0)
                {
                    var artwork=artworks[Random.Range(0,artworks.Length)];
                    artworkImage.texture=artwork;
                    artworkFit.aspectRatio=(float)artwork.width/artwork.height;
                }
            }
            screen.SetActive(active);
        }
        if(!active)return;
        screen.transform.SetAsLastSibling();
        int dots=Mathf.FloorToInt((Time.unscaledTime-loadingStarted)/.5f)%4;
        status.text=(GameLocalization.Korean?"로딩중":"LOADING")+new string('.',dots);
        GameLocalization.Style(status);
    }

    private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);
        rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }

    private void OnDestroy() {if(screen!=null)Destroy(screen);}
}
