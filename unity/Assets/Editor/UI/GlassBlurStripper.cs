using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools.UI
{
    /// <summary>
    /// P3-UIX-09 build check: the desktop-only glass blur never reaches the headset. When the player is built for Android every variant of the glass shader with PS_GLASS_BLUR
    /// is removed before compilation, so the Quest build cannot sample the opaque texture even if something turned the keyword on. Desktop builds keep it. A separate class
    /// from QuestShaderStripping so the evidence-based Quest list stays as it is.
    /// </summary>
    public sealed class GlassBlurStripper : IPreprocessShaders
    {
        public int callbackOrder => 101;

        /// <summary>Whether a variant survives for a build target (the rule itself, testable without a build).</summary>
        public static bool Keep(BuildTarget target, bool blurVariant) => !(blurVariant && target == BuildTarget.Android);

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || shader.name != ProcSense.VR.UI.Theme.GlassPanel.ShaderName || data == null) return;
            var target = EditorUserBuildSettings.activeBuildTarget;
            var blur = new ShaderKeyword(ProcSense.VR.UI.Theme.GlassBlur.Keyword);
            for (int i = data.Count - 1; i >= 0; i--)
                if (!Keep(target, data[i].shaderKeywordSet.IsEnabled(blur))) data.RemoveAt(i);
        }
    }
}
