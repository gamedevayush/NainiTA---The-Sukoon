using UnityEngine;

namespace Nainital
{
    /// <summary>Keeps the depth-tested sign-text material pointing at the dynamic font's current texture.</summary>
    public class SignText : MonoBehaviour
    {
        public Font font;
        public Material material;

        void Awake()
        {
            if (!font || !material) return;
            material.mainTexture = font.material.mainTexture;
            Font.textureRebuilt += OnRebuilt;
        }

        void OnDestroy() => Font.textureRebuilt -= OnRebuilt;

        void OnRebuilt(Font f)
        {
            if (f == font && material) material.mainTexture = f.material.mainTexture;
        }
    }
}
