using UnityEngine;

public sealed class PlayerCosmeticVisual : MonoBehaviour
{
    private readonly GameObject[] slots = new GameObject[17];
    private Material dark, red, gold, white;
    private Material olive, khaki, blue, brown, silver, camoGreen, camoSand;
    private readonly System.Collections.Generic.List<Mesh> extraMeshes = new();
    private Mesh tieMesh, starMesh;
    private MaterialPropertyBlock tintProperties;

    public void Build()
    {
        if (slots[0] != null) return;
        dark = Material("Cosmetic charcoal", new Color(.055f, .065f, .08f));
        red = Material("Cosmetic red", new Color(.85f, .13f, .12f));
        gold = Material("Cosmetic gold", new Color(1f, .73f, .12f));
        white = Material("Cosmetic cream", new Color(.95f, .93f, .81f));
        olive=Material("Helmet green",new Color(.25f,.32f,.17f));
        khaki=Material("Canvas khaki",new Color(.63f,.51f,.31f));
        blue=Material("Knit navy",new Color(.13f,.21f,.35f));
        brown=Material("Leather brown",new Color(.24f,.12f,.065f));
        silver=Material("Glasses silver",new Color(.65f,.69f,.72f));
        camoGreen=Material("Camo forest",new Color(.12f,.19f,.10f));
        camoSand=Material("Camo tan",new Color(.43f,.39f,.22f));
        string[] names={ "Necktie", "StarBadge", "Sunglasses", "TopHat", "ArmyHelmet", "BucketHat", "RoundGlasses", "Beanie", "Hood", "ExplorerHat", "SantaHelmet", "Crown", "Ribbon", "Moustache", "Belt", "BowTie", "Necklace" };
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new GameObject(names[i]);
            slots[i].transform.SetParent(transform, false);
        }
        // Front is local +Z, matching PlayerEyes. Keep accessories clear of the eyes.
        Part(0, "Knot", PrimitiveType.Cube, new Vector3(0,.36f,.49f), new Vector3(.13f,.13f,.055f), red,
            Quaternion.Euler(0,0,45));
        tieMesh = Extrude(new[] { new Vector2(-.055f,.28f), new Vector2(-.10f,-.12f),
            new Vector2(0,-.22f), new Vector2(.10f,-.12f), new Vector2(.055f,.28f) }, .045f);
        MeshPart(0,"Tie", tieMesh, new Vector3(0,0,.505f),red);
        var points = new Vector2[10];
        for(int i=0;i<10;i++)
        {
            float angle=(90+i*36)*Mathf.Deg2Rad;
            points[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(i%2==0?.15f:.069f);
        }
        starMesh=Extrude(points,.045f);
        MeshPart(1,"Star",starMesh,new Vector3(-.27f,.23f,.445f),gold);
        for(int side=-1;side<=1;side+=2)
        {
            Part(2,"Frame",PrimitiveType.Cube,new Vector3(side*.18f,.665f,.575f),new Vector3(.35f,.25f,.06f),dark);
            Part(2,"Lens",PrimitiveType.Cube,new Vector3(side*.18f,.665f,.61f),new Vector3(.27f,.17f,.025f),dark);
            Part(2,"Glint",PrimitiveType.Cube,new Vector3(side*.18f-.065f,.71f,.626f),new Vector3(.065f,.025f,.01f),white,
                Quaternion.Euler(0,0,25));
            Part(2,"Temple",PrimitiveType.Cube,new Vector3(side*.36f,.675f,.38f),new Vector3(.045f,.055f,.40f),dark);
        }
        Part(2,"Bridge",PrimitiveType.Cube,new Vector3(0,.69f,.59f),new Vector3(.13f,.055f,.055f),dark);
        Part(3,"Brim",PrimitiveType.Cylinder,new Vector3(0,1.015f,0),new Vector3(1.15f,.045f,1.06f),dark);
        Part(3,"Crown",PrimitiveType.Cylinder,new Vector3(0,1.23f,0),new Vector3(.77f,.20f,.74f),dark);
        Part(3,"Ribbon",PrimitiveType.Cylinder,new Vector3(0,1.09f,0),new Vector3(.79f,.055f,.76f),red);
        BuildNewItems();
        // Seat each hat around the capsule's rounded crown rather than above its tip.
        slots[3].transform.localPosition=Vector3.down*.17f;
        SeatHatBack(4,-.04f,-.075f,-14f);
        SeatHatBack(5,-.075f,-.065f,-14f);
        slots[7].transform.localPosition=Vector3.down*.16f;
        slots[9].transform.localPosition=Vector3.down*.20f;
        slots[10].transform.localPosition=Vector3.down*.16f;
        slots[11].transform.localPosition=Vector3.down*.23f;
        slots[12].transform.localPosition=Vector3.down*.10f;
        Apply(0);
    }

