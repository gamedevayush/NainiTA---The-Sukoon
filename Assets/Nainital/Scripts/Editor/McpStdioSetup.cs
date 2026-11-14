using UnityEditor;

namespace Nainital.EditorTools
{
    /// <summary>
    /// MCP for Unity (com.coplaydev.unity-mcp) starts in HTTP mode by default, but Claude Code on this PC talks to it over
    /// stdio (claude mcp: UnityMCP = uvx mcpforunityserver). Switch the editor to stdio once, so its bridge starts on load.
    /// Changing the transport in Window > MCP for Unity afterwards is kept: this only runs while the pref is unset.
    /// </summary>
    [InitializeOnLoad]
    static class McpStdioSetup
    {
        const string Key = "MCPForUnity.UseHttpTransport";

        static McpStdioSetup()
        {
            if (EditorPrefs.HasKey(Key)) return;
            EditorPrefs.SetBool(Key, false);
            UnityEngine.Debug.Log("[Nainital] MCP for Unity set to stdio for Claude Code; reloading scripts so its bridge starts");
            EditorApplication.delayCall += EditorUtility.RequestScriptReload;
        }
    }
}
