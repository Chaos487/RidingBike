using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 管理单局 endless run 的状态:显示距离、监听摔车、结算并支持按 R 重开。
/// </summary>
public class RunManager : MonoBehaviour
{
    Transform bikeTransform;
    CrashDetector crashDetector;
    BikeController bikeController;

    Text distanceText;
    Text statusText;
    float startX;
    bool runEnded;

    void Awake()
    {
        BuildUI();
    }

    public void Initialize(Transform bike, CrashDetector detector, BikeController controller)
    {
        bikeTransform = bike;
        crashDetector = detector;
        bikeController = controller;
        startX = bikeTransform.position.x;
        crashDetector.OnCrash += HandleCrash;
    }

    void Update()
    {
        if (runEnded)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
            return;
        }

        float distance = Mathf.Max(0f, bikeTransform.position.x - startX);
        distanceText.text = $"距离: {distance:0} m";
    }

    void HandleCrash()
    {
        if (runEnded) return;
        runEnded = true;

        if (bikeController != null)
        {
            if (bikeController.backWheelJoint != null)
            {
                JointMotor2D motor = bikeController.backWheelJoint.motor;
                motor.motorSpeed = 0f;
                bikeController.backWheelJoint.motor = motor;
            }
            bikeController.enabled = false;
        }

        float distance = Mathf.Max(0f, bikeTransform.position.x - startX);
        statusText.text = $"摔车了! 距离 {distance:0} m\n按 R 重新开始";
        statusText.gameObject.SetActive(true);
    }

    void BuildUI()
    {
        GameObject canvasGO = new GameObject("EndlessRunCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        distanceText = CreateText(canvasGO.transform, "DistanceText", new Vector2(0f, 1f), new Vector2(20f, -20f), 24, TextAnchor.UpperLeft);

        statusText = CreateText(canvasGO.transform, "StatusText", new Vector2(0.5f, 0.5f), Vector2.zero, 32, TextAnchor.MiddleCenter);
        statusText.gameObject.SetActive(false);
    }

    static Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, int fontSize, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(500f, 120f);

        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }
}
