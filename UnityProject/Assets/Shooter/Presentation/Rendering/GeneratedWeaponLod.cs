using Shooter.Generation;
using UnityEngine;

namespace Shooter.Presentation
{
    /// <summary>Changes only visual children. Explicit inspection cameras can override automatic selection.</summary>
    public sealed class GeneratedWeaponLod : MonoBehaviour
    {
        public Camera ViewCamera;
        public float DiameterMeters;
        public bool Automatic = true;
        public bool FirstPerson;
        public int Current { get; private set; } = -1;
        private void LateUpdate()
        {
            if (!Automatic || ViewCamera == null) return;
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), Mathf.Abs(transform.lossyScale.z));
            float depth = Vector3.Dot(transform.position - ViewCamera.transform.position, ViewCamera.transform.forward);
            float height = ViewCamera.orthographic ? DiameterMeters * scale / (2 * ViewCamera.orthographicSize) :
                ScreenHeightLod.PerspectiveHeight(DiameterMeters * scale, depth, ViewCamera.fieldOfView);
            Apply(ScreenHeightLod.Select(height, Current, FirstPerson));
        }
        public void Inspect(int lod) { Automatic = false; Apply(lod); }
        private void Apply(int lod)
        {
            if (lod == Current) return;
            for (int i = 0; i < transform.childCount; i++) transform.GetChild(i).gameObject.SetActive(i == lod);
            Current = lod;
        }
    }
}
