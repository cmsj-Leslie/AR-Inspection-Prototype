using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Inspection.Models;
using Inspection.Network;

namespace Inspection.UI
{
    public class HistoryListUI : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private Transform listContent;
        [SerializeField] private GameObject listItemPrefab;
        [SerializeField] private TextMeshProUGUI emptyNoticeText;
        [SerializeField] private TextMeshProUGUI totalCountText;

        [Header("Buttons")]
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button closeButton;

        public event Action<Issue> OnIssueSelected;

        private void Awake()
        {
            if (refreshButton != null) refreshButton.onClick.AddListener(FetchIssues);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            gameObject.SetActive(true);
            FetchIssues();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void FetchIssues()
        {
            if (emptyNoticeText != null)
            {
                emptyNoticeText.text = "正在加载问题列表...";
                emptyNoticeText.gameObject.SetActive(true);
            }

            InspectionApiClient.Instance.GetIssues((success, res, err) =>
            {
                if (success && res != null && res.items != null)
                {
                    PopulateList(res.items);
                }
                else
                {
                    if (emptyNoticeText != null)
                    {
                        emptyNoticeText.text = $"加载失败: {err}";
                        emptyNoticeText.gameObject.SetActive(true);
                    }
                }
            });
        }

        private void PopulateList(Issue[] issues)
        {
            // 清理旧项
            if (listContent != null)
            {
                for (int i = listContent.childCount - 1; i >= 0; i--)
                {
                    Destroy(listContent.GetChild(i).gameObject);
                }
            }

            if (totalCountText != null)
            {
                totalCountText.text = $"共 {issues.Length} 项";
            }

            if (issues.Length == 0)
            {
                if (emptyNoticeText != null)
                {
                    emptyNoticeText.text = "暂无已上报问题";
                    emptyNoticeText.gameObject.SetActive(true);
                }
                return;
            }

            if (emptyNoticeText != null) emptyNoticeText.gameObject.SetActive(false);

            foreach (var item in issues)
            {
                CreateItemRow(item);
            }
        }

        private void CreateItemRow(Issue issue)
        {
            if (listContent == null) return;

            GameObject rowObj = new GameObject($"Item_{issue.id}");
            rowObj.transform.SetParent(listContent, false);

            var layout = rowObj.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 8, 8);

            var bg = rowObj.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.16f, 0.24f, 0.9f);

            var btn = rowObj.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                OnIssueSelected?.Invoke(issue);
                Close();
            });

            // 文本信息
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(rowObj.transform, false);
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 14;

            string pLabel = issue.priority switch { "high" => "高", "medium" => "中", "low" => "低", _ => issue.priority };
            string sLabel = issue.status switch { "open" => "待处理", "in_progress" => "处理中", "resolved" => "已解决", _ => issue.status };

            text.text = $"<b>{issue.title}</b>  <color=#{ColorUtility.ToHtmlStringRGB(issue.GetPriorityColor())}>[{pLabel}]</color>  <color=#{ColorUtility.ToHtmlStringRGB(issue.GetStatusColor())}>[{sLabel}]</color>\n<size=11><color=#8899aa>({issue.position.x:F2}, {issue.position.y:F2}, {issue.position.z:F2})</color></size>";
        }
    }
}
