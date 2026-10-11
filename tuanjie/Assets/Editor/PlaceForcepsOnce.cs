using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlaceForcepsOnce
{
    const string Marker = "Assets/Editor/forceps-placed.txt";
    const string ModelPath = "Assets/Forceps/tripo_convert_307f47c4-957a-4f30-bd90-a405e7a31723.fbx";

    static PlaceForcepsOnce()
    {
        if (System.IO.File.Exists(Marker))
            return;
        EditorApplication.delayCall += () => EditorApplication.delayCall += Place;
    }

    [MenuItem("Tools/Place Forceps")]
    static void Menu()
    {
        if (System.IO.File.Exists(Marker))
            System.IO.File.Delete(Marker);
        Place();
    }

    static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (System.IO.File.Exists(Marker))
            return;

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (asset == null)
        {
            Debug.LogWarning("[PlaceForceps] FBX is not imported yet.");
            return;
        }

        Transform patient = null;
        foreach (var renderer in Object.FindObjectsOfType<Renderer>())
        {
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;
            if (renderer.transform.root.name == "Forceps")
                continue;
            patient = renderer.transform.root;
            break;
        }

        if (patient == null)
        {
            Debug.LogWarning("[PlaceForceps] Patient is not in the scene.");
            return;
        }

        var existing = GameObject.Find("Forceps");
        GameObject forcepsObject = existing != null
            ? existing
            : (GameObject)PrefabUtility.InstantiatePrefab(asset);
        forcepsObject.name = "Forceps";

        Transform forceps = forcepsObject.transform;
        forceps.localScale = Vector3.one * 0.2f;

        Quaternion best = Quaternion.Euler(90f, 0f, 0f);
        float bestScore = 0f;
        foreach (var rotation in Orientations())
        {
            forceps.rotation = rotation;
            forceps.position = Vector3.zero;
            float score = TipDownScore(forceps);
            if (score > bestScore)
            {
                bestScore = score;
                best = rotation;
            }
        }

        forceps.rotation = best;
        forceps.position = Vector3.zero;
        var toolBounds = WorldBounds(forceps);
        var bodyBounds = WorldBounds(patient);
        forceps.position = new Vector3(
            bodyBounds.center.x - toolBounds.center.x,
            bodyBounds.max.y + 0.03f - toolBounds.min.y,
            bodyBounds.center.z - toolBounds.center.z);

        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Surgery.unity");
        System.IO.File.WriteAllText(Marker, "placed");

        var view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            var framed = WorldBounds(patient);
            framed.Encapsulate(WorldBounds(forceps));
            view.orthographic = true;
            view.LookAt(framed.center, Quaternion.Euler(90f, 0f, 0f), Mathf.Max(framed.size.x, framed.size.z) * 0.7f);
            view.Repaint();
        }

        Debug.Log("[PlaceForceps] Tip down above the chest. euler " + forceps.eulerAngles + " score " + bestScore);
    }

    static System.Collections.Generic.IEnumerable<Quaternion> Orientations()
    {
        Vector3[] ups =
        {
            Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back
        };
        foreach (var up in ups)
        {
            Vector3 hint = Mathf.Abs(Vector3.Dot(up, Vector3.forward)) > 0.9f ? Vector3.right : Vector3.forward;
            Vector3 side = Vector3.Cross(up, hint).normalized;
            Vector3 forward = Vector3.Cross(side, up).normalized;
            for (int turn = 0; turn < 4; turn++)
            {
                Vector3 facing = Quaternion.AngleAxis(90f * turn, up) * forward;
                yield return Quaternion.LookRotation(facing, up);
            }
        }
    }

    static float TipDownScore(Transform root)
    {
        var bounds = WorldBounds(root);
        if (bounds.size.y + 0.001f < bounds.size.x || bounds.size.y + 0.001f < bounds.size.z)
            return -1000f;

        float low = 0f;
        float high = 0f;
        int lowCount = 0;
        int highCount = 0;
        float height = Mathf.Max(bounds.size.y, 0.0001f);

        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;
            Accumulate(filter.sharedMesh, filter.transform.localToWorldMatrix, bounds, height, ref low, ref high, ref lowCount, ref highCount);
        }

        if (lowCount == 0 || highCount == 0)
            return -1000f;

        return (high / highCount) - (low / lowCount);
    }

    static void Accumulate(Mesh mesh, Matrix4x4 localToWorld, Bounds bounds, float height, ref float low, ref float high, ref int lowCount, ref int highCount)
    {
        try
        {
            using (var dataArray = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var data = dataArray[0];
                using (var vertices = new NativeArray<Vector3>(data.vertexCount, Allocator.Temp))
                {
                    data.GetVertices(vertices);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 point = localToWorld.MultiplyPoint3x4(vertices[i]);
            float rel = (point.y - bounds.min.y) / height;
            float radius = Mathf.Sqrt(
                (point.x - bounds.center.x) * (point.x - bounds.center.x) +
                (point.z - bounds.center.z) * (point.z - bounds.center.z));
            if (rel < 0.2f)
            {
                low += radius;
                lowCount++;
            }
            else if (rel > 0.8f)
            {
                high += radius;
                highCount++;
            }
                    }
                }
            }
        }
        catch (System.Exception)
        {
            return;
        }
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
