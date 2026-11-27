using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Collider2D))]
public class SceneLoadingPortal : MonoBehaviour
{
    [SerializeField] string destinationScene;

    CharMovement playerInRange;
    bool loading;

    void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        CharMovement movement = other.GetComponentInParent<CharMovement>();
        if (movement != null)
            playerInRange = movement;
    }

    void OnTriggerExit2D(Collider2D other)
    {
        CharMovement movement = other.GetComponentInParent<CharMovement>();
        if (movement != null && movement == playerInRange)
            playerInRange = null;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (loading || playerInRange == null || keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            return;

        if (string.IsNullOrWhiteSpace(destinationScene))
        {
            Debug.LogError("SceneLoadingPortal needs a destination scene name.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(destinationScene))
        {
            Debug.LogError(
                $"Scene '{destinationScene}' is not included in the Build Settings.",
                this);
            return;
        }

        loading = true;
        SceneLoadingOverlay.Begin(destinationScene, playerInRange);
    }
}

public sealed class SceneLoadingOverlay : MonoBehaviour
{
    static SceneLoadingOverlay instance;

    CharMovement player;
    string destinationScene;
    AsyncOperation loadOperation;
    GUIStyle titleStyle;
    GUIStyle progressStyle;

    public static void Begin(string sceneName, CharMovement movement)
    {
        if (instance != null)
            return;

        GameObject overlayObject = new GameObject("SceneLoadingOverlay");
        instance = overlayObject.AddComponent<SceneLoadingOverlay>();
        instance.destinationScene = sceneName;
        instance.player = movement;
        DontDestroyOnLoad(overlayObject);
        instance.StartCoroutine(instance.LoadScene());
    }

    IEnumerator LoadScene()
    {
        if (player != null)
            DontDestroyOnLoad(player.gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
        loadOperation = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Debug.LogError($"Could not start loading scene '{destinationScene}'.", this);
            Destroy(gameObject);
            yield break;
        }

        loadOperation.allowSceneActivation = false;
        while (loadOperation.progress < 0.9f)
            yield return null;

        yield return new WaitForSecondsRealtime(0.35f);
        loadOperation.allowSceneActivation = true;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (player != null)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CharMovement[] scenePlayers = root.GetComponentsInChildren<CharMovement>(true);
                foreach (CharMovement scenePlayer in scenePlayers)
                {
                    if (scenePlayer != null && scenePlayer != player)
                        Destroy(scenePlayer.gameObject);
                }
            }

            GameObject spawn = GameObject.Find("PlayerSpawn");
            if (spawn != null)
                player.transform.position = spawn.transform.position;

            player.SetFollowCamera(Camera.main);
            player.ResetAfterRoomTransition();
        }

        Destroy(gameObject);
    }

    void OnGUI()
    {
        if (loadOperation == null)
            return;

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Max(22, Screen.height / 24),
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.89f, 0.84f, 0.72f) }
            };
            progressStyle = new GUIStyle(GUI.skin.box);
        }

        float barWidth = Mathf.Min(Screen.width * 0.55f, 520f);
        float barHeight = 18f;
        float barX = (Screen.width - barWidth) * 0.5f;
        float barY = Screen.height * 0.58f;
        float progress = Mathf.Clamp01(loadOperation.progress / 0.9f);

        GUI.color = new Color(0.035f, 0.045f, 0.055f, 0.97f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(
            new Rect(0f, barY - 76f, Screen.width, 48f),
            $"MEMUAT {destinationScene.ToUpperInvariant()}",
            titleStyle);
        GUI.Box(new Rect(barX, barY, barWidth, barHeight), GUIContent.none, progressStyle);
        GUI.color = new Color(0.66f, 0.77f, 0.61f);
        GUI.DrawTexture(
            new Rect(barX + 3f, barY + 3f, (barWidth - 6f) * progress, barHeight - 6f),
            Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
