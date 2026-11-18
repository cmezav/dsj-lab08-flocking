using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class Lab08DiverExtrasSetup
{
    [MenuItem("Lab08/Agregar alimentacion y manos de buzo")]
    static void Setup()
    {
        if(EditorApplication.isPlaying) {Debug.LogWarning("Sal de Play antes de configurar.");return;}
        Camera camera=Camera.main;
        if(camera==null) {Debug.LogError("No hay camara activa con Tag MainCamera.");return;}
        var flock=Object.FindAnyObjectByType<Flock>();
        if(flock==null) {Debug.LogError("Abre la escena del cardumen: no se encontro Flock.");return;}
        var component=camera.GetComponent<Lab08DiverExtras>();
        if(component==null)component=Undo.AddComponent<Lab08DiverExtras>(camera.gameObject);
        Undo.RecordObject(component,"Configurar buzo");component.flock=flock;
        component.foodWeight=3f; component.foodDuration=9f; component.cooldown=1f;
        component.foodSpread=2.5f; component.eatDistance=1f; component.feedingSpacing=1.4f;
        component.feedingSeparationWeight=5f;
        EditorUtility.SetDirty(component);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        Selection.activeGameObject=camera.gameObject;
        Debug.Log("Listo. Guarda con Ctrl+S. En Play: boton derecho apunta, rueda cambia alcance, soltar lanza. E tambien lanza.");
    }
}
