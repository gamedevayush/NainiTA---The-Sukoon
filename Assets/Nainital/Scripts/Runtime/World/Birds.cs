using UnityEngine;

namespace Nainital
{
    /// <summary>Flocks of small birds over the lake and slow-soaring kites/eagles (cheel) above the hills.</summary>
    public class Birds : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 550f;
        public float baseHeight = 1990f;
        public int smallBirds = 28, eagles = 5;
        public Material material;

        Transform[] body, wingL, wingR;
        float[] ang, spd, rad, hgt, flap, wob;
        bool[] isEagle;

        static Mesh Part(bool wing, float scale, int pal)
        {
            var mb = new MeshBuilder();
            Vector2 uv = Atlas.SolidUV(pal);
            if (wing)
            {
                mb.Tri(Vector3.zero, new Vector3(0.9f, 0, -0.15f) * scale, new Vector3(0.2f, 0, -0.45f) * scale, uv, Vector3.up);
                mb.Tri(Vector3.zero, new Vector3(0.9f, 0, -0.15f) * scale, new Vector3(0.2f, 0, -0.45f) * scale, uv, Vector3.down);
            }
            else mb.Sphere(Vector3.zero, new Vector3(0.12f, 0.1f, 0.4f) * scale, 5, 3, uv);
            return mb.ToMesh(wing ? "Wing" : "BirdBody");
        }

        void Start()
        {
            int n = smallBirds + eagles;
            body = new Transform[n]; wingL = new Transform[n]; wingR = new Transform[n];
            ang = new float[n]; spd = new float[n]; rad = new float[n]; hgt = new float[n]; flap = new float[n]; wob = new float[n];
            isEagle = new bool[n];
            Mesh sb = Part(false, 0.5f, Pal.HairBlack), sw = Part(true, 0.5f, Pal.HairBlack);
            Mesh eb = Part(false, 2.2f, Pal.HairBrown), ew = Part(true, 2.2f, Pal.HairBrown);
            for (int i = 0; i < n; i++)
            {
                bool e = i >= smallBirds;
                isEagle[i] = e;
                var go = new GameObject(e ? "Cheel" : "Bird");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = e ? eb : sb;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                body[i] = go.transform;
                for (int s = 0; s < 2; s++)
                {
                    var w = new GameObject("Wing");
                    w.transform.SetParent(go.transform, false);
                    w.transform.localScale = new Vector3(s == 0 ? -1 : 1, 1, 1);
                    w.AddComponent<MeshFilter>().sharedMesh = e ? ew : sw;
                    w.AddComponent<MeshRenderer>().sharedMaterial = material;
                    if (s == 0) wingL[i] = w.transform; else wingR[i] = w.transform;
                }
                int flock = i % 3;
                ang[i] = Random.value * Mathf.PI * 2f;
                rad[i] = e ? Random.Range(300f, radius * 1.3f) : radius * (0.25f + flock * 0.2f) + Random.Range(-25f, 25f);
                spd[i] = e ? Random.Range(9f, 13f) : Random.Range(11f, 15f);
                hgt[i] = baseHeight + (e ? Random.Range(220f, 380f) : 25f + flock * 30f + Random.Range(-6f, 6f));
                flap[i] = Random.value * 10f;
                wob[i] = Random.value * 10f;
                if (!e && i > 0 && Random.value < 0.7f) { ang[i] = ang[i - 1] + Random.Range(0.01f, 0.04f); rad[i] = rad[i - 1] + Random.Range(-6f, 6f); }
            }
        }

        void Update()
        {
            if (body == null) return;
            float dt = Time.deltaTime, t = Time.time;
            for (int i = 0; i < body.Length; i++)
            {
                ang[i] += spd[i] / rad[i] * dt;
                float wobble = Mathf.Sin(t * 0.3f + wob[i]) * (isEagle[i] ? 40f : 15f);
                float r = rad[i] + wobble;
                Vector3 p = center + new Vector3(Mathf.Cos(ang[i]) * r, 0, Mathf.Sin(ang[i]) * r);
                p.y = hgt[i] + Mathf.Sin(t * 0.5f + wob[i]) * 6f;
                Vector3 tangent = new Vector3(-Mathf.Sin(ang[i]), 0, Mathf.Cos(ang[i]));
                body[i].SetPositionAndRotation(p, Quaternion.LookRotation(tangent) * Quaternion.Euler(0, 0, isEagle[i] ? 18f : 8f));
                float a = isEagle[i] ? 6f + 4f * Mathf.Sin(t * 0.8f + flap[i]) : 45f * Mathf.Sin(t * 14f + flap[i]);
                wingL[i].localRotation = Quaternion.Euler(0, 0, -a);
                wingR[i].localRotation = Quaternion.Euler(0, 0, a);
            }
        }
    }
}
