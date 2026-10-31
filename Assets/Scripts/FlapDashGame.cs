using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// =====================================================================
//  FlapDash - Big-O Edition  (single file, ZERO setup)
//
//  How to use:
//    1. Put this file anywhere inside your Unity project's Assets folder.
//    2. Press Play.  That's it. The script builds the whole game by itself
//       (camera, bird, ground, pipes, answer zones, UI). No GameObject has
//       to be created or attached by hand.
//
//  Gameplay:
//    - SPACE / mouse click / tap = flap.
//    - A code snippet is shown at the top of the screen.
//    - Every pipe gap contains 3 coloured zones with a Big-O label.
//    - Fly through the zone whose label matches the snippet's complexity.
//    - Correct zone = +1 score.  Wrong zone, pipe or ground = Game Over
//      (the game then shows the correct answer + a short explanation).
//    - New complexity classes unlock as the score grows
//      (level 1: O(1), O(n)  ->  level 2: + O(n^2)  ->  level 3: + O(log n)).
// =====================================================================

// ---------------------------------------------------------------------
//  Question bank: code snippet + correct complexity + short explanation
// ---------------------------------------------------------------------
public class FlapDashQuestion
{
    public string code;
    public string correct;
    public string why;
    public int level;   // minimum level at which this question may appear

    public FlapDashQuestion(int level, string code, string correct, string why)
    {
        this.level = level;
        this.code = code;
        this.correct = correct;
        this.why = why;
    }

    public static readonly string[] AllClasses = { "O(1)", "O(log n)", "O(n)", "O(n^2)" };

    public static readonly FlapDashQuestion[] Bank = new FlapDashQuestion[]
    {
        // ---- level 0 : O(1) and O(n) ----
        new FlapDashQuestion(0, "x = arr[0];\nreturn x;", "O(1)",
            "One direct access. The number of steps does not depend on n."),
        new FlapDashQuestion(0, "total = a + b;\nreturn total;", "O(1)",
            "A single calculation, no matter how big the input is."),
        new FlapDashQuestion(0, "return list[list.Count - 1];", "O(1)",
            "Reading the last element is one step, whatever the size."),
        new FlapDashQuestion(0, "for (i = 0; i < n; i++)\n    sum += arr[i];", "O(n)",
            "One loop that runs n times: steps grow linearly with n."),
        new FlapDashQuestion(0, "for (i = 0; i < n; i++)\n    print(arr[i]);", "O(n)",
            "One loop over the n elements: n steps."),

        // ---- level 1 : adds O(n^2) ----
        new FlapDashQuestion(1, "for (i = 0; i < n; i++)\n  for (j = 0; j < n; j++)\n    count++;", "O(n^2)",
            "A loop inside a loop: n x n steps."),
        new FlapDashQuestion(1, "for (i = 0; i < n; i++)\n  for (j = 0; j < n; j++)\n    print(i, j);", "O(n^2)",
            "Nested loops, both run n times: n x n = n^2."),
        new FlapDashQuestion(1, "for (i = 0; i < n; i++) { a++; }\nfor (j = 0; j < n; j++) { b++; }", "O(n)",
            "Two loops one AFTER the other: n + n = 2n, which is O(n)."),

        // ---- level 2 : adds O(log n) ----
        new FlapDashQuestion(2, "while (n > 1)\n    n = n / 2;", "O(log n)",
            "n is halved at every step, so only about log n steps are needed."),
        new FlapDashQuestion(2, "while (low <= high) {\n  mid = (low + high) / 2;  // halve the range\n}", "O(log n)",
            "Binary search: the search range is halved each time."),
    };
}

// ---------------------------------------------------------------------
//  Small helper components (added to objects at runtime by FlapDashGame)
// ---------------------------------------------------------------------
public class FlapDashZone : MonoBehaviour
{
    public bool isCorrect;
    public FlapDashPipe pipe;
}

