using System.Collections;
using NUnit.Framework;
using Shooter.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests
{
    public sealed class GeneratedLodTests
    {
        [UnityTest]
        public IEnumerator CameraCutsAndInspectionSelectExactlyOneVisualWithoutCollisionChanges()
        {
            var root = new GameObject("LOD integration"); var cameraRoot = new GameObject("LOD test camera");
            try
            {
                for (int i = 0; i < 4; i++) new GameObject("visual " + i).transform.SetParent(root.transform, false);
                var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(.2f, .3f, 1);
                var camera = cameraRoot.AddComponent<Camera>(); camera.fieldOfView = 60;
                var selector = root.AddComponent<GeneratedWeaponLod>(); selector.ViewCamera = camera; selector.DiameterMeters = 1;
                camera.transform.position = new Vector3(0, 0, -1);
                yield return null;
                Assert.That(selector.Current, Is.EqualTo(0));
                camera.transform.position = new Vector3(0, 0, -100);
                yield return null;
                Assert.That(selector.Current, Is.EqualTo(3));
                selector.Inspect(1); yield return null;
                Assert.That(selector.Current, Is.EqualTo(1));
                for (int i = 0; i < 4; i++) Assert.That(root.transform.GetChild(i).gameObject.activeSelf, Is.EqualTo(i == 1));
                Assert.That(collider.enabled, Is.True); Assert.That(collider.size, Is.EqualTo(new Vector3(.2f, .3f, 1)));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot); }
        }
    }
}
