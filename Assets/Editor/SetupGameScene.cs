using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using TMPro;
using System.Linq;

public class SetupGameScene : EditorWindow
{
    [MenuItem("Tools/Build Bhop Scene")]
    public static void BuildScene()
    {
        // 1. Setup Folders
        if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
            AssetDatabase.CreateFolder("Assets", "Scripts");
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
            AssetDatabase.CreateFolder("Assets", "Editor");

        // 2. Create Scene
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        newScene.name = "BhopPrototype";

        // 3. Materials
        Material floorMat = CreateOrGetMaterial("Assets/Materials/Grid.mat", new Color(0.6f, 0.6f, 0.6f)); // Neutral gray
        Material enemyMat = CreateOrGetMaterial("Assets/Materials/GlowCyan.mat", Color.cyan); // Changed from Red to Cyan
        enemyMat.EnableKeyword("_EMISSION");
        enemyMat.SetColor("_EmissionColor", Color.cyan * 2f);
        Material obstacleMat = CreateOrGetMaterial("Assets/Materials/Obstacle.mat", new Color(0.3f, 0.3f, 0.3f)); // Darker gray for obstacles

        // 4. Floor Surface
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(40, 1, 40); // Enlarged platform to 400x400
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        // 4.1 Obstacles
        for (int i = 0; i < 40; i++)
        {
            GameObject obs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obs.name = "Obstacle " + i;
            obs.GetComponent<Renderer>().sharedMaterial = obstacleMat;
            
            float sizeX = Random.Range(3f, 15f);
            float sizeY = Random.Range(3f, 12f);
            float sizeZ = Random.Range(3f, 15f);
            
            float posX = Random.Range(-180f, 180f);
            float posZ = Random.Range(-180f, 180f);
            
            // Avoid spawning on top of player's center or enemy spawn
            if (Vector3.Distance(new Vector3(posX, 0, posZ), Vector3.zero) < 20f || 
                Vector3.Distance(new Vector3(posX, 0, posZ), new Vector3(15, 0, 15)) < 20f)
            {
                posX += 30f;
            }

            obs.transform.position = new Vector3(posX, sizeY / 2f, posZ);
            obs.transform.localScale = new Vector3(sizeX, sizeY, sizeZ);
            obs.transform.SetParent(floor.transform);
        }

        var navMeshSurfaceType = System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == "NavMeshSurface");

