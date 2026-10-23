using UnityEngine;

/// <summary>Local visual fragments only: no network objects, colliders or physics impulses.</summary>
public sealed class BoulderBreakEffect : MonoBehaviour
{
    private readonly Transform[] pieces=new Transform[2];
    private Vector3 scale,splitDirection;
    private Quaternion initialRotation;
    private float elapsed;
    private Material cutMaterial;
    private static Mesh leftMesh,rightMesh;
    private static AudioClip breakSound;

    public static void PrepareResources()
    {
        if(leftMesh==null)leftMesh=Resources.Load<Mesh>("Vfx/BoulderHalfLeft");
        if(rightMesh==null)rightMesh=Resources.Load<Mesh>("Vfx/BoulderHalfRight");
        if(breakSound==null)breakSound=Resources.Load<AudioClip>("Sfx/BoulderBreak");
        if(breakSound!=null && breakSound.loadState==AudioDataLoadState.Unloaded)breakSound.LoadAudioData();
    }

    public static void Play(MeshRenderer source,Vector3 position,Quaternion rotation,Vector3 worldScale)
    {
        if(source==null)return;
        PrepareResources();
        if(leftMesh==null || rightMesh==null) {Debug.LogWarning("Boulder fracture meshes are missing.");return;}
        var root=new GameObject("BoulderShatter");root.transform.position=position;
        var effect=root.AddComponent<BoulderBreakEffect>();effect.scale=worldScale;
        effect.initialRotation=rotation;effect.splitDirection=rotation*Vector3.right;
        effect.cutMaterial=new Material(source.sharedMaterial) {name="Broken stone interior"};
        Color color=effect.cutMaterial.color;effect.cutMaterial.color=new Color(color.r*.8f,color.g*.8f,color.b*.8f,color.a);
        if(effect.cutMaterial.HasProperty("_Smoothness"))effect.cutMaterial.SetFloat("_Smoothness",0);
        for(int i=0;i<2;i++)
        {
            var piece=new GameObject(i==0?"Left half":"Right half",typeof(MeshFilter),typeof(MeshRenderer));
            piece.transform.SetParent(root.transform,false);piece.transform.rotation=rotation;piece.transform.localScale=worldScale;
            piece.GetComponent<MeshFilter>().sharedMesh=i==0?leftMesh:rightMesh;
            piece.GetComponent<MeshRenderer>().sharedMaterials=new[]{source.sharedMaterial,effect.cutMaterial};
            effect.pieces[i]=piece.transform;
        }
        if(breakSound!=null)
        {
            var audio=root.AddComponent<AudioSource>();audio.spatialBlend=1;audio.minDistance=5;audio.maxDistance=75;
            audio.volume=GameSettingsPreferences.SfxVolume;audio.PlayOneShot(breakSound);
        }
        Destroy(root,1.6f);
    }

    private void Update()
    {
        elapsed+=Time.deltaTime;
        float t=Mathf.Min(elapsed,1.05f);
        for(int i=0;i<2;i++)
        {
            if(pieces[i]==null)continue;
            float sign=i==0?-1:1;
            pieces[i].position=transform.position+splitDirection*(sign*(.045f+t*1.3f))+
                Vector3.up*(.85f*t-3f*t*t);
            pieces[i].rotation=Quaternion.AngleAxis(sign*t*65,initialRotation*Vector3.forward)*initialRotation;
            float shrink=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.05f,t));
            pieces[i].localScale=scale*shrink;
        }
    }

    private void OnDestroy() {if(cutMaterial!=null)Destroy(cutMaterial);}
}
