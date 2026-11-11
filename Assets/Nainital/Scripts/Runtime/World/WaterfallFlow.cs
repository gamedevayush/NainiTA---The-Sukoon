using UnityEngine;

namespace Nainital
{
    /// <summary>Runs the streaks of a waterfall downwards (scrolls the material's texture).</summary>
    public class WaterfallFlow : MonoBehaviour
    {
        public float speed = 1.6f;
        Material mat;

        void Start()
        {
            var r = GetComponent<Renderer>();
            if (r) mat = r.material;
        }

        void Update()
        {
            if (!mat) return;
            var o = mat.mainTextureOffset;
            o.y = Mathf.Repeat(o.y + speed * Time.deltaTime, 1f);
            mat.mainTextureOffset = o;
        }
    }
}