    private void SeatHatBack(int slot,float height,float back,float tilt)
    {
        Vector3 pivot=new Vector3(0,.9f,0);
        Quaternion rotation=Quaternion.Euler(tilt,0,0);
        slots[slot].transform.localRotation=rotation;
        slots[slot].transform.localPosition=pivot-rotation*pivot+new Vector3(0,height,back);
    }

    public void Apply(int selection,uint bodyColor=0,uint headColor=0)
    {
        selection = PlayerCosmetics.Normalize(selection);
        int body=PlayerCosmetics.GetItem(selection,0),head=PlayerCosmetics.GetItem(selection,1);
        int bodySlot=body==0?-1:body<=2?body-1:body+11;
        int headSlot=head==0?-1:head+1;
        for(int i=0;i<slots.Length;i++) if(slots[i]!=null) slots[i].SetActive(i==bodySlot || i==headSlot);
        TintSlot(bodySlot,PlayerCosmetics.GetColor(selection,0),bodyColor);
        TintSlot(headSlot,PlayerCosmetics.GetColor(selection,1),headColor);
    }

    private void TintSlot(int slot,int colorIndex,uint customColor)
    {
        if(slot<0 || slots[slot]==null)return;
        if(tintProperties==null)tintProperties=new MaterialPropertyBlock();
        foreach(var renderer in slots[slot].GetComponentsInChildren<MeshRenderer>(true))
        {
            if(renderer.sharedMaterial!=PrimaryMaterial(slot))continue;
            // Sunglass lenses remain dark; only the frame changes color.
            if(slot==2 && renderer.name=="Lens")continue;
            if(colorIndex==0 && customColor==0) {renderer.SetPropertyBlock(null);continue;}
            tintProperties.Clear();
            Color color=customColor==0?PlayerCosmetics.Tint(colorIndex):PlayerColorPalette.Unpack(customColor);
            tintProperties.SetColor("_BaseColor",color);tintProperties.SetColor("_Color",color);
            renderer.SetPropertyBlock(tintProperties);
        }
    }

    private Material PrimaryMaterial(int slot)
    {
        switch(slot)
        {
            case 0:case 10:case 12:return red;
            case 1:case 11:case 16:return gold;
            case 2:case 3:case 15:return dark;
            case 4:return olive;
            case 5:case 9:return khaki;
            case 6:return silver;
            case 7:case 8:return blue;
            default:return brown;
        }
    }

    public Color OriginalColor(int selection,int category)
    {
        int item=PlayerCosmetics.GetItem(selection,category);
        if(item==0)return Color.white;
        int slot=category==1?item+1:item<=2?item-1:item+11;
        return PrimaryMaterial(slot).color;
    }

