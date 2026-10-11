using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class StartHandControlOnce
{
    const string Marker = "Assets/Editor/hand-control-started.txt";
    static int tries;

    static StartHandControlOnce()
    {
        if (File.Exists(Marker))
            return;
        EditorApplication.delayCall += () => EditorApplication.delayCall += Start;
    }

    static void Start()
    {
        if (File.Exists(Marker))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var forceps = GameObject.Find("Forceps");
        var receiverType = System.Type.GetType("HandReceiver, Assembly-CSharp");
        if (forceps == null || receiverType == null)
        {
            if (++tries < 40)
                EditorApplication.delayCall += Start;
            else
                Debug.LogWarning("[HandReceiver] Waiting for the forceps and the receiver script.");
            return;
        }

        if (forceps.GetComponent(receiverType) == null)
            forceps.AddComponent(receiverType);

        var scene = forceps.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Marker, "started");
        Debug.Log("[HandReceiver] Playing. Move your index finger in front of the camera.");
        EditorApplication.isPlaying = true;
    }
}
