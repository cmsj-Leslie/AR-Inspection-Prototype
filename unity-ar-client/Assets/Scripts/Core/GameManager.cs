using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Inspection.AR;
using Inspection.Models;
using Inspection.Network;
using Inspection.UI;

namespace Inspection.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Managers")]
        [SerializeField] private ARPlacementManager placementManager;
        [SerializeField] private InspectionApiClient apiClient;

        [Header("UI Panels")]
        [SerializeField] private InspectionFormUI formUI;
        [SerializeField] private SettingsDialogUI settingsUI;
        [SerializeField] private HistoryListUI historyUI;
        [SerializeField] private ToastUI toastUI;

        [Header("HUD Elements")]
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button historyButton;
        [SerializeField] private Button refreshButton;
        [SerializeField] private GameObject scanningHintPanel;
        [SerializeField] private TextMeshProUGUI statusPillText;
        [SerializeField] private Image statusPillDot;

        private Coroutine heartbeatCoroutine;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnEnable()
        {
            SubscribeConnectionEvents();
        }

        private void OnDisable()
        {
            UnsubscribeConnectionEvents();
            if (heartbeatCoroutine != null)
            {
                StopCoroutine(heartbeatCoroutine);
                heartbeatCoroutine = null;
            }
        }

        private void Start()
        {
            SubscribeConnectionEvents();
            BindEvents();
            CheckBackendConnection();

            if (heartbeatCoroutine != null) StopCoroutine(heartbeatCoroutine);
            heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());
        }

        private void SubscribeConnectionEvents()
        {
            if (InspectionApiClient.Instance != null)
            {
                InspectionApiClient.Instance.OnConnectionStatusChanged -= HandleConnectionStatusChanged;
                InspectionApiClient.Instance.OnConnectionStatusChanged += HandleConnectionStatusChanged;
            }
        }

        private void UnsubscribeConnectionEvents()
        {
            if (InspectionApiClient.Instance != null)
            {
                InspectionApiClient.Instance.OnConnectionStatusChanged -= HandleConnectionStatusChanged;
            }
        }

        private void HandleConnectionStatusChanged(bool success, HealthResponse health, string err)
        {
            UpdateStatusPill(success, success ? "后端正常" : "后端离线");
        }

        public void UpdateStatusPill(bool isOnline, string text)
        {
            if (statusPillText != null)
            {
                statusPillText.text = text;
            }
            if (statusPillDot != null)
            {
                statusPillDot.color = isOnline ? new Color(0.1f, 0.9f, 0.5f) : new Color(1f, 0.35f, 0.45f);
            }
        }

        private IEnumerator HeartbeatCoroutine()
        {
            var wait = new WaitForSeconds(6.0f);
            while (true)
            {
                yield return wait;
                if (InspectionApiClient.Instance != null)
                {
                    // 周期性静默健康检查，其结果会自动通过 OnConnectionStatusChanged 刷新状态胶囊
                    InspectionApiClient.Instance.CheckHealth(null);
                }
            }
        }

        private void BindEvents()
        {
            if (placementManager != null)
            {
                placementManager.OnPlaneTapped += HandlePlaneTapped;
                placementManager.OnMarkerTapped += HandleMarkerTapped;
            }

            if (formUI != null)
            {
                formUI.OnSubmitSuccess += HandleSubmitSuccess;
                formUI.OnCancelled += HandleFormCancelled;
            }

            if (settingsButton != null) settingsButton.onClick.AddListener(() => settingsUI?.Open());
            if (historyButton != null) historyButton.onClick.AddListener(() => historyUI?.Open());
            if (refreshButton != null) refreshButton.onClick.AddListener(SyncAllIssuesFromBackend);

            // 点击状态胶囊亦可直接呼出设置面板，便于用户随时排查连接
            if (statusPillDot != null)
            {
                var pillBtn = statusPillDot.GetComponentInParent<Button>();
                if (pillBtn == null && statusPillDot.transform.parent != null)
                {
                    pillBtn = statusPillDot.transform.parent.gameObject.AddComponent<Button>();
                }
                if (pillBtn != null)
                {
                    pillBtn.onClick.AddListener(() => settingsUI?.Open());
                }
            }

            if (historyUI != null)
            {
                historyUI.OnIssueSelected += (issue) =>
                {
                    ToastUI.Instance?.ShowInfo($"已选中问题: {issue.title}");
                };
            }
        }

        private void HandlePlaneTapped(Vector3 worldPos)
        {
            // 点击平面，隐藏引导提示，呼出表单
            if (scanningHintPanel != null) scanningHintPanel.SetActive(false);
            formUI?.Open(worldPos);
        }

        private void HandleMarkerTapped(MarkerController marker)
        {
            if (marker != null && marker.IssueData != null)
            {
                ToastUI.Instance?.ShowInfo($"查看标记: {marker.IssueData.title} [{marker.IssueData.status}]");
            }
        }

        private void HandleSubmitSuccess(Issue newIssue)
        {
            // 在 AR 场景中生成永久标记
            placementManager?.SpawnOrUpdateMarker(newIssue);
            placementManager?.HidePendingIndicator();

            if (scanningHintPanel != null) scanningHintPanel.SetActive(true);
        }

        private void HandleFormCancelled()
        {
            placementManager?.HidePendingIndicator();
            if (scanningHintPanel != null) scanningHintPanel.SetActive(true);
        }

        public void CheckBackendConnection()
        {
            if (statusPillText != null) statusPillText.text = "连接检测中";
            if (statusPillDot != null) statusPillDot.color = Color.yellow;

            InspectionApiClient.Instance?.CheckHealth((success, health, err) =>
            {
                UpdateStatusPill(success, success ? "后端正常" : "后端离线");

                if (success)
                {
                    SyncAllIssuesFromBackend();
                }
            });
        }

        public void SyncAllIssuesFromBackend()
        {
            InspectionApiClient.Instance?.GetIssues((success, res, err) =>
            {
                if (success && res != null && res.items != null)
                {
                    foreach (var item in res.items)
                    {
                        placementManager?.SpawnOrUpdateMarker(item);
                    }
                    ToastUI.Instance?.ShowSuccess($"已同步 {res.items.Length} 个巡检标记");
                }
                else if (!success)
                {
                    UpdateStatusPill(false, "后端离线");
                }
            });
        }
    }
}
