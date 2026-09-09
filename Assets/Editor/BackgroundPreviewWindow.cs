using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 배경 프리뷰 도구.
///
/// 새 배경을 그릴 때마다 반복하는 작업을 버튼 하나로 만든다.
///   1) Aseprite 에서 레이어를 나눠 그린다 (아래 레이어 = 먼 배경)
///   2) File ▸ Export As ▸ "Split Layers" 체크 → PNG 여러 장
///   3) Assets/Art/Backgrounds/&lt;배경이름&gt;/ 폴더에 넣는다 (파일명 앞에 00_, 01_ … 순번)
///   4) Tools ▸ 잔혹동화 ▸ 배경 프리뷰 열고 → 배경 고르고 → 버튼
///
/// .aseprite 를 직접 쓰려면 인스펙터에서 Layer Import Mode = Individual Layers 로 바꾸면
/// 스프라이트가 레이어별로 생성되어 자동 인식된다.
/// </summary>
public class BackgroundPreviewWindow : EditorWindow
{
    const float PPU = 32f;
    const float ORTHO = 8.4375f;                  // 960x540 @ PPU32
    static float ScreenH { get { return ORTHO * 2f; } }              // 16.875 유닛
    static float ScreenW { get { return ORTHO * 2f * 16f / 9f; } }   // 30 유닛

    static readonly string[] SCAN_DIRS = { "Assets/Art/Backgrounds", "Assets/Aseprites/Backgrounds" };
    const int LAYER_GROUND = 6, LAYER_PLAYER = 8;

    class BgSet
    {
        public string name;
        public string path;
        public List<Sprite> sprites = new List<Sprite>();
        public int W, H;
    }

    List<BgSet> sets = new List<BgSet>();
    int selected;
    Dictionary<string, float> seam = new Dictionary<string, float>();
    Vector2 scroll;

    enum FitMode { Native, FitScreenHeight, FitRoomHeight }
    FitMode fit = FitMode.FitScreenHeight;
    bool integerScale = true;

    float maxFactor = 0.90f;
    bool useForeground = true;
    float foregroundFactor = 1.15f;
    float verticalRatio = 0.25f;
    float curve = 1.6f;

    int roomW = 160, roomH = 32;
    bool spawnPlayer = true;

    [MenuItem("Tools/잔혹동화/배경 프리뷰 %#b")]
    static void Open()
    {
        var w = GetWindow<BackgroundPreviewWindow>("배경 프리뷰");
        w.minSize = new Vector2(380, 520);
        w.Rescan();
    }

    void OnEnable() { Rescan(); }

