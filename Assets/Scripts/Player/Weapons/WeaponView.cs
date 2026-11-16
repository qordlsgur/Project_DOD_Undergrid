using UnityEngine;

namespace Prohect_DOD_Undergird.Player
{
    /// <summary>
    /// 현재 무기의 아이콘을 표시하고 지정한 플레이어 기준점에 배치합니다.
    /// </summary>
    // WeaponView가 표시할 Renderer가 오브젝트에 항상 함께 있도록 요구합니다.
    [RequireComponent(typeof(SpriteRenderer))]
    public class WeaponView : MonoBehaviour
    {
        [SerializeField] private WeaponManager weaponManager;
        [SerializeField, Min(0f)] private float weaponDistance = 0.5f;
        [SerializeField] private Camera aimCamera;

        private SpriteRenderer spriteRenderer;
        private Transform playerTransform;
        private int lastAimUpdateFrame = -1;

        /// <summary>
        /// 현재 화면에 표시된 무기의 오른쪽(+X) 방향을 월드 조준 방향으로 반환합니다.
        /// </summary>
        public Vector2 AimDirection => new Vector2(transform.right.x, transform.right.y).normalized;

        private void Awake()
        {
            // 반복적인 컴포넌트 검색을 피하도록 초기화 시점에 참조를 캐싱합니다.
            spriteRenderer = GetComponent<SpriteRenderer>();
            playerTransform = transform.parent;

            // Inspector에서 카메라를 지정하지 않으면 MainCamera를 한 번 찾아 캐싱합니다.
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (weaponManager != null)
            {
                weaponManager.OnWeaponChanged += HandleWeaponChanged;
                UpdateView(weaponManager.CurrentWeapon);
            }
        }

        private void OnDisable()
        {
            if (weaponManager != null)
            {
                weaponManager.OnWeaponChanged -= HandleWeaponChanged;
            }
        }

        private void LateUpdate()
        {
            RefreshAim();
        }

        /// <summary>
        /// 현재 마우스 위치에 맞춰 무기 위치와 회전을 갱신합니다.
        /// 공격 요청에서도 호출해 LateUpdate 전 입력의 조준 방향을 최신화합니다.
        /// </summary>
        public void RefreshAim()
        {
            // 공격 요청과 LateUpdate가 같은 프레임에 모두 불려도 조준 계산은 한 번만 수행합니다.
            if (lastAimUpdateFrame == Time.frameCount)
            {
                return;
            }

            lastAimUpdateFrame = Time.frameCount;

            if (playerTransform == null || aimCamera == null)
            {
                return;
            }

            Vector3 playerPosition = playerTransform.position;

            // ScreenToWorldPoint의 z는 월드 z좌표가 아니라 카메라 앞쪽으로의 깊이입니다.
            float cameraDepth = Vector3.Dot(
                playerPosition - aimCamera.transform.position,
                aimCamera.transform.forward);

            Vector3 mouseScreenPosition = Input.mousePosition;
            mouseScreenPosition.z = cameraDepth;
            Vector3 mouseWorldPosition = aimCamera.ScreenToWorldPoint(mouseScreenPosition);

            Vector2 aimDirection = new Vector2(
                mouseWorldPosition.x - playerPosition.x,
                mouseWorldPosition.y - playerPosition.y);

            // 커서가 플레이어와 거의 같은 위치면 기존 무기 위치와 회전을 유지합니다.
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            aimDirection.Normalize();
            transform.position = new Vector3(
                playerPosition.x + aimDirection.x * weaponDistance,
                playerPosition.y + aimDirection.y * weaponDistance,
                playerPosition.z);

            // 무기 스프라이트의 기본 방향(+X)을 조준 방향에 맞춥니다.
            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void HandleWeaponChanged(WeaponData previousWeapon, WeaponData newWeapon)
        {
            UpdateView(newWeapon);
        }

        private void UpdateView(WeaponData weaponData)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = weaponData != null ? weaponData.Icon : null;
            spriteRenderer.enabled = weaponData != null && weaponData.Icon != null;
        }
    }
}