        if (navMeshSurfaceType != null)
        {
            var surface = floor.AddComponent(navMeshSurfaceType);
            navMeshSurfaceType.GetMethod("BuildNavMesh")?.Invoke(surface, null);
        }
        else
        {
            GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);
            foreach (Transform child in floor.transform)
            {
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.NavigationStatic);
            }
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
        }

        // 5. Player
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = new Vector3(0, 2f, 0);

        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }
        cam.transform.SetParent(player.transform);
        cam.transform.localPosition = new Vector3(0, 0.6f, 0);
        cam.transform.localRotation = Quaternion.identity;

        player.AddComponent<CharacterController>();
        PlayerController pc = player.AddComponent<PlayerController>();

        // 5.1 Music
        AudioClip musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music.mp3");
        if (musicClip != null)
        {
            AudioSource audioSource = player.AddComponent<AudioSource>();
            audioSource.clip = musicClip;
            audioSource.loop = true;
            audioSource.playOnAwake = true;
            audioSource.volume = 0.1f; // Started quiet
            audioSource.Play();
            pc.speedMusicSource = audioSource;
        }
        else
        {
            Debug.LogWarning("Music.mp3 not found in Assets folder. Audio scaling won't work.");
        }

        // 6. Enemy
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemy.name = "Enemy";
        enemy.transform.position = new Vector3(15, 1.5f, 15); // Shifted up slightly since it's taller
        enemy.transform.localScale = new Vector3(1f, 2f, 1f); // Made the enemy collision box taller (1x2x1)
        
        // Remove the MeshRenderer so the cube is completely invisible, leaving only the script and collider
        Object.DestroyImmediate(enemy.GetComponent<Renderer>());

        NavMeshAgent agent = enemy.AddComponent<NavMeshAgent>();
        agent.height = 2f; // Update NavMeshAgent to track the new height
        agent.baseOffset = 1f; // Raises the transform origin above the NavMesh so the object and sprite don't clip into the floor
        
        EnemyAI enemyAI = enemy.AddComponent<EnemyAI>();
        enemyAI.target = player.transform;

        // 6.1 Enemy Face Sprite
        Sprite faceSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Enemy_Face.png");
        if (faceSprite != null)
        {
            GameObject faceObj = new GameObject("FaceSprite");
            faceObj.transform.SetParent(enemy.transform);
            // Since the parent cube is invisible and 1x2x1, we center the sprite in front of it and scale it to mostly cover the 1x2 bounding box.
            faceObj.transform.localPosition = new Vector3(0, 0.2f, 0.51f); // Lifted slightly above center
            
            // We want the face proportion to look normal, so we scale Y by 0.5 because the parent is 2x taller, 
            // keeping the visual scale uniform (1 * 1 = 1x width, 0.5 * 2 = 1x height).
            faceObj.transform.localScale = new Vector3(1f, 0.5f, 1f); 
            
            SpriteRenderer sr = faceObj.AddComponent<SpriteRenderer>();
            sr.sprite = faceSprite;

            // Make it billboard (face the player) via a simple script addition or just fixed rotation
            // We'll add a quick Billboard component to it
            var billboard = faceObj.AddComponent<Billboard>();
            billboard.camTransform = cam.transform;
        }
        else
        {
            Debug.LogWarning("Enemy_Face.png not found in Assets folder (or not imported as a Sprite). Face won't be assigned.");
        }

        // 7. Light
        Light[] lights = Object.FindObjectsOfType<Light>();
        bool hasDirLight = false;
        foreach (var l in lights)
        {
            if (l.type == LightType.Directional) hasDirLight = true;
        }
        if (!hasDirLight)
        {
            GameObject lightObj = new GameObject("Directional Light");
            Light dirLight = lightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        // 8. UI Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 8.1 Speed Text
        GameObject speedTextObj = new GameObject("SpeedText");
        speedTextObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI tmpSpeed = speedTextObj.AddComponent<TextMeshProUGUI>();
        tmpSpeed.text = "Speed: 0";
        tmpSpeed.fontSize = 24;
        tmpSpeed.fontStyle = FontStyles.Bold;
        tmpSpeed.color = Color.white;
        RectTransform rtSpeed = speedTextObj.GetComponent<RectTransform>();
        rtSpeed.anchorMin = new Vector2(0, 1);
        rtSpeed.anchorMax = new Vector2(0, 1);
        rtSpeed.pivot = new Vector2(0, 1);
        rtSpeed.anchoredPosition = new Vector2(30, -30);
        rtSpeed.sizeDelta = new Vector2(300, 40);

        // 8.2 Max Speed Text
        GameObject maxSpeedTextObj = new GameObject("MaxSpeedText");
        maxSpeedTextObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI tmpMaxSpeed = maxSpeedTextObj.AddComponent<TextMeshProUGUI>();
        tmpMaxSpeed.text = "Max Speed: 0";
        tmpMaxSpeed.fontSize = 24;
        tmpMaxSpeed.fontStyle = FontStyles.Bold;
        tmpMaxSpeed.color = Color.cyan;
        RectTransform rtMaxSpeed = maxSpeedTextObj.GetComponent<RectTransform>();
        rtMaxSpeed.anchorMin = new Vector2(0, 1);
        rtMaxSpeed.anchorMax = new Vector2(0, 1);
        rtMaxSpeed.pivot = new Vector2(0, 1);
        rtMaxSpeed.anchoredPosition = new Vector2(30, -70);
        rtMaxSpeed.sizeDelta = new Vector2(300, 40);

        // 8.3 Score Text
        GameObject scoreTextObj = new GameObject("ScoreText");
        scoreTextObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI tmpScore = scoreTextObj.AddComponent<TextMeshProUGUI>();
        tmpScore.text = "Score: 0";
        tmpScore.fontSize = 24;
        tmpScore.fontStyle = FontStyles.Bold;
        tmpScore.color = Color.yellow;
        RectTransform rtScore = scoreTextObj.GetComponent<RectTransform>();
        rtScore.anchorMin = new Vector2(0, 1);
        rtScore.anchorMax = new Vector2(0, 1);
        rtScore.pivot = new Vector2(0, 1);
        rtScore.anchoredPosition = new Vector2(30, -110);
        rtScore.sizeDelta = new Vector2(300, 40);

        // 8.4 Game Over Panel
        GameObject panelObj = new GameObject("GameOverPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image panelImg = panelObj.AddComponent<UnityEngine.UI.Image>();
        panelImg.color = new Color(0, 0, 0, 0.8f);
        RectTransform rtPanel = panelObj.GetComponent<RectTransform>();
        rtPanel.anchorMin = new Vector2(0, 0);
        rtPanel.anchorMax = new Vector2(1, 1);
        rtPanel.offsetMin = Vector2.zero;
        rtPanel.offsetMax = Vector2.zero;
        
        GameObject goTextObj = new GameObject("GameOverText");
        goTextObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI tmpGO = goTextObj.AddComponent<TextMeshProUGUI>();
        tmpGO.text = "GAME OVER\nYOU WERE CAUGHT";
        tmpGO.fontSize = 60;
        tmpGO.fontStyle = FontStyles.Bold;
        tmpGO.color = Color.red;
        tmpGO.alignment = TextAlignmentOptions.Center;
        RectTransform rtGO = goTextObj.GetComponent<RectTransform>();
        rtGO.anchorMin = new Vector2(0.5f, 0.5f);
        rtGO.anchorMax = new Vector2(0.5f, 0.5f);
        rtGO.anchoredPosition = new Vector2(0, 100);
        rtGO.sizeDelta = new Vector2(800, 200);

        // Retry Button
        GameObject btnObj = new GameObject("RetryBtn");
        btnObj.transform.SetParent(panelObj.transform, false);
        UnityEngine.UI.Image btnImg = btnObj.AddComponent<UnityEngine.UI.Image>();
        btnImg.color = Color.white;
        UnityEngine.UI.Button btn = btnObj.AddComponent<UnityEngine.UI.Button>();
        RectTransform rtBtn = btnObj.GetComponent<RectTransform>();
        rtBtn.anchorMin = new Vector2(0.5f, 0.5f);
        rtBtn.anchorMax = new Vector2(0.5f, 0.5f);
        rtBtn.anchoredPosition = new Vector2(0, -50);
        rtBtn.sizeDelta = new Vector2(200, 60);
        
        GameObject btnTextObj = new GameObject("Text");
        btnTextObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI tmpBtn = btnTextObj.AddComponent<TextMeshProUGUI>();
        tmpBtn.text = "TRY AGAIN";
        tmpBtn.fontSize = 24;
        tmpBtn.color = Color.black;
        tmpBtn.alignment = TextAlignmentOptions.Center;
        RectTransform rtBtnText = btnTextObj.GetComponent<RectTransform>();
        rtBtnText.anchorMin = Vector2.zero;
        rtBtnText.anchorMax = Vector2.one;
        rtBtnText.offsetMin = Vector2.zero;
        rtBtnText.offsetMax = Vector2.zero;

        // Assign to PlayerController
        pc.speedText = tmpSpeed;
        pc.maxSpeedText = tmpMaxSpeed;
        pc.scoreText = tmpScore;
        pc.gameOverPanel = panelObj;

        // Add a script to handle the retry button
        var retryScript = canvasObj.AddComponent<RetryGame>();
        retryScript.retryButton = btn;

        Debug.Log("Bhop Escape Scenario Built Successfully!");
    }

    private static Material CreateOrGetMaterial(string path, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        
        Shader defaultShader = Shader.Find("Standard");
        if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
        {
            // Use URP or HDRP default shader if a render pipeline is active to prevent magenta materials
            defaultShader = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.defaultShader;
        }
        else if (defaultShader == null)
        {
            // Fallback just in case
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            defaultShader = temp.GetComponent<Renderer>().sharedMaterial.shader;
            DestroyImmediate(temp);
        }

        if (mat == null)
        {
            mat = new Material(defaultShader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader" || mat.shader.name != defaultShader.name)
        {
            mat.shader = defaultShader;
        }
        
        mat.color = color;
        // For URP Lit shader, the color property is _BaseColor instead of _Color
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
