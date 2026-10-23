using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Stage8PictogramSetup
{
    private static readonly Color Ink = new Color(.96f,.97f,1);
    private static readonly Color Red = new Color(1,.12f,.08f);
    private static readonly Color Yellow = new Color(1,.85f,.08f);
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.InvalidOperationException("Exit play mode first.");
        Transform sign=GameObject.Find("FullCourseSections").transform.Find("Section8/Stage8CannonInstructions");
        foreach(string name in new[]{"Instructions","RedMark","ShootingPictogram"})
        {
            Transform old=sign.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);
        }
        Draw(sign);
        EditorSceneManager.MarkSceneDirty(sign.gameObject.scene);
        EditorSceneManager.SaveScene(sign.gameObject.scene);
    }
    public static void Draw(Transform sign)
    {
        GameObject canvas=new GameObject("ShootingPictogram",typeof(RectTransform),typeof(Canvas));
        canvas.transform.SetParent(sign,false);canvas.transform.localPosition=new Vector3(0,5.3f,-.28f);
        canvas.transform.localScale=Vector3.one*.014f;
        canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;
        RectTransform root=canvas.GetComponent<RectTransform>();root.sizeDelta=new Vector2(1200,440);
        RectTransform gun=Rect(root,"Pistol",new Vector2(-330,-40),new Vector2(280,220));
        gun.localRotation=Quaternion.Euler(0,0,13);
        Box(gun,"Slide",new Vector2(0,25),new Vector2(250,65),Ink);
        Box(gun,"Frame",new Vector2(-32,-13),new Vector2(180,35),Ink);
        RectTransform grip=Box(gun,"Grip",new Vector2(-88,-83),new Vector2(65,133),Ink);
        grip.localRotation=Quaternion.Euler(0,0,-16);
        Box(gun,"FrontSight",new Vector2(103,66),new Vector2(15,18),Ink);
        Box(gun,"RearSight",new Vector2(-94,65),new Vector2(21,16),Ink);
        Stroke(gun,"TriggerGuard",new Vector2(-34,-26),new Vector2(42,-26),11,Ink);
        Stroke(gun,"TriggerGuard",new Vector2(42,-26),new Vector2(30,-83),11,Ink);
        Stroke(gun,"TriggerGuard",new Vector2(30,-83),new Vector2(-49,-83),11,Ink);
        Stroke(gun,"Trigger",new Vector2(0,-28),new Vector2(-12,-57),10,Ink);
        Vector2 muzzle=new Vector2(-211,14);
        Vector2 impact=new Vector2(264,110);
        Stroke(root,"YellowShot",muzzle+new Vector2(40,6),impact-new Vector2(24,3),8,Yellow);
        Stroke(root,"MuzzleFlash",muzzle+new Vector2(3,-24),muzzle+new Vector2(34,0),9,Yellow);
        Stroke(root,"MuzzleFlash",muzzle+new Vector2(34,0),muzzle+new Vector2(14,29),9,Yellow);
        Stroke(root,"MuzzleFlash",muzzle+new Vector2(14,29),muzzle+new Vector2(60,11),9,Yellow);
        RectTransform target=Rect(root,"RedWeakPoint",new Vector2(355,105),new Vector2(180,180));
        // The actual target is a bare red cylinder, so show its round face and
        // short shaded side without a cabinet, number, or electrical symbol.
        Ellipse(target,"CylinderBack",new Vector2(15,13),new Vector2(172,172),new Color(.55f,.025f,.015f));
        Box(target,"CylinderSide",new Vector2(1,1),new Vector2(30,166),new Color(.55f,.025f,.015f));
        Ellipse(target,"CylinderRim",new Vector2(-14,-10),new Vector2(178,178),new Color(.7f,.035f,.02f));
        Ellipse(target,"CylinderFace",new Vector2(-17,-8),new Vector2(163,163),Red);
        Stroke(root,"HitSpark",impact+new Vector2(-16,31),impact+new Vector2(1,12),8,Yellow);
        Stroke(root,"HitSpark",impact+new Vector2(-22,-26),impact+new Vector2(-1,-10),8,Yellow);
        // Picture only: the dotted ground area is centred on the boulder, with
        // the elevated button projected inside it and a green confirmation mark.
        Color green=new Color(.3f,.95f,.5f);
        Vector2 centre=new Vector2(90,-85);
        for(int i=0;i<48;i+=2)
        {
            float a=i/48f*Mathf.PI*2,b=(i+1)/48f*Mathf.PI*2;
            Stroke(root,"BoulderRange",centre+new Vector2(Mathf.Cos(a)*380,Mathf.Sin(a)*100),
                centre+new Vector2(Mathf.Cos(b)*380,Mathf.Sin(b)*100),4,new Color(.3f,.95f,.5f,.6f));
        }
        Ellipse(root,"BoulderShadow",new Vector2(90,-143),new Vector2(145,24),new Color(.12f,.14f,.17f));
        Ellipse(root,"Boulder",new Vector2(90,-91),new Vector2(132,112),new Color(.55f,.57f,.59f));
        Stroke(root,"RockCrack",new Vector2(62,-46),new Vector2(76,-82),5,new Color(.25f,.27f,.29f));
        Stroke(root,"RockCrack",new Vector2(76,-82),new Vector2(56,-104),5,new Color(.25f,.27f,.29f));
        Stroke(root,"RockCrack",new Vector2(76,-82),new Vector2(112,-76),5,new Color(.25f,.27f,.29f));
        for(int y=-65;y<10;y+=20)
            Stroke(root,"ButtonGroundProjection",new Vector2(355,y),new Vector2(355,y+10),4,Ink);
        Stroke(root,"BringBoulderNear",new Vector2(173,-95),new Vector2(322,-95),7,green);
        Stroke(root,"BringBoulderNear",new Vector2(322,-95),new Vector2(302,-80),7,green);
        Stroke(root,"BringBoulderNear",new Vector2(322,-95),new Vector2(302,-110),7,green);
        Stroke(root,"Allowed",new Vector2(350,-110),new Vector2(365,-126),10,green);
        Stroke(root,"Allowed",new Vector2(365,-126),new Vector2(400,-87),10,green);
    }
    private static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        RectTransform rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    private static void Ellipse(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        RectTransform ellipse=Rect(parent,name,position,size);
        const int rows=64;
        for(int i=0;i<rows;i++)
        {
            float y=(i+.5f)/rows*2-1;
            float width=Mathf.Sqrt(Mathf.Max(0,1-y*y))*size.x;
            Box(ellipse,"Fill",new Vector2(0,y*size.y*.5f),new Vector2(width,size.y/rows+.1f),color);
        }
    }
    private static RectTransform Box(Transform parent,string name,Vector2 position,Vector2 size,Color color)
    {
        RectTransform rect=Rect(parent,name,position,size);
        UnityEngine.UI.Image image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return rect;
    }
    private static void Stroke(Transform parent,string name,Vector2 from,Vector2 to,float width,Color color)
    {
        RectTransform rect=Box(parent,name,(from+to)*.5f,new Vector2(Vector2.Distance(from,to),width),color);
        rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg);
    }
}
