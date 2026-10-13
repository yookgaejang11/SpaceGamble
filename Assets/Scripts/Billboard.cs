using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
using TMPro;
#endif

/// <summary>
/// 월드 스페이스 텍스트/스프라이트가 어느 방향에서 보든 정방향으로 보이게 해주는 컴포넌트.
/// - 온라인 멀티: Camera Mode = MainCamera (각 클라이언트가 자기 카메라 기준으로 돌림, 동기화 X)
/// - 분할 화면: Camera Mode = EveryCamera (카메라마다 렌더링 직전에 각각 돌림)
/// 메뉴: GameObject > 3D Object > Billboard Text (TMP) 로 바로 생성 가능.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)] // 카메라 이동(Cinemachine 등) 이후에 실행되도록
public class Billboard : MonoBehaviour
{
    public enum FaceMode
    {
        [Tooltip("카메라 화면과 평행. 위/아래에서 봐도 똑바로 보임")]
        CameraPlane,
        [Tooltip("Y축으로만 회전. 이름표처럼 세워둔 채로 돌아야 할 때")]
        YAxisOnly
    }

    public enum CameraMode
    {
        [Tooltip("Camera.main 기준. 온라인 멀티(각자 화면 1개)에 사용")]
        MainCamera,
        [Tooltip("모든 카메라 각각 기준. 분할 화면에 사용")]
        EveryCamera,
        [Tooltip("지정한 카메라 기준. SetCamera()로 런타임에 지정 가능")]
        Specific
    }

    [SerializeField] FaceMode faceMode = FaceMode.CameraPlane;
    [SerializeField] CameraMode cameraMode = CameraMode.MainCamera;
    [SerializeField] Camera specificCamera;
    [Tooltip("글자가 좌우 반전돼 보이면 체크 (텍스트가 아닌 일반 메쉬/쿼드에 쓸 때 등)")]
    [SerializeField] bool flip;

    /// <summary>Specific 모드에서 사용할 카메라를 런타임에 지정 (예: 로컬 플레이어 카메라)</summary>
    public void SetCamera(Camera cam)
    {
        specificCamera = cam;
        cameraMode = CameraMode.Specific;
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering; // URP / HDRP
        Camera.onPreCull += OnPreCullCamera;                                  // Built-in
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        Camera.onPreCull -= OnPreCullCamera;
    }

    void LateUpdate()
    {
        if (cameraMode == CameraMode.EveryCamera) return;

        Camera cam = cameraMode == CameraMode.Specific ? specificCamera : Camera.main;
        if (cam != null) Face(cam);
    }

    void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
    {
        if (cameraMode == CameraMode.EveryCamera) Face(cam);
    }

    void OnPreCullCamera(Camera cam)
    {
        if (cameraMode == CameraMode.EveryCamera) Face(cam);
    }

    void Face(Camera cam)
    {
        Transform camT = cam.transform;
        Vector3 forward = camT.forward;
        Vector3 up = camT.up;

        if (faceMode == FaceMode.YAxisOnly)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return; // 카메라가 수직으로 내려다볼 때
            up = Vector3.up;
        }

        // 텍스트의 forward = 카메라의 forward 여야 정방향 (LookAt(카메라)는 좌우 반전됨)
        Quaternion rot = Quaternion.LookRotation(forward, up);
        if (flip) rot *= Quaternion.Euler(0f, 180f, 0f);
        transform.rotation = rot;
    }

#if UNITY_EDITOR
    [MenuItem("GameObject/3D Object/Billboard Text (TMP)", false, 10)]
    static void CreateBillboardText(MenuCommand command)
    {
        var go = new GameObject("Billboard Text");
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = "Text";
        tmp.fontSize = 4f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);

        go.AddComponent<Billboard>();

        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Billboard Text");
        Selection.activeObject = go;
    }
#endif
}
