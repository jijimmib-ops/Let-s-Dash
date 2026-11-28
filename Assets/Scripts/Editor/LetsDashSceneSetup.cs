#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using LetsDash.Core;
using LetsDash.Bullets;
using LetsDash.Combat;
using LetsDash.Entities;
using LetsDash.Items;
using LetsDash.UI;
using LetsDash.Audio;
using LetsDash.VFX;

namespace LetsDash.Editor
{
    public static class LetsDashSceneSetup
    {
        private static readonly string[] RequiredLayers =
        {
            "Walls",
            "LeftPlayer",
            "RightPlayer",
            "Boss"
        };

        [MenuItem("Tools/Let's Dash/Auto Setup MainGame Scene")]
        public static void AutoSetupScene()
        {
            EnsureLayersAndCollisionMatrix();

            Sprite squareSprite = EnsureSolidSpriteAsset("Assets/Art/Generated/SolidSquare.png", false);
            Sprite circleSprite = EnsureSolidSpriteAsset("Assets/Art/Generated/SolidCircle.png", true);
            Sprite soundOnSprite = EnsureSoundIconSpriteAsset("Assets/Art/Generated/Icon_SoundOn.png", true);
            Sprite soundOffSprite = EnsureSoundIconSpriteAsset("Assets/Art/Generated/Icon_SoundOff.png", false);

            GameObject bullet1 = EnsureBulletPrefab("Assets/Prefab/Generated/Bullet_1.prefab", circleSprite, 0.22f, new Color(1f, 0.72f, 0.72f));
            GameObject bullet2 = EnsureBulletPrefab("Assets/Prefab/Generated/Bullet_2.prefab", circleSprite, 0.36f, new Color(0.95f, 0.32f, 0.32f));
            GameObject bullet3 = EnsureBulletPrefab("Assets/Prefab/Generated/Bullet_3.prefab", circleSprite, 0.54f, new Color(0.98f, 0.52f, 0.15f));
            GameObject cratePrefab = EnsureCratePrefab("Assets/Prefab/Generated/PowerUpCrate.prefab", squareSprite);

            SetupWallBoundaries();
            SetupEntities(out PlayerController leftPlayer, out PlayerController rightPlayer, out BossController boss);
            SetupManagers(bullet1, bullet2, bullet3, cratePrefab);
            SetupCanvasUI(squareSprite, soundOnSprite, soundOffSprite);

            if (leftPlayer != null && rightPlayer != null)
            {
                WirePartner(leftPlayer, rightPlayer);
                WirePartner(rightPlayer, leftPlayer);
                CreatePlayerTetherLink(leftPlayer.transform, rightPlayer.transform);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("[Let's Dash] MainGame Scene setup complete! Press Ctrl+S to save the scene.");
        }

        private static void EnsureLayersAndCollisionMatrix()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            foreach (string layerName in RequiredLayers)
            {
                if (LayerMask.NameToLayer(layerName) != -1) continue;

                for (int i = 8; i < layersProp.arraySize; i++)
                {
                    SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(sp.stringValue))
                    {
                        sp.stringValue = layerName;
                        break;
                    }
                }
            }
            tagManager.ApplyModifiedProperties();

            int walls = LayerMask.NameToLayer("Walls");
            int leftPlayer = LayerMask.NameToLayer("LeftPlayer");
            int rightPlayer = LayerMask.NameToLayer("RightPlayer");
            int boss = LayerMask.NameToLayer("Boss");

            if (leftPlayer >= 0 && rightPlayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(leftPlayer, rightPlayer, true);
            }
            if (walls >= 0)
            {
                if (leftPlayer >= 0) Physics2D.IgnoreLayerCollision(leftPlayer, walls, false);
                if (rightPlayer >= 0) Physics2D.IgnoreLayerCollision(rightPlayer, walls, false);
                if (boss >= 0) Physics2D.IgnoreLayerCollision(boss, walls, true);
            }
            if (boss >= 0)
            {
                if (leftPlayer >= 0) Physics2D.IgnoreLayerCollision(leftPlayer, boss, true);
                if (rightPlayer >= 0) Physics2D.IgnoreLayerCollision(rightPlayer, boss, true);
            }
            // Allow PowerUpCrate (Default layer 0, isTrigger=true) to trigger OnTriggerEnter2D with players
            if (leftPlayer >= 0) Physics2D.IgnoreLayerCollision(leftPlayer, 0, false);
            if (rightPlayer >= 0) Physics2D.IgnoreLayerCollision(rightPlayer, 0, false);
        }

