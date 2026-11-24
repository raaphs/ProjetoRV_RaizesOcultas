using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RaizesOcultas
{
    public sealed class FieldSession : MonoBehaviour
    {
        [SerializeField] private GameObject equipment;
        [SerializeField] private Button startButton;
        [SerializeField] private Text introText;
        [SerializeField] private GameObject journal;
        [SerializeField] private Text journalText;
        [SerializeField] private Button wristButton;
        [SerializeField] private GameObject catalog;
        [SerializeField] private Text catalogText;
        [SerializeField] private FieldSite[] sites;
        [SerializeField] private FieldFeedback feedback;

        private readonly HashSet<FieldSite> registered = new HashSet<FieldSite>();
        private InputAction journalAction;
        private bool started;

        private void Awake()
        {
            startButton.onClick.AddListener(Begin);
            wristButton.onClick.AddListener(UseWristButton);
            journalAction = new InputAction("Diário de campo", InputActionType.Button,
                "<XRController>{LeftHand}/primaryButton");
            journalAction.AddBinding("<Keyboard>/j");
            journalAction.performed += ToggleJournal;
            UpdateJournal();
        }

        private void OnEnable()
        {
            journalAction.Enable();
        }

        private void OnDisable()
        {
            journalAction.Disable();
        }

        private void OnDestroy()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(Begin);
            if (wristButton != null)
                wristButton.onClick.RemoveListener(UseWristButton);
            journalAction?.Dispose();
        }

        private void Begin()
        {
            if (started)
                return;

            started = true;
            equipment.SetActive(true);
            startButton.gameObject.SetActive(false);
            wristButton.GetComponentInChildren<Text>(true).text = "Fechar diário";
            introText.text = "Pegue a pá e o pincel na bancada à direita.\n" +
                             "Faça três cavadas, levantando a pá para fora da área entre elas. Depois use o pincel.\n" +
                             "Segure com a empunhadura lateral. Abra o diário com X.";
            UpdateJournal();
        }

        private void ToggleJournal(InputAction.CallbackContext _) => journal.SetActive(!journal.activeSelf);

        private void UseWristButton()
        {
            if (started)
                journal.SetActive(false);
            else
                Begin();
        }

        public void Record(FieldSite site)
        {
            if (!registered.Add(site))
                return;

            feedback.Discovery();
            UpdateJournal();

            if (registered.Count != sites.Length)
                return;

            var report = new StringBuilder();
            foreach (FieldSite entry in sites)
                report.Append(entry.PlotName).Append("  ·  ").AppendLine(entry.ItemName)
                    .AppendLine(entry.CatalogNote).AppendLine();

            catalogText.text = report.ToString().TrimEnd();
            catalog.SetActive(true);
        }

        private void UpdateJournal()
        {
            var contents = new StringBuilder().Append(registered.Count).Append('/').Append(sites.Length)
                .AppendLine(" descobertas registradas").AppendLine();
            foreach (FieldSite entry in sites)
            {
                contents.Append(entry.PlotName).Append("  ·  ");
                contents.AppendLine(registered.Contains(entry) ? entry.ItemName : "A investigar");
            }

            if (!started)
                contents.AppendLine().Append("Comece no painel da entrada.");
            else if (registered.Count == sites.Length)
                contents.AppendLine().Append("Catálogo aberto no fim da trilha.");

            journalText.text = contents.ToString();
        }
    }
}
