using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public class Lab08DiverExtras : MonoBehaviour
{
    public Flock flock;
    [Header("Lanzamiento: boton derecho / E y rueda")]
    public float launchSpeed = 7f;
    public float minLaunchSpeed = 3f;
    public float maxLaunchSpeed = 15f;
    public float launchGravity = 4f;
    public float scrollSensitivity = 3f;
    public float cooldown = 1f;
    [Header("Alimentacion")]
    public float foodDuration = 9f;
    public float attractionRadius = 24f;
    public float foodWeight = 3f;
    public float foodSpread = 2.5f;
    public int pelletCount = 24;
    public float eatDistance = .8f;
    [Header("Espacio entre peces al comer")]
    public float feedingSpacing = 1.4f;
    public float feedingSeparationWeight = 5f;
    [Header("Manos")]
    public bool showHands = true;
    [Range(.5f,1.5f)] public float handScale = 1f;

    static readonly List<Lab08DiverExtras> sources = new List<Lab08DiverExtras>();
    CameraController diver;
    Camera view;
    GameObject hands;
    Transform leftHand,rightHand;
    Material sleeveMat,gloveMat,cuffMat,foodMat,previewMat;
    GameObject foodRoot,flightVisual,aimMarker;
    LineRenderer arc;
    readonly List<Transform> pellets=new List<Transform>();
    readonly Dictionary<FlockUnit,int> assignedPellets=new Dictionary<FlockUnit,int>();
    int nextAssignment;
    Vector3 foodPoint,flightOrigin,flightVelocity,flightLanding;
    float foodUntil,nextFeed,feedGestureUntil,flightStarted,flightDuration;
    bool foodActive,inFlight,aiming;
    int eaten;
    string message="";
    float messageUntil;
    GUIStyle titleStyle,bodyStyle;
    Vector3 Gravity {get {return Vector3.down*Mathf.Max(.1f,launchGravity);}}
    bool IsDiverMode {get {return diver==null || diver.enabled;}}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSources(){sources.Clear();}
    void OnEnable(){if(!sources.Contains(this))sources.Add(this);}
    void Start()
    {
        view=GetComponent<Camera>();diver=GetComponent<CameraController>();
        if(flock==null)flock=FindAnyObjectByType<Flock>();
        sleeveMat=MakeMaterial("Neopreno azul",new Color(.055f,.13f,.20f));
        gloveMat=MakeMaterial("Guantes",new Color(.18f,.24f,.27f));
        cuffMat=MakeMaterial("Punos turquesa",new Color(.05f,.66f,.61f));
        foodMat=MakeMaterial("Comida dorada",new Color(1f,.73f,.22f));
        previewMat=MakeMaterial("Arco",new Color(.1f,1f,.8f));
        CreateHands();
        var line=new GameObject("ArcoComida_Runtime");arc=line.AddComponent<LineRenderer>();
        arc.sharedMaterial=previewMat;arc.useWorldSpace=true;arc.startWidth=.035f;arc.endWidth=.018f;
        arc.shadowCastingMode=ShadowCastingMode.Off;arc.receiveShadows=false;arc.enabled=false;
        aimMarker=Primitive(null,"DestinoComida_Runtime",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.22f,previewMat);
        aimMarker.SetActive(false);
    }
    void Update()
    {
        if(foodActive && Time.time>=foodUntil){ClearFood();Notify("El alimento restante se disolvio.");}
        if(inFlight){
            float t=Mathf.Min(Time.time-flightStarted,flightDuration);
            if(flightVisual!=null)flightVisual.transform.position=flightOrigin+flightVelocity*t+Gravity*(.5f*t*t);
            if(t>=flightDuration){
                inFlight=false;if(flightVisual!=null)Destroy(flightVisual);flightVisual=null;
                ScatterFood(flightLanding);
            }
        }
        if(!IsDiverMode){aiming=false;return;}
        launchSpeed=Mathf.Clamp(launchSpeed+Input.mouseScrollDelta.y*scrollSensitivity,minLaunchSpeed,maxLaunchSpeed);
        aiming=Input.GetMouseButton(1);
        if((Input.GetMouseButtonUp(1) || Input.GetKeyDown(KeyCode.E)) && Time.time>=nextFeed)Launch();
    }
    void LateUpdate()
    {
        if(arc!=null){
            bool visible=IsDiverMode && aiming;
            arc.enabled=visible;aimMarker.SetActive(visible);
            if(visible){
                var points=new List<Vector3>();Vector3 origin,velocity,end;float duration;
                bool valid=Trace(out origin,out velocity,out end,out duration,points);
                arc.positionCount=points.Count;arc.SetPositions(points.ToArray());
                aimMarker.transform.position=end;
                previewMat.color=valid?new Color(.1f,1f,.8f):new Color(1f,.4f,.15f);
            }
        }
        if(hands==null)return;
        hands.SetActive(showHands && IsDiverMode);
        if(!hands.activeSelf)return;
        bool swimming=Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.D);
        float amount=swimming?1f:.12f;
        float stroke=Mathf.Sin(Time.time*(swimming?3.2f:1.3f));
        float halfWidth=Mathf.Tan(view.fieldOfView*Mathf.Deg2Rad*.5f)*.62f*view.aspect;
        float x=Mathf.Min(.30f,halfWidth*.65f),z=Mathf.Max(.62f,view.nearClipPlane+.25f);
        hands.transform.localScale=Vector3.one*handScale;
        leftHand.localPosition=new Vector3(-x,-.25f+stroke*.035f*amount,z+stroke*.035f*amount);
        rightHand.localPosition=new Vector3(x,-.25f-stroke*.035f*amount,z-stroke*.035f*amount);
        leftHand.localRotation=Quaternion.Euler(12f+stroke*12f*amount,-12,-8);
        rightHand.localRotation=Quaternion.Euler(12f-stroke*12f*amount,12,8);
        if(aiming)rightHand.localPosition+=new Vector3(-.04f,.04f,.07f);
        if(Time.time<feedGestureUntil){
            float t=1-(feedGestureUntil-Time.time)/.65f,gesture=Mathf.Sin(t*Mathf.PI);
            rightHand.localPosition+=new Vector3(-.08f,.05f,.10f)*gesture;
            rightHand.localRotation*=Quaternion.Euler(-25*gesture,0,0);
        }
    }

    bool Trace(out Vector3 origin,out Vector3 velocity,out Vector3 end,out float duration,List<Vector3> points=null)
    {
        origin=transform.position+transform.forward*.65f;
        velocity=transform.forward*launchSpeed;
        end=origin;duration=5f;
        if(points!=null)points.Add(origin);
        // El arco usa la misma formula que el paquete durante el vuelo.
        if(Physics.CheckSphere(origin,.12f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return false;
        Vector3 previous=origin;
        for(int i=1;i<=100;i++){
            float t=i*.05f;
            Vector3 p=origin+velocity*t+Gravity*(.5f*t*t);
            Vector3 delta=p-previous;
            if(delta.sqrMagnitude>.000001f && Physics.SphereCast(previous,.12f,delta.normalized,out RaycastHit hit,
                delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)){
                end=hit.point+hit.normal*.4f;
                float fraction=Mathf.Clamp01(hit.distance/delta.magnitude);
                duration=(i-1+fraction)*.05f;
                if(points!=null)points.Add(end);
                return InFlockArea(end);
            }
            previous=p;end=p;if(points!=null)points.Add(p);
        }
        return false;
    }
    bool InFlockArea(Vector3 point)
    {
        return flock!=null && Vector3.Distance(point,flock.transform.position)<=Mathf.Max(3,flock.boundsDistance*.85f);
    }
    void Launch()
    {
        if(flock==null || flock.allUnits==null || flock.allUnits.Length==0){Notify("Asigna Flock y espera a que aparezcan los peces.");return;}
        Vector3 origin,velocity,end;float duration;
        if(!Trace(out origin,out velocity,out end,out duration)){
            Notify("Arco naranja: acorta con la rueda o apunta al fondo cerca del cardumen.");return;
        }
        ClearFood();
        inFlight=true;flightOrigin=origin;flightVelocity=velocity;flightLanding=end;
        flightDuration=duration;flightStarted=Time.time;nextFeed=Time.time+cooldown;feedGestureUntil=Time.time+.65f;
        flightVisual=Primitive(null,"PaqueteComida_Runtime",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.20f,foodMat);
        flightVisual.transform.position=origin;
        Notify("Comida lanzada.");
    }
    void ScatterFood(Vector3 center)
    {
        foodPoint=center;foodRoot=new GameObject("ComidaTemporal");
        eaten=0;nextAssignment=0;assignedPellets.Clear();
        int count=Mathf.Clamp(pelletCount,4,60);
        for(int i=0;i<count;i++){
            float angle=i*2.39996f,r=Mathf.Sqrt((i+.5f)/count)*Mathf.Max(.5f,foodSpread);
            Vector3 p=center+new Vector3(Mathf.Cos(angle)*r,.15f+(i%3)*.18f,Mathf.Sin(angle)*r);
            foreach(Terrain t in Terrain.activeTerrains){
                Vector3 local=p-t.transform.position;
                if(local.x>=0 && local.z>=0 && local.x<=t.terrainData.size.x && local.z<=t.terrainData.size.z)
                    p.y=Mathf.Max(p.y,t.SampleHeight(p)+t.transform.position.y+.55f);
            }
            if(Physics.CheckSphere(p,.1f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
            var pellet=Primitive(foodRoot.transform,"Granulo",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.13f,foodMat);
            pellet.transform.position=p;pellets.Add(pellet.transform);
        }
        foodActive=pellets.Count>0;foodUntil=Time.time+foodDuration;
        if(!foodActive){ClearFood();Notify("No hubo espacio libre para los granulos. Prueba sobre arena.");}
        else Notify("Comida repartida: los peces consumen cada granulo al acercarse.");
    }
    int LivePellets(){int n=0;foreach(var p in pellets)if(p!=null)n++;return n;}
    void EatNearby(Vector3 p)
    {
        float radius=Mathf.Max(.1f,eatDistance);
        // Consumo real de hasta un granulo por pez y frame, incluso si no era su objetivo.
        for(int i=0;i<pellets.Count;i++){
            Transform pellet=pellets[i];
            if(pellet!=null && (pellet.position-p).sqrMagnitude<=radius*radius){
                pellets[i]=null;Destroy(pellet.gameObject);eaten++;
                if(LivePellets()==0){ClearFood();Notify("Los peces terminaron la comida.");}
                return;
            }
        }
    }
    Vector3 FoodForce(FlockUnit fish,Vector3 p)
    {
        if(!foodActive || Time.time>=foodUntil || (p-foodPoint).sqrMagnitude>attractionRadius*attractionRadius)return Vector3.zero;
        EatNearby(p);if(!foodActive)return Vector3.zero;
        int slot;
        if(!assignedPellets.TryGetValue(fish,out slot)){slot=nextAssignment++%pellets.Count;assignedPellets[fish]=slot;}
        if(slot>=pellets.Count || pellets[slot]==null){
            slot=-1;float best=float.PositiveInfinity;
            for(int i=0;i<pellets.Count;i++)if(pellets[i]!=null){
                float d=(pellets[i].position-p).sqrMagnitude;if(d<best){best=d;slot=i;}
            }
            if(slot<0)return Vector3.zero;assignedPellets[fish]=slot;
        }
        Vector3 delta=pellets[slot].position-p;
        return delta.normalized*foodWeight*Mathf.Clamp01(delta.magnitude/2f);
    }
    public static Vector3 GetFoodForce(FlockUnit fish,Vector3 position)
    {
        Vector3 force=Vector3.zero;
        foreach(var s in sources)if(s!=null && s.isActiveAndEnabled)force+=s.FoodForce(fish,position);
        return force;
    }
    public static Vector3 GetFeedingSeparation(Vector3 position,List<FlockUnit> neighbors)
    {
        Vector3 force=Vector3.zero;
        foreach(var s in sources){
            if(s==null || !s.foodActive || Time.time>=s.foodUntil || !s.isActiveAndEnabled)continue;
            float range=s.foodSpread+6f;
            if((position-s.foodPoint).sqrMagnitude>range*range)continue;
            float spacing=Mathf.Max(.2f,s.feedingSpacing);
            Vector3 local=Vector3.zero;
            foreach(var other in neighbors){
                if(other==null)continue;
                Vector3 away=position-other.transform.position;
                float d=away.magnitude;
                if(d>.001f && d<spacing)local+=away/d*(1-d/spacing);
            }
            force+=Vector3.ClampMagnitude(local,1.5f)*s.feedingSeparationWeight;
        }
        return force;
    }
    void CreateHands()
    {
        hands=new GameObject("ManosBuzo_Runtime");hands.transform.SetParent(transform,false);
        leftHand=CreateHand("Izquierda",-1);
        rightHand=CreateHand("Derecha",1);
    }
    Transform CreateHand(string name,int side)
    {
        var pivot=new GameObject(name).transform;pivot.SetParent(hands.transform,false);
        var arm=Primitive(pivot,"Antebrazo",PrimitiveType.Capsule,new Vector3(0,-.075f,-.14f),new Vector3(.095f,.16f,.095f),sleeveMat);
        arm.transform.localRotation=Quaternion.Euler(65,0,0);
        Primitive(pivot,"Puno",PrimitiveType.Cube,new Vector3(0,-.012f,-.025f),new Vector3(.105f,.065f,.065f),cuffMat);
        Primitive(pivot,"Palma",PrimitiveType.Sphere,new Vector3(0,0,.035f),new Vector3(.11f,.055f,.14f),gloveMat);
        for(int i=0;i<4;i++) {
            var finger=Primitive(pivot,"Dedo",PrimitiveType.Capsule,new Vector3((i-1.5f)*.024f,0,.12f),new Vector3(.024f,.045f,.025f),gloveMat);
            finger.transform.localRotation=Quaternion.Euler(90,0,0);
        }
        var thumb=Primitive(pivot,"Pulgar",PrimitiveType.Capsule,new Vector3(-side*.065f,-.005f,.045f),new Vector3(.032f,.04f,.032f),gloveMat);
        thumb.transform.localRotation=Quaternion.Euler(65,0,side*40);
        return pivot;
    }
    static GameObject Primitive(Transform parent,string name,PrimitiveType type,Vector3 local,Vector3 scale,Material mat)
    {
        GameObject go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=local;go.transform.localScale=scale;
        Collider collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=mat;
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        return go;
    }
    static Material MakeMaterial(string name,Color color)
    {
        Shader shader=Shader.Find("Unlit/Color");
        if(shader == null) shader=Shader.Find("Standard");
        var mat=new Material(shader){name=name,color=color};return mat;
    }
    void Notify(string text){message=text;messageUntil=Time.time+3f;}
    void ClearFood()
    {
        foodActive=false;inFlight=false;assignedPellets.Clear();pellets.Clear();
        if(foodRoot!=null)Destroy(foodRoot);foodRoot=null;
        if(flightVisual!=null)Destroy(flightVisual);flightVisual=null;
    }
    void OnDisable()
    {
        sources.Remove(this);ClearFood();if(hands!=null)hands.SetActive(false);
        if(arc!=null)arc.enabled=false;if(aimMarker!=null)aimMarker.SetActive(false);
    }
    void OnDestroy()
    {
        if(hands!=null)Destroy(hands);if(arc!=null)Destroy(arc.gameObject);if(aimMarker!=null)Destroy(aimMarker);
        foreach(var m in new[]{sleeveMat,gloveMat,cuffMat,foodMat,previewMat})if(m!=null)Destroy(m);
    }
    void OnGUI()
    {
        if(titleStyle==null){
            titleStyle=new GUIStyle(GUI.skin.label){fontSize=15,fontStyle=FontStyle.Bold};
            bodyStyle=new GUIStyle(GUI.skin.label){fontSize=13,wordWrap=true};
        }
        float width=Mathf.Min(430,Screen.width-24);
        GUI.Box(new Rect(12,12,width,112),GUIContent.none);
        GUI.Label(new Rect(24,19,width-24,24),IsDiverMode?"BUZO - Lanzamiento de comida":"VISTA DEL PEZ - F para volver",titleStyle);
        GUI.Label(new Rect(24,46,width-24,36),"Derecho: apuntar / soltar: lanzar | Rueda: alcance | E: lanzar\nFuerza: "+launchSpeed.ToString("0.0"),bodyStyle);
        string text=Time.time<messageUntil?message:(foodActive?"Granulos restantes: "+LivePellets()+" | Comidos: "+eaten:"Inclina la vista para cambiar el angulo. F: vista del pez.");
        GUI.Label(new Rect(24,85,width-24,36),text,bodyStyle);
    }
}