    private void BuildNewItems()
    {
        // Helmet: rounded steel shell, flared rim and a short front strap.
        Surface(4,"Steel shell",new[] { new Vector2(.53f,.84f),new Vector2(.53f,.96f),new Vector2(.45f,1.13f),new Vector2(.27f,1.25f),new Vector2(0,1.29f) },olive);
        Part(4,"Helmet rim",PrimitiveType.Cylinder,new Vector3(0,.85f,0),new Vector3(1.13f,.025f,1.10f),olive);
        Part(4,"Front strap",PrimitiveType.Cube,new Vector3(0,.87f,.55f),new Vector3(.18f,.055f,.04f),brown);
        BuildHelmetDetails();
        // Bucket hat: sloping brim and tapered canvas crown.
        Surface(5,"Canvas hat",new[] { new Vector2(.66f,.91f),new Vector2(.49f,1.02f),new Vector2(.40f,1.32f),new Vector2(0,1.32f) },khaki);
        Ring(5,"Hat seam",new Vector3(0,1.06f,0),.46f,.018f,Quaternion.identity,brown);
        for(int side=-1;side<=1;side+=2)
        {
            Ring(6,"Round frame",new Vector3(side*.18f,.665f,.59f),.162f,.025f,Quaternion.Euler(90,0,0),silver);
            Part(6,"Temple",PrimitiveType.Cube,new Vector3(side*.35f,.685f,.37f),new Vector3(.035f,.035f,.40f),silver);
        }
        Part(6,"Bridge",PrimitiveType.Cube,new Vector3(0,.69f,.59f),new Vector3(.09f,.035f,.035f),silver);
        Surface(7,"Knit cap",new[] {new Vector2(.49f,.87f),new Vector2(.48f,1.02f),new Vector2(.38f,1.22f),new Vector2(.22f,1.31f),new Vector2(0,1.34f)},blue);
        Surface(7,"Folded cuff",new[] {new Vector2(.51f,.86f),new Vector2(.51f,.98f),new Vector2(.47f,1.0f)},blue);
        for(int i=0;i<16;i++)
        {
            float angle=i*Mathf.PI*2/16;
            Part(7,"Knit rib",PrimitiveType.Cube,new Vector3(Mathf.Cos(angle)*.502f,.925f,Mathf.Sin(angle)*.502f),new Vector3(.014f,.10f,.014f),dark);
        }
        // Open-front hood: rear shell and an oval cloth edge around the face.
        Surface(8,"Hood back",new[] {new Vector2(.53f,.16f),new Vector2(.56f,.55f),new Vector2(.54f,.88f),new Vector2(.39f,1.10f),new Vector2(0,1.20f)},blue,Mathf.PI,Mathf.PI*2);
        var hood=Ring(8,"Face opening",new Vector3(0,.64f,.10f),.53f,.085f,Quaternion.Euler(90,0,0),blue);
        hood.localScale=new Vector3(1,1,1.12f);
        Surface(9,"Explorer brim",new[] {new Vector2(.72f,.94f),new Vector2(.60f,1.0f),new Vector2(.48f,1.02f),new Vector2(.40f,1.26f),new Vector2(0,1.27f)},khaki);
        Ring(9,"Leather hat band",new Vector3(0,1.06f,0),.46f,.038f,Quaternion.identity,brown);
        Part(9,"Buckle",PrimitiveType.Cube,new Vector3(0,1.065f,.48f),new Vector3(.16f,.105f,.045f),gold);
        Surface(10,"Santa helmet",new[] {new Vector2(.51f,.88f),new Vector2(.50f,1.03f),new Vector2(.41f,1.18f),new Vector2(.20f,1.36f),new Vector2(0,1.51f)},red);
        Ring(10,"Fur trim",new Vector3(0,.91f,0),.49f,.075f,Quaternion.identity,white);
        Part(10,"Pom pom",PrimitiveType.Sphere,new Vector3(0,1.51f,0),Vector3.one*.19f,white);
        BuildCrown();
        Bow(12,new Vector3(.20f,1.01f,.29f),.20f,red);
        // A separate curled lobe on each side of the upper lip, without adding a mouth.
        for(int side=-1;side<=1;side+=2)
        {
            var lobe=PartTransform(13,"Moustache lobe",PrimitiveType.Sphere,new Vector3(side*.13f,.39f,.515f),new Vector3(.30f,.105f,.09f),brown);
            lobe.localRotation=Quaternion.Euler(0,0,side*15);
            Part(13,"Curled tip",PrimitiveType.Sphere,new Vector3(side*.28f,.43f,.51f),new Vector3(.10f,.095f,.075f),brown);
        }
        Surface(14,"Leather belt",new[] {new Vector2(.515f,-.35f),new Vector2(.515f,-.19f)},brown);
        Part(14,"Buckle",PrimitiveType.Cube,new Vector3(0,-.27f,.53f),new Vector3(.21f,.17f,.055f),gold);
        Part(14,"Buckle inset",PrimitiveType.Cube,new Vector3(0,-.27f,.565f),new Vector3(.12f,.08f,.015f),dark);
        Bow(15,new Vector3(0,.33f,.51f),.17f,dark);
        BuildNecklace();
    }

