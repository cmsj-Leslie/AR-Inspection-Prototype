using UnityEngine;
using TMPro;
using Inspection.Models;

namespace Inspection.AR
{
    public class MarkerController : MonoBehaviour
    {
        [Header("UI & Visual Elements")]
        [SerializeField] private MeshRenderer bodyRenderer;
        [SerializeField] private TextMeshPro titleText;
        [SerializeField] private TextMeshPro statusBadgeText;
        [SerializeField] private GameObject selectionGlow;

        private Issue currentIssue;
        private Camera arCamera;

        public Issue IssueData => currentIssue;

        private void Awake()
        {
            arCamera = Camera.main;
            EnsureComponents();
        }

        private void LateUpdate()
        {
            // Billboard 效果：让文字面板始终面朝 AR 摄像机
            if (arCamera != null && titleText != null)
            {
                Vector3 lookDirection = titleText.transform.position - arCamera.transform.position;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    titleText.transform.rotation = Quaternion.LookRotation(lookDirection);
                }
            }
        }

        public void BindIssue(Issue issue)
        {
            currentIssue = issue;
            UpdateVisuals();
        }

        public void SetSelected(bool selected)
        {
            if (selectionGlow != null)
            {
                selectionGlow.SetActive(selected);
            }
        }

        public void UpdateVisuals()
        {
            if (currentIssue == null) return;

            Color pColor = currentIssue.GetPriorityColor();
            Color sColor = currentIssue.GetStatusColor();

            if (bodyRenderer != null)
            {
                Material mat = bodyRenderer.material;
                mat.color = pColor;
            }

            if (titleText != null)
            {
                titleText.text = currentIssue.title;
            }

            if (statusBadgeText != null)
            {
                string statusLabel = currentIssue.status switch
                {
                    "open" => "待处理",
                    "in_progress" => "处理中",
                    "resolved" => "已解决",
                    _ => currentIssue.status
                };
                statusBadgeText.text = $"[{statusLabel}]";
                statusBadgeText.color = sColor;
            }
        }

        /// <summary>
        /// 当场景中没有预设 Prefab 资源时，代码动态生成立体的图钉标记几何体
        /// </summary>
        public void BuildDefaultVisuals(Color initialColor, string title)
        {
            if (bodyRenderer != null) return;

            // 1. 底座立柱
            GameObject column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            column.transform.SetParent(transform, false);
            column.transform.localPosition = new Vector3(0, 0.08f, 0);
            column.transform.localScale = new Vector3(0.04f, 0.08f, 0.04f);
            Destroy(column.GetComponent<Collider>());

            // 2. 顶部球体
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = new Vector3(0, 0.2f, 0);
            sphere.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
            bodyRenderer = sphere.GetComponent<MeshRenderer>();
            bodyRenderer.material = new Material(Shader.Find("Standard"))
            {
                color = initialColor
            };

            // 3. 浮动文字
            GameObject textObj = new GameObject("TitleText");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = new Vector3(0, 0.32f, 0);
            titleText = textObj.AddComponent<TextMeshPro>();
            titleText.fontSize = 2.5f;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = Color.white;
            titleText.text = title;

            // 4. 状态角标
            GameObject statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(transform, false);
            statusObj.transform.localPosition = new Vector3(0, 0.27f, 0);
            statusBadgeText = statusObj.AddComponent<TextMeshPro>();
            statusBadgeText.fontSize = 1.8f;
            statusBadgeText.alignment = TextAlignmentOptions.Center;
            statusBadgeText.color = new Color(0.2f, 0.9f, 1f);
            statusBadgeText.text = "[待处理]";

            // 5. 点击碰撞体
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.15f, 0);
            col.size = new Vector3(0.25f, 0.35f, 0.25f);
        }

        private void EnsureComponents()
        {
            if (GetComponent<Collider>() == null)
            {
                BoxCollider col = gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, 0.15f, 0);
                col.size = new Vector3(0.25f, 0.35f, 0.25f);
            }
        }
    }
}
