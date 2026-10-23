using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Frame-budgeted local preparation before either host or client connects.</summary>
public class RoomResourcePreloader : MonoBehaviour
{
    [SerializeField] private GameObject[] gameplayPrefabs = Array.Empty<GameObject>();
    [SerializeField] private AudioClip[] soundEffects = Array.Empty<AudioClip>();
    [SerializeField] private Material[] extraMaterials = Array.Empty<Material>();
    private TaskCompletionSource<bool> completion;
    private Coroutine preparation;
    private RenderTexture target;
    public bool IsLoading { get; private set; }
    public float Progress { get; private set; }
    public bool IsReady { get; private set; }

    public Task PrepareAsync()
    {
        if (IsReady) return Task.CompletedTask;
        if (IsLoading) return completion.Task;
        completion = new TaskCompletionSource<bool>();
        IsLoading = true;
        preparation = StartCoroutine(RunPreparation());
        return completion.Task;
    }

    private IEnumerator RunPreparation()
    {
        IEnumerator work=Prepare();
        while(true)
        {
            bool next;
            object current;
            try { next=work.MoveNext();current=next?work.Current:null; }
            catch(Exception exception)
            {
                ReleaseTarget();IsLoading=false;preparation=null;
                completion.TrySetException(exception);
                yield break;
            }
            if(!next)yield break;
            yield return current;
        }
    }

    private IEnumerator Prepare()
    {
        // Let the lobby and its loading status become visible before doing work.
        yield return null;
        BoulderBreakEffect.PrepareResources();
        var draws = new List<Draw>();
        var seen = new HashSet<(Mesh,Material,int)>();
        foreach (GameObject prefab in gameplayPrefabs)
        {
            if (prefab == null) continue;
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                Collect(draws,seen,filter.sharedMesh,renderer != null ? renderer.sharedMaterials : null);
            }
            foreach (SkinnedMeshRenderer renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Collect(draws,seen,renderer.sharedMesh,renderer.sharedMaterials);
        }
        // Reuse one mesh to prepare tracer/warning materials too.
        if (draws.Count > 0) Collect(draws,seen,draws[0].mesh,extraMaterials);
        int total = Mathf.Max(1,draws.Count+soundEffects.Length);
        int done = 0;
        if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
        {
            target = new RenderTexture(32,32,16) { name = "RoomResourceWarmup", hideFlags = HideFlags.HideAndDontSave };
            target.Create();
        }
        foreach (Draw draw in draws)
        {
            // Never instantiate gameplay or network scripts while preloading.
            // A tiny offscreen draw prepares mesh buffers, textures and the used
            // material pass without showing or spawning anything in the scene.
            try { PrepareDraw(draw); }
            catch (Exception exception) { Debug.LogWarning("Could not prewarm "+draw.mesh.name+": "+exception.Message); }
            Progress = ++done/(float)total;
            yield return null;
        }
        foreach (AudioClip clip in soundEffects)
        {
            if (clip != null && clip.loadType != AudioClipLoadType.Streaming &&
                clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
                float deadline = Time.realtimeSinceStartup + 10f;
                while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (clip.loadState != AudioDataLoadState.Loaded)
                    Debug.LogWarning("Audio could not be prepared before entering the room: "+clip.name);
            }
            Progress = ++done/(float)total;
            yield return null;
        }
        ReleaseTarget();
        Progress = 1f;
        IsReady = true;
        IsLoading = false;
        preparation = null;
        completion.TrySetResult(true);
    }

    private struct Draw { public Mesh mesh; public Material material; public int submesh; }
    private static void Collect(List<Draw> draws,HashSet<(Mesh,Material,int)> seen,Mesh mesh,Material[] materials)
    {
        if (mesh == null || materials == null) return;
        for (int i=0;i<Mathf.Min(mesh.subMeshCount,materials.Length);i++)
            if(materials[i]!=null && seen.Add((mesh,materials[i],i)))
                draws.Add(new Draw{mesh=mesh,material=materials[i],submesh=i});
    }
    private void PrepareDraw(Draw draw)
    {
        if(target==null)return;
        Bounds bounds=draw.mesh.bounds;
        float scale=1f/Mathf.Max(.001f,bounds.size.x,bounds.size.y,bounds.size.z);
        Matrix4x4 model=Matrix4x4.TRS(-bounds.center*scale,Quaternion.identity,Vector3.one*scale);
        Matrix4x4 view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(new Vector3(0,0,-3),Quaternion.identity,Vector3.one).inverse;
        Matrix4x4 projection=GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-1,1,-1,1,.1f,10),true);
        RenderTexture previous=RenderTexture.active;
        CommandBuffer commands=new CommandBuffer{name="Prepare Room Material"};
        try
        {
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(true,true,Color.clear);
            commands.SetViewProjectionMatrices(view,projection);
            commands.DrawMesh(draw.mesh,model,draw.material,draw.submesh,0);
            Graphics.ExecuteCommandBuffer(commands);
        }
        finally { commands.Release(); RenderTexture.active=previous; }
    }
    private void ReleaseTarget()
    {
        if(target==null)return;
        target.Release();Destroy(target);target=null;
    }
    private void OnDestroy()
    {
        if(preparation!=null)StopCoroutine(preparation);
        ReleaseTarget();
        completion?.TrySetCanceled();
    }
}
