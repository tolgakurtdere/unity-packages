using UnityEngine;
using UnityEngine.UI;

namespace TK.IAP.UI
{
    /// <summary>Drop-in restore button: triggers <see cref="IapService.RestorePurchases"/> on click.</summary>
    [RequireComponent(typeof(Button))]
    public class IapRestoreButton : MonoBehaviour
    {
        private Button _iapButton;
        private bool _isInitialized;

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            if (_isInitialized)
                return;

            _iapButton = GetComponent<Button>();
            _iapButton.onClick.AddListener(OnRestoreButtonTapped);
            _isInitialized = true;
        }

        private void OnDestroy()
        {
            if (_iapButton) _iapButton.onClick.RemoveListener(OnRestoreButtonTapped);
        }

        private void OnRestoreButtonTapped()
        {
            if (IapService.Instance == null)
            {
                Debug.LogError("[IapRestoreButton] IapService not created yet — construct it before UI loads.");
                return;
            }

            IapService.Instance.RestorePurchases();
        }
    }
}
