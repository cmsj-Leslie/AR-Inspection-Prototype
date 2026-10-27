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
        public static InspectionApiClient Instance { get; private set; }

        private const string PrefsKeyUrl = "Inspection_Backend_Url";
        public const string DefaultBaseUrl = "http://192.168.1.100:8080";

        [SerializeField]
        private string currentBaseUrl;

        public string BaseUrl
        {
            get => currentBaseUrl;
            set
            {
                currentBaseUrl = NormalizeUrl(value);
                PlayerPrefs.SetString(PrefsKeyUrl, currentBaseUrl);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                currentBaseUrl = PlayerPrefs.GetString(PrefsKeyUrl, DefaultBaseUrl);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return DefaultBaseUrl;
            url = url.Trim();
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "http://" + url;
            }
            return url.TrimEnd('/');
        }

        /// <summary>
        /// 检查后端健康状态与延迟
        /// </summary>
        public void CheckHealth(Action<bool, HealthResponse, string> callback)
        {
            StartCoroutine(GetCoroutine<HealthResponse>("/api/health", 5, callback));
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
