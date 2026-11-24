using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RaizesOcultas
{
    public sealed class FieldFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource contactSound;
        [SerializeField] private AudioSource discoverySound;
        [SerializeField] private AudioClip shovelClip;
        [SerializeField] private AudioClip brushClip;
        [SerializeField] private AudioClip discoveryClip;
        [SerializeField] private ParticleSystem dust;

        private float nextStroke;

        private void Start()
        {
            dust.Play();
        }

        public void Stroke(FieldTool.Kind tool, Vector3 point, XRGrabInteractable grab)
        {
            if (Time.unscaledTime < nextStroke)
                return;

            nextStroke = Time.unscaledTime + 0.16f;
            bool brushing = tool == FieldTool.Kind.Brush;
            contactSound.transform.position = point;
            contactSound.PlayOneShot(brushing ? brushClip : shovelClip);
            dust.Emit(new ParticleSystem.EmitParams { position = point }, brushing ? 9 : 14);

            if (grab.firstInteractorSelecting is XRBaseInputInteractor controller)
                controller.SendHapticImpulse(brushing ? 0.16f : 0.28f, 0.05f);
        }

        public void Discovery()
        {
            discoverySound.PlayOneShot(discoveryClip);
        }
    }
}
