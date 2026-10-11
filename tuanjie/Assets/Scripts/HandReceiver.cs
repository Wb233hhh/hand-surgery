using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class HandReceiver : MonoBehaviour
{
    const int Port = 5005;

    UdpClient client;
    Thread thread;
    volatile bool running;
    readonly object gate = new object();
    float fingerX = 0.5f;
    float fingerY = 0.5f;
    float pinch = 0.4f;
    bool hasPacket;
    bool areaReady;
    Vector3 areaMin;
    Vector3 areaMax;

    void Start()
    {
        var patientObject = GameObject.Find("Patient");
        if (patientObject != null)
        {
            var bounds = CombinedBounds(patientObject);
            areaMin = bounds.min - new Vector3(0.04f, 0f, 0.04f);
            areaMax = bounds.max + new Vector3(0.04f, 0f, 0.04f);
            areaReady = true;
        }

        try
        {
            client = new UdpClient(Port);
            running = true;
            thread = new Thread(Listen) { IsBackground = true };
            thread.Start();
            Debug.Log("[HandReceiver] Listening on 127.0.0.1:" + Port);
        }
        catch (Exception error)
        {
            Debug.LogError("[HandReceiver] Could not listen on port " + Port + ". " + error.Message);
        }
    }

    void Listen()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (running)
        {
            try
            {
                byte[] data = client.Receive(ref remote);
                string text = Encoding.ASCII.GetString(data).Trim();
                string[] parts = text.Split(',');
                if (parts.Length < 3)
                    continue;
                if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
                    continue;
                if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    continue;
                if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float p))
                    continue;
                if (x < 0f) x = 0f;
                else if (x > 1f) x = 1f;
                if (y < 0f) y = 0f;
                else if (y > 1f) y = 1f;
                lock (gate)
                {
                    fingerX = x;
                    fingerY = y;
                    pinch = p;
                    hasPacket = true;
                }
            }
            catch (SocketException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    void Update()
    {
        if (!hasPacket || !areaReady)
            return;

        float x;
        float y;
        float p;
        lock (gate)
        {
            x = fingerX;
            y = fingerY;
            p = pinch;
        }

        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds tool = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            tool.Encapsulate(renderers[i].bounds);

        float desiredX = Mathf.Lerp(areaMin.x, areaMax.x, x);
        float desiredZ = Mathf.Lerp(areaMin.z, areaMax.z, y);
        Vector3 desired = transform.position + new Vector3(desiredX - tool.center.x, 0f, desiredZ - tool.center.z);
        float follow = 1f - Mathf.Exp(-14f * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, follow);

        float open = Mathf.InverseLerp(0.12f, 0.85f, p);
        float gap = Mathf.Lerp(0.05f, 0.28f, Mathf.Clamp01(open));
        Vector3 scale = transform.localScale;
        scale.y = Mathf.Lerp(scale.y, gap, follow);
        transform.localScale = scale;
    }

    static Bounds CombinedBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    void OnDestroy()
    {
        running = false;
        if (client != null)
            client.Close();
    }
}
