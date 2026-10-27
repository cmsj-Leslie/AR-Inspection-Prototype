using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Inspection.UI
{
    public enum ToastType
    {
        Success,
        Error,
        Info
    }

    public class ToastUI : MonoBehaviour
    {
        private static ToastUI instance;
        public static ToastUI Instance
        {
            get
            {
                if (instance == null) instance = FindObjectOfType<ToastUI>(true);
                return instance;
            }
        }

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private Image iconImage;

        private Coroutine activeToastCoroutine;

        private readonly Color successColor = new Color(0.06f, 0.73f, 0.51f, 0.95f); // Emerald
        private readonly Color errorColor   = new Color(0.96f, 0.25f, 0.37f, 0.95f); // Rose
        private readonly Color infoColor    = new Color(0.13f, 0.83f, 0.93f, 0.95f); // Cyan

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                EnsureComponents();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void Show(string message, ToastType type = ToastType.Info, float duration = 2.5f)
        {
            if (activeToastCoroutine != null)
            {
                StopCoroutine(activeToastCoroutine);
            }
            activeToastCoroutine = StartCoroutine(ToastRoutine(message, type, duration));
        }

        public void ShowSuccess(string message) => Show(message, ToastType.Success, 2.5f);
        public void ShowError(string message) => Show(message, ToastType.Error, 3.5f);
        public void ShowInfo(string message) => Show(message, ToastType.Info, 2.0f);

        private IEnumerator ToastRoutine(string message, ToastType type, float duration)
        {
            if (messageText != null) messageText.text = message;
            if (backgroundImage != null)
            {
                backgroundImage.color = type switch
                {
                    ToastType.Success => successColor,
                    ToastType.Error => errorColor,
                    _ => infoColor
                };
            }

            gameObject.SetActive(true);

            // 淡入
            float t = 0;
            while (t < 0.2f)
            {
                t += Time.unscaledDeltaTime;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.2f);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // 停留
            yield return new WaitForSecondsRealtime(duration);

            // 淡出
            t = 0;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.25f);
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 0f;

            gameObject.SetActive(false);
            activeToastCoroutine = null;
        }

        private void EnsureComponents()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0;
            gameObject.SetActive(false);
        }
    }
}