    // ────────────────────────────────────────────────────────────── 스캔
    void Rescan()
    {
        sets.Clear();
        foreach (var root in SCAN_DIRS)
        {
            if (!AssetDatabase.IsValidFolder(root)) continue;

            // 폴더 하나 = 배경 세트 (안의 이미지들이 레이어)
            foreach (var sub in AssetDatabase.GetSubFolders(root))
            {
                var s = new BgSet { name = Path.GetFileName(sub), path = sub };
                var guids = AssetDatabase.FindAssets("t:Sprite", new[] { sub });
                var paths = guids.Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p).ToList();
                foreach (var p in paths)
                {
                    var sp = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                    if (sp != null) s.sprites.Add(sp);
                }
                if (s.sprites.Count > 0) { Measure(s); sets.Add(s); }
            }

            // .aseprite 한 장 = 배경 세트 (Individual Layers 로 임포트된 경우 레이어별 스프라이트)
            foreach (var guid in AssetDatabase.FindAssets("t:Object", new[] { root }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.EndsWith(".aseprite") && !p.EndsWith(".ase")) continue;
                var subs = AssetDatabase.LoadAllAssetsAtPath(p).OfType<Sprite>().ToList();
                if (subs.Count == 0) continue;
                var s = new BgSet { name = Path.GetFileNameWithoutExtension(p) + " (.aseprite)", path = p };
                s.sprites.AddRange(subs);
                Measure(s);
                sets.Add(s);
            }
        }
        selected = Mathf.Clamp(selected, 0, Mathf.Max(0, sets.Count - 1));
    }

    static void Measure(BgSet s)
    {
        s.W = 0; s.H = 0;
        foreach (var sp in s.sprites)
        {
            s.W = Mathf.Max(s.W, Mathf.RoundToInt(sp.rect.width));
            s.H = Mathf.Max(s.H, Mathf.RoundToInt(sp.rect.height));
        }
    }

    // ────────────────────────────────────────────────────────────── UI
    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("배경 세트", EditorStyles.boldLabel);
        if (GUILayout.Button("폴더 다시 스캔")) Rescan();

        if (sets.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "배경을 못 찾았어요.\n\n" +
                "Assets/Art/Backgrounds/<이름>/ 폴더를 만들고 레이어 PNG 를 넣으세요.\n" +
                "파일명 앞에 00_, 01_ … 순번을 붙이면 그 순서가 뒤→앞 순서가 됩니다.\n\n" +
                "Aseprite: File ▸ Export As ▸ Split Layers 체크",
                MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        selected = EditorGUILayout.Popup("사용할 배경", selected, sets.Select(x => x.name).ToArray());
        var set = sets[selected];

        EditorGUILayout.LabelField($"레이어 {set.sprites.Count}장 · 캔버스 {set.W} × {set.H} px");
        EditorGUI.indentLevel++;
        for (int i = 0; i < set.sprites.Count; i++)
            EditorGUILayout.LabelField($"[{i}] {set.sprites[i].name}", EditorStyles.miniLabel);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("스케일", EditorStyles.boldLabel);
        fit = (FitMode)EditorGUILayout.EnumPopup("맞춤 방식", fit);
        integerScale = EditorGUILayout.Toggle("정수 배율로 반올림", integerScale);

        float scale = ComputeScale(set);
        float srcUnitsH = set.H / PPU;
        EditorGUILayout.LabelField($"배율 ×{scale:0.###}   →   화면에서 세로 {srcUnitsH * scale:0.##} 유닛 " +
                                   $"(화면 {ScreenH:0.###} 유닛)");

        if (Mathf.Abs(scale - Mathf.Round(scale)) > 0.001f)
            EditorGUILayout.HelpBox($"배율 {scale:0.###} 이 정수가 아닙니다. 픽셀이 뭉개져 보입니다.", MessageType.Warning);
        if (scale > 1.01f)
            EditorGUILayout.HelpBox(
                $"이 배경은 캔버스가 {set.H}px 이라 화면({ScreenH * PPU:0}px)을 채우려면 {scale:0.##}배 확대해야 합니다.\n" +
                $"확대하면 배경 픽셀이 캐릭터 픽셀보다 {scale:0.##}배 굵어집니다.\n" +
                $"본작업 캔버스는 1024 × 768 px 입니다.", MessageType.Warning);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("시차", EditorStyles.boldLabel);
        maxFactor = EditorGUILayout.Slider("최대 가로 계수", maxFactor, 0.3f, 1f);
        curve = EditorGUILayout.Slider("분배 곡선", curve, 1f, 3f);
        useForeground = EditorGUILayout.Toggle("맨 앞을 전경으로", useForeground);
        using (new EditorGUI.DisabledScope(!useForeground))
            foregroundFactor = EditorGUILayout.Slider("  전경 계수", foregroundFactor, 1f, 1.5f);
        verticalRatio = EditorGUILayout.Slider("세로 = 가로 ×", verticalRatio, 0f, 0.5f);

        EditorGUILayout.LabelField("계산된 계수", EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        for (int i = 0; i < set.sprites.Count; i++)
        {
            Vector2 f = FactorFor(i, set.sprites.Count);
            EditorGUILayout.LabelField($"[{i}] {set.sprites[i].name}",
                $"가로 {f.x:0.00}   세로 {f.y:0.000}", EditorStyles.miniLabel);
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("가로 이음매 (Tiled 로 이어 붙일 때)", EditorStyles.boldLabel);
        if (GUILayout.Button("이음매 검사"))
        {
            seam.Clear();
            CheckSeams(set, true);
        }
        EditorGUI.indentLevel++;
        foreach (var sp in set.sprites)
        {
            if (sp == null) continue;
            string ap = AssetDatabase.GetAssetPath(sp);
            float d;
            EditorGUILayout.LabelField(sp.name,
                seam.TryGetValue(ap, out d) ? $"{SeamVerdict(d)}   (차이 {d:0.#})" : "미검사",
                EditorStyles.miniLabel);
        }
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("테스트 룸", EditorStyles.boldLabel);
        roomW = EditorGUILayout.IntSlider("가로 (타일)", roomW, 40, 400);
        roomH = EditorGUILayout.IntSlider("세로 (타일)", roomH, 20, 64);
        spawnPlayer = EditorGUILayout.Toggle("플레이어 배치", spawnPlayer);
        EditorGUILayout.LabelField($"= {roomW / ScreenW:0.0} 화면 × {roomH / ScreenH:0.0} 화면",
            EditorStyles.miniLabel);

        float needH = ScreenH * PPU + MaxVertical(set.sprites.Count) * (roomH * PPU - ScreenH * PPU);
        float haveH = set.H * scale;
        EditorGUILayout.LabelField($"세로 커버리지: 필요 {needH:0}px / 확보 {haveH:0}px",
            haveH >= needH ? EditorStyles.miniLabel : EditorStyles.boldLabel);
        if (haveH < needH)
            EditorGUILayout.HelpBox("세로가 모자랍니다. 방을 낮추거나 세로 시차를 줄이세요.", MessageType.Error);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("임포트 설정 맞추기 (PPU 32 · Point · Full Rect)", GUILayout.Height(24)))
            FixImport(set);
        if (GUILayout.Button("배경만 갱신", GUILayout.Height(28)))
            BuildBackground(set);
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        if (GUILayout.Button("테스트 룸 전체 생성 (지형 + 플레이어 + 배경)", GUILayout.Height(34)))
            BuildAll(set);
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    // ────────────────────────────────────────────────────────────── 계산
    float ComputeScale(BgSet set)
    {
        if (set.H <= 0) return 1f;
        float srcUnits = set.H / PPU;
        float want;
        switch (fit)
        {
            case FitMode.FitScreenHeight: want = ScreenH; break;
            case FitMode.FitRoomHeight:   want = roomH;   break;
            default:                      return 1f;
        }
        float s = want / srcUnits;
        if (integerScale && s > 1f) s = Mathf.Max(1f, Mathf.Round(s));
        return s;
    }

    Vector2 FactorFor(int i, int n)
    {
        float fx;
        if (n <= 1) fx = 0f;
        else if (useForeground && i == n - 1) fx = foregroundFactor;
        else
        {
            int last = useForeground ? n - 2 : n - 1;
            float t = last <= 0 ? 0f : (float)i / last;
            fx = maxFactor * Mathf.Pow(t, curve);
        }
        return new Vector2(fx, fx * verticalRatio);
    }

    float MaxVertical(int n)
    {
        float m = 0f;
        for (int i = 0; i < n; i++) m = Mathf.Max(m, FactorFor(i, n).y);
        return m;
    }

    // ────────────────────────────────────────────────────────────── 이음매 검사
    /// <summary>
    /// 가로 심리스 여부를 검사한다. 캔버스의 맨 왼쪽 열과 맨 오른쪽 열을 비교해서,
    /// 차이가 크면 Tiled 로 이어 붙일 때 세로 줄이 보인다는 뜻.
    /// 임포트 설정을 건드리지 않으려고 PNG 파일을 직접 디코딩해서 읽는다.
    /// </summary>
    void CheckSeams(BgSet set, bool log)
    {
        foreach (var sp in set.sprites)
        {
            if (sp == null) continue;
            string ap = AssetDatabase.GetAssetPath(sp);
            if (seam.ContainsKey(ap)) continue;
            float d = -1f;
            try
            {
                string full = Path.Combine(Path.GetDirectoryName(Application.dataPath), ap);
                if (File.Exists(full) && (ap.EndsWith(".png") || ap.EndsWith(".PNG")))
                {
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(File.ReadAllBytes(full)))
                    {
                        var L = tex.GetPixels(0, 0, 1, tex.height);
                        var R = tex.GetPixels(tex.width - 1, 0, 1, tex.height);
                        double sum = 0;
                        for (int i = 0; i < L.Length; i++)
                            sum += Mathf.Abs(L[i].r - R[i].r) + Mathf.Abs(L[i].g - R[i].g)
                                 + Mathf.Abs(L[i].b - R[i].b) + Mathf.Abs(L[i].a - R[i].a);
                        d = (float)(sum / (L.Length * 4)) * 255f;
                    }
                    DestroyImmediate(tex);
                }
            }
            catch { d = -1f; }
            seam[ap] = d;
            if (log && d > 15f)
                Debug.LogWarning($"[배경 프리뷰] '{sp.name}' 가로 심리스가 아닙니다 (좌우 차이 {d:0.#}). " +
                                 "Tiled 로 이어 붙일 때 세로 줄이 보입니다.");
        }
    }

    static string SeamVerdict(float d)
    {
        if (d < 0f) return "검사 불가";
        if (d < 3f) return "심리스 ✓";
        if (d < 15f) return "약간 어긋남";
        return "이음매 보임 ✗";
    }

    // ────────────────────────────────────────────────────────────── 임포트
    void FixImport(BgSet set) { FixImport(set, true); }

    void FixImport(BgSet set, bool rescan)
    {
        // 리임포트하면 Sprite 참조가 무효화되므로 경로를 먼저 기억해 둔다
        var paths = set.sprites.Select(AssetDatabase.GetAssetPath).ToList();
        var reimported = new HashSet<string>();

        var done = new HashSet<string>();
        foreach (var p in paths)
        {
            if (!done.Add(p)) continue;
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;                     // .aseprite 는 별도 임포터
            reimported.Add(p);
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = PPU;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.maxTextureSize = 4096;
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;  // Tiled 필수
            st.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(st);
            ti.SaveAndReimport();
        }
        // 무효화된 참조를 새로 로드한 것으로 교체
        for (int i = 0; i < paths.Count; i++)
        {
            if (!reimported.Contains(paths[i])) continue;
            var sp = AssetDatabase.LoadAllAssetsAtPath(paths[i]).OfType<Sprite>().FirstOrDefault();
            if (sp == null) sp = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            if (sp != null) set.sprites[i] = sp;
            else Debug.LogWarning($"[배경 프리뷰] 리임포트 후 스프라이트를 못 찾음: {paths[i]}");
        }
        if (reimported.Count > 0) Debug.Log($"[배경 프리뷰] 임포트 설정 적용: {reimported.Count}개");
        if (rescan) Rescan();
    }

    // ────────────────────────────────────────────────────────────── 생성
    void BuildAll(BgSet set)
    {
        EnsureLayers();
        BuildTerrain();
        if (spawnPlayer) BuildPlayer();
        BuildBackground(set);
        SaveScene();
    }

    void BuildBackground(BgSet set)
    {
        FixImport(set, false);          // Full Rect 아니면 Tiled 가 깨지므로 항상 먼저 맞춘다
        seam.Clear();
        CheckSeams(set, true);          // 심리스 아니면 콘솔에 경고
        var cam = EnsureCamera();
        float scale = ComputeScale(set);
        float tileWorldW = (set.W / PPU) * scale;
        float worldH = (set.H / PPU) * scale;

        // 예전 프리뷰 잔재까지 모두 제거 (겹쳐 렌더되는 것 방지)
        foreach (var nm in new[] { "Background", "BG_ParallaxTest", "ScaleRef", "RoomRef" })
        {
            var o = GameObject.Find(nm);
            while (o != null) { DestroyImmediate(o); o = GameObject.Find(nm); }
        }
        var root = new GameObject("Background");
        Undo.RegisterCreatedObjectUndo(root, "배경 생성");

        int n = set.sprites.Count;
        for (int i = 0; i < n; i++)
        {
            var spr = set.sprites[i];
            if (spr == null)
            {
                Debug.LogWarning($"[배경 프리뷰] {i}번 레이어 스프라이트가 유효하지 않아 건너뜁니다.");
                continue;
            }
            Vector2 f = FactorFor(i, n);
            var go = new GameObject($"{i:00}_{spr.name}  x{f.x:0.00}");
            go.transform.SetParent(root.transform);
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;

            // 가로: 카메라가 훑는 전 구간 + 시차 이동분
            float needW = ScreenW + f.x * Mathf.Max(0f, roomW - ScreenW) + tileWorldW * 2f;
            sr.size = new Vector2(needW / scale, set.H / PPU);   // 세로는 캔버스 1장 (세로 타일링 금지)
            sr.sortingOrder = f.x > 1f ? 150 : i;                // 전경은 지형보다 앞

            var pl = go.AddComponent<ParallaxLayer>();
            pl.factor = f;
            pl.anchor = new Vector2(
                (roomW / 2f) * f.x,
                worldH / 2f - ORTHO * (1f - f.y));
            pl.cam = cam.transform;
            pl.Apply();
        }

        Debug.Log($"[배경 프리뷰] '{set.name}' {n}장 배치. 배율 ×{scale:0.###}, " +
                  $"타일 폭 {tileWorldW:0.##}유닛, 세로 {worldH:0.##}유닛");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    void BuildTerrain()
    {
        var old = GameObject.Find("Terrain");
        if (old != null) DestroyImmediate(old);
        var t = new GameObject("Terrain");
        Undo.RegisterCreatedObjectUndo(t, "지형 생성");

        Solid(t, "Floor", new Vector2(roomW / 2f, 1f), new Vector2(roomW, 2f));
        Solid(t, "WallL", new Vector2(-0.5f, roomH / 2f), new Vector2(1f, roomH));
        Solid(t, "WallR", new Vector2(roomW + 0.5f, roomH / 2f), new Vector2(1f, roomH));

        var rng = new System.Random(20260909);
        float x = 14f;
        int k = 0;
        while (x < roomW - 16f)
        {
            switch (rng.Next(4))
            {
                case 0:                                   // 계단
                    for (int i = 0; i < 3; i++)
                        Solid(t, $"step{k}_{i}", new Vector2(x + i * 4f, 3.5f + i * 2.5f), new Vector2(4f, 1f));
                    x += 18f; break;
                case 1:                                   // 떠 있는 발판
                    for (int i = 0; i < 3; i++)
                        Solid(t, $"plat{k}_{i}", new Vector2(x + i * 5f, 4.5f + (i % 2) * 3.5f), new Vector2(3.5f, 1f));
                    x += 20f; break;
                case 2:                                   // 기둥 + 꼭대기
                    Solid(t, $"pillar{k}", new Vector2(x, 7f), new Vector2(2f, 10f));
                    Solid(t, $"pillartop{k}", new Vector2(x + 5f, 12.5f), new Vector2(7f, 1f));
                    x += 16f; break;
                default:                                  // 2단 선반
                    Solid(t, $"ledge{k}a", new Vector2(x + 4f, 5f), new Vector2(10f, 1f));
                    Solid(t, $"ledge{k}b", new Vector2(x + 10f, 9.5f), new Vector2(7f, 1f));
                    x += 22f; break;
            }
            x += 7f; k++;
        }
        Debug.Log($"[배경 프리뷰] 지형 생성: {roomW} × {roomH} 타일, 블록 {t.transform.childCount}개");
    }

    void BuildPlayer()
    {
        var old = GameObject.Find("Player");
        if (old != null) DestroyImmediate(old);

        var p = new GameObject("Player") { layer = LAYER_PLAYER };
        Undo.RegisterCreatedObjectUndo(p, "플레이어 생성");
        p.transform.position = new Vector3(6f, 2.2f, 0f);

        var rb = p.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var cc = p.AddComponent<CapsuleCollider2D>();
        cc.direction = CapsuleDirection2D.Vertical;
        cc.size = new Vector2(0.82f, 2.16f);
        cc.offset = new Vector2(0f, 1.08f);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(p.transform, false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Aseprites/Red_Hood_001.aseprite");
        if (prefab != null)
        {
            var art = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            art.name = "RedHood_Sprite";
            art.transform.SetParent(visual.transform, false);
            foreach (var s in art.GetComponentsInChildren<SpriteRenderer>()) s.sortingOrder = 50;
        }

        var mv = p.AddComponent<SimpleMover>();
        mv.visual = visual.transform;
        mv.groundMask = 1 << LAYER_GROUND;

        var cam = EnsureCamera();
        var f = cam.GetComponent<SimpleCameraFollow>();
        if (f == null) f = cam.gameObject.AddComponent<SimpleCameraFollow>();
        f.target = p.transform;
        f.offset = new Vector2(0f, 2.5f);
        f.smooth = 0.15f;
        f.clampToRoom = true;
        f.roomMin = Vector2.zero;
        f.roomMax = new Vector2(roomW, roomH);
    }

    Camera EnsureCamera()
    {
        var cam = Camera.main;
        if (cam == null) cam = FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = ORTHO;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(1f, 0f, 1f);      // 미커버 = 마젠타
        var pos = cam.transform.position;
        cam.transform.position = new Vector3(Mathf.Max(pos.x, ScreenW / 2f), ORTHO, -10f);
        return cam;
    }

    static void EnsureLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        layers.GetArrayElementAtIndex(LAYER_GROUND).stringValue = "Ground";
        layers.GetArrayElementAtIndex(LAYER_PLAYER).stringValue = "Player";
        tm.ApplyModifiedPropertiesWithoutUndo();
    }

    static Texture2D _white;
    static void Solid(GameObject parent, string name, Vector2 pos, Vector2 size)
    {
        if (_white == null)
        {
            _white = new Texture2D(4, 4);
            var c = new Color[16];
            for (int i = 0; i < 16; i++) c[i] = Color.white;
            _white.SetPixels(c); _white.filterMode = FilterMode.Point; _white.Apply();
        }
        var go = new GameObject(name) { layer = LAYER_GROUND };
        go.transform.SetParent(parent.transform);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(_white, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = size;
        sr.color = new Color(0.06f, 0.07f, 0.10f);
        sr.sortingOrder = 100;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    static void SaveScene()
    {
        var s = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);
    }

    // ────────────────────────────────────────────────────────────── 스크립트 진입점
    /// <summary>메뉴를 거치지 않고 코드에서 바로 구성할 때 쓴다.</summary>
    public static void BuildFromScript(string setName, string fitMode, int roomWidth, int roomHeight, bool withPlayer)
    {
        var w = CreateInstance<BackgroundPreviewWindow>();
        w.Rescan();
        int idx = w.sets.FindIndex(s => s.name == setName);
        if (idx < 0) idx = w.sets.FindIndex(s => s.name.StartsWith(setName));
        if (idx < 0)
        {
            Debug.LogError($"[배경 프리뷰] '{setName}' 못 찾음. 있는 것: {string.Join(" / ", w.sets.Select(s => s.name))}");
            DestroyImmediate(w);
            return;
        }
        w.selected = idx;
        w.fit = fitMode == "native" ? FitMode.Native
              : fitMode == "room"   ? FitMode.FitRoomHeight
                                    : FitMode.FitScreenHeight;
        w.roomW = roomWidth;
        w.roomH = roomHeight;
        w.spawnPlayer = withPlayer;
        w.BuildAll(w.sets[idx]);
        DestroyImmediate(w);
    }

    /// <summary>스캔된 배경 세트 이름 목록.</summary>
    public static string[] ListSets()
    {
        var w = CreateInstance<BackgroundPreviewWindow>();
        w.Rescan();
        var names = w.sets.Select(s => $"{s.name}  [{s.sprites.Count}장 {s.W}x{s.H}]").ToArray();
        DestroyImmediate(w);
        return names;
    }
}
