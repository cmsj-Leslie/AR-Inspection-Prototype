using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Inspection.Models;

namespace Inspection.Network
{
    public class InspectionApiClient : MonoBehaviour
    {
        private static InspectionApiClient instance;
        public static InspectionApiClient Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<InspectionApiClient>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("InspectionApiClient");
                        instance = go.AddComponent<InspectionApiClient>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        private const string PrefsKeyUrl = "Inspection_Backend_Url";
        public const string DefaultBaseUrl = "http://192.168.31.6:8080";

        [SerializeField]
        private string currentBaseUrl;

        public string BaseUrl
        {
            get
            {
                if (string.IsNullOrEmpty(currentBaseUrl))
                {
                    currentBaseUrl = PlayerPrefs.GetString(PrefsKeyUrl, DefaultBaseUrl);
                }
                return currentBaseUrl;
            }
            set
            {
                currentBaseUrl = NormalizeUrl(value);
                PlayerPrefs.SetString(PrefsKeyUrl, currentBaseUrl);
                PlayerPrefs.Save();
                Debug.Log($"[InspectionApiClient] 基础服务地址已更新并持久化: {currentBaseUrl}");
            }
        }

        public event Action<bool, HealthResponse, string> OnConnectionStatusChanged;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                // 优先读取用户在【设置】面板保存的自定义 IP；只有初次打开从未设置过时，才使用 DefaultBaseUrl
                currentBaseUrl = PlayerPrefs.GetString(PrefsKeyUrl, DefaultBaseUrl);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        public static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return DefaultBaseUrl;
            
            // 容错处理：过滤空格，将手机输入法偶尔输入的中文全角冒号 "：" 转为半角 ":"
            url = url.Trim().Replace(" ", "").Replace("：", ":");
            
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "http://" + url;
            }
            url = url.TrimEnd('/');

            // 智能补全端口：如果用户只输入了 IP（如 192.168.31.6），没有输入端口号，自动为其补全 :8080
            int schemeIdx = url.IndexOf("://", StringComparison.Ordinal);
            string hostAndPort = schemeIdx >= 0 ? url.Substring(schemeIdx + 3) : url;
            if (!hostAndPort.Contains(":"))
            {
                url += ":8080";
            }

            return url;
        }

        /// <summary>
        /// 检查后端健康状态与延迟
        /// </summary>
        public void CheckHealth(Action<bool, HealthResponse, string> callback)
        {
            StartCoroutine(GetCoroutine<HealthResponse>("/api/health", 3, (success, health, err) =>
            {
                OnConnectionStatusChanged?.Invoke(success, health, err);
                callback?.Invoke(success, health, err);
            }));
        }

        /// <summary>
        /// 获取问题列表
        /// </summary>
        public void GetIssues(Action<bool, IssueListResponse, string> callback)
        {
            StartCoroutine(GetCoroutine<IssueListResponse>("/api/issues", 10, callback));
        }

        /// <summary>
        /// 上报新问题
        /// </summary>
        public void CreateIssue(CreateIssueRequest request, Action<bool, Issue, string> callback)
        {
            string jsonBody = request.ToJson();
            StartCoroutine(PostCoroutine<Issue>("/api/issues", jsonBody, 10, callback));
        }

        /// <summary>
        /// 更新问题处理状态
        /// </summary>
        public void UpdateStatus(string issueId, string newStatus, Action<bool, Issue, string> callback)
        {
            var req = new UpdateStatusRequest { status = newStatus };
            string jsonBody = req.ToJson();
            StartCoroutine(PatchCoroutine<Issue>($"/api/issues/{Uri.EscapeDataString(issueId)}/status", jsonBody, 10, callback));
        }

        private IEnumerator GetCoroutine<T>(string endpoint, int timeoutSeconds, Action<bool, T, string> callback) where T : class
        {
            string url = BaseUrl + endpoint;
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = timeoutSeconds;
                yield return req.SendWebRequest();

                HandleResponse(req, callback);
            }
        }

        private IEnumerator PostCoroutine<T>(string endpoint, string jsonBody, int timeoutSeconds, Action<bool, T, string> callback) where T : class
        {
            string url = BaseUrl + endpoint;
            using (UnityWebRequest req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = timeoutSeconds;

                yield return req.SendWebRequest();

                HandleResponse(req, callback);
            }
        }

        private IEnumerator PatchCoroutine<T>(string endpoint, string jsonBody, int timeoutSeconds, Action<bool, T, string> callback) where T : class
        {
            string url = BaseUrl + endpoint;
            using (UnityWebRequest req = new UnityWebRequest(url, "PATCH"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = timeoutSeconds;

                yield return req.SendWebRequest();

                HandleResponse(req, callback);
            }
        }

        private void HandleResponse<T>(UnityWebRequest req, Action<bool, T, string> callback) where T : class
        {
            string responseText = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;

            if (req.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    T data = JsonUtility.FromJson<T>(responseText);
                    callback?.Invoke(true, data, null);
                }
                catch (Exception e)
                {
                    callback?.Invoke(false, null, $"解析响应失败: {e.Message}");
                }
            }
            else
            {
                string errorMessage = ParseBackendError(responseText, req.error);
                // 若出现网络传输或连接级错误，自动广播连接异常以更新首页状态胶囊
                if (req.result == UnityWebRequest.Result.ConnectionError || req.responseCode == 0)
                {
                    OnConnectionStatusChanged?.Invoke(false, null, errorMessage);
                }
                callback?.Invoke(false, null, errorMessage);
            }
        }

        private string ParseBackendError(string responseText, string defaultError)
        {
            if (!string.IsNullOrEmpty(responseText))
            {
                try
                {
                    ApiErrorResponse errRes = JsonUtility.FromJson<ApiErrorResponse>(responseText);
                    if (errRes != null && errRes.error != null && !string.IsNullOrEmpty(errRes.error.message))
                    {
                        return errRes.error.message;
                    }
                }
                catch
                {
                    // Ignore parse error and fallback
                }
            }

            if (defaultError != null && defaultError.Contains("Cannot resolve destination host"))
            {
                return $"无法连接到服务器 ({BaseUrl})，请检查局域网 IP 与端口设置";
            }

            return defaultError ?? "网络请求异常";
        }
    }
}