public class FlapDashBird : MonoBehaviour
{
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        if (rb == null || !rb.simulated) return;
        // tilt the bird up/down according to its vertical speed
        float angle = Mathf.Clamp(rb.linearVelocity.y * 6f, -70f, 30f);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    // Solid collision: pipe or ground
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (FlapDashGame.Instance != null) FlapDashGame.Instance.OnCrash();
    }

    // Trigger overlap: one of the answer zones inside the gap
    void OnTriggerEnter2D(Collider2D other)
    {
        FlapDashZone zone = other.GetComponent<FlapDashZone>();
        if (zone != null && FlapDashGame.Instance != null)
            FlapDashGame.Instance.OnZoneEntered(zone);
    }
}

public class FlapDashPipe : MonoBehaviour
{
    public FlapDashQuestion question;
    public bool answered;
    public Text[] labels;
    public float[] offsets;

    void Update()
    {
        FlapDashGame g = FlapDashGame.Instance;
        if (g == null || g.state != FlapDashGame.State.Playing) return;

        transform.position += Vector3.left * g.CurrentSpeed * Time.deltaTime;

        // off-screen -> destroy (memory management)
        if (transform.position.x < -(g.HalfW + 3f)) Destroy(gameObject);
    }

    void LateUpdate()
    {
        FlapDashGame g = FlapDashGame.Instance;
        if (g == null || labels == null) return;
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null) continue;
            Vector3 sp = g.cam.WorldToScreenPoint(transform.position + new Vector3(0f, offsets[i], 0f));
            labels[i].rectTransform.position = sp;
        }
    }

    void OnDestroy()
    {
        if (labels == null) return;
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] != null) Destroy(labels[i].gameObject);
    }
}

// ---------------------------------------------------------------------
//  The game itself
// ---------------------------------------------------------------------
public class FlapDashGame : MonoBehaviour
{
    public enum State { Ready, Playing, GameOver }

    public static FlapDashGame Instance;

    [Header("Physics tuning")]
    public float flapVelocity = 7.5f;
    public float gravityScale = 2.8f;

    [Header("Pipes")]
    public float pipeSpeed = 2.6f;
    public float spawnInterval = 4.2f;
    public float gapHeight = 6.0f;
    public float pipeWidth = 1.6f;
    public float gapRandomRange = 0.4f;

    [HideInInspector] public State state = State.Ready;
    [HideInInspector] public Camera cam;

    // runtime objects
    private Sprite square;
    private Font font;
    private Transform worldRoot;
    private Transform uiRoot;
    private GameObject bird;
    private Rigidbody2D birdRb;
    private readonly List<FlapDashPipe> pipes = new List<FlapDashPipe>();

    // UI
    private Text scoreText, snippetText, hintText, overlayText;
    private Image overlay;
    private Image snippetPanel;

    // tutorial + visual hint
    private GameObject tutorialRoot;
    private GameObject hintPanel;
    private Image hintPanelImg;
    private Text hintTitle;
    private readonly List<Image> hintDots = new List<Image>();
    private bool hintVisible = true;
    private bool paused;
    private FlapDashQuestion hintShownFor;
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    // state
    private int score;
    private int best;
    private float spawnTimer;
    private float deadTime;
    private FlapDashQuestion lastQuestion;
    private const string BestKey = "FlapDashBest";

    public float HalfW { get { return cam.orthographicSize * cam.aspect; } }
    public float BirdX { get { return -HalfW * 0.5f; } }
    public int CurrentLevel { get { return Mathf.Min(2, score / 5); } }
    public float CurrentSpeed { get { return Mathf.Min(pipeSpeed + score * 0.05f, 4.5f); } }
    private float CurrentInterval { get { return Mathf.Max(2.8f, spawnInterval - score * 0.05f); } }