    private void BuildHelmetDetails()
    {
        // Polygon patches follow the actual shell profile, avoiding floating decal plates.
        for(int patch=0;patch<24;patch++)
        {
            float angle=patch*2.399963f;
            float height=.99f+(patch%4)*.067f;
            var vertices=new Vector3[7];var triangles=new int[18];
            vertices[0]=HelmetPoint(angle,height);
            for(int edge=0;edge<6;edge++)
            {
                float a=edge*Mathf.PI*2/6;
                float uneven=(edge+patch)%3==0?.72f:1f;
                vertices[edge+1]=HelmetPoint(angle+Mathf.Cos(a)*(.15f+(patch%3)*.035f)*uneven,
                    height+Mathf.Sin(a)*.04f*uneven);
                int k=edge*3;triangles[k]=0;triangles[k+1]=(edge+1)%6+1;triangles[k+2]=edge+1;
            }
            MeshPart(4,"Camouflage patch",OwnMesh(vertices,triangles),Vector3.zero,patch%3==0?camoSand:patch%3==1?camoGreen:brown);
        }
        Surface(4,"Helmet webbing band",new[] {new Vector2(.538f,.905f),new Vector2(.538f,.957f)},brown);
        for(int side=-1;side<=1;side+=2)
        {
            Part(4,"Band keeper",PrimitiveType.Cube,new Vector3(side*.536f,.93f,0),new Vector3(.025f,.09f,.055f),olive);
            Part(4,"Side rivet",PrimitiveType.Sphere,new Vector3(side*.546f,.875f,0),Vector3.one*.045f,silver);
        }
    }

    private static Vector3 HelmetPoint(float angle,float height)
    {
        float radius=height<=.96f?.53f:height<=1.13f?Mathf.Lerp(.53f,.45f,(height-.96f)/.17f):
            height<=1.25f?Mathf.Lerp(.45f,.27f,(height-1.13f)/.12f):Mathf.Lerp(.27f,0,(height-1.25f)/.04f);
        radius+=.004f;
        return new Vector3(Mathf.Cos(angle)*radius,height,Mathf.Sin(angle)*radius);
    }

    private void BuildNecklace()
    {
        const int n=64,m=8;
        var vertices=new Vector3[(n+1)*(m+1)];var triangles=new int[n*m*6];
        for(int i=0;i<=n;i++)
        {
            float a=i*Mathf.PI*2/n;
            var outward=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            // The chain rests against the torso and dips down toward the pendant.
            var center=outward*.515f+Vector3.up*(.32f-.20f*Mathf.Max(0,Mathf.Sin(a)));
            for(int j=0;j<=m;j++)
            {
                float b=j*Mathf.PI*2/m;
                vertices[i*(m+1)+j]=center+(outward*Mathf.Cos(b)+Vector3.up*Mathf.Sin(b))*.022f;
            }
        }
        for(int i=0;i<n;i++)for(int j=0;j<m;j++)
        {
            int a=i*(m+1)+j,b=a+1,c=a+m+1,d=c+1,k=(i*m+j)*6;
            triangles[k]=a;triangles[k+1]=b;triangles[k+2]=c;triangles[k+3]=b;triangles[k+4]=d;triangles[k+5]=c;
        }
        MeshPart(16,"Draped chain",OwnMesh(vertices,triangles),Vector3.zero,gold);
        Ring(16,"Pendant loop",new Vector3(0,.115f,.535f),.038f,.012f,Quaternion.Euler(90,0,0),gold);
        Part(16,"Pendant",PrimitiveType.Sphere,new Vector3(0,.035f,.53f),new Vector3(.14f,.18f,.055f),gold);
    }

