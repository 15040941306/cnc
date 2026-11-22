using ReCnc.Unity.Assets;
using ReCnc.Unity.CameraControl;
using ReCnc.Unity.InputControl;
using UnityEngine;

namespace ReCnc.Unity.View
{
    public static class Ra2AutoBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureDemoRunningInActiveScene()
        {
            if (Object.FindObjectOfType<Ra2GameDirector>() != null)
            {
                return;
            }

            // 1. Ensure Main Camera has RTS 3D Controller
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.13f, 0.18f);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 300f;
            cam.fieldOfView = 42f;

            if (cam.GetComponent<RtsCamera3DController>() == null)
            {
                cam.gameObject.AddComponent<RtsCamera3DController>();
            }

            // 2. Ensure Directional Sun Light with soft shadows exists
            Light[] lights = Object.FindObjectsOfType<Light>();
            Light dirLight = null;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                {
                    dirLight = lights[i];
                    break;
                }
            }
            if (dirLight == null)
            {
                var lightGo = new GameObject("Directional Sun Light");
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.intensity = 1.25f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            // 3. Spawn Ra2GameDirector & HUD Controller automatically!
            var directorGo = new GameObject("Ra2GameDirector (AutoBootstrapped)");
            var director = directorGo.AddComponent<Ra2GameDirector>();
            director.AssetRegistry = Resources.Load<Ra2AssetRegistry>("Ra2AssetRegistry");
            director.PlayerSide = "Allied";

            var hud = directorGo.AddComponent<RtsInputAndHudController>();
            hud.Director = director;
        }
    }
}
