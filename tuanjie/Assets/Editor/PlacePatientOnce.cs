using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlacePatientOnce
{
    const string Marker = "Assets/Editor/patient-supine.txt";

    static PlacePatientOnce()
    {
        if (System.IO.File.Exists(Marker))
            return;
        EditorApplication.delayCall += () => EditorApplication.delayCall += Adjust;
    }

    [MenuItem("Tools/Lay Patient Face Up")]
    static void Menu()
    {
        if (System.IO.File.Exists(Marker))
            System.IO.File.Delete(Marker);
        Adjust();
    }

    static void Adjust()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (System.IO.File.Exists(Marker))
            return;

        Transform patient = null;
        foreach (var renderer in Object.FindObjectsOfType<Renderer>())
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;
            patient = renderer.transform.root;
            break;
        }

        if (patient == null)
        {
            Debug.LogWarning("[PlacePatient] No model in the open scene yet.");
            return;
        }

        patient.localScale = Vector3.one;
        patient.name = "Patient";
        patient.rotation = Quaternion.Euler(-90f, 0f, 0f);
        patient.position = Vector3.zero;

        var bounds = WorldBounds(patient);
        patient.position = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
        bounds = WorldBounds(patient);

        var camera = Camera.main;
        if (camera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<AudioListener>();
        }

        float span = Mathf.Max(bounds.size.x, bounds.size.z);
        float distance = span * 0.6f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        distance = Mathf.Max(distance, bounds.size.y + 0.4f);
        camera.transform.position = bounds.center + Vector3.up * distance;
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = Mathf.Max(100f, distance * 8f);

        var light = Object.FindObjectOfType<Light>();
        if (light != null)
        {
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.1f;
        }

        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.orthographic = true;
            view.LookAt(bounds.center, Quaternion.Euler(90f, 0f, 0f), span * 0.65f);
            view.Repaint();
        }

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Surgery.unity");
        System.IO.File.WriteAllText(Marker, "supine");
        Debug.Log("[PlacePatient] Face up. euler " + patient.eulerAngles + " size " + bounds.size);
    }

    static Bounds WorldBounds(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}
