using UnityEngine;

namespace Inspection.AR
{
    public static class MaterialHelper
    {
        /// <summary>
        /// 兼容 URP (Universal Render Pipeline)、内置渲染管线 (Built-in) 与移动端 Shader，避免粉红丢失材质
        /// </summary>
        public static Material CreateColoredMaterial(Color color, float smoothness = 0.5f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Mobile/Diffuse");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            return mat;
        }
    }
}
