using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace RaizesOcultas
{
    public sealed class FieldTeleportFade : MonoBehaviour
    {
        public const float Delay = 0.14f;

        [SerializeField] private Image veil;
        private float startedAt;

        public void Begin(TeleportingEventArgs _)
        {
            startedAt = Time.unscaledTime;
            veil.color = Color.clear;
            veil.enabled = true;
        }

        private void Update()
        {
            if (!veil.enabled)
                return;

            float elapsed = Time.unscaledTime - startedAt;
            float enter = Mathf.Clamp01(elapsed / 0.10f);
            float exit = Mathf.Clamp01((elapsed - (Delay + 0.08f)) / 0.16f);
            veil.color = new Color(0f, 0f, 0f, enter * (1f - exit));
            if (exit >= 1f)
                veil.enabled = false;
        }
    }
}