    private void Bow(int slot,Vector3 center,float size,Material material)
    {
        for(int side=-1;side<=1;side+=2)
        {
            var outline=new[] {new Vector2(0,0),new Vector2(size*side,size*.65f),new Vector2(size*side,-size*.65f)};
            if(side>0)System.Array.Reverse(outline);
            Mesh mesh=Extrude(outline,.065f);extraMeshes.Add(mesh);MeshPart(slot,"Ribbon wing",mesh,center,material);
        }
        Part(slot,"Knot",PrimitiveType.Sphere,center+Vector3.forward*.045f,new Vector3(size*.55f,size*.6f,.09f),material);
    }

    private Transform PartTransform(int slot,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        Part(slot,name,type,position,scale,material);return slots[slot].transform.GetChild(slots[slot].transform.childCount-1);
    }

    private void BuildCrown()
    {
        const int n=16;var vertices=new Vector3[n*2];var triangles=new int[n*12];
        for(int i=0;i<n;i++)
        {
            float a=i*Mathf.PI*2/n;float x=Mathf.Cos(a)*.45f,z=Mathf.Sin(a)*.45f;
            vertices[i]=new Vector3(x,.93f,z);vertices[n+i]=new Vector3(x,i%2==0?1.40f:1.16f,z);
            int j=(i+1)%n,k=i*12;
            int[] face={i,n+i,j,j,n+i,n+j,j,n+i,i,n+j,n+i,j};
            for(int f=0;f<12;f++)triangles[k+f]=face[f];
            if(i%2==0)Part(11,"Crown jewel",PrimitiveType.Sphere,new Vector3(x*1.02f,1.16f,z*1.02f),Vector3.one*.075f,red);
        }
        MeshPart(11,"Pointed crown",OwnMesh(vertices,triangles),Vector3.zero,gold);
        Ring(11,"Crown band",new Vector3(0,.98f,0),.45f,.045f,Quaternion.identity,gold);
    }

    private void Surface(int slot,string name,Vector2[] profile,Material material,float start=0,float end=Mathf.PI*2)
    {
        const int n=32;var vertices=new Vector3[profile.Length*(n+1)];var triangles=new int[(profile.Length-1)*n*6];
        for(int row=0;row<profile.Length;row++)for(int col=0;col<=n;col++)
        {
            float a=Mathf.Lerp(start,end,(float)col/n);var p=profile[row];
            vertices[row*(n+1)+col]=new Vector3(Mathf.Cos(a)*p.x,p.y,Mathf.Sin(a)*p.x);
        }
        for(int row=0;row<profile.Length-1;row++)for(int col=0;col<n;col++)
        {
            int a=row*(n+1)+col,b=a+1,c=a+n+1,d=c+1,k=(row*n+col)*6;
            triangles[k]=a;triangles[k+1]=c;triangles[k+2]=b;triangles[k+3]=b;triangles[k+4]=c;triangles[k+5]=d;
        }
        // Separate reversed faces give the underside its own inward-facing normals.
        // This covers hat brims, open crowns and the hood without changing materials.
        int vertexCount=vertices.Length,indexCount=triangles.Length;
        var linedVertices=new Vector3[vertexCount*2];
        var linedTriangles=new int[indexCount*2];
        System.Array.Copy(vertices,0,linedVertices,0,vertexCount);
        System.Array.Copy(vertices,0,linedVertices,vertexCount,vertexCount);
        System.Array.Copy(triangles,linedTriangles,indexCount);
        for(int i=0;i<indexCount;i+=3)
        {
            linedTriangles[indexCount+i]=triangles[i]+vertexCount;
            linedTriangles[indexCount+i+1]=triangles[i+2]+vertexCount;
            linedTriangles[indexCount+i+2]=triangles[i+1]+vertexCount;
        }
        MeshPart(slot,name,OwnMesh(linedVertices,linedTriangles),Vector3.zero,material);
    }

