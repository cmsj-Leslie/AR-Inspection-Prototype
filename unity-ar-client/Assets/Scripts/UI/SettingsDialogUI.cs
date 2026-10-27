using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        }

        public void Open()
        {
            if (urlInputField != null)
            {
                urlInputField.text = InspectionApiClient.Instance.BaseUrl;
            }
            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = "请输入电脑端 Go 服务的局域网地址（如 http://192.168.x.x:8080）";
                statusFeedbackText.color = Color.white;
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
            if (string.IsNullOrEmpty(url)) return;

            // 临时设置测试
            InspectionApiClient.Instance.BaseUrl = url;

            if (statusFeedbackText != null)
            {
                statusFeedbackText.text = "正在测试连接 Go 后端...";
                statusFeedbackText.color = new Color(0.2f, 0.85f, 1f);
            }

            var stopwatch = Stopwatch.StartNew();
            InspectionApiClient.Instance.CheckHealth((success, health, error) =>
            {
                stopwatch.Stop();
                if (success && health != null)
                {
                    string msg = $"连接成功！延迟: {stopwatch.ElapsedMilliseconds}ms · 数据库: {health.database} · 当前问题数: {health.issueCount}";
                    if (statusFeedbackText != null)
                    {
                        statusFeedbackText.text = msg;
                        statusFeedbackText.color = new Color(0.1f, 0.9f, 0.5f);
                    }
                    ToastUI.Instance.ShowSuccess("后端连接正常！");
                }
                else
                {
                    string msg = $"连接失败: {error}";
                    if (statusFeedbackText != null)
                    {
                        statusFeedbackText.text = msg;
                        statusFeedbackText.color = new Color(1f, 0.35f, 0.45f);
                    }
                    ToastUI.Instance.ShowError("无法连接到该地址，请检查 IP、端口及手机 Wi-Fi");
                }
            });
        }

        private void OnSaveClicked()
        {
            string url = urlInputField != null ? urlInputField.text.Trim() : string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                InspectionApiClient.Instance.BaseUrl = url;
                ToastUI.Instance.ShowSuccess("服务器配置已保存！");
            }
            Close();
        }
    }
}
