using System.Collections;
using UnityEngine;

/// <summary>
/// Attach this to the lever root and assign the imported GLB child named LeverPivot.
/// Call Pull() from the interaction system, then ReturnToRest() when the lever resets.
/// </summary>
public sealed class SpaceGambleLeverAnimator : MonoBehaviour
{
    [SerializeField] private Transform leverPivot;
    [SerializeField] private float restAngleX = -4f;
    [SerializeField] private float pulledAngleX = 52f;
    [SerializeField, Min(0.05f)] private float travelSeconds = 0.35f;

    private Coroutine motion;

    private void Reset()
    {
        if (leverPivot == null)
            leverPivot = transform.Find("BaseRoot/LeverPivot") ?? transform.Find("LeverPivot");
    }

    public void Pull()
    {
        AnimateTo(pulledAngleX);
    }

    public void ReturnToRest()
    {
        AnimateTo(restAngleX);
    }

    private void AnimateTo(float angleX)
    {
        if (leverPivot == null)
        {
            Debug.LogWarning("Assign the GLB LeverPivot transform before animating the lever.", this);
            return;
        }

        if (motion != null)
            StopCoroutine(motion);
        motion = StartCoroutine(RotatePivot(angleX));
    }

    private IEnumerator RotatePivot(float targetAngleX)
    {
        Quaternion start = leverPivot.localRotation;
        Quaternion target = Quaternion.AngleAxis(targetAngleX, Vector3.right);
        float duration = Mathf.Max(0.05f, travelSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t); // SmoothStep
            leverPivot.localRotation = Quaternion.Slerp(start, target, t);
            yield return null;
        }

        leverPivot.localRotation = target;
        motion = null;
    }
}
