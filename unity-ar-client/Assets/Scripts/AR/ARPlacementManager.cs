using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Inspection.Models;

namespace Inspection.AR
{
    [RequireComponent(typeof(ARRaycastManager))]
    public class ARPlacementManager : MonoBehaviour
    {
        [Header("AR References")]
        [SerializeField] private ARRaycastManager raycastManager;
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private Camera arCamera;

        [Header("Prefabs & Templates")]
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private GameObject pendingIndicatorPrefab;

        private GameObject currentPendingIndicator;
        private Vector3 lastHitPosition;
        private Quaternion lastHitRotation;

        private readonly Dictionary<string, MarkerController> spawnedMarkers = new Dictionary<string, MarkerController>();
        private static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        public System.Action<Vector3> OnPlaneTapped;
        public System.Action<MarkerController> OnMarkerTapped;

        private void Awake()
        {
            if (raycastManager == null) raycastManager = GetComponent<ARRaycastManager>();
            if (arCamera == null) arCamera = Camera.main;
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            // 1. 真机触摸输入
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);

                if (touch.phase == TouchPhase.Began)
                {
                    // 关键要求：点击按钮或输入框时，不应误在 AR 场景中放置标记
                    if (IsPointerOverUI(touch))
                    {
                        return;
                    }

                    ProcessTap(touch.position);
                }
                return;
            }

            // 2. Unity 编辑器鼠标输入支持（方便本地免打包调试）
#if UNITY_EDITOR
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                ProcessTap(Input.mousePosition);
            }
#endif
        }

        private void ProcessTap(Vector2 screenPosition)
        {
            // 首先检测是否点击了已有的 3D 标记物体
            Ray ray = arCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hitInfo))
            {
                MarkerController marker = hitInfo.collider.GetComponentInParent<MarkerController>();
                if (marker != null)
                {
                    OnMarkerTapped?.Invoke(marker);
                    return;
                }
            }

            // 针对 AR 水平平面的多边形命中检测
            if (raycastManager != null && raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
            {
                Pose hitPose = hits[0].pose;
                lastHitPosition = hitPose.position;
                lastHitRotation = hitPose.rotation;

                ShowPendingIndicator(lastHitPosition, lastHitRotation);
                OnPlaneTapped?.Invoke(lastHitPosition);
            }
#if UNITY_EDITOR
            else
            {
                // 编辑器内模拟平面命中（前方 1.5 米处放置）
                Vector3 simPos = arCamera.transform.position + arCamera.transform.forward * 1.5f + Vector3.down * 0.3f;
                lastHitPosition = simPos;
                lastHitRotation = Quaternion.identity;

                ShowPendingIndicator(lastHitPosition, lastHitRotation);
                OnPlaneTapped?.Invoke(lastHitPosition);
            }
#endif
        }

        /// <summary>
        /// 双重严格 UI 防穿透校验（EventSystem + 射线测试）
        /// </summary>
        private bool IsPointerOverUI(Touch touch)
        {
            if (EventSystem.current == null) return false;

            // 1. 常规触摸 ID 判断
            if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            {
                return true;
            }

            // 2. 自定义 PointerEventData 射线碰撞检测（应对部分 Android 机的边缘事件漏测）
            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = touch.position
            };
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            return results.Count > 0;
        }

        private void ShowPendingIndicator(Vector3 pos, Quaternion rot)
        {
            if (currentPendingIndicator == null)
            {
                if (pendingIndicatorPrefab != null)
                {
                    currentPendingIndicator = Instantiate(pendingIndicatorPrefab, pos, rot);
                }
                else
                {
                    // 动态生成临时黄色定位指示物
                    currentPendingIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    currentPendingIndicator.GetComponent<MeshRenderer>().material = MaterialHelper.CreateColoredMaterial(new Color(0.2f, 0.85f, 1f, 0.8f));
                    Destroy(currentPendingIndicator.GetComponent<Collider>());
                }
            }
            else
            {
                currentPendingIndicator.SetActive(true);
                currentPendingIndicator.transform.SetPositionAndRotation(pos, rot);
            }
        }

        public void HidePendingIndicator()
        {
            if (currentPendingIndicator != null)
            {
                currentPendingIndicator.SetActive(false);
            }
        }

        /// <summary>
        /// 根据已保存的 Issue 在 AR 空间实例化或更新标记
        /// </summary>
        public MarkerController SpawnOrUpdateMarker(Issue issue)
        {
            if (spawnedMarkers.TryGetValue(issue.id, out MarkerController existing))
            {
                existing.BindIssue(issue);
                return existing;
            }

            Vector3 worldPos = issue.position.ToVector3();
            GameObject obj;

            if (markerPrefab != null)
            {
                obj = Instantiate(markerPrefab, worldPos, Quaternion.identity);
            }
            else
            {
                obj = new GameObject($"Marker_{issue.id}");
                obj.transform.position = worldPos;
                var mc = obj.AddComponent<MarkerController>();
                mc.BuildDefaultVisuals(issue.GetPriorityColor(), issue.title);
            }

            MarkerController controller = obj.GetComponent<MarkerController>();
            controller.BindIssue(issue);
            spawnedMarkers[issue.id] = controller;
            return controller;
        }

        public void ClearMarkers()
        {
            foreach (var kvp in spawnedMarkers)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            spawnedMarkers.Clear();
        }
    }
}
