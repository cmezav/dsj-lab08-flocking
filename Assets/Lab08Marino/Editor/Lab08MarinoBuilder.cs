using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class Lab08MarinoBuilder
{
    const string Base = "Assets/Lab08Marino/";
    const string Output = Base + "Generado/";
    static Transform root;
    static Terrain terrain;
    static int layer;

    [MenuItem("Lab08/Montar fondo marino decorado")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Sal de Play primero."); return; }
        if (GameObject.Find("FondoMarinoDecorado") != null) {
            Debug.LogWarning("El fondo ya existe. No se ha duplicado."); return;
        }
        if (Selection.activeGameObject == null || !Selection.activeGameObject.TryGetComponent<Terrain>(out var oldTerrain)) {
            Debug.LogWarning("Selecciona tu Terrain en Hierarchy y ejecuta el menu otra vez."); return;
        }
        Mesh rock = LoadMesh("rock_large");
        Mesh smallRock = LoadMesh("rock_medium");
        if (rock == null || smallRock == null) { Debug.LogError("Faltan modelos. Copia la carpeta Assets/Lab08Marino completa."); return; }
        if (!AssetDatabase.IsValidFolder(Output.TrimEnd('/'))) AssetDatabase.CreateFolder(Base.TrimEnd('/'), "Generado");
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Montar fondo marino");
        root = new GameObject("FondoMarinoDecorado").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Crear fondo");
        layer = LayerMask.NameToLayer("Obstacle");
        if (layer < 0) layer = oldTerrain.gameObject.layer;
        var oldRandom = UnityEngine.Random.state;
        UnityEngine.Random.InitState(816);
        try {
            CreateTerrain(oldTerrain);
            Material rockMat = Material("RocaPizarra", new Color(.27f,.37f,.40f));
            Material lightRock = Material("RocaClara", new Color(.44f,.53f,.51f));
            Material kelp = Material("AlgaVerde", new Color(.08f,.39f,.29f));
            Material kelpLight = Material("AlgaLima", new Color(.25f,.48f,.26f));
            Material coralPink = Material("CoralRosa", new Color(.76f,.31f,.42f));
            Material coralOrange = Material("CoralNaranja", new Color(.86f,.48f,.28f));
            PlaceRock(rock, rockMat, "Roca_Alargada_Principal", 12, 0, new Vector3(11,6.2f,4), -18);
            PlaceRock(rock, lightRock, "Arrecife_Izquierdo", -19, 12, new Vector3(8,6,6), 25);
            PlaceRock(rock, rockMat, "Arrecife_Fondo", 7, 27, new Vector3(10,5,7), 65);
            PlaceRock(rock, lightRock, "Roca_Frontal", -11,-12,new Vector3(6,2.4f,3.5f), -30);
            for (int i=0;i<20;i++) {
                float angle=i*2.39996f;
                float radius=UnityEngine.Random.Range(16,34);
                float x=Mathf.Cos(angle)*radius, z=Mathf.Sin(angle)*radius;
                float scale=UnityEngine.Random.Range(1,3);
                PlaceRock(smallRock, i%2==0?rockMat:lightRock, "Piedra_"+i,x,z,new Vector3(scale*1.7f,scale*.65f,scale),i*37);
            }
            for (int i=0;i<34;i++) {
                float a=i*2.39996f;
                float r=UnityEngine.Random.Range(18,37);
                float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r;
                var plant=new GameObject("Grupo_Algas_"+i).transform; plant.SetParent(root);
                plant.position=new Vector3(x,Surface(x,z),z);
                for(int j=0;j<4;j++) {
                    float h=UnityEngine.Random.Range(1.3f,3.8f);
                    var blade=new GameObject("Hoja"); blade.transform.SetParent(plant,false);
                    blade.transform.localPosition=new Vector3(UnityEngine.Random.Range(-.45f,.45f),0,UnityEngine.Random.Range(-.45f,.45f));
                    blade.transform.localRotation=Quaternion.Euler(0,UnityEngine.Random.Range(0,360),0);
                    var mf=blade.AddComponent<MeshFilter>(); mf.sharedMesh=Blade(h,UnityEngine.Random.Range(.15f,.32f));
                    blade.AddComponent<MeshRenderer>().sharedMaterial=j%2==0?kelp:kelpLight;
                }
            }
            for(int i=0;i<12;i++) {
                float a=i*2.39996f; float r=UnityEngine.Random.Range(15,29);
                float x=Mathf.Cos(a)*r,z=Mathf.Sin(a)*r;
                Vector3 p=new Vector3(x,Surface(x,z),z);
                Material m=i%2==0?coralPink:coralOrange;
                float h=UnityEngine.Random.Range(.8f,1.7f);
                var group=new GameObject("Coral_"+i).transform; group.SetParent(root);
                Branch(group,p,p+Vector3.up*h,.12f,m);
                for(int j=0;j<4;j++) {
                    float t=j*1.8f;Vector3 start=p+Vector3.up*h*(.3f+j*.13f);
                    Vector3 end=start+new Vector3(Mathf.Cos(t)*h*.55f,h*.5f,Mathf.Sin(t)*h*.55f);
                    Branch(group,start,end,.07f,m);
                    Branch(group,end,end+new Vector3(.1f,h*.28f,.05f),.05f,m);
                }
            }
            Undo.RecordObject(oldTerrain.gameObject,"Ocultar terreno anterior"); oldTerrain.gameObject.SetActive(false);
            GameObject cube=GameObject.Find("Roca_Prueba");
            if(cube!=null) { Undo.RecordObject(cube,"Ocultar cubo anterior");cube.SetActive(false); }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(oldTerrain.gameObject.scene);
            Selection.activeGameObject=root.gameObject;
            Debug.Log("Fondo listo. Guarda la escena con Ctrl+S. Peces y camaras conservados. Obstaculos en capa: "+LayerMask.LayerToName(layer));
        }
        finally { UnityEngine.Random.state=oldRandom; Undo.CollapseUndoOperations(undoGroup); }
    }

    static Mesh LoadMesh(string name) {
        foreach(var a in AssetDatabase.LoadAllAssetsAtPath(Base+"Models/"+name+".obj")) if(a is Mesh mesh) return mesh;
        return null;
    }
    static Material Material(string name,Color c) {
        string path=Output+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m!=null)return m;
        m=new Material(Shader.Find("Standard"));m.color=c;m.SetFloat("_Glossiness",.12f);
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static void CreateTerrain(Terrain old) {
        var data=new TerrainData {heightmapResolution=257,size=new Vector3(100,12,100)};
        float[,] heights=new float[257,257];
        for(int z=0;z<257;z++)for(int x=0;x<257;x++) {
            float wx=x/256f*100-50,wz=z/256f*100-50;
            float radius=new Vector2(wx,wz).magnitude;
            float rim=Mathf.SmoothStep(0,1,Mathf.InverseLerp(18,46,radius));
            float n=Mathf.PerlinNoise(wx*.065f+23,wz*.065f+71);
            float ripple=.08f*(1+Mathf.Sin(wx*1.2f+Mathf.Sin(wz*.17f)));
            heights[z,x]=(.15f+ripple+rim*(1.2f+4.0f*n))/12f;
        }
        data.SetHeights(0,0,heights);
        var texture=new Texture2D(128,128,TextureFormat.RGB24,false);
        for(int y=0;y<128;y++)for(int x=0;x<128;x++) {
            float v=.5f+.5f*Mathf.Sin(2*Mathf.PI*y/16f+.32f*Mathf.Sin(2*Mathf.PI*x/128f));
            texture.SetPixel(x,y,Color.Lerp(new Color(.55f,.49f,.32f),new Color(.76f,.70f,.49f),v));
        }
        texture.Apply();texture.wrapMode=TextureWrapMode.Repeat;
        string id=Guid.NewGuid().ToString("N").Substring(0,8);
        AssetDatabase.CreateAsset(texture,Output+"Arena_"+id+".asset");
        var tl=new TerrainLayer {diffuseTexture=texture,tileSize=new Vector2(10,10)};
        AssetDatabase.CreateAsset(tl,Output+"CapaArena_"+id+".terrainlayer");
        data.terrainLayers=new[]{tl};data.alphamapResolution=128;
        float[,,] alpha=new float[128,128,1];for(int z=0;z<128;z++)for(int x=0;x<128;x++)alpha[z,x,0]=1;
        data.SetAlphamaps(0,0,alpha);
        AssetDatabase.CreateAsset(data,Output+"Relieve_"+id+".asset");
        var go=Terrain.CreateTerrainGameObject(data);go.name="Suelo_Arena_Dunas";go.transform.SetParent(root);
        go.transform.position=new Vector3(-50,old.transform.position.y,-50);go.layer=layer;
        terrain=go.GetComponent<Terrain>();terrain.drawInstanced=true;
    }
    static float Surface(float x,float z) { return terrain.SampleHeight(new Vector3(x,0,z))+terrain.transform.position.y; }
    static void PlaceRock(Mesh mesh,Material m,string name,float x,float z,Vector3 size,float yaw) {
        var go=new GameObject(name);go.transform.SetParent(root);go.layer=layer;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=m;
        Bounds b=mesh.bounds;
        Vector3 scale=new Vector3(size.x/Mathf.Max(b.size.x,.001f),size.y/Mathf.Max(b.size.y,.001f),size.z/Mathf.Max(b.size.z,.001f));
        go.transform.localScale=scale;go.transform.rotation=Quaternion.Euler(0,yaw,0);
        Vector3 offset=new Vector3(b.center.x*scale.x,b.min.y*scale.y,b.center.z*scale.z);
        go.transform.position=new Vector3(x,Surface(x,z)-.12f,z)-go.transform.rotation*offset;
        go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    static Mesh Blade(float height,float width) {
        // Cinta cruzada con caras en ambas direcciones, sin shaders especiales.
        var vertices=new System.Collections.Generic.List<Vector3>();var tris=new System.Collections.Generic.List<int>();
        for(int axis=0;axis<2;axis++)for(int s=0;s<5;s++) {
            float t=s/5f,t2=(s+1)/5f;
            Vector3 p=new Vector3(Mathf.Sin(t*3)*.25f,height*t,0);
            Vector3 q=new Vector3(Mathf.Sin(t2*3)*.25f,height*t2,0);
            Vector3 w=axis==0?Vector3.right:Vector3.forward;
            int k=vertices.Count;
            vertices.Add(p-w*width*(1-t));vertices.Add(p+w*width*(1-t));vertices.Add(q-w*width*(1-t2));vertices.Add(q+w*width*(1-t2));
            tris.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3,k+1,k+2,k,k+3,k+2,k+1});
        }
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Output+"Alga_"+Guid.NewGuid().ToString("N").Substring(0,8)+".asset");return mesh;
    }
    static void Branch(Transform parent,Vector3 a,Vector3 b,float radius,Material mat) {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Rama";go.transform.SetParent(parent);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.position=(a+b)*.5f;go.transform.up=(b-a).normalized;
        go.transform.localScale=new Vector3(radius*2,Vector3.Distance(a,b)*.5f,radius*2);
        go.GetComponent<Renderer>().sharedMaterial=mat;
    }
}