    // ------------------------------------------------------------------
    //  Auto start: runs by itself after the first scene is loaded
    // ------------------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<FlapDashGame>() != null) return;
        new GameObject("FlapDashGame").AddComponent<FlapDashGame>();
    }

    void Awake()
    {
        Instance = this;
        best = PlayerPrefs.GetInt(BestKey, 0);

        BuildCamera();
        BuildAssets();
        worldRoot = new GameObject("World").transform;
        BuildGround();
        BuildBird();
        BuildUI();
        ResetGame();
    }

    // ------------------------------------------------------------------
    //  Build everything
    // ------------------------------------------------------------------
    void BuildCamera()
    {
        cam = Camera.main;
        if (cam == null)
        {
            GameObject go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.44f, 0.77f, 0.81f);
    }

    void BuildAssets()
    {
        // 1x1 unit white square sprite, tinted with colours later
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        square = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
    }

    GameObject MakeBox(string name, Transform parent, Vector2 localPos, Vector2 size, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = square;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    BoxCollider2D AddCollider(GameObject go, bool trigger)
    {
        BoxCollider2D c = go.AddComponent<BoxCollider2D>();
        c.size = Vector2.one;
        c.isTrigger = trigger;
        return c;
    }

    void BuildGround()
    {
        GameObject ground = MakeBox("Ground", worldRoot, new Vector2(0f, -4.6f), new Vector2(80f, 1.6f),
            new Color(0.84f, 0.71f, 0.42f), 4);
        AddCollider(ground, false);
        MakeBox("Grass", worldRoot, new Vector2(0f, -3.85f), new Vector2(80f, 0.2f),
            new Color(0.42f, 0.69f, 0.30f), 5);
    }

    void BuildBird()
    {
        bird = MakeBox("Bird", null, new Vector2(BirdX, 0f), new Vector2(0.7f, 0.7f),
            new Color(1f, 0.92f, 0.23f), 10);

        // physics: Rigidbody2D (gravity + flap) and a solid collider
        birdRb = bird.AddComponent<Rigidbody2D>();
        birdRb.gravityScale = gravityScale;
        birdRb.freezeRotation = true;
        birdRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        AddCollider(bird, false);
        bird.AddComponent<FlapDashBird>();

        // eye + beak (cosmetic)
        MakeBox("Eye", bird.transform, new Vector2(0.18f, 0.15f), new Vector2(0.32f, 0.32f), Color.white, 11);
        MakeBox("Pupil", bird.transform, new Vector2(0.24f, 0.15f), new Vector2(0.14f, 0.14f), Color.black, 12);
        MakeBox("Beak", bird.transform, new Vector2(0.62f, -0.08f), new Vector2(0.34f, 0.2f),
            new Color(1f, 0.44f, 0.26f), 11);
    }

    // ------------------------------------------------------------------
    //  UI (built with code, no manual Canvas needed)
    // ------------------------------------------------------------------
    Text MakeText(string name, Transform parent, int size, TextAnchor anchor, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    Image MakeImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void SetRect(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    static void AddOutline(GameObject go)
    {
        Outline o = go.AddComponent<Outline>();
        o.effectColor = Color.black;
        o.effectDistance = new Vector2(2f, -2f);
    }

    void BuildUI()
    {
        GameObject cgo = new GameObject("FlapDashCanvas");
        Canvas canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        uiRoot = cgo.transform;

        // score (top right)
        scoreText = MakeText("Score", uiRoot, 96, TextAnchor.UpperRight, Color.white);
        SetRect(scoreText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -30f), new Vector2(400f, 140f));
        AddOutline(scoreText.gameObject);

        // code snippet panel (top centre)
        Image panel = MakeImage("SnippetPanel", uiRoot, new Color(0f, 0f, 0f, 0.78f));
        snippetPanel = panel;
        SetRect(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1000f, 230f));
        snippetText = MakeText("SnippetText", panel.transform, 34, TextAnchor.MiddleLeft, new Color(0.62f, 0.86f, 1f));
        Stretch(snippetText.rectTransform, 24f);

        // visual hint panel (top left): dots = number of steps when n = 8
        hintPanelImg = MakeImage("VisualHint", uiRoot, new Color(0f, 0f, 0f, 0.7f));
        hintPanel = hintPanelImg.gameObject;
        SetRect(hintPanelImg.rectTransform, TopLeft, TopLeft, new Vector2(30f, -30f), new Vector2(300f, 300f));
        hintTitle = MakeText("HintTitle", hintPanel.transform, 26, TextAnchor.UpperLeft, new Color(1f, 0.92f, 0.5f));
        SetRect(hintTitle.rectTransform, TopLeft, TopLeft, new Vector2(16f, -12f), new Vector2(280f, 70f));
        for (int i = 0; i < 64; i++)
        {
            Image d = MakeImage("Dot" + i, hintPanel.transform, new Color(1f, 0.92f, 0.23f));
            SetRect(d.rectTransform, TopLeft, TopLeft, Vector2.zero, new Vector2(22f, 22f));
            hintDots.Add(d);
        }
        hintPanel.SetActive(false);

        // hint (bottom)
        hintText = MakeText("Hint", uiRoot, 30, TextAnchor.LowerCenter, Color.white);
        SetRect(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1200f, 60f));
        AddOutline(hintText.gameObject);

        // overlay for start / game-over (created last = drawn on top)
        overlay = MakeImage("Overlay", uiRoot, new Color(0f, 0f, 0f, 0.85f));
        Stretch(overlay.rectTransform, 0f);
        overlayText = MakeText("OverlayText", overlay.transform, 44, TextAnchor.MiddleCenter, Color.white);
        Stretch(overlayText.rectTransform, 60f);

        BuildTutorial();
    }

    Text MakeLabel()
    {
        Text t = MakeText("ZoneLabel", uiRoot, 46, TextAnchor.MiddleCenter, Color.white);
        t.rectTransform.sizeDelta = new Vector2(300f, 90f);
        AddOutline(t.gameObject);
        // keep labels below the overlay
        t.transform.SetSiblingIndex(overlay.transform.GetSiblingIndex());
        return t;
    }

    // ------------------------------------------------------------------
    //  Game flow
    // ------------------------------------------------------------------
    void ResetGame()
    {
        foreach (FlapDashPipe p in pipes)
            if (p != null) Destroy(p.gameObject);
        pipes.Clear();

        score = 0;
        spawnTimer = 0f;
        lastQuestion = null;
        state = State.Ready;

        birdRb.simulated = false;
        birdRb.linearVelocity = Vector2.zero;
        bird.transform.position = new Vector3(BirdX, 0f, 0f);
        bird.transform.rotation = Quaternion.identity;

        UpdateScoreUI();
        snippetText.text = "// Get ready...";
        snippetPanel.gameObject.SetActive(false);   // hidden on the start screen
        paused = false;
        Time.timeScale = 1f;
        hintShownFor = null;
        hintPanel.SetActive(false);

        overlayText.alignment = TextAnchor.UpperCenter;
        overlayText.fontSize = 40;
        overlayText.text =
            "<b>FLAPDASH  -  Big-O Edition</b>\n" +
            "Fly through the zone whose label matches the time complexity of the code.\n" +
            "Correct zone = +1     Wrong zone / pipe / ground = Game Over     H = visual hint";
        tutorialRoot.SetActive(true);
        overlay.color = new Color(0f, 0f, 0f, 0.8f);
        overlay.gameObject.SetActive(true);
    }

    void StartPlaying()
    {
        state = State.Playing;
        overlay.gameObject.SetActive(false);
        birdRb.simulated = true;
        snippetPanel.gameObject.SetActive(true);
        bird.transform.position = new Vector3(BirdX, 0f, 0f);
        spawnTimer = 0f;
        SpawnPipe(BirdX + 11f);
    }

    void Flap()
    {
        birdRb.linearVelocity = new Vector2(0f, flapVelocity);
    }

    static bool FlapPressed()
    {
#if ENABLE_INPUT_SYSTEM
        bool key = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool touch = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        return key || mouse || touch;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    void Update()
    {
        pipes.RemoveAll(p => p == null);
        bool pressed = FlapPressed();

        if (HintTogglePressed())
        {
            hintVisible = !hintVisible;
            hintShownFor = null;
        }

        // pause / resume (only while playing)
        if (state == State.Playing && PauseTogglePressed())
            SetPaused(!paused);
        if (paused) return;

        switch (state)
        {
            case State.Ready:
                bird.transform.position = new Vector3(BirdX, 0f, 0f);   // keep the bird on screen if the window was resized
                if (pressed)
                {
                    StartPlaying();
                    Flap();
                }
                break;

            case State.Playing:
                if (pressed) Flap();

                // timer based pipe spawning
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= CurrentInterval)
                {
                    spawnTimer = 0f;
                    SpawnPipe(HalfW + 2f);
                }

                // flew above the screen
                if (bird.transform.position.y > cam.orthographicSize + 1f) OnCrash();

                UpdateSnippet();
                break;

            case State.GameOver:
                if (pressed && Time.time - deadTime > 0.6f) ResetGame();
                break;
        }
    }

    // ------------------------------------------------------------------
    //  Spawning (procedural: random gap height, random question, shuffled zones)
    // ------------------------------------------------------------------
    FlapDashQuestion NextQuestion()
    {
        List<FlapDashQuestion> pool = new List<FlapDashQuestion>();
        foreach (FlapDashQuestion q in FlapDashQuestion.Bank)
            if (q.level <= CurrentLevel && q != lastQuestion) pool.Add(q);

        FlapDashQuestion pick = pool[Random.Range(0, pool.Count)];
        lastQuestion = pick;
        return pick;
    }

    // correct answer + 2 decoys, shuffled
    string[] BuildOptions(FlapDashQuestion q)
    {
        List<string> decoys = new List<string>();
        foreach (string c in FlapDashQuestion.AllClasses)
        {
            if (c == q.correct) continue;
            if (c == "O(log n)" && CurrentLevel < 2) continue;   // not unlocked yet
            decoys.Add(c);
        }
        while (decoys.Count > 2) decoys.RemoveAt(Random.Range(0, decoys.Count));

        List<string> opts = new List<string>(decoys);
        opts.Add(q.correct);

        for (int i = opts.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string tmp = opts[i];
            opts[i] = opts[j];
            opts[j] = tmp;
        }
        return opts.ToArray();
    }

    void SpawnPipe(float x)
    {
        FlapDashQuestion q = NextQuestion();
        string[] options = BuildOptions(q);

        float cy = Random.Range(-gapRandomRange, gapRandomRange);
        GameObject root = new GameObject("PipePair");
        root.transform.SetParent(worldRoot, false);
        root.transform.position = new Vector3(x, cy, 0f);

        Rigidbody2D prb = root.AddComponent<Rigidbody2D>();
        prb.bodyType = RigidbodyType2D.Kinematic;

        float half = gapHeight * 0.5f;
        float len = 12f;
        Color green = new Color(0.30f, 0.69f, 0.31f);
        Color dark = new Color(0.18f, 0.49f, 0.20f);

        // solid pipes
        AddCollider(MakeBox("PipeTop", root.transform, new Vector2(0f, half + len * 0.5f), new Vector2(pipeWidth, len), green, 1), false);
        AddCollider(MakeBox("PipeBottom", root.transform, new Vector2(0f, -half - len * 0.5f), new Vector2(pipeWidth, len), green, 1), false);
        MakeBox("CapTop", root.transform, new Vector2(0f, half + 0.2f), new Vector2(pipeWidth + 0.3f, 0.4f), dark, 2);
        MakeBox("CapBottom", root.transform, new Vector2(0f, -half - 0.2f), new Vector2(pipeWidth + 0.3f, 0.4f), dark, 2);

        // 3 answer zones (trigger colliders) filling the gap
        FlapDashPipe pipe = root.AddComponent<FlapDashPipe>();
        pipe.question = q;
        pipe.labels = new Text[3];
        pipe.offsets = new float[3];

        Color[] zoneColors =
        {
            new Color(0.15f, 0.45f, 0.95f, 0.55f),
            new Color(0.95f, 0.50f, 0.10f, 0.55f),
            new Color(0.60f, 0.25f, 0.85f, 0.55f)
        };

        float zoneH = gapHeight / 3f;
        for (int i = 0; i < 3; i++)
        {
            float oy = (1 - i) * zoneH;     // top, middle, bottom
            GameObject z = MakeBox("Zone" + i, root.transform, new Vector2(0f, oy), new Vector2(pipeWidth, zoneH), zoneColors[i], 3);
            AddCollider(z, true);
            FlapDashZone zone = z.AddComponent<FlapDashZone>();
            zone.isCorrect = (options[i] == q.correct);
            zone.pipe = pipe;

            Text lbl = MakeLabel();
            lbl.text = options[i];
            pipe.labels[i] = lbl;
            pipe.offsets[i] = oy;
        }

        pipes.Add(pipe);
    }

    // ------------------------------------------------------------------
    //  Events from the bird
    // ------------------------------------------------------------------
    public void OnZoneEntered(FlapDashZone zone)
    {
        if (state != State.Playing) return;
        if (zone.pipe == null || zone.pipe.answered) return;

        zone.pipe.answered = true;

        if (zone.isCorrect)
        {
            score++;
            UpdateScoreUI();
        }
        else
        {
            GameOver("Wrong zone!", zone.pipe.question);
        }
    }

    public void OnCrash()
    {
        if (state != State.Playing) return;
        GameOver("You crashed!", CurrentQuestion());
    }

    // ------------------------------------------------------------------
    //  UI updates
    // ------------------------------------------------------------------
    // the question belonging to the nearest pipe the bird has not answered yet
    FlapDashQuestion CurrentQuestion()
    {
        float bx = bird.transform.position.x;
        foreach (FlapDashPipe p in pipes)
        {
            if (p != null && !p.answered && p.transform.position.x + pipeWidth * 0.5f > bx - 0.4f)
                return p.question;
        }
        return null;
    }

    void UpdateSnippet()
    {
        FlapDashQuestion q = CurrentQuestion();
        snippetText.text = (q != null)
            ? "// What is the time complexity of this code?\n" + q.code
            : "// Get ready...";
        UpdateHint(q);
    }

    static bool PauseTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null &&
               (Keyboard.current.pKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    // Pause freezes everything (Time.timeScale = 0): physics, pipe movement and spawning
    void SetPaused(bool value)
    {
        paused = value;
        Time.timeScale = value ? 0f : 1f;

        if (value)
        {
            tutorialRoot.SetActive(false);
            overlayText.alignment = TextAnchor.MiddleCenter;
            overlayText.fontSize = 64;
            overlay.color = new Color(0f, 0f, 0f, 0.5f);   // light dim so the bird stays visible
            overlayText.text = "<b>PAUSED</b>\n\nPress P or Esc to resume";
            overlay.gameObject.SetActive(true);
        }
        else
        {
            overlay.gameObject.SetActive(false);
        }
    }

    static bool HintTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.H);
