using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.ATT
{
    /// <summary>
    ///     Fake CMP (Consent Management Platform) dialog for Editor testing.
    ///     Mimics GDPR consent flow.
    /// </summary>
    public class FakeCMPDialog : MonoBehaviour
    {
        [SerializeField] Button acceptAllButton;
        [SerializeField] Button rejectAllButton;

        /// <summary>Invoked with true if accepted, false if rejected</summary>
        public event Action<bool> OnDecision;

        void Awake()
        {
            acceptAllButton.onClick.AddListener(() => HandleDecision(true));
            rejectAllButton.onClick.AddListener(() => HandleDecision(false));
        }

        void OnDestroy()
        {
            acceptAllButton.onClick.RemoveAllListeners();
            rejectAllButton.onClick.RemoveAllListeners();
        }

        void HandleDecision(bool accepted)
        {
            Debug.Log($"[Palette:CMP] Fake CMP decision: {(accepted ? "Accepted" : "Rejected")}");
            OnDecision?.Invoke(accepted);
            Destroy(gameObject);
        }

        /// <summary>
        ///     Show the fake CMP dialog and get user decision.
        /// </summary>
        public static void Show(Action<bool> onDecision)
        {
            var prefab = Resources.Load<GameObject>("FakeCMPDialog");
            if (prefab == null)
            {
                Debug.LogWarning("[Palette:CMP] FakeCMPDialog prefab not found. Run Palette > ATT > Create Fake CMP Popup Prefab");
                onDecision?.Invoke(true); // Default to accepted
                return;
            }

            GameObject instance = Instantiate(prefab);
            var dialog = instance.GetComponent<FakeCMPDialog>();
            dialog.OnDecision += onDecision;
        }
    }
}
