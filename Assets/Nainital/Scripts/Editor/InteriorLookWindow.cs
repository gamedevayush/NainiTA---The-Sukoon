using UnityEditor;
using UnityEngine;

namespace Nainital.EditorTools
{
    /// <summary>
    /// Nainital -> Fake Interior Look (user, 2026-11-18: "tool jisse main set kr sku, pos and zoom wagera sab, for all").
    /// Sliders for every room at once: Mall Road's shops, the other ground floors, and the light. Changes show at once in
    /// the Scene and Game views, in play mode too, and are kept in Assets/Resources/InteriorLook.asset, which the game
    /// reads at start (so builds and the IPA get the same look). Undo works.
    /// </summary>
    [InitializeOnLoad]
    public class InteriorLookWindow : EditorWindow
    {
        const string AssetPath = "Assets/Resources/" + InteriorLook.ResourceName + ".asset";
        Vector2 scroll;

        static InteriorLookWindow()
        {
            // the editor views show the saved look from the start, not the shader's defaults
            EditorApplication.delayCall += () => { var a = AssetDatabase.LoadAssetAtPath<InteriorLook>(AssetPath); if (a) a.Apply(); };
        }

        [MenuItem("Nainital/Fake Interior Look")]
        public static void Open()
        {
            var w = GetWindow<InteriorLookWindow>("Fake Interiors");
            w.minSize = new Vector2(340, 520);
        }

        static InteriorLook Asset()
        {
            var a = AssetDatabase.LoadAssetAtPath<InteriorLook>(AssetPath);
            if (a) return a;
            a = CreateInstance<InteriorLook>();
            System.IO.Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.CreateAsset(a, AssetPath);
            AssetDatabase.SaveAssets();
            return a;
        }

        static float Row(string label, string tip, float v, float min, float max) =>
            EditorGUILayout.Slider(new GUIContent(label, tip), v, min, max);

        void OnGUI()
        {
            var a = Asset();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Sab rooms pe ek saath lagta hai. Scene / Game view mein turant dikhta hai (Play mein bhi). " +
                                    "Ye values Assets/Resources/InteriorLook.asset mein save hoti hain - game aur IPA wahi padhte hain.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(a, "Fake interior look");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Mall Road ki dukaanein (kaanch, khuli, kiosk)", EditorStyles.boldLabel);
            a.mallSize = Row("Zoom: photo size (m)", "Zyada = room chhota / door dikhe", a.mallSize, 1.5f, 12f);
            a.mallMid = Row("Upar / neeche (m)", "Photo ka beech farsh se kitna upar. Kam = sab neeche", a.mallMid, -2f, 4f);
            a.mallSide = Row("Left / right (m)", "+ = daayein (sadak se dekhne pe)", a.mallSide, -3f, 3f);
            a.mallGlassDepth = Row("Gehraai: kaanch wale (m)", "Restaurant, cafe, bank, hotel: room kaanch se kitna peeche", a.mallGlassDepth, 0.2f, 10f);
            a.mallOpenDepth = Row("Gehraai: khuli dukaan (m)", "Khuli dukaan aur kiosk: room kitna peeche", a.mallOpenDepth, 0.2f, 10f);
            a.mallStretchX = Row("Photo X stretch", "Photo ko chauda / patla (1 = jaisi hai)", a.mallStretchX, 0.3f, 3f);
            a.mallStretchY = Row("Photo Y stretch", "Photo ko lamba / chhota (1 = jaisi hai)", a.mallStretchY, 0.3f, 3f);
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Kaanch / opening ka size (mesh)", EditorStyles.miniBoldLabel);
            a.mallOpenWidth = Row("Opening X: chaudai (%)", "Dukaan ke front ka kitna hissa kaanch/khula ho (1 = poora). Baaki panel", a.mallOpenWidth, 0.2f, 1f);
            a.mallSill = Row("Opening Y: neeche se (m)", "Farsh se kitna upar opening shuru ho", a.mallSill, 0f, 2f);
            a.mallTop = Row("Opening Y: upar tak (m)", "Farsh se kitna upar tak opening ho (2.75 = poora)", a.mallTop, 0.8f, 2.75f);
            a.mallPanel = EditorGUILayout.ColorField(new GUIContent("Panel ka rang", "Opening chhota karne pe bacha hua hissa"), a.mallPanel);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Baaki ground floor (ghar ki khidki, bazaar)", EditorStyles.boldLabel);
            a.baySize = Row("Zoom: photo size (m)", "Zyada = room chhota / door dikhe", a.baySize, 1.5f, 12f);
            a.bayMid = Row("Upar / neeche (m)", "Photo ka beech farsh se kitna upar. Kam = sab neeche", a.bayMid, -2f, 4f);
            a.baySide = Row("Left / right (m)", "+ = daayein (bahar se dekhne pe)", a.baySide, -3f, 3f);
            a.bayWindowDepth = Row("Gehraai: khidki (m)", "Ghar ki khidki ke peeche room kitna door", a.bayWindowDepth, 0.2f, 10f);
            a.bayShopDepth = Row("Gehraai: bazaar dukaan (m)", "Bazaar ki khuli dukaan ka room kitna peeche", a.bayShopDepth, 0.2f, 10f);
            a.bayStretchX = Row("Photo X stretch", "Photo ko chauda / patla (1 = jaisi hai)", a.bayStretchX, 0.3f, 3f);
            a.bayStretchY = Row("Photo Y stretch", "Photo ko lamba / chhota (1 = jaisi hai)", a.bayStretchY, 0.3f, 3f);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Roshni", EditorStyles.boldLabel);
            a.dayLight = Row("Din: sooraj se", "Din mein room kitna roshan", a.dayLight, 0f, 1f);
            a.ambient = Row("Hamesha wali roshni", "Andhere mein bhi thoda dikhe", a.ambient, 0f, 0.4f);
            a.nightLight = Row("Raat: jalti khidki/dukaan", "Raat ko jalte room kitne chamkein", a.nightLight, 0f, 8f);
            a.shopDayLight = Row("Mall dukaan ki din ki tube lights", "Din mein bhi dukaan andar se kitni roshan", a.shopDayLight, 0f, 0.8f);
            a.maxSlide = Row("Max sarakna (m)", "Tirchha dekhne pe room kitna sarak sakta hai", a.maxSlide, 0.5f, 10f);

            if (EditorGUI.EndChangeCheck()) Changed(a);

            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Defaults"))
                {
                    Undo.RecordObject(a, "Fake interior look defaults");
                    var d = CreateInstance<InteriorLook>();
                    EditorUtility.CopySerialized(d, a);
                    a.name = InteriorLook.ResourceName;   // CopySerialized copies the empty name too
                    DestroyImmediate(d);
                    Changed(a);
                }
                if (GUILayout.Button("Asset dikhao")) { Selection.activeObject = a; EditorGUIUtility.PingObject(a); }
            }
            EditorGUILayout.EndScrollView();
        }

        static void Changed(InteriorLook a)
        {
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssetIfDirty(a);
            a.Apply();
            SceneView.RepaintAll();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }
    }
}
