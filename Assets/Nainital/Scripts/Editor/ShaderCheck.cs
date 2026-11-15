using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>Writes the compile messages of the project's own shaders to Logs/shadercheck.txt (the editor log only says
    /// "all subshaders removed"). Bridge: "call Nainital.EditorTools.ShaderCheck.Run".</summary>
    public static class ShaderCheck
    {
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets/Nainital/Shaders" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var s = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (!s) continue;
                var msgs = ShaderUtil.GetShaderMessages(s);
                sb.AppendLine($"{path}: supported={s.isSupported} messages={msgs.Length}");
                foreach (var m in msgs)
                    sb.AppendLine($"  [{m.severity}] {m.platform} {m.file}:{m.line} {m.message} {m.messageDetails}");
            }
            File.WriteAllText("Logs/shadercheck.txt", sb.ToString());
            Debug.Log("[Nainital] shader check written to Logs/shadercheck.txt");
        }
    }
}