    private Transform Ring(int slot,string name,Vector3 center,float radius,float thickness,Quaternion rotation,Material material)
    {
        const int n=32,m=8;var vertices=new Vector3[(n+1)*(m+1)];var triangles=new int[n*m*6];
        for(int i=0;i<=n;i++)for(int j=0;j<=m;j++)
        {
            float a=i*Mathf.PI*2/n,b=j*Mathf.PI*2/m,r=radius+Mathf.Cos(b)*thickness;
            vertices[i*(m+1)+j]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(b)*thickness,Mathf.Sin(a)*r);
        }
        for(int i=0;i<n;i++)for(int j=0;j<m;j++)
        {
            int a=i*(m+1)+j,b=a+1,c=a+m+1,d=c+1,k=(i*m+j)*6;
            triangles[k]=a;triangles[k+1]=b;triangles[k+2]=c;triangles[k+3]=b;triangles[k+4]=d;triangles[k+5]=c;
        }
        MeshPart(slot,name,OwnMesh(vertices,triangles),center,material);
        var part=slots[slot].transform.GetChild(slots[slot].transform.childCount-1);part.localRotation=rotation;return part;
    }

    private Mesh OwnMesh(Vector3[] vertices,int[] triangles)
    {
        var mesh=new Mesh {name="Cosmetic shaped mesh",vertices=vertices,triangles=triangles};
        mesh.RecalculateNormals();mesh.RecalculateBounds();extraMeshes.Add(mesh);return mesh;
    }

    private static Material Material(string name, Color color)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material=new Material(shader) { name=name };
        material.color=color;
        if(material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",.05f);
        return material;
    }

    private void Part(int slot,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Quaternion rotation=default)
    {
        var go=GameObject.CreatePrimitive(type);
        var collider=go.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
        go.name=name; go.transform.SetParent(slots[slot].transform,false);
        go.transform.localPosition=position; go.transform.localScale=scale;
        go.transform.localRotation=rotation==default?Quaternion.identity:rotation;
        go.GetComponent<MeshRenderer>().sharedMaterial=material;
    }

    private void MeshPart(int slot,string name,Mesh mesh,Vector3 position,Material material)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
        go.transform.SetParent(slots[slot].transform,false); go.transform.localPosition=position;
        go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<MeshRenderer>().sharedMaterial=material;
    }

    // Center fan works for the star and tie, whose outlines are visible from their centers.
    private static Mesh Extrude(Vector2[] outline,float depth)
    {
        int n=outline.Length; var vertices=new Vector3[n*2+2];
        var triangles=new int[n*12];
        for(int i=0;i<n;i++) { vertices[i]=new Vector3(outline[i].x,outline[i].y,depth); vertices[n+i]=new Vector3(outline[i].x,outline[i].y,0); }
        vertices[n*2]=new Vector3(0,0,depth); vertices[n*2+1]=Vector3.zero;
        for(int i=0;i<n;i++)
        {
            int j=(i+1)%n,k=i*12;
            triangles[k]=n*2;triangles[k+1]=i;triangles[k+2]=j;
            triangles[k+3]=n*2+1;triangles[k+4]=n+j;triangles[k+5]=n+i;
            triangles[k+6]=i;triangles[k+7]=n+i;triangles[k+8]=n+j;
            triangles[k+9]=i;triangles[k+10]=n+j;triangles[k+11]=j;
        }
        var mesh=new Mesh { name="Cosmetic silhouette" }; mesh.vertices=vertices;mesh.triangles=triangles;
        mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }

    private void OnDestroy()
    {
        Destroy(dark);Destroy(red);Destroy(gold);Destroy(white);Destroy(tieMesh);Destroy(starMesh);
        Destroy(olive);Destroy(khaki);Destroy(blue);Destroy(brown);Destroy(silver);
        Destroy(camoGreen);Destroy(camoSand);
        foreach(var mesh in extraMeshes)Destroy(mesh);
    }
}

