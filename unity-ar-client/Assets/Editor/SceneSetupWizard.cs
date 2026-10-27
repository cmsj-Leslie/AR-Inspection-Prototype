#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using Inspection.AR;
using Inspection.Core;
using Inspection.Network;
using Inspection.UI;

namespace Inspection.Editor
{
    public static class SceneSetupWizard
    {
        [MenuItem("Tools/巡检系统/一键自动创建并配置 AR 场景 (Setup AR Scene)", false, 1)]
        public static void SetupScene()
        {
            // 1. 确保 Scenes 目录存在
            string scenesDir = "Assets/Scenes";
            if (!Directory.Exists(scenesDir))
            {
                Directory.CreateDirectory(scenesDir);
                AssetDatabase.Refresh();
            }

            string scenePath = Path.Combine(scenesDir, "MainARScene.unity").Replace('\\', '/');
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. 基础灯光
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.0f;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            // 3. AR Session
            GameObject arSessionObj = new GameObject("AR Session");
            arSessionObj.AddComponent<ARSession>();
            arSessionObj.AddComponent<ARInputManager>();

            // 4. XR Origin / AR Session Origin
            GameObject originObj = new GameObject("XR Origin");
            var raycastManager = originObj.AddComponent<ARRaycastManager>();
            var planeManager = originObj.AddComponent<ARPlaneManager>();
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;

            // 相机挂载
            GameObject cameraOffset = new GameObject("Camera Offset");
            cameraOffset.transform.SetParent(originObj.transform, false);

            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.transform.SetParent(cameraOffset.transform, false);
            cameraObj.tag = "MainCamera";
            Camera cam = cameraObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20f;
            cameraObj.AddComponent<ARCameraManager>();
            cameraObj.AddComponent<ARCameraBackground>();

            // 5. EventSystem
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            eventSystemObj.AddComponent<StandaloneInputModule>();

            // 6. UI Canvas
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2400);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 7. 构建 UI 子元素
            // 7.1 Toast
            GameObject toastObj = CreateToastUI(canvasObj.transform);
            ToastUI toastUI = toastObj.GetComponent<ToastUI>();

            // 7.2 HUD
            GameObject hudObj = CreateHUD(canvasObj.transform);
            var settingsBtn = hudObj.transform.Find("TopBar/BtnSettings")?.GetComponent<Button>();
            var historyBtn = hudObj.transform.Find("TopBar/BtnHistory")?.GetComponent<Button>();
            var refreshBtn = hudObj.transform.Find("TopBar/BtnRefresh")?.GetComponent<Button>();
            var statusPillText = hudObj.transform.Find("TopBar/StatusPill/Text")?.GetComponent<TextMeshProUGUI>();
            var statusPillDot = hudObj.transform.Find("TopBar/StatusPill/Dot")?.GetComponent<Image>();
            var hintPanel = hudObj.transform.Find("BottomHint")?.gameObject;

            // 7.3 表单 Dialog
            GameObject formObj = CreateFormUI(canvasObj.transform);
            InspectionFormUI formUI = formObj.GetComponent<InspectionFormUI>();

            // 7.4 设置 Dialog
            GameObject settingsObj = CreateSettingsUI(canvasObj.transform);
            SettingsDialogUI settingsUI = settingsObj.GetComponent<SettingsDialogUI>();

            // 7.5 历史列表 Dialog
            GameObject historyObj = CreateHistoryUI(canvasObj.transform);
            HistoryListUI historyUI = historyObj.GetComponent<HistoryListUI>();

            // 8. Managers 根节点
            GameObject managerObj = new GameObject("AppManager");
            var placementManager = managerObj.AddComponent<ARPlacementManager>();
            var apiClient = managerObj.AddComponent<InspectionApiClient>();
            var gameManager = managerObj.AddComponent<GameManager>();

            // 9. 反射赋值私有 SerializedField 属性
            var serializedPlacement = new SerializedObject(placementManager);
            serializedPlacement.FindProperty("raycastManager").objectReferenceValue = raycastManager;
            serializedPlacement.FindProperty("planeManager").objectReferenceValue = planeManager;
            serializedPlacement.FindProperty("arCamera").objectReferenceValue = cam;
            serializedPlacement.ApplyModifiedProperties();