        private static Sprite EnsureSolidSpriteAsset(string assetPath, bool circle)
        {
            string dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(assetPath))
            {
                int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = (size - 1) * 0.5f;
                float radius = size * 0.46f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool inside = !circle || Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) <= radius;
                        tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                    }
                }
                tex.Apply();
                File.WriteAllBytes(assetPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Sprite EnsureSoundIconSpriteAsset(string assetPath, bool soundOn)
        {
            string dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(assetPath))
            {
                int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool draw = false;
                        // Speaker body box on left side
                        if (x >= 12 && x <= 22 && y >= 24 && y <= 40)
                        {
                            draw = true;
                        }
                        // Speaker cone expanding toward center
                        if (x > 22 && x <= 34)
                        {
                            float t = (x - 22f) / 12f;
                            float halfH = Mathf.Lerp(8f, 18f, t);
                            if (Mathf.Abs(y - 32f) <= halfH) draw = true;
                        }

                        if (soundOn)
                        {
                            // Concentric sound wave arcs on right side
                            float dist = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
                            if (x >= 36 && ((dist >= 8f && dist <= 11f) || (dist >= 15f && dist <= 18f)))
                            {
                                draw = true;
                            }
                        }
                        else
                        {
                            // Diagonal X mute mark on right side
                            if (x >= 39 && x <= 53 && y >= 24 && y <= 40)
                            {
                                float dx = x - 46f;
                                float dy = y - 32f;
                                if (Mathf.Abs(dx - dy) <= 1.8f || Mathf.Abs(dx + dy) <= 1.8f)
                                {
                                    draw = true;
                                }
                            }
                        }

                        tex.SetPixel(x, y, draw ? Color.white : Color.clear);
                    }
                }
                tex.Apply();
                File.WriteAllBytes(assetPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static GameObject EnsureBulletPrefab(string path, Sprite sprite, float scale, Color tint)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = new GameObject(Path.GetFileNameWithoutExtension(path));
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            go.transform.localScale = Vector3.one * scale;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.48f;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject EnsureCratePrefab(string path, Sprite sprite)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = new GameObject("PowerUpCrate");
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.96f, 0.72f, 0.15f);
            go.transform.localScale = Vector3.one * 0.5f;

            BoxCollider2D col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;

            go.AddComponent<PowerUpCrate>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static void SetupWallBoundaries()
        {
            int wallsLayer = LayerMask.NameToLayer("Walls");

            // Remove any legacy center divider object so both players share the full arena freely
            GameObject legacyDivider = GameObject.Find("Wall_CenterDivider");
            if (legacyDivider != null)
            {
                Object.DestroyImmediate(legacyDivider);
            }

            GameObject boundariesGroup = GameObject.Find("Wall_Boundaries");
            if (boundariesGroup != null)
            {
                foreach (Collider2D wallCol in boundariesGroup.GetComponentsInChildren<Collider2D>(true))
                {
                    if (wallsLayer >= 0) wallCol.gameObject.layer = wallsLayer;
                    wallCol.isTrigger = false;
                }
            }

            foreach (Collider2D col in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                string lowerName = col.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("wall") || lowerName.Contains("boundary"))
                {
                    if (wallsLayer >= 0) col.gameObject.layer = wallsLayer;
                    col.isTrigger = false;
                }
            }
        }

        private static void SetupEntities(out PlayerController leftPlayer, out PlayerController rightPlayer, out BossController boss)
        {
            int leftLayer = LayerMask.NameToLayer("LeftPlayer");
            int rightLayer = LayerMask.NameToLayer("RightPlayer");
            int bossLayer = LayerMask.NameToLayer("Boss");

            leftPlayer = FindOrAssignPlayer(new[] { "PlayerLeft", "Player_Left" }, 0, leftLayer, bossLayer);
            rightPlayer = FindOrAssignPlayer(new[] { "PlayerRight", "Player_Right" }, 1, rightLayer, bossLayer);

            boss = Object.FindFirstObjectByType<BossController>();
            if (boss != null)
            {
                Transform hitboxTf = boss.transform.Find("HitBox") ?? boss.transform.Find("Hitbox");
                if (hitboxTf != null)
                {
                    if (bossLayer >= 0) hitboxTf.gameObject.layer = bossLayer;
                    Collider2D bossCol = hitboxTf.GetComponent<Collider2D>();
                    if (bossCol == null)
                    {
                        CircleCollider2D defaultBossCol = hitboxTf.gameObject.AddComponent<CircleCollider2D>();
                        defaultBossCol.radius = 0.8f;
                        bossCol = defaultBossCol;
                    }
                    bossCol.isTrigger = true;
                    if (hitboxTf.GetComponent<HitboxRelay>() == null)
                    {
                        hitboxTf.gameObject.AddComponent<HitboxRelay>();
                    }
                }
                else
                {
                    if (bossLayer >= 0) boss.gameObject.layer = bossLayer;
                    Collider2D bossCol = boss.GetComponent<Collider2D>();
                    if (bossCol == null)
                    {
                        CircleCollider2D defaultBossCol = boss.gameObject.AddComponent<CircleCollider2D>();
                        defaultBossCol.radius = 0.8f;
                        bossCol = defaultBossCol;
                    }
                    bossCol.isTrigger = true;
                    if (boss.GetComponent<HitboxRelay>() == null)
                    {
                        boss.gameObject.AddComponent<HitboxRelay>();
                    }
                }

                Transform pivotGroup = boss.transform.Find("PivotPointGroup") ?? boss.transform.Find("PivotPoints_Group");
                SerializedObject bossSo = new SerializedObject(boss);
                bossSo.FindProperty("bossID").intValue = 2;
                if (pivotGroup != null)
                {
                    bossSo.FindProperty("pivotPointsGroup").objectReferenceValue = pivotGroup;

                    SerializedProperty pivotConfigsProp = bossSo.FindProperty("pivotConfigs");
                    if (pivotConfigsProp != null)
                    {
                        int pivotCount = pivotGroup.childCount > 0 ? pivotGroup.childCount : 1;
                        pivotConfigsProp.arraySize = pivotCount;
                        for (int i = 0; i < pivotCount; i++)
                        {
                            Transform targetPivot = pivotGroup.childCount > 0 ? pivotGroup.GetChild(i) : pivotGroup;
                            SerializedProperty cfgProp = pivotConfigsProp.GetArrayElementAtIndex(i);
                            cfgProp.FindPropertyRelative("pivotTransform").objectReferenceValue = targetPivot;

                            SerializedProperty patternsProp = cfgProp.FindPropertyRelative("allowedPatterns");
                            patternsProp.arraySize = 5;
                            patternsProp.GetArrayElementAtIndex(0).enumValueIndex = (int)BulletPathType.LinearRay;
                            patternsProp.GetArrayElementAtIndex(1).enumValueIndex = (int)BulletPathType.HeartCardioidBurst;
                            patternsProp.GetArrayElementAtIndex(2).enumValueIndex = (int)BulletPathType.CurvedArc;
                            patternsProp.GetArrayElementAtIndex(3).enumValueIndex = (int)BulletPathType.ArchimedeanSpiral;
                            patternsProp.GetArrayElementAtIndex(4).enumValueIndex = (int)BulletPathType.Space;
                        }
                    }
                }

                int combinedPlayerMask = (leftLayer >= 0 ? (1 << leftLayer) : 0)
                                       | (rightLayer >= 0 ? (1 << rightLayer) : 0);
                if (combinedPlayerMask != 0)
                {
                    bossSo.FindProperty("playerTargetMask").intValue = combinedPlayerMask;
                }
                bossSo.ApplyModifiedProperties();
            }
        }

        private static PlayerController FindOrAssignPlayer(string[] candidateNames, int playerID, int playerLayer, int bossLayer)
        {
            GameObject playerObj = null;
            foreach (string name in candidateNames)
            {
                playerObj = GameObject.Find(name);
                if (playerObj != null) break;
            }
            if (playerObj == null) return null;

            // Assign LeftPlayer / RightPlayer directly to parent GameObject
            if (playerLayer >= 0) playerObj.layer = playerLayer;

            // Remove legacy child ColliderArea / HitBox objects (single collider now lives on parent)
            foreach (string legacyChildName in new[] { "ColliderArea", "HitBox", "Hitbox" })
            {
                Transform legacyChild = playerObj.transform.Find(legacyChildName);
                if (legacyChild != null)
                {
                    Object.DestroyImmediate(legacyChild.gameObject);
                }
            }

            PlayerController pc = playerObj.GetComponent<PlayerController>() ?? playerObj.AddComponent<PlayerController>();

            Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>() ?? playerObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // Single Collider2D (any shape: Circle, Polygon, Capsule, Box) on parent handles walls, bullet CircleCast, and crate triggers
            Collider2D singleCollider = playerObj.GetComponent<Collider2D>();
            if (singleCollider == null)
            {
                CircleCollider2D defaultCircle = playerObj.AddComponent<CircleCollider2D>();
                defaultCircle.radius = 0.35f;
                singleCollider = defaultCircle;
            }
            singleCollider.isTrigger = false;

            // Attach HitboxRelay directly on parent PlayerLeft / PlayerRight
            if (playerObj.GetComponent<HitboxRelay>() == null)
            {
                playerObj.AddComponent<HitboxRelay>();
            }

            Transform firePivotTf = playerObj.transform.Find("FirePivot");

            SerializedObject so = new SerializedObject(pc);
            so.FindProperty("playerID").intValue = playerID;
            so.FindProperty("maxHP").floatValue = 250f;
            so.FindProperty("extraLives").intValue = 3;
            so.FindProperty("reviveInvulnerabilityDuration").floatValue = 3f;
            if (firePivotTf != null) so.FindProperty("firePivot").objectReferenceValue = firePivotTf;
            if (bossLayer >= 0) so.FindProperty("bossLayerMask").intValue = 1 << bossLayer;
            so.ApplyModifiedProperties();

            return pc;
        }

        private static void WirePartner(PlayerController self, PlayerController partner)
        {
            SerializedObject so = new SerializedObject(self);
            so.FindProperty("partnerPlayer").objectReferenceValue = partner;
            so.ApplyModifiedProperties();
        }

        private static void CreatePlayerTetherLink(Transform playerLeft, Transform playerRight)
        {
            if (playerLeft == null || playerRight == null) return;

            GameObject linkObj = GameObject.Find("Player_ParallelLink");
            if (linkObj == null)
            {
                linkObj = new GameObject("Player_ParallelLink");
            }

            LineRenderer lr = linkObj.GetComponent<LineRenderer>() ?? linkObj.AddComponent<LineRenderer>();
            LinkEffectRenderer linkEffect = linkObj.GetComponent<LinkEffectRenderer>() ?? linkObj.AddComponent<LinkEffectRenderer>();

            Material spriteDefaultMat = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            if (spriteDefaultMat == null)
            {
                Shader defaultShader = Shader.Find("Sprites/Default");
                if (defaultShader != null)
                {
                    spriteDefaultMat = new Material(defaultShader);
                }
            }

            if (spriteDefaultMat != null)
            {
                lr.sharedMaterial = spriteDefaultMat;
            }

            lr.useWorldSpace = true;
            lr.sortingLayerName = "Default";
            lr.sortingOrder = -1;
            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;
            lr.startColor = new Color(0.38f, 0.65f, 0.98f, 0.75f);
            lr.endColor = new Color(0.96f, 0.45f, 0.71f, 0.75f);

            SerializedObject linkSo = new SerializedObject(linkEffect);
            linkSo.FindProperty("pointA").objectReferenceValue = playerLeft;
            linkSo.FindProperty("pointB").objectReferenceValue = playerRight;
            linkSo.FindProperty("hideWhenEndpointInactive").boolValue = true;
            linkSo.FindProperty("sortingLayerName").stringValue = "Default";
            linkSo.FindProperty("sortingOrder").intValue = -1;
            linkSo.FindProperty("lineWidth").floatValue = 0.08f;
            linkSo.ApplyModifiedProperties();

            linkEffect.SetEndpoints(playerLeft, playerRight);
        }

        private static void SetupManagers(GameObject bullet1, GameObject bullet2, GameObject bullet3, GameObject cratePrefab)
        {
            GameObject managers = GameObject.Find("Managers") ?? GameObject.Find("@GameManager") ?? new GameObject("Managers");

            if (managers.GetComponent<GameManager>() == null) managers.AddComponent<GameManager>();
            if (managers.GetComponent<PathFormulaLibrary>() == null) managers.AddComponent<PathFormulaLibrary>();

            BulletPoolManager poolMgr = managers.GetComponent<BulletPoolManager>() ?? managers.AddComponent<BulletPoolManager>();
            SerializedObject poolSo = new SerializedObject(poolMgr);
            SerializedProperty configsProp = poolSo.FindProperty("bulletConfigs");
            configsProp.arraySize = 3;

            SetBulletConfig(configsProp.GetArrayElementAtIndex(0), BulletVisualType.Bullet_1, bullet1, 1200, 0.12f, 1f);
            SetBulletConfig(configsProp.GetArrayElementAtIndex(1), BulletVisualType.Bullet_2, bullet2, 900, 0.22f, 1f);
            SetBulletConfig(configsProp.GetArrayElementAtIndex(2), BulletVisualType.Bullet_3, bullet3, 600, 0.35f, 1f);

            int wallsLayer = LayerMask.NameToLayer("Walls");
            if (wallsLayer >= 0) poolSo.FindProperty("wallBlockingMask").intValue = 1 << wallsLayer;
            poolSo.ApplyModifiedProperties();

            ParallelItemSpawner spawner = managers.GetComponent<ParallelItemSpawner>() ?? managers.AddComponent<ParallelItemSpawner>();
            Transform areaLeft = managers.transform.Find("SpawnArea_Left") ?? CreateSpawnArea(managers.transform, "SpawnArea_Left", new Vector3(-4.2f, -1.5f, 0f), new Vector2(6.5f, 4.5f));
            Transform areaRight = managers.transform.Find("SpawnArea_Right") ?? CreateSpawnArea(managers.transform, "SpawnArea_Right", new Vector3(4.2f, -1.5f, 0f), new Vector2(6.5f, 4.5f));

            SerializedObject spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("cratePrefab").objectReferenceValue = cratePrefab;
            spawnerSo.FindProperty("spawnInterval").floatValue = 15f;
            spawnerSo.FindProperty("leftSpawnArea").objectReferenceValue = areaLeft.GetComponent<BoxCollider2D>();
            spawnerSo.FindProperty("rightSpawnArea").objectReferenceValue = areaRight.GetComponent<BoxCollider2D>();
            spawnerSo.ApplyModifiedProperties();

            AudioAndVFXManager audioMgr = managers.GetComponent<AudioAndVFXManager>() ?? managers.AddComponent<AudioAndVFXManager>();
            AudioSource[] sources = managers.GetComponents<AudioSource>();
            AudioSource bgm = sources.Length > 0 ? sources[0] : managers.AddComponent<AudioSource>();
            AudioSource sfx = sources.Length > 1 ? sources[1] : managers.AddComponent<AudioSource>();
            bgm.loop = true;
            bgm.playOnAwake = false;
            sfx.playOnAwake = false;

            SerializedObject audioSo = new SerializedObject(audioMgr);
            audioSo.FindProperty("bgmSource").objectReferenceValue = bgm;
            audioSo.FindProperty("sfxSource").objectReferenceValue = sfx;
            audioSo.ApplyModifiedProperties();
        }

        private static Transform CreateSpawnArea(Transform parent, string name, Vector3 pos, Vector2 size)
        {
            GameObject area = new GameObject(name);
            area.transform.SetParent(parent);
            area.transform.position = pos;
            BoxCollider2D col = area.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = size;
            return area.transform;
        }

        private static void SetBulletConfig(SerializedProperty element, BulletVisualType type, GameObject prefab, int poolSize, float radius, float scale)
        {
            element.FindPropertyRelative("visualType").enumValueIndex = (int)type;
            element.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("initialPoolSize").intValue = poolSize;
            element.FindPropertyRelative("hitRadius").floatValue = radius;
            element.FindPropertyRelative("scale").floatValue = scale;
        }

        private static void SetupCanvasUI(Sprite solidSprite, Sprite soundOnSprite, Sprite soundOffSprite)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            // Clean old partial HUD placeholder if present to avoid duplicates
            Transform oldHub = canvas.transform.Find("Hub_Gameplay");
            if (oldHub != null) Object.DestroyImmediate(oldHub.gameObject);
            Transform oldHud = canvas.transform.Find("HUD_Gameplay");
            if (oldHud != null) Object.DestroyImmediate(oldHud.gameObject);

            foreach (string oldPanel in new[] { "Panel_Menu", "Panel_Loading", "Text_Countdown", "Panel_Pause", "Panel_EndGame" })
            {
                Transform t = canvas.transform.Find(oldPanel);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            UIManager uiMgr = canvas.GetComponent<UIManager>() ?? canvas.gameObject.AddComponent<UIManager>();

            // 1. HUD_Gameplay
            GameObject hud = CreatePanel(canvas.transform, "HUD_Gameplay", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0), solidSprite);
            CreateHealthBar(hud.transform, "HP_Bar_Left", 0, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(210f, -45f), new Vector2(360f, 28f), new Color(0.22f, 0.51f, 0.96f), solidSprite);
            CreateExtraLivesUI(hud.transform, "ExtraLives_Left", 0, new Vector2(0f, 1f), new Vector2(210f, -84f), new Color(0.38f, 0.65f, 0.98f), solidSprite);

            CreateHealthBar(hud.transform, "HP_Bar_Right", 1, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-210f, -45f), new Vector2(360f, 28f), new Color(0.92f, 0.28f, 0.60f), solidSprite);
            CreateExtraLivesUI(hud.transform, "ExtraLives_Right", 1, new Vector2(1f, 1f), new Vector2(-210f, -84f), new Color(0.96f, 0.45f, 0.71f), solidSprite);

            CreateHealthBar(hud.transform, "HP_Bar_Boss", 2, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(520f, 24f), new Color(0.93f, 0.27f, 0.27f), solidSprite);

            TMP_Text timerText = CreateTMP(hud.transform, "Timer_Text", "08:00", 30, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(200f, 40f));
            Button pauseBtn = CreateButton(hud.transform, "Btn_Pause", "Pause", new Vector2(0.5f, 1f), new Vector2(170f, -82f), new Vector2(110f, 36f), solidSprite);
            Button hudSoundBtn = CreateIconButton(hud.transform, "Btn_Sound_HUD", new Vector2(0.5f, 1f), new Vector2(-150f, -82f), new Vector2(40f, 40f), soundOnSprite);

            // 2. Panel_Menu
            GameObject menu = CreatePanel(canvas.transform, "Panel_Menu", Vector2.zero, Vector2.one, new Color(0.04f, 0.07f, 0.13f, 0.92f), solidSprite);
            CreateTMP(menu.transform, "Title_Menu", "LET'S DASH", 64, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(600f, 90f));
            Button startBtn = CreateButton(menu.transform, "Btn_Start", "Start Game", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(240f, 54f), solidSprite);
            Button menuSoundBtn = CreateIconButton(menu.transform, "Btn_Sound_Menu", new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(52f, 52f), soundOnSprite);

            // 3. Panel_Loading
            GameObject loading = CreatePanel(canvas.transform, "Panel_Loading", Vector2.zero, Vector2.one, new Color(0.04f, 0.07f, 0.13f, 0.95f), solidSprite);
            CreateTMP(loading.transform, "Title_Loading", "Prewarming Bullet Pool...", 32, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(500f, 50f));
            Slider loadingSlider = CreateLoadingSlider(loading.transform, solidSprite);

            // 4. Text_Countdown
            GameObject countdownObj = new GameObject("Text_Countdown", typeof(RectTransform));
            countdownObj.transform.SetParent(canvas.transform, false);
            RectTransform cdRt = countdownObj.GetComponent<RectTransform>();
            cdRt.anchorMin = new Vector2(0.5f, 0.5f);
            cdRt.anchorMax = new Vector2(0.5f, 0.5f);
            cdRt.sizeDelta = new Vector2(300f, 120f);
            TMP_Text countdownText = countdownObj.AddComponent<TextMeshProUGUI>();
            countdownText.text = "5";
            countdownText.fontSize = 84;
            countdownText.alignment = TextAlignmentOptions.Center;

            // 5. Panel_Pause
            GameObject pausePanel = CreatePanel(canvas.transform, "Panel_Pause", Vector2.zero, Vector2.one, new Color(0.04f, 0.07f, 0.13f, 0.88f), solidSprite);
            CreateTMP(pausePanel.transform, "Title_Pause", "PAUSED", 48, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(400f, 70f));
            Button resumeBtn = CreateButton(pausePanel.transform, "Btn_Resume", "Resume", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(220f, 48f), solidSprite);
            Button restartPauseBtn = CreateButton(pausePanel.transform, "Btn_Restart_Pause", "Restart", new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(220f, 48f), solidSprite);
            Button menuPauseBtn = CreateButton(pausePanel.transform, "Btn_Menu_Pause", "Back to Menu", new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(220f, 48f), solidSprite);
            Button pauseSoundBtn = CreateIconButton(pausePanel.transform, "Btn_Sound_Pause", new Vector2(0.5f, 0.5f), new Vector2(0f, -148f), new Vector2(52f, 52f), soundOnSprite);

            // 6. Panel_EndGame
            GameObject endPanel = CreatePanel(canvas.transform, "Panel_EndGame", Vector2.zero, Vector2.one, new Color(0.04f, 0.07f, 0.13f, 0.92f), solidSprite);
            TMP_Text endTitle = CreateTMP(endPanel.transform, "Text_EndGameTitle", "GAME OVER", 44, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(760f, 80f));
            Button restartEndBtn = CreateButton(endPanel.transform, "Btn_Restart_End", "Restart", new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(220f, 50f), solidSprite);
            Button menuEndBtn = CreateButton(endPanel.transform, "Btn_Menu_End", "Back to Menu", new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(220f, 50f), solidSprite);

            SerializedObject uiSo = new SerializedObject(uiMgr);
            uiSo.FindProperty("panelMenu").objectReferenceValue = menu;
            uiSo.FindProperty("panelLoading").objectReferenceValue = loading;
            uiSo.FindProperty("hudGameplay").objectReferenceValue = hud;
            uiSo.FindProperty("panelPause").objectReferenceValue = pausePanel;
            uiSo.FindProperty("panelEndGame").objectReferenceValue = endPanel;
            uiSo.FindProperty("loadingProgressBar").objectReferenceValue = loadingSlider;
            uiSo.FindProperty("textCountdown").objectReferenceValue = countdownText;
            uiSo.FindProperty("textSurvivalTimer").objectReferenceValue = timerText;
            uiSo.FindProperty("textEndGameTitle").objectReferenceValue = endTitle;

            uiSo.FindProperty("startButton").objectReferenceValue = startBtn;
            uiSo.FindProperty("pauseButton").objectReferenceValue = pauseBtn;
            uiSo.FindProperty("resumeButton").objectReferenceValue = resumeBtn;

            AssignButtonArray(uiSo.FindProperty("restartButtons"), restartPauseBtn, restartEndBtn);
            AssignButtonArray(uiSo.FindProperty("backToMenuButtons"), menuPauseBtn, menuEndBtn);
            AssignButtonArray(uiSo.FindProperty("soundToggleButtons"), menuSoundBtn, hudSoundBtn, pauseSoundBtn);

            uiSo.FindProperty("soundOnSprite").objectReferenceValue = soundOnSprite;
            uiSo.FindProperty("soundOffSprite").objectReferenceValue = soundOffSprite;
            uiSo.ApplyModifiedProperties();

            loading.SetActive(false);
            countdownObj.SetActive(false);
            hud.SetActive(false);
            pausePanel.SetActive(false);
            endPanel.SetActive(false);
        }

        private static void AssignButtonArray(SerializedProperty prop, params Button[] buttons)
        {
            prop.arraySize = buttons.Length;
            for (int i = 0; i < buttons.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
            }
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = color.a > 0.01f;
            return go;
        }

        private static void CreateHealthBar(Transform parent, string name, int entityID, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color fillColor, Sprite sprite)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Slider), typeof(DynamicHealthBarUI));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image bg = root.GetComponent<Image>();
            bg.sprite = sprite;
            bg.color = new Color(0.08f, 0.11f, 0.18f, 0.9f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform fillAreaRt = fillArea.GetComponent<RectTransform>();
            fillAreaRt.anchorMin = Vector2.zero;
            fillAreaRt.anchorMax = Vector2.one;
            fillAreaRt.offsetMin = new Vector2(2f, 2f);
            fillAreaRt.offsetMax = new Vector2(-2f, -2f);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Image fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = sprite;
            fillImg.color = fillColor;
            fillImg.type = Image.Type.Sliced;

            Slider slider = root.GetComponent<Slider>();
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.fillRect = fillRt;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            TMP_Text hpText = CreateTMP(root.transform, "HP_Text", "250 / 250", 16, new Vector2(0.5f, 0.5f), Vector2.zero, size);

            DynamicHealthBarUI bar = root.GetComponent<DynamicHealthBarUI>();
            SerializedObject so = new SerializedObject(bar);
            so.FindProperty("targetEntityID").intValue = entityID;
            so.FindProperty("hpSlider").objectReferenceValue = slider;
            so.FindProperty("hpValueText").objectReferenceValue = hpText;
            so.ApplyModifiedProperties();
        }

        private static void CreateExtraLivesUI(Transform parent, string name, int playerID, Vector2 anchor, Vector2 pos, Color iconColor, Sprite sprite)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(DynamicExtraLivesUI));
            root.transform.SetParent(parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(240f, 28f);

            GameObject[] icons = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject icon = new GameObject($"Life_{i + 1}", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(root.transform, false);
                RectTransform iconRt = icon.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(14f + i * 28f, 0f);
                iconRt.sizeDelta = new Vector2(20f, 20f);
                Image img = icon.GetComponent<Image>();
                img.sprite = sprite;
                img.color = iconColor;
                icons[i] = icon;
            }

            TMP_Text livesText = CreateTMP(root.transform, "Lives_Text", "Lives: 3", 16, new Vector2(1f, 0.5f), new Vector2(-55f, 0f), new Vector2(110f, 28f));

            DynamicExtraLivesUI livesUi = root.GetComponent<DynamicExtraLivesUI>();
            SerializedObject so = new SerializedObject(livesUi);
            so.FindProperty("targetPlayerID").intValue = playerID;
            SerializedProperty iconsProp = so.FindProperty("lifeIcons");
            iconsProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            }
            so.FindProperty("livesText").objectReferenceValue = livesText;
            so.ApplyModifiedProperties();
        }

        private static TMP_Text CreateTMP(Transform parent, string name, string text, float fontSize, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = new Color(0.15f, 0.39f, 0.92f);

            CreateTMP(go.transform, "Label", label, 18, new Vector2(0.5f, 0.5f), Vector2.zero, size);

            return go.GetComponent<Button>();
        }

        private static Button CreateIconButton(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Sprite iconSprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            img.sprite = iconSprite;
            img.color = Color.white;
            img.preserveAspect = true;

            return go.GetComponent<Button>();
        }

        private static Slider CreateLoadingSlider(Transform parent, Sprite sprite)
        {
            GameObject go = new GameObject("Loading_Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -20f);
            rt.sizeDelta = new Vector2(420f, 24f);
            Image bg = go.GetComponent<Image>();
            bg.sprite = sprite;
            bg.color = new Color(0.12f, 0.16f, 0.24f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.offsetMin = Vector2.zero;
            faRt.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Image fillImg = fill.GetComponent<Image>();
            fillImg.sprite = sprite;
            fillImg.color = new Color(0.22f, 0.51f, 0.96f);

            Slider slider = go.GetComponent<Slider>();
            slider.interactable = false;
            slider.fillRect = fillRt;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }
    }
}
#endif
