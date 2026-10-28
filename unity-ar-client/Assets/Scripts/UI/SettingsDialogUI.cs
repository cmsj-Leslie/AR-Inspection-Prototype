using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Inspection.Core;
using Inspection.Network;

namespace Inspection.UI
{
    public class SettingsDialogUI : MonoBehaviour
    {
        [Header("Inputs & Displays")]
        [SerializeField] private TMP_InputField urlInputField;
        [SerializeField] private TextMeshProUGUI statusFeedbackText;

        [Header("Buttons")]
        [SerializeField] private Button testConnectionButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button closeButton;

        private void Awake()
        {
            if (testConnectionButton != null) testConnectionButton.onClick.AddListener(OnTestConnectionClicked);
            if (saveButton != null) saveButton.onClick.AddListener(OnSaveClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            CreatePresetButtonsIfMissing();
        }

        public void Open()
        {
            if (urlInputField != null)
            {
                urlInputField.text = InspectionApiClient.Instance.BaseUrl;
            }
            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = "电脑端 Go 服务运行在 :8080 端口\n• USB模式: 点下方快捷填入 127.0.0.1\n• Wi-Fi模式: 输入电脑局域网 IP (如 192.168.31.6)";
                statusFeedbackText.color = new Color(0.85f, 0.9f, 0.95f);
            }
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void OnTestConnectionClicked()
        {
            string url = urlInputField != null ? urlInputField.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(url))
            {
                if (statusFeedbackText != null)
                {
                    statusFeedbackText.text = "请输入有效的 Go 服务地址";
                    statusFeedbackText.color = new Color(1f, 0.35f, 0.45f);
                }
                return;
            }

            string normalizedUrl = InspectionApiClient.NormalizeUrl(url);
            if (urlInputField != null) urlInputField.text = normalizedUrl;

            // 临时设置测试地址
            InspectionApiClient.Instance.BaseUrl = normalizedUrl;

            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = $"正在连接: {normalizedUrl} ...";
                statusFeedbackText.color = new Color(0.2f, 0.85f, 1f);
            }

            var stopwatch = Stopwatch.StartNew();
            InspectionApiClient.Instance.CheckHealth((success, health, error) =>
            {
                stopwatch.Stop();
                if (success && health != null)
                {
                    string msg = $"✓ 连接成功！延迟: {stopwatch.ElapsedMilliseconds}ms · 数据库: {health.database} · 问题数: {health.issueCount}";
                    if (statusFeedbackText != null)
                    {
                        statusFeedbackText.text = msg;
                        statusFeedbackText.color = new Color(0.1f, 0.9f, 0.5f);
                    }
                    ToastUI.Instance?.ShowSuccess("后端连接正常！");
                }
                else
                {
                    string msg = $"✕ 连接失败 ({stopwatch.ElapsedMilliseconds}ms)\n原因: {error}\n排查建议:\n1. 电脑端 Go 服务是否运行 (:8080)\n2. 手机与电脑是否在同一 Wi-Fi\n3. 或插 USB 数据线使用 127.0.0.1:8080";
                    if (statusFeedbackText != null)
                    {
                        statusFeedbackText.text = msg;
                        statusFeedbackText.color = new Color(1f, 0.35f, 0.45f);
                    }
                    ToastUI.Instance?.ShowError("无法连接到该地址，请检查网络！");
                }
            });
        }

        private void OnSaveClicked()
        {
            string url = urlInputField != null ? urlInputField.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(url))
            {
                ToastUI.Instance?.ShowError("请输入有效的服务地址");
                return;
            }

            string normalizedUrl = InspectionApiClient.NormalizeUrl(url);
            InspectionApiClient.Instance.BaseUrl = normalizedUrl;
            if (urlInputField != null) urlInputField.text = normalizedUrl;

            GameManager.Instance?.UpdateStatusPill(false, "连接切换中");
            ToastUI.Instance?.ShowInfo("正在验证并切换后端地址...");

            InspectionApiClient.Instance.CheckHealth((success, health, error) =>
            {
                if (success)
                {
                    ToastUI.Instance?.ShowSuccess($"已保存并连接成功: {normalizedUrl}");
                    GameManager.Instance?.SyncAllIssuesFromBackend();
                    Close();
                }
                else
                {
                    ToastUI.Instance?.ShowError("已保存新地址，但连接失败！首页已标记为离线");
                    Close();
                }
            });
        }

        private void CreatePresetButtonsIfMissing()
        {
            if (transform.Find("PresetUSB") != null) return;

            // 调整反馈文本的高度给快捷填入按钮腾出空间
            if (statusFeedbackText != null)
            {
                var fRt = statusFeedbackText.rectTransform;
                fRt.anchorMin = new Vector2(0.08f, 0.33f);
                fRt.anchorMax = new Vector2(0.92f, 0.46f);
            }

            CreateSmallPresetButton("PresetUSB", "填入 USB (127.0.0.1)", new Vector2(0.08f, 0.48f), new Vector2(0.48f, 0.56f), () =>
            {
                if (urlInputField != null) urlInputField.text = "http://127.0.0.1:8080";
                OnTestConnectionClicked();
            });

            CreateSmallPresetButton("PresetLAN", "填入 Wi-Fi (192.168.31.6)", new Vector2(0.52f, 0.48f), new Vector2(0.92f, 0.56f), () =>
            {
                if (urlInputField != null) urlInputField.text = "http://192.168.31.6:8080";
                OnTestConnectionClicked();
            });
        }

        private void CreateSmallPresetButton(string name, string text, Vector2 anchorMin, Vector2 anchorMax, System.Action onClick)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = obj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.24f, 0.36f, 0.95f);

            var btn = obj.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var tRt = textObj.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            if (statusFeedbackText != null && statusFeedbackText.font != null)
            {
                tmp.font = statusFeedbackText.font;
            }
            tmp.text = text;
            tmp.fontSize = 22;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.35f, 0.85f, 1f);
        }
    }
}
