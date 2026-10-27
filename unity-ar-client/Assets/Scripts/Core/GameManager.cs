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

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            BindEvents();
            CheckBackendConnection();
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
            InspectionApiClient.Instance?.CheckHealth((success, health, err) =>
            {
                if (statusPillText != null)
                {
                    statusPillText.text = success ? "后端正常" : "后端离线";
                }
                if (statusPillDot != null)
                {
                    statusPillDot.color = success ? new Color(0.1f, 0.9f, 0.5f) : new Color(1f, 0.35f, 0.45f);
                }

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
            });
        }
    }
}