            var serializedGM = new SerializedObject(gameManager);
            serializedGM.FindProperty("placementManager").objectReferenceValue = placementManager;
            serializedGM.FindProperty("apiClient").objectReferenceValue = apiClient;
            serializedGM.FindProperty("formUI").objectReferenceValue = formUI;
            serializedGM.FindProperty("settingsUI").objectReferenceValue = settingsUI;
            serializedGM.FindProperty("historyUI").objectReferenceValue = historyUI;
            serializedGM.FindProperty("toastUI").objectReferenceValue = toastUI;
            serializedGM.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            serializedGM.FindProperty("historyButton").objectReferenceValue = historyBtn;
            serializedGM.FindProperty("refreshButton").objectReferenceValue = refreshBtn;
            serializedGM.FindProperty("scanningHintPanel").objectReferenceValue = hintPanel;
            serializedGM.FindProperty("statusPillText").objectReferenceValue = statusPillText;
            serializedGM.FindProperty("statusPillDot").objectReferenceValue = statusPillDot;
            serializedGM.ApplyModifiedProperties();

            // 10. 保存场景并注册到 Build Settings
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettingsScene[] existingScenes = EditorBuildSettings.scenes;
            bool alreadyInBuild = false;
            foreach (var s in existingScenes)
            {
                if (s.path == scenePath) { alreadyInBuild = true; break; }
            }
            if (!alreadyInBuild)
            {
                var newScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
                existingScenes.CopyTo(newScenes, 0);
                newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = newScenes;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("创建成功",
                $"AR 巡检场景已成功生成至:\n{scenePath}\n\n已自动挂载 ARSession、平面检测器、HUD 状态栏、网络配置面板与表单。\n可以直接点击 Play 调试或打包 APK！", "好");
        }

        private static GameObject CreateToastUI(Transform parent)
        {
            GameObject toastObj = new GameObject("ToastBanner");
            toastObj.transform.SetParent(parent, false);

            var rt = toastObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.9f);
            rt.anchorMax = new Vector2(0.9f, 0.96f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = toastObj.AddComponent<Image>();
            img.color = new Color(0.1f, 0.15f, 0.25f, 0.95f);

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(toastObj.transform, false);
            var textRt = textObj.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(20, 0);
            textRt.offsetMax = new Vector2(-20, 0);
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 32;
            text.color = Color.white;

            var toast = toastObj.AddComponent<ToastUI>();
            var so = new SerializedObject(toast);
            so.FindProperty("backgroundImage").objectReferenceValue = img;
            so.FindProperty("messageText").objectReferenceValue = text;
            so.ApplyModifiedProperties();

            toastObj.SetActive(false);
            return toastObj;
        }

        private static GameObject CreateHUD(Transform parent)
        {
            GameObject hud = new GameObject("HUD");
            hud.transform.SetParent(parent, false);
            var rt = hud.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // TopBar
            GameObject topBar = new GameObject("TopBar");
            topBar.transform.SetParent(hud.transform, false);
            var tbRt = topBar.AddComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0, 0.92f);
            tbRt.anchorMax = new Vector2(1, 1);
            tbRt.offsetMin = Vector2.zero;
            tbRt.offsetMax = Vector2.zero;
            var tbImg = topBar.AddComponent<Image>();
            tbImg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);

