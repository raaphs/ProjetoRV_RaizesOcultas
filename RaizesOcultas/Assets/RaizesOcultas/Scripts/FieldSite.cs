using UnityEngine;
using UnityEngine.UI;

namespace RaizesOcultas
{
    public sealed class FieldSite : MonoBehaviour
    {
        [SerializeField] private string plotName;
        [SerializeField] private string itemName;
        [SerializeField] private string catalogNote;
        [SerializeField] private Transform digPoint;
        [SerializeField] private Transform brushPoint;
        [SerializeField, Min(0.01f)] private float digRadius = 0.39f;
        [SerializeField, Min(0.01f)] private float brushRadius = 0.17f;
        [SerializeField, Min(0.01f)] private float digDistancePerLayer = 0.24f;
        [SerializeField, Min(0.01f)] private float digExitMargin = 0.08f;
        [SerializeField, Min(0.01f)] private float brushDistancePerLayer = 0.20f;
        [SerializeField] private GameObject[] soilLayers;
        [SerializeField] private GameObject[] dirtPatches;
        [SerializeField] private FieldObject artifact;
        [SerializeField] private GameObject storyCard;
        [SerializeField] private Text statusText;
        [SerializeField] private FieldSession session;

        private int removed;
        private float motion;
        private bool uncovered;
        private bool completed;
        private bool waitingForShovelExit;

        public string PlotName => plotName;
        public string ItemName => itemName;
        public string CatalogNote => catalogNote;

        private void Start()
        {
            UpdateLabel();
        }

        public bool ApplyStroke(FieldTool.Kind tool, Vector3 from, Vector3 to, out Vector3 contact)
        {
            contact = default;
            FieldTool.Kind requiredTool = uncovered ? FieldTool.Kind.Brush : FieldTool.Kind.Shovel;
            if (completed || tool != requiredTool)
                return false;

            Vector3 center = uncovered ? brushPoint.position : digPoint.position;
            float radius = uncovered ? brushRadius : digRadius;
            if (waitingForShovelExit && !uncovered)
            {
                float rearmRadius = digRadius + digExitMargin;
                if ((from - center).sqrMagnitude >= rearmRadius * rearmRadius ||
                    (to - center).sqrMagnitude >= rearmRadius * rearmRadius)
                {
                    waitingForShovelExit = false;
                    motion = 0f;
                    UpdateLabel();
                }
                return false;
            }

            float travelled = DistanceInsideSphere(from, to, center, radius, out contact);
            if (travelled <= 0f)
            {
                ResetToolMotion(tool);
                return false;
            }

            motion += travelled;
            if (!uncovered)
            {
                // Uma entrada da pá só remove uma camada. O próximo movimento
                // começa depois que a ponta sair do volume de escavação.
                if (motion >= digDistancePerLayer)
                {
                    soilLayers[removed].SetActive(false);
                    removed++;
                    motion = 0f;
                    waitingForShovelExit = true;
                    if (removed == soilLayers.Length)
                        AdvanceStage();
                    else
                        UpdateLabel();
                }
                else if ((to - center).sqrMagnitude > digRadius * digRadius)
                {
                    motion = 0f;
                }
                return true;
            }

            while (removed < dirtPatches.Length && motion >= brushDistancePerLayer)
            {
                dirtPatches[removed].SetActive(false);
                removed++;
                motion -= brushDistancePerLayer;
                UpdateLabel();
            }

            if (removed == dirtPatches.Length)
                AdvanceStage();

            return true;
        }

        public void ResetToolMotion(FieldTool.Kind tool)
        {
            if (tool == FieldTool.Kind.Shovel && !uncovered)
                motion = 0f;
        }

        private void AdvanceStage()
        {
            if (!uncovered)
            {
                uncovered = true;
                removed = 0;
                motion = 0f;
                waitingForShovelExit = false;
                artifact.gameObject.SetActive(true);
            }
            else
            {
                completed = true;
                artifact.Unlock();
                storyCard.SetActive(true);
                session.Record(this);
            }

            UpdateLabel();
        }

        private void UpdateLabel()
        {
            if (completed)
                statusText.text = "Registrado · Pegue a peça e leia o painel.";
            else if (uncovered)
                statusText.text = $"Pincel · Manchas removidas {removed}/{dirtPatches.Length}";
            else if (waitingForShovelExit)
                statusText.text = $"Pá · {removed}/{soilLayers.Length} cavadas · Levante a pá para fora da área.";
            else
                statusText.text = $"Pá · {removed}/{soilLayers.Length} cavadas · Entre na terra e faça uma passada.";
        }

        // Mede somente o trecho do movimento que realmente cruza a zona de trabalho.
        private static float DistanceInsideSphere(Vector3 from, Vector3 to, Vector3 center,
            float radius, out Vector3 contact)
        {
            contact = default;
            Vector3 movement = to - from;
            float length = movement.magnitude;
            if (length <= 0f)
                return 0f;

            Vector3 direction = movement / length;
            Vector3 offset = from - center;
            float projection = Vector3.Dot(offset, direction);
            float discriminant = projection * projection - offset.sqrMagnitude + radius * radius;
            if (discriminant <= 0f)
                return 0f;

            float halfChord = Mathf.Sqrt(discriminant);
            float entry = Mathf.Max(0f, -projection - halfChord);
            float exit = Mathf.Min(length, -projection + halfChord);
            if (exit <= entry)
                return 0f;

            contact = from + direction * ((entry + exit) * 0.5f);
            return exit - entry;
        }

        private void OnDrawGizmosSelected()
        {
            if (digPoint != null)
            {
                Gizmos.color = new Color(0.95f, 0.58f, 0.20f);
                Gizmos.DrawWireSphere(digPoint.position, digRadius);
                Gizmos.color = new Color(0.95f, 0.80f, 0.45f, 0.5f);
                Gizmos.DrawWireSphere(digPoint.position, digRadius + digExitMargin);
            }

            if (brushPoint != null)
            {
                Gizmos.color = new Color(0.55f, 0.83f, 0.72f);
                Gizmos.DrawWireSphere(brushPoint.position, brushRadius);
            }
        }
    }
}
