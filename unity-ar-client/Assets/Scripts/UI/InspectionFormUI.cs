using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Inspection.Models;
using Inspection.Network;

namespace Inspection.UI
{
    public class InspectionFormUI : MonoBehaviour
    {
        [Header("Form Inputs")]
        [SerializeField] private TMP_InputField titleInput;
        [SerializeField] private TMP_InputField descriptionInput;
        [SerializeField] private TMP_Dropdown priorityDropdown;
        [SerializeField] private TextMeshProUGUI coordsText;

        [Header("Buttons & Feedback")]
        [SerializeField] private Button submitButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI submitButtonText;
        [SerializeField] private TextMeshProUGUI validationErrorText;

        private Vector3 currentWorldPosition;
        private bool isSubmitting = false;

        public event Action<Issue> OnSubmitSuccess;
        public event Action OnCancelled;

        private void Awake()
        {
            if (submitButton != null) submitButton.onClick.AddListener(OnSubmitClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
            if (validationErrorText != null) validationErrorText.gameObject.SetActive(false);

            InitPriorityDropdown();
        }

        private void InitPriorityDropdown()
        {
            if (priorityDropdown == null) return;
            priorityDropdown.ClearOptions();
            priorityDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "高 (high)",
                "中 (medium)",
                "低 (low)"
            });
            priorityDropdown.value = 0; // 默认高
        }

        public void Open(Vector3 position)
        {
            currentWorldPosition = position;
            isSubmitting = false;

            if (titleInput != null)
            {
                titleInput.text = string.Empty;
                titleInput.interactable = true;
            }
            if (descriptionInput != null)
            {
                descriptionInput.text = string.Empty;
                descriptionInput.interactable = true;
            }
            if (priorityDropdown != null)
            {
                priorityDropdown.interactable = true;
            }
            if (coordsText != null)
            {
                coordsText.text = $"坐标: ({position.x:F2}, {position.y:F2}, {position.z:F2})";
            }
            if (validationErrorText != null)
            {
                validationErrorText.gameObject.SetActive(false);
            }
            if (submitButton != null)
            {
                submitButton.interactable = true;
                if (submitButtonText != null) submitButtonText.text = "提交问题";
            }

            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void OnCancelClicked()
        {
            if (isSubmitting) return;
            Close();
            OnCancelled?.Invoke();
        }

        private void OnSubmitClicked()
        {
            if (isSubmitting) return;

            string title = titleInput != null ? titleInput.text.Trim() : string.Empty;
            string desc = descriptionInput != null ? descriptionInput.text.Trim() : string.Empty;

            // 必填项校验：标题不可为空
            if (string.IsNullOrEmpty(title))
            {
                ShowValidationError("标题为必填项，请输入问题标题！");
                return;
            }

            string priorityStr = "high";
            if (priorityDropdown != null)
            {
                priorityStr = priorityDropdown.value switch
                {
                    0 => "high",
                    1 => "medium",
                    2 => "low",
                    _ => "high"
                };
            }

            var request = new CreateIssueRequest
            {
                title = title,
                description = desc,
                priority = priorityStr,
                position = new Models.Position(currentWorldPosition)
            };

            SetSubmittingState(true);

            InspectionApiClient.Instance.CreateIssue(request, (success, issue, error) =>
            {
                SetSubmittingState(false);

                if (success && issue != null)
                {
                    ToastUI.Instance.ShowSuccess($"问题上报成功！ID: {issue.id.Substring(0, 8)}...");
                    Close();
                    OnSubmitSuccess?.Invoke(issue);
                }
                else
                {
                    string msg = error ?? "提交失败，请重试";
                    ShowValidationError(msg);
                    ToastUI.Instance.ShowError(msg);
                }
            });
        }

        private void SetSubmittingState(bool submitting)
        {
            isSubmitting = submitting;
            if (submitButton != null) submitButton.interactable = !submitting;
            if (cancelButton != null) cancelButton.interactable = !submitting;
            if (titleInput != null) titleInput.interactable = !submitting;
            if (descriptionInput != null) descriptionInput.interactable = !submitting;
            if (priorityDropdown != null) priorityDropdown.interactable = !submitting;

            if (submitButtonText != null)
            {
                submitButtonText.text = submitting ? "正在提交..." : "提交问题";
            }
        }

        private void ShowValidationError(string message)
        {
            if (validationErrorText != null)
            {
                validationErrorText.text = message;
                validationErrorText.gameObject.SetActive(true);
            }
        }
    }
}
