using System.Collections;
using System.IO;
using UnityEngine;
using Coop.Gameplay;

namespace Coop.App
{
    // Optional evidence capture. PNGs contain the actual rendered player, no desktop capture.
    public sealed class FrameRecorder : MonoBehaviour
    {
        public void Begin(string directory)
        {
            Directory.CreateDirectory(directory);
            StartCoroutine(Capture(directory));
        }
        IEnumerator Capture(string directory)
        {
            int frame = 0;
            var captureCamera = new GameObject("Evidence camera").AddComponent<Camera>();
            captureCamera.transform.SetParent(transform);
            captureCamera.enabled = false;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var canvas = GetComponentInChildren<Canvas>();
            while (true)
            {
                yield return new WaitForEndOfFrame();
                var source = PlayerAvatar.Local ? PlayerAvatar.Local.ViewCamera : GetComponentInChildren<Camera>();
                if (source)
                {
                    captureCamera.CopyFrom(source);
                    captureCamera.enabled = false;
                    captureCamera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                    captureCamera.targetTexture = target;
                    captureCamera.cullingMask = ~0;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = captureCamera;
                    canvas.planeDistance = .5f;
                    Canvas.ForceUpdateCanvases();
                    captureCamera.Render();
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    pixels.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.Combine(directory, $"frame-{frame++:D5}.png"), pixels.EncodeToPNG());
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = null;
                }
                yield return new WaitForSecondsRealtime(.1f);
            }
        }
    }
}
