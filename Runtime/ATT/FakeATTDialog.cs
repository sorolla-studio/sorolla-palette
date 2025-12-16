using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.ATT
{
    /// <summary>
    ///     Fake ATT dialog for Editor testing.
    ///     Mimics iOS ATT permission dialog behavior.
    /// </summary>
    public class FakeATTDialog : MonoBehaviour
    {
        [SerializeField] Button allowButton;
        [SerializeField] Button denyButton;

        /// <summary>Invoked with true if allowed, false if denied</summary>
        public event Action<bool> OnDecision;

        void Awake()
        {
            allowButton.onClick.AddListener(() => HandleDecision(true));
            denyButton.onClick.AddListener(() => HandleDecision(false));
        }

        void OnDestroy()
        {
            allowButton.onClick.RemoveAllListeners();
            denyButton.onClick.RemoveAllListeners();
        }

        void HandleDecision(bool allowed)
        {
            Debug.Log($"[SorollaSDK:ATT] Fake ATT decision: {(allowed ? "Allowed" : "Denied")}");
            OnDecision?.Invoke(allowed);
            Destroy(gameObject);
        }

        /// <summary>
        ///     Show the fake ATT dialog and get user decision.
        /// </summary>
        public static void Show(Action<bool> onDecision)
        {
            var prefab = Resources.Load<GameObject>("FakeATTDialog");
            if (prefab == null)
            {
                Debug.LogWarning("[SorollaSDK:ATT] FakeATTDialog prefab not found. Run SorollaSDK > ATT > Create Fake ATT Popup Prefab");
                onDecision?.Invoke(true); // Default to allowed
                return;
            }

            GameObject instance = Instantiate(prefab);
            var dialog = instance.GetComponent<FakeATTDialog>();
            dialog.OnDecision += onDecision;
        }
    }
}