#else
        return false;
#endif
    }

    // how many steps a snippet of each class runs when n = 8
    static int StepsFor(string complexity)
    {
        switch (complexity)
        {
            case "O(1)": return 1;
            case "O(log n)": return 3;
            case "O(n)": return 8;
            default: return 64;   // O(n^2)
        }
    }

    static int ColsFor(string complexity)
    {
        switch (complexity)
        {
            case "O(1)": return 1;
            case "O(log n)": return 3;
            default: return 8;
        }
    }

    // Visual hint: one dot per step the code runs when n = 8.
    // The class name is NOT shown, the player must still connect the pattern to the complexity.
    void UpdateHint(FlapDashQuestion q)
    {
        bool show = hintVisible && q != null && state == State.Playing;
        if (!show)
        {
            if (hintPanel.activeSelf) hintPanel.SetActive(false);
            hintShownFor = null;
            return;
        }
        if (hintShownFor == q && hintPanel.activeSelf) return;
        hintShownFor = q;

        int steps = StepsFor(q.correct);
        int cols = ColsFor(q.correct);
        int rows = (steps + cols - 1) / cols;

        hintPanel.SetActive(true);
        hintTitle.text = "Hint  [H = hide]\nn = 8  ->  " + steps + (steps == 1 ? " step" : " steps");

        for (int i = 0; i < hintDots.Count; i++)
        {
            bool on = i < steps;
            hintDots[i].gameObject.SetActive(on);
            if (on)
                hintDots[i].rectTransform.anchoredPosition =
                    new Vector2(20f + (i % cols) * 32f, -(90f + (i / cols) * 32f));
        }
        hintPanelImg.rectTransform.sizeDelta = new Vector2(300f, 100f + rows * 32f + 10f);
    }

    // Tutorial shown on the start screen: the 4 classes, an example, and dots for n = 8
    void BuildTutorial()
    {
        GameObject root = new GameObject("Tutorial", typeof(RectTransform));
        root.transform.SetParent(overlay.transform, false);
        Stretch(root.GetComponent<RectTransform>(), 0f);
        tutorialRoot = root;

        Text header = MakeText("StepsHeader", root.transform, 28, TextAnchor.MiddleLeft, new Color(0.8f, 0.8f, 0.8f));
        SetRect(header.rectTransform, TopLeft, TopLeft, new Vector2(1380f, -300f), new Vector2(420f, 40f));
        header.text = "Steps when n = 8";

        string[] classes = { "O(1)", "O(log n)", "O(n)", "O(n^2)" };
        string[] info =
        {
            "Constant: always the same, no loop\nx = arr[0];",
            "Logarithmic: the problem is halved each step\nn = n / 2;",
            "Linear: one loop over n items\nfor (i = 0; i < n; i++)",
            "Quadratic: a loop inside a loop (n x n)\nfor (...)  for (...)"
        };

        for (int r = 0; r < 4; r++)
        {
            float y = -(345f + r * 140f);

            Text lab = MakeText("Class" + r, root.transform, 56, TextAnchor.MiddleLeft, new Color(1f, 0.92f, 0.5f));
            SetRect(lab.rectTransform, TopLeft, TopLeft, new Vector2(160f, y), new Vector2(260f, 120f));
            lab.text = classes[r];

            Text desc = MakeText("Info" + r, root.transform, 30, TextAnchor.MiddleLeft, Color.white);
            SetRect(desc.rectTransform, TopLeft, TopLeft, new Vector2(440f, y), new Vector2(880f, 120f));
            desc.text = info[r];

            int steps = StepsFor(classes[r]);
            int cols = ColsFor(classes[r]);
            int rows = (steps + cols - 1) / cols;
            float top = y - (120f - rows * 16f) * 0.5f;
            for (int i = 0; i < steps; i++)
            {
                Image d = MakeImage("D", root.transform, new Color(1f, 0.92f, 0.23f));
                SetRect(d.rectTransform, TopLeft, TopLeft,
                    new Vector2(1380f + (i % cols) * 16f, top - (i / cols) * 16f), new Vector2(12f, 12f));
            }
        }

        Text start = MakeText("StartText", root.transform, 48, TextAnchor.LowerCenter, new Color(0.6f, 1f, 0.6f));
        SetRect(start.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(1200f, 80f));
        start.text = "Click or press SPACE to start";
    }

    void UpdateScoreUI()
    {
        scoreText.text = score.ToString();
        hintText.text = "SPACE / Click = Flap        H = visual hint        P = pause        Level " + (CurrentLevel + 1) + "        Best: " + best;
    }

    void GameOver(string reason, FlapDashQuestion q)
    {
        state = State.GameOver;
        deadTime = Time.time;
        birdRb.simulated = false;

        if (score > best)
        {
            best = score;
            PlayerPrefs.SetInt(BestKey, best);
            PlayerPrefs.Save();
        }
        UpdateScoreUI();

        string t = "<b>GAME OVER</b>\n" + reason + "\n\nScore: " + score + "      Best: " + best;
        if (q != null)
        {
            t += "\n\nThe code was:\n" + q.code +
                 "\n\nCorrect answer:  <color=#7CFC00>" + q.correct + "</color>\n" + q.why;
        }
        t += "\n\nClick or press SPACE to play again";

        hintPanel.SetActive(false);
        hintShownFor = null;
        snippetPanel.gameObject.SetActive(false);   // the result text already shows the code
        tutorialRoot.SetActive(false);
        overlayText.alignment = TextAnchor.MiddleCenter;
        overlayText.fontSize = 44;
        overlayText.text = t;
        overlay.color = new Color(0f, 0f, 0f, 0.85f);
        overlay.gameObject.SetActive(true);
    }
}
