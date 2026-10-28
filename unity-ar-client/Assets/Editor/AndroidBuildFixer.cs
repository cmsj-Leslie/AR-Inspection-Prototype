#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace Inspection.Editor
{
    /// <summary>
    /// 自动修复 Android 打包 ARCore 所需的 SDK、图形 API 与架构配置
    /// </summary>
    [InitializeOnLoad]
    public static class AndroidBuildFixer
    {
        static AndroidBuildFixer()
        {
            EditorApplication.delayCall += FixSettings;
        }

        [MenuItem("Tools/巡检系统/一键修复 Android 构建配置 (Fix Android Settings)", false, 2)]
        public static void FixSettings()
        {
            bool changed = false;

            // 1. ARCore 要求 Minimum API Level >= 24 (Android 7.0)
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel24)
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
                changed = true;
                Debug.Log("[巡检系统] 已将 Android Minimum API Level 修改为 24 (Android 7.0)");
            }

            // 2. ARCore 不支持 Vulkan，强制使用 OpenGLES3
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) || apis.Length == 0 || apis[0] != GraphicsDeviceType.OpenGLES3)
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new GraphicsDeviceType[] { GraphicsDeviceType.OpenGLES3 });
                changed = true;
                Debug.Log("[巡检系统] 已移除 Vulkan，设置 Android 图形 API 为 OpenGLES3 (ARCore 要求)");
            }

            // 3. 脚本后端改为 IL2CPP (ARM64 架构必需)
            var currentBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            if (currentBackend != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                changed = true;
                Debug.Log("[巡检系统] 已将 Android Scripting Backend 修改为 IL2CPP");
            }

            // 4. 目标架构启用 ARM64 (三星 S23 Ultra / 骁龙8Gen2 纯64位设备必需)
            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
                changed = true;
                Debug.Log("[巡检系统] 已将 Android Target Architectures 启用 ARM64 + ARMv7");
            }

            // 5. 允许局域网明文 HTTP 通信 (http://192.168.x.x:8080)
            if (PlayerSettings.insecureHttpOption != InsecureHttpOption.AlwaysAllowed)
            {
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                changed = true;
                Debug.Log("[巡检系统] 已开启 Android 局域网 HTTP 明文网络通信支持！");
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("<color=green>[巡检系统] Android 构建设置已自动配置完毕！可以直接点击 Build And Run 进行打包。</color>");
            }
        }
    }
}
#endif
