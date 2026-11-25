using UnityEngine;

namespace ReCnc.Unity.CameraControl
{
    public class RtsCamera3DController : MonoBehaviour
    {
        public Vector3 FocusPoint = new Vector3(24f, 1.4f, 24f);
        public float Distance = 38f;
        public float MinDistance = 12f;
        public float MaxDistance = 95f;
        public float DefaultYawDegrees = -45f;
        public float YawDegrees = -45f;
        public float PitchDegrees = 36f;
        public float MoveSpeed = 28f;
        public float ZoomSpeed = 22f;
        public bool EnableEdgeScroll = false;
        public Vector2 WorldMinBounds = new Vector2(2f, 2f);
        public Vector2 WorldMaxBounds = new Vector2(126f, 126f);

        private Vector3 _lastMousePos;
        private bool _isMiddleDragging;

        public void JumpTo(Vector3 worldPos)
        {
            FocusPoint = new Vector3(
                Mathf.Clamp(worldPos.x, WorldMinBounds.x, WorldMaxBounds.x),
                worldPos.y,
                Mathf.Clamp(worldPos.z, WorldMinBounds.y, WorldMaxBounds.y));
            ApplyTransform();
        }

        private void Start()
        {
            ApplyTransform();
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            // 1. Keyboard WASD / Arrow Pan
            float moveH = 0f;
            float moveV = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveV += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveV -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveH += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveH -= 1f;

            if (EnableEdgeScroll && Application.isFocused)
            {
                Vector3 mp = Input.mousePosition;
                const float margin = 14f;
                if (mp.x >= 0 && mp.x < margin) moveH -= 1f;
                else if (mp.x <= Screen.width - 230f && mp.x > Screen.width - 230f - margin) moveH += 1f;
                if (mp.y >= 0 && mp.y < margin) moveV -= 1f;
                else if (mp.y <= Screen.height && mp.y > Screen.height - margin) moveV += 1f;
            }

            Quaternion yawRot = Quaternion.Euler(0f, YawDegrees, 0f);
            Vector3 forwardFlat = yawRot * Vector3.forward;
            Vector3 rightFlat = yawRot * Vector3.right;

            float speedScale = Mathf.Lerp(0.55f, 1.55f, Mathf.InverseLerp(MinDistance, MaxDistance, Distance));
            FocusPoint += (forwardFlat * moveV + rightFlat * moveH) * (MoveSpeed * speedScale * dt);

            // 2. Q / E or Middle Mouse Orbit / Pan
            if (Input.GetKey(KeyCode.Q)) YawDegrees -= 75f * dt;
            if (Input.GetKey(KeyCode.E)) YawDegrees += 75f * dt;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                YawDegrees = DefaultYawDegrees;
                PitchDegrees = 36f;
            }

            if (Input.GetMouseButtonDown(2))
            {
                _isMiddleDragging = true;
                _lastMousePos = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(2))
            {
                _isMiddleDragging = false;
            }

            if (_isMiddleDragging)
            {
                Vector3 delta = Input.mousePosition - _lastMousePos;
                _lastMousePos = Input.mousePosition;
                if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                {
                    YawDegrees += delta.x * 0.28f;
                    PitchDegrees = Mathf.Clamp(PitchDegrees - delta.y * 0.2f, 26f, 68f);
                }
                else
                {
                    float dragFactor = Distance * 0.0018f;
                    FocusPoint -= (rightFlat * delta.x + forwardFlat * delta.y) * dragFactor;
                }
            }

            // 3. Scroll wheel zoom (ignore if hovering over right-side sidebar UI)
            if (Input.mousePosition.x < Screen.width - 230f)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    Distance = Mathf.Clamp(Distance - scroll * ZoomSpeed * 0.16f, MinDistance, MaxDistance);
                }
            }

            FocusPoint.x = Mathf.Clamp(FocusPoint.x, WorldMinBounds.x, WorldMaxBounds.x);
            FocusPoint.z = Mathf.Clamp(FocusPoint.z, WorldMinBounds.y, WorldMaxBounds.y);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            float dynamicPitch = Mathf.Lerp(34f, PitchDegrees, Mathf.InverseLerp(MinDistance, MaxDistance * 0.7f, Distance));
            Quaternion rot = Quaternion.Euler(dynamicPitch, YawDegrees, 0f);
            transform.rotation = rot;
            transform.position = FocusPoint - (rot * Vector3.forward) * Distance;
        }
    }
}