            // Title
            GameObject titleObj = new GameObject("AppTitle");
            titleObj.transform.SetParent(topBar.transform, false);
            var tRt = titleObj.AddComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.04f, 0);
            tRt.anchorMax = new Vector2(0.45f, 1);
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            var tText = titleObj.AddComponent<TextMeshProUGUI>();
            tText.text = "AR 巡检问题标注";
            tText.fontSize = 36;
            tText.fontStyle = FontStyles.Bold;
            tText.alignment = TextAlignmentOptions.MidlineLeft;
            tText.color = Color.white;

            // StatusPill
            GameObject pill = new GameObject("StatusPill");
            pill.transform.SetParent(topBar.transform, false);
            var pRt = pill.AddComponent<RectTransform>();
            pRt.anchorMin = new Vector2(0.46f, 0.2f);
            pRt.anchorMax = new Vector2(0.68f, 0.8f);
            pRt.offsetMin = Vector2.zero;
            pRt.offsetMax = Vector2.zero;
            var pImg = pill.AddComponent<Image>();
            pImg.color = new Color(0.12f, 0.16f, 0.24f, 0.9f);

            GameObject dot = new GameObject("Dot");
            dot.transform.SetParent(pill.transform, false);
            var dotRt = dot.AddComponent<RectTransform>();
            dotRt.anchorMin = new Vector2(0.08f, 0.35f);
            dotRt.anchorMax = new Vector2(0.18f, 0.65f);
            dotRt.offsetMin = Vector2.zero;
            dotRt.offsetMax = Vector2.zero;
            var dotImg = dot.AddComponent<Image>();
            dotImg.color = Color.yellow;

            GameObject pTextObj = new GameObject("Text");
            pTextObj.transform.SetParent(pill.transform, false);
            var ptRt = pTextObj.AddComponent<RectTransform>();
            ptRt.anchorMin = new Vector2(0.24f, 0);
            ptRt.anchorMax = new Vector2(0.95f, 1);
            ptRt.offsetMin = Vector2.zero;
            ptRt.offsetMax = Vector2.zero;
            var ptText = pTextObj.AddComponent<TextMeshProUGUI>();
            ptText.text = "检测连接中";
            ptText.fontSize = 26;
            ptText.alignment = TextAlignmentOptions.MidlineLeft;
            ptText.color = new Color(0.85f, 0.9f, 0.95f);

            // Buttons: Settings, History, Refresh
            CreateSimpleButton(topBar.transform, "BtnSettings", "⚙ 设置", new Vector2(0.70f, 0.15f), new Vector2(0.84f, 0.85f));
            CreateSimpleButton(topBar.transform, "BtnHistory", "历史", new Vector2(0.85f, 0.15f), new Vector2(0.97f, 0.85f));

            // BottomHint
            GameObject hint = new GameObject("BottomHint");
            hint.transform.SetParent(hud.transform, false);
            var hRt = hint.AddComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0.1f, 0.04f);
            hRt.anchorMax = new Vector2(0.9f, 0.09f);
            hRt.offsetMin = Vector2.zero;
            hRt.offsetMax = Vector2.zero;
            var hImg = hint.AddComponent<Image>();
            hImg.color = new Color(0.05f, 0.08f, 0.14f, 0.85f);

            GameObject hintTextObj = new GameObject("Text");
            hintTextObj.transform.SetParent(hint.transform, false);
            var htRt = hintTextObj.AddComponent<RectTransform>();
            htRt.anchorMin = Vector2.zero;
            htRt.anchorMax = Vector2.one;
            htRt.offsetMin = Vector2.zero;
            htRt.offsetMax = Vector2.zero;
            var htText = hintTextObj.AddComponent<TextMeshProUGUI>();
            htText.text = "对准地面扫描识别平面，点击地面放置巡检标记";
            htText.fontSize = 28;
            htText.alignment = TextAlignmentOptions.Center;
            htText.color = new Color(0.3f, 0.85f, 1f);

            return hud;
        }

        private static GameObject CreateFormUI(Transform parent)
        {
            GameObject formObj = new GameObject("FormDialog");
            formObj.transform.SetParent(parent, false);
            var rt = formObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, 0.22f);
            rt.anchorMax = new Vector2(0.92f, 0.78f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = formObj.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.11f, 0.18f, 0.96f);

            // 标题文本
            CreateLabel(formObj.transform, "TitleLabel", "<b>上报巡检问题</b>", new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f), 36);

            // 坐标显示
            var coords = CreateLabel(formObj.transform, "CoordsText", "坐标: (0.00, 0.00, 0.00)", new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.86f), 26, new Color(0.3f, 0.85f, 1f));

            // 标题输入框
            CreateLabel(formObj.transform, "Lbl1", "问题标题 (必填):", new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.78f), 26);
            var titleInput = CreateInputField(formObj.transform, "InputTitle", "例如: 3号墙面破损裂纹", new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.71f));

            // 描述输入框
            CreateLabel(formObj.transform, "Lbl2", "问题描述 (可选):", new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.60f), 26);
            var descInput = CreateInputField(formObj.transform, "InputDesc", "详细描述现场状况...", new Vector2(0.08f, 0.40f), new Vector2(0.92f, 0.53f));

            // 优先级下拉
            CreateLabel(formObj.transform, "Lbl3", "优先级:", new Vector2(0.08f, 0.32f), new Vector2(0.40f, 0.38f), 26);
            var priorityDropdown = CreateDropdown(formObj.transform, "DropdownPriority", new Vector2(0.42f, 0.30f), new Vector2(0.92f, 0.38f));

            // 错误提示
            var errorText = CreateLabel(formObj.transform, "ErrorText", "", new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.28f), 24, new Color(1f, 0.35f, 0.45f));
            errorText.gameObject.SetActive(false);

            // 提交 / 取消按钮
            var cancelBtn = CreateSimpleButton(formObj.transform, "BtnCancel", "取消", new Vector2(0.08f, 0.06f), new Vector2(0.45f, 0.18f), new Color(0.2f, 0.25f, 0.35f));
            var submitBtn = CreateSimpleButton(formObj.transform, "BtnSubmit", "提交问题", new Vector2(0.55f, 0.06f), new Vector2(0.92f, 0.18f), new Color(0.1f, 0.65f, 0.95f));

            var formComp = formObj.AddComponent<InspectionFormUI>();
            var so = new SerializedObject(formComp);
            so.FindProperty("titleInput").objectReferenceValue = titleInput;
            so.FindProperty("descriptionInput").objectReferenceValue = descInput;
            so.FindProperty("priorityDropdown").objectReferenceValue = priorityDropdown;
            so.FindProperty("coordsText").objectReferenceValue = coords;
            so.FindProperty("submitButton").objectReferenceValue = submitBtn;
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            so.FindProperty("submitButtonText").objectReferenceValue = submitBtn.GetComponentInChildren<TextMeshProUGUI>();
            so.FindProperty("validationErrorText").objectReferenceValue = errorText;
            so.ApplyModifiedProperties();

            formObj.SetActive(false);
            return formObj;
        }

        private static GameObject CreateSettingsUI(Transform parent)
        {
            GameObject settingsObj = new GameObject("SettingsDialog");
            settingsObj.transform.SetParent(parent, false);
            var rt = settingsObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.08f, 0.30f);
            rt.anchorMax = new Vector2(0.92f, 0.70f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = settingsObj.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            CreateLabel(settingsObj.transform, "Header", "<b>Go 后端服务配置</b>", new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.94f), 34);
            CreateLabel(settingsObj.transform, "Lbl", "局域网服务地址:", new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.80f), 26);
            var urlInput = CreateInputField(settingsObj.transform, "InputUrl", "http://192.168.1.100:8080", new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.70f));

            var feedback = CreateLabel(settingsObj.transform, "Feedback", "请输入运行 Go 后端的电脑 IP", new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.54f), 24, new Color(0.7f, 0.8f, 0.9f));

            var testBtn = CreateSimpleButton(settingsObj.transform, "BtnTest", "测试连接", new Vector2(0.08f, 0.18f), new Vector2(0.48f, 0.32f), new Color(0.15f, 0.55f, 0.85f));
            var saveBtn = CreateSimpleButton(settingsObj.transform, "BtnSave", "保存", new Vector2(0.52f, 0.18f), new Vector2(0.92f, 0.32f), new Color(0.1f, 0.75f, 0.45f));
            var closeBtn = CreateSimpleButton(settingsObj.transform, "BtnClose", "关闭", new Vector2(0.30f, 0.04f), new Vector2(0.70f, 0.14f), new Color(0.2f, 0.25f, 0.35f));

            var settingsComp = settingsObj.AddComponent<SettingsDialogUI>();
            var so = new SerializedObject(settingsComp);
            so.FindProperty("urlInputField").objectReferenceValue = urlInput;
            so.FindProperty("statusFeedbackText").objectReferenceValue = feedback;
            so.FindProperty("testConnectionButton").objectReferenceValue = testBtn;
            so.FindProperty("saveButton").objectReferenceValue = saveBtn;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedProperties();

            settingsObj.SetActive(false);
            return settingsObj;
        }

        private static GameObject CreateHistoryUI(Transform parent)
        {
            GameObject histObj = new GameObject("HistoryDialog");
            histObj.transform.SetParent(parent, false);
            var rt = histObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.15f);
            rt.anchorMax = new Vector2(0.94f, 0.85f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = histObj.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.11f, 0.18f, 0.98f);

            CreateLabel(histObj.transform, "Header", "<b>已上报巡检问题</b>", new Vector2(0.08f, 0.90f), new Vector2(0.60f, 0.97f), 32);
            var countLabel = CreateLabel(histObj.transform, "Count", "共 0 项", new Vector2(0.62f, 0.90f), new Vector2(0.92f, 0.97f), 26, new Color(0.3f, 0.85f, 1f));

            // ScrollView 容器
            GameObject scrollObj = new GameObject("ScrollArea");
            scrollObj.transform.SetParent(histObj.transform, false);
            var sRt = scrollObj.AddComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0.06f, 0.14f);
            sRt.anchorMax = new Vector2(0.94f, 0.88f);
            sRt.offsetMin = Vector2.zero;
            sRt.offsetMax = Vector2.zero;
            var sImg = scrollObj.AddComponent<Image>();
            sImg.color = new Color(0.05f, 0.07f, 0.12f, 0.8f);

            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(scrollObj.transform, false);
            var cRt = contentObj.AddComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0, 1);
            cRt.anchorMax = new Vector2(1, 1);
            cRt.pivot = new Vector2(0.5f, 1);
            cRt.sizeDelta = new Vector2(0, 600);
            var vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.spacing = 10;
            vlg.padding = new RectOffset(10, 10, 10, 10);
            contentObj.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var emptyNotice = CreateLabel(scrollObj.transform, "EmptyNotice", "正在加载列表...", new Vector2(0, 0), new Vector2(1, 1), 28, Color.gray);

            var refreshBtn = CreateSimpleButton(histObj.transform, "BtnRefresh", "刷新", new Vector2(0.10f, 0.03f), new Vector2(0.45f, 0.11f), new Color(0.15f, 0.55f, 0.85f));
            var closeBtn = CreateSimpleButton(histObj.transform, "BtnClose", "关闭", new Vector2(0.55f, 0.03f), new Vector2(0.90f, 0.11f), new Color(0.2f, 0.25f, 0.35f));

            var historyComp = histObj.AddComponent<HistoryListUI>();
            var so = new SerializedObject(historyComp);
            so.FindProperty("listContent").objectReferenceValue = contentObj.transform;
            so.FindProperty("emptyNoticeText").objectReferenceValue = emptyNotice;
            so.FindProperty("totalCountText").objectReferenceValue = countLabel;
            so.FindProperty("refreshButton").objectReferenceValue = refreshBtn;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.ApplyModifiedProperties();

            histObj.SetActive(false);
            return histObj;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color? color = null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color ?? Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            return tmp;
        }

        private static Button CreateSimpleButton(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Color? bg = null)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>();
            img.color = bg ?? new Color(0.15f, 0.2f, 0.3f, 0.9f);
            var btn = obj.AddComponent<Button>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var tRt = textObj.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = Vector2.zero;
            tRt.offsetMax = Vector2.zero;
            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return btn;
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>();
            img.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            var tRt = textObj.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(15, 5);
            tRt.offsetMax = new Vector2(-15, -5);
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 26;
            text.color = Color.white;

            GameObject phObj = new GameObject("Placeholder");
            phObj.transform.SetParent(obj.transform, false);
            var phRt = phObj.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(15, 5);
            phRt.offsetMax = new Vector2(-15, -5);
            var phText = phObj.AddComponent<TextMeshProUGUI>();
            phText.text = placeholder;
            phText.fontSize = 26;
            phText.fontStyle = FontStyles.Italic;
            phText.color = new Color(0.5f, 0.6f, 0.7f, 0.7f);

            var input = obj.AddComponent<TMP_InputField>();
            input.textComponent = text;
            input.placeholder = phText;
            return input;
        }

        private static TMP_Dropdown CreateDropdown(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = obj.AddComponent<Image>();
            img.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(obj.transform, false);
            var tRt = textObj.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.offsetMin = new Vector2(15, 0);
            tRt.offsetMax = new Vector2(-15, 0);
            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.text = "高 (high)";
            text.fontSize = 26;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = Color.white;

            var dropdown = obj.AddComponent<TMP_Dropdown>();
            dropdown.captionText = text;
            return dropdown;
        }
    }
}
#endif
