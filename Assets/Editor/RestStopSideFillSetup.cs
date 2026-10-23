using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RestStopSideFillSetup
{
    [MenuItem("Tools/Course/Fill Rest Stop Sides")]
    public static void Apply()
    {
        // Remove only the fills created by the earlier, unrelated ramp operation.
        foreach (var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (f.name == "RampSideFill" && AssetDatabase.GetAssetPath(f.sharedMesh).StartsWith("Assets/Meshes/RampSideFills/"))
                Object.DestroyImmediate(f.gameObject);
        foreach (string name in new[] {"Ascent_01","Ascent_02","Ascent_03","Stage5Ascent_01","Stage5Ascent_02","Stage5Ascent_03","Stage5Ascent_04"})
            AssetDatabase.DeleteAsset("Assets/Meshes/RampSideFills/" + name + ".asset");

        foreach (var f in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(f => f.name.StartsWith("RestStop_") && f.sharedMesh != null))
        {
            Mesh mesh = f.sharedMesh;
            Undo.RegisterCompleteObjectUndo(mesh,"Fill rest stop sides");
            // The first eight vertices are the existing four pairs along the top.
            // Preserve them exactly, then close each side down to the 14-degree road.
            var top = mesh.vertices.Take(8).ToArray();
            var vertices = new List<Vector3>(top);
            var uv = new List<Vector2>(mesh.uv.Take(8));
            while (uv.Count < 8) uv.Add(Vector2.zero);
            var triangles = new List<int> {0,2,1,1,2,3,2,4,3,3,4,5,4,6,5,5,6,7};
            Vector3 Bottom(int i) => new Vector3(top[i].x, Mathf.Tan(14f*Mathf.Deg2Rad)*top[i].z-.03f, top[i].z);
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int n=vertices.Count;
                vertices.AddRange(new[]{a,b,c,d});
                uv.AddRange(new[]{Vector2.zero,new Vector2(Vector3.Distance(a,b),0),new Vector2(Vector3.Distance(a,b),Vector3.Distance(b,c)),new Vector2(0,Vector3.Distance(a,d))});
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            for (int i=0;i<6;i+=2)
            {
                Face(top[i],Bottom(i),Bottom(i+2),top[i+2]);
                Face(top[i+1],top[i+3],Bottom(i+3),Bottom(i+1));
                Face(Bottom(i),Bottom(i+1),Bottom(i+3),Bottom(i+2));
            }
            Face(top[0],top[1],Bottom(1),Bottom(0));
            Face(top[6],Bottom(6),Bottom(7),top[7]);
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var collider=f.GetComponent<MeshCollider>();
            if(collider!=null) {collider.sharedMesh=null;collider.sharedMesh=mesh;}
            EditorSceneManager.MarkSceneDirty(f.gameObject.scene);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
    }
}
