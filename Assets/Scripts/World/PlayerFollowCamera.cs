using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>
    /// 기존 Orthographic Camera를 플레이어 추적과 월드 경계 보정에 연결합니다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class PlayerFollowCamera : MonoBehaviour
    {
        [Header("추적 대상")]
        [SerializeField] private Transform target;
        [SerializeField] private WorldGridBootstrap worldGridBootstrap;

        [Header("추적 설정")]
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;

        private Camera targetCamera;
        private Vector3 smoothVelocity;
        private float cameraZ;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            cameraZ = transform.position.z;
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null || targetCamera == null)
            {
                return;
            }

            Vector3 desiredPosition = target.position;
            desiredPosition.z = cameraZ;
            desiredPosition = ClampToWorld(desiredPosition);

            // LateUpdate는 프레임 내 플레이어 이동이 끝난 뒤 카메라를 갱신해 화면 흔들림을 줄입니다.
            Vector3 smoothedPosition = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref smoothVelocity,
                Mathf.Max(0.01f, smoothTime));
            smoothedPosition.z = cameraZ;
            transform.position = ClampToWorld(smoothedPosition);
        }

        private void SnapToTarget()
        {
            if (target == null || targetCamera == null)
            {
                return;
            }

            Vector3 initialPosition = target.position;
            initialPosition.z = cameraZ;
            transform.position = ClampToWorld(initialPosition);
            smoothVelocity = Vector3.zero;
        }

        private Vector3 ClampToWorld(Vector3 cameraPosition)
        {
            WorldGrid gridData = worldGridBootstrap != null ? worldGridBootstrap.GridData : null;
            if (gridData == null || targetCamera == null || targetCamera.aspect <= 0f)
            {
                return cameraPosition;
            }

            float worldMinX = gridData.OriginX;
            float worldMinY = gridData.OriginY;
            float worldMaxX = worldMinX + gridData.Width * gridData.CellWidth;
            float worldMaxY = worldMinY + gridData.Height * gridData.CellHeight;
            float halfViewHeight = Mathf.Max(0f, targetCamera.orthographicSize);
            float halfViewWidth = halfViewHeight * targetCamera.aspect;

            // 화면이 월드보다 큰 축은 양쪽 경계를 넘지 않도록 월드 중앙에 고정합니다.
            cameraPosition.x = ClampAxisToWorld(
                cameraPosition.x,
                worldMinX,
                worldMaxX,
                halfViewWidth);
            cameraPosition.y = ClampAxisToWorld(
                cameraPosition.y,
                worldMinY,
                worldMaxY,
                halfViewHeight);
            cameraPosition.z = cameraZ;
            return cameraPosition;
        }

        private static float ClampAxisToWorld(float position, float worldMinimum, float worldMaximum, float halfViewSize)
        {
            float minimumCenter = worldMinimum + halfViewSize;
            float maximumCenter = worldMaximum - halfViewSize;
            if (minimumCenter > maximumCenter)
            {
                return (worldMinimum + worldMaximum) * 0.5f;
            }

            return Mathf.Clamp(position, minimumCenter, maximumCenter);
        }
    }
}
