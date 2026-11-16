using Prohect_DOD_Undergird.DOD.World;
using UnityEngine;

namespace Prohect_DOD_Undergird.World
{
    /// <summary>
    /// 플레이어의 공통 행동 입력을 가까운 벽 채굴로 연결하고 WorldGrid 피해를 요청합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMiningController : MonoBehaviour
    {
        [Header("월드 및 장비")]
        [SerializeField] private WorldGridBootstrap worldGridBootstrap;
        [SerializeField] private WorldMiningToolData equippedTool;
        [SerializeField] private Camera miningCamera;

        private WorldGrid gridData;
        private float nextMiningTime;
        private bool miningActionClaimed;

        public WorldMiningToolData EquippedTool => equippedTool;

        private void Awake()
        {
            if (miningCamera == null)
            {
                miningCamera = Camera.main;
            }
        }

        private void Start()
        {
            if (worldGridBootstrap != null)
            {
                Initialize(worldGridBootstrap.GridData);
            }
        }

        private void OnDisable()
        {
            miningActionClaimed = false;
        }

        /// <summary>
        /// 공유 Bootstrap이 만든 DOD 월드를 채굴 처리에 연결합니다.
        /// </summary>
        public bool Initialize(WorldGrid source)
        {
            if (source == null)
            {
                return false;
            }

            gridData = source;
            return true;
        }

        /// <summary>
        /// 장비 시스템이 현재 채굴 장비를 교체할 때 능력치 에셋을 전달합니다.
        /// </summary>
        public void SetEquippedTool(WorldMiningToolData tool)
        {
            equippedTool = tool;
            nextMiningTime = 0f;
        }

        /// <summary>
        /// 좌클릭을 채굴이 사용할지 판단하고, 채굴 중에는 무기 공격 입력과 겹치지 않게 소비합니다.
        /// </summary>
        public bool HandlePrimaryAction(bool isHeld, Vector3 mouseScreenPosition)
        {
            if (!isHeld)
            {
                miningActionClaimed = false;
                return false;
            }

            if (!TryGetMouseWorldPosition(mouseScreenPosition, out Vector2 mouseWorldPosition) ||
                !TryGetMiningTarget(mouseWorldPosition, out WorldGridCoordinate coordinate))
            {
                return miningActionClaimed;
            }

            miningActionClaimed = true;
            if (Time.time < nextMiningTime)
            {
                return true;
            }

            nextMiningTime = Time.time + Mathf.Max(0.01f, equippedTool.MiningInterval);
            gridData.TryDamageWall(coordinate, equippedTool.MiningPower, out _, out _);
            return true;
        }

        /// <summary>
        /// 월드 좌표가 가리키는 유효한 벽 한 칸에 장비의 채굴력을 한 번 적용합니다.
        /// 입력 쿨다운 없이 판정과 피해를 검증할 때 사용할 수 있습니다.
        /// </summary>
        public bool TryMineAtWorldPosition(Vector2 targetWorldPosition)
        {
            if (!TryGetMiningTarget(targetWorldPosition, out WorldGridCoordinate coordinate))
            {
                return false;
            }

            return gridData.TryDamageWall(coordinate, equippedTool.MiningPower, out _, out _);
        }

        private bool TryGetMouseWorldPosition(Vector3 mouseScreenPosition, out Vector2 mouseWorldPosition)
        {
            mouseWorldPosition = default;
            if (miningCamera == null)
            {
                return false;
            }

            // ScreenToWorldPoint의 z 입력은 월드 z가 아니라 카메라 앞쪽까지의 깊이입니다.
            float cameraDepth = Vector3.Dot(
                transform.position - miningCamera.transform.position,
                miningCamera.transform.forward);
            mouseScreenPosition.z = cameraDepth;
            Vector3 worldPosition = miningCamera.ScreenToWorldPoint(mouseScreenPosition);
            mouseWorldPosition = new Vector2(worldPosition.x, worldPosition.y);
            return true;
        }

        private bool TryGetMiningTarget(
            Vector2 targetWorldPosition,
            out WorldGridCoordinate coordinate)
        {
            coordinate = default;
            if (gridData == null || equippedTool == null || equippedTool.MiningPower <= 0 ||
                equippedTool.MiningInterval <= 0f || equippedTool.MiningRange < 0f ||
                !gridData.TryWorldToCell(targetWorldPosition.x, targetWorldPosition.y, out coordinate) ||
                !gridData.TryGetWall(coordinate, out _))
            {
                return false;
            }

            if (!gridData.TryGetCellCenterWorldPosition(coordinate, out float cellX, out float cellY))
            {
                return false;
            }

            Vector2 playerPosition = transform.position;
            Vector2 offset = new Vector2(cellX - playerPosition.x, cellY - playerPosition.y);
            return offset.sqrMagnitude <= equippedTool.MiningRange * equippedTool.MiningRange;
        }
    }
}
