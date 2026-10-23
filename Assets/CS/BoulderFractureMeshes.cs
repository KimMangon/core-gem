using System.Collections.Generic;
using UnityEngine;

/// <summary>Clips the convex boulder mesh into two closed halves, preserving the outer UVs.</summary>
public static class BoulderFractureMeshes
{
    private struct Vertex
    {
        public Vector3 position,normal;
        public Vector2 uv;
        public static Vertex Lerp(Vertex a,Vertex b,float t) => new Vertex {
            position=Vector3.Lerp(a.position,b.position,t),normal=Vector3.Lerp(a.normal,b.normal,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};
    }

    public static Mesh CreateHalf(Mesh source,bool left)
    {
        var positions=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();
        var outside=new List<int>();var cap=new List<int>();var rim=new List<Vector3>();
        Vector3[] points=source.vertices,sourceNormals=source.normals;Vector2[] sourceUV=source.uv;
        int[] triangles=source.triangles;float plane=source.bounds.center.x;
        for(int i=0;i<triangles.Length;i+=3)
        {
            var polygon=new List<Vertex>(3);
            for(int j=0;j<3;j++)
            {
                int index=triangles[i+j];
                polygon.Add(new Vertex {position=points[index],normal=sourceNormals.Length==points.Length?sourceNormals[index]:points[index].normalized,
                    uv=sourceUV.Length==points.Length?sourceUV[index]:Vector2.zero});
            }
            var clipped=new List<Vertex>(4);
            for(int j=0;j<polygon.Count;j++)
            {
                Vertex a=polygon[j],b=polygon[(j+1)%polygon.Count];
                bool insideA=left?a.position.x<=plane:a.position.x>=plane;
                bool insideB=left?b.position.x<=plane:b.position.x>=plane;
                if(insideA)clipped.Add(a);
                if(insideA!=insideB)
                {
                    Vertex intersection=Vertex.Lerp(a,b,(plane-a.position.x)/(b.position.x-a.position.x));
                    clipped.Add(intersection);
                    if(!rim.Exists(p=>(p-intersection.position).sqrMagnitude<.00000001f))rim.Add(intersection.position);
                }
            }
            if(clipped.Count<3)continue;
            int start=positions.Count;
            foreach(var vertex in clipped) {positions.Add(vertex.position);normals.Add(vertex.normal);uvs.Add(vertex.uv);}
            for(int j=1;j<clipped.Count-1;j++) {outside.Add(start);outside.Add(start+j);outside.Add(start+j+1);}
        }
        if(rim.Count>=3)
        {
            Vector3 center=Vector3.zero;foreach(var point in rim)center+=point;center/=rim.Count;
            rim.Sort((a,b)=>Mathf.Atan2(a.z-center.z,a.y-center.y).CompareTo(Mathf.Atan2(b.z-center.z,b.y-center.y)));
            Vector3 normal=left?Vector3.right:Vector3.left;
            int start=positions.Count;
            positions.Add(center);normals.Add(normal);uvs.Add(new Vector2(.5f,.5f));
            foreach(var point in rim)
            {
                positions.Add(point);normals.Add(normal);
                uvs.Add(new Vector2((point.y-source.bounds.min.y)/source.bounds.size.y,(point.z-source.bounds.min.z)/source.bounds.size.z));
            }
            for(int i=0;i<rim.Count;i++)
            {
                cap.Add(start);cap.Add(start+1+(left?i:(i+1)%rim.Count));cap.Add(start+1+(left?(i+1)%rim.Count:i));
            }
        }
        var mesh=new Mesh {name=left?"Boulder half left":"Boulder half right",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
        mesh.SetVertices(positions);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.subMeshCount=2;
        mesh.SetTriangles(outside,0);mesh.SetTriangles(cap,1);mesh.RecalculateBounds();mesh.RecalculateTangents();
        return mesh;
    }
}
