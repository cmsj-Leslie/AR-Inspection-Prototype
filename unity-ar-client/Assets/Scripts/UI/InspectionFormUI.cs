using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Inspection.Models;
using Inspection.Network;

namespace Inspection.UI
{
    public class ClickTrigger : MonoBehaviour, IPointerClickHandler
    {
        public Action onClick;
        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke();
        }
    }

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
        private int currentPriorityIndex = 0; // 0=high, 1=medium, 2=low

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

            // 禁用 TMP_Dropdown 自身逻辑，防止拦截触摸
            priorityDropdown.enabled = false;

            // 给底色框绑定点击触发器
            var trigger = priorityDropdown.gameObject.GetComponent<ClickTrigger>();
            if (trigger == null) trigger = priorityDropdown.gameObject.AddComponent<ClickTrigger>();
            trigger.onClick = CyclePriority;

            // 同时给文字也绑定点击触发器，触摸任何位置都能 100% 切换
            if (priorityDropdown.captionText != null)
            {
                var textTrigger = priorityDropdown.captionText.gameObject.GetComponent<ClickTrigger>();
                if (textTrigger == null) textTrigger = priorityDropdown.captionText.gameObject.AddComponent<ClickTrigger>();
                textTrigger.onClick = CyclePriority;
            }

            UpdatePriorityUI();
        }

        public void CyclePriority()
        {
            currentPriorityIndex = (currentPriorityIndex + 1) % 3;
            UpdatePriorityUI();
        }

        private void UpdatePriorityUI()
        {
            if (priorityDropdown != null)
            {
                priorityDropdown.value = currentPriorityIndex;
                string label = currentPriorityIndex switch
                {
                    0 => "高 (high)  ·  点击切换",
                    1 => "中 (medium)  ·  点击切换",
                    2 => "低 (low)  ·  点击切换",
                    _ => "高 (high)  ·  点击切换"
                };

                if (priorityDropdown.captionText != null)
                {
                    priorityDropdown.captionText.text = label;
                    priorityDropdown.captionText.alignment = TextAlignmentOptions.Center;
                    priorityDropdown.captionText.color = Color.white;
                }

                var img = priorityDropdown.GetComponent<Image>();
                if (img != null)
                {
                    img.color = currentPriorityIndex switch
                    {
                        0 => new Color(0.85f, 0.2f, 0.25f, 0.95f), // 高：鲜明红色
                        1 => new Color(0.92f, 0.55f, 0.1f, 0.95f),  // 中：橙黄色
                        2 => new Color(0.2f, 0.6f, 0.85f, 0.95f),  // 低：冷静蓝色
                        _ => new Color(0.1f, 0.15f, 0.25f, 0.95f)
                    };
                }
            }
        }

        public void Open(Vector3 position)
        {
            currentWorldPosition = position;
            isSubmitting = false;

            currentPriorityIndex = 0;
            UpdatePriorityUI();

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

            // -------------------------------------------------------------
            // 对应任务书 3.1 移动端需求 4 与 5：
            //   - 标题：必填（空字符串拦截并标红提示）
            //   - 描述：可选
            //   - 优先级：low、medium、high 三级枚举
            //   - 位置：当前放置标记的 x/y/z 局部物理坐标
            //   - 需求 5: 提交成功或失败时给出明确的浮动 Toast 反馈
            // -------------------------------------------------------------
            if (string.IsNullOrEmpty(title))
            {
                ShowValidationError("标题为必填项，请输入问题标题！");
                return;
            }

            string priorityStr = currentPriorityIndex switch
            {
                0 => "high",
                1 => "medium",
                2 => "low",
                _ => "high"
            };

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
                    // 任务书 3.1.5: 成功明确提示
                    ToastUI.Instance.ShowSuccess($"问题上报成功！ID: {issue.id.Substring(0, 8)}...");
                    Close();
                    OnSubmitSuccess?.Invoke(issue);
                }
                else
                {
                    // 任务书 3.1.5: 失败明确提示（展示后端返回的具体原因）
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
