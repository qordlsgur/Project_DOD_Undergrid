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
        private const bool EnableMiningRewardLogs = true;

        [Header("월드 및 장비")]
        [SerializeField] private WorldGridBootstrap worldGridBootstrap;
        [SerializeField] private WorldMiningToolData equippedTool;
        [SerializeField] private Camera miningCamera;

        private WorldGrid gridData;
        private WorldGridWorld chunkWorld;
        private readonly WorldResourceInventory resourceInventory = new WorldResourceInventory();
        private float nextMiningTime;
        private bool miningActionClaimed;

        public WorldMiningToolData EquippedTool => equippedTool;
        /// <summary>채굴로 획득한 자원의 현재 수량을 제공하는 DOD 데이터입니다.</summary>
        public WorldResourceInventory ResourceInventory => resourceInventory;

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
                if (worldGridBootstrap.IsChunkStreamingEnabled)
                {
                    Initialize(worldGridBootstrap.ChunkWorld);
                }
                else
                {
                    Initialize(worldGridBootstrap.GridData);
                }
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
            chunkWorld = null;
            return true;
        }

        /// <summary>스트리밍 월드의 절대 좌표 API를 채굴 처리에 연결합니다.</summary>
        public bool Initialize(WorldGridWorld source)
        {
            if (source == null)
            {
                return false;
            }

            chunkWorld = source;
            gridData = null;
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
                !TryGetMiningTarget(
                    mouseWorldPosition,
                    out WorldGridCoordinate localCoordinate,
                    out WorldCellCoordinate worldCoordinate))
            {
                return miningActionClaimed;
            }

            miningActionClaimed = true;
            if (Time.time < nextMiningTime)
            {
                return true;
            }

            nextMiningTime = Time.time + Mathf.Max(0.01f, equippedTool.MiningInterval);
            ApplyMiningDamage(localCoordinate, worldCoordinate);
            return true;
        }

        /// <summary>
        /// 월드 좌표가 가리키는 유효한 벽 한 칸에 장비의 채굴력을 한 번 적용합니다.
        /// 입력 쿨다운 없이 판정과 피해를 검증할 때 사용할 수 있습니다.
        /// </summary>
        public bool TryMineAtWorldPosition(Vector2 targetWorldPosition)
        {
            if (!TryGetMiningTarget(
                    targetWorldPosition,
                    out WorldGridCoordinate localCoordinate,
                    out WorldCellCoordinate worldCoordinate))
            {
                return false;
            }

            return ApplyMiningDamage(localCoordinate, worldCoordinate);
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
            out WorldGridCoordinate localCoordinate,
            out WorldCellCoordinate worldCoordinate)
        {
            localCoordinate = default;
            worldCoordinate = default;
            if ((gridData == null && chunkWorld == null) || equippedTool == null || equippedTool.MiningPower <= 0 ||
                equippedTool.MiningInterval <= 0f || equippedTool.MiningRange < 0f ||
                !TryResolveMiningCell(targetWorldPosition, out localCoordinate, out worldCoordinate,
                    out float cellX, out float cellY))
            {
                return false;
            }

            Vector2 playerPosition = transform.position;
            Vector2 offset = new Vector2(cellX - playerPosition.x, cellY - playerPosition.y);
            return offset.sqrMagnitude <= equippedTool.MiningRange * equippedTool.MiningRange;
        }

        private bool TryResolveMiningCell(
            Vector2 targetWorldPosition,
            out WorldGridCoordinate localCoordinate,
            out WorldCellCoordinate worldCoordinate,
            out float centerX,
            out float centerY)
        {
            localCoordinate = default;
            worldCoordinate = default;
            centerX = 0f;
            centerY = 0f;

            if (chunkWorld != null)
            {
                return chunkWorld.TryWorldPositionToCell(
                           targetWorldPosition.x,
                           targetWorldPosition.y,
                           out worldCoordinate) &&
                       chunkWorld.GetAccessStatus(worldCoordinate) == WorldCellAccessStatus.Ready &&
                       chunkWorld.TryGetWall(worldCoordinate, out _, out _) &&
                       chunkWorld.TryGetCellCenterWorldPosition(worldCoordinate, out centerX, out centerY);
            }

            if (gridData == null ||
                !gridData.TryWorldToCell(targetWorldPosition.x, targetWorldPosition.y, out localCoordinate) ||
                !gridData.TryGetWall(localCoordinate, out _))
            {
                return false;
            }

            return gridData.TryGetCellCenterWorldPosition(localCoordinate, out centerX, out centerY);
        }

        private bool ApplyMiningDamage(
            WorldGridCoordinate localCoordinate,
            WorldCellCoordinate worldCoordinate)
        {
            WorldWallData wallBefore;
            bool wasDestroyed;
            if (chunkWorld != null)
            {
                if (!chunkWorld.TryGetWall(worldCoordinate, out wallBefore, out _) ||
                    !chunkWorld.TryDamageWall(
                        worldCoordinate,
                        equippedTool.MiningPower,
                        out _,
                        out wasDestroyed,
                        out _))
                {
                    return false;
                }
            }
            else if (!gridData.TryGetWall(localCoordinate, out wallBefore) ||
                     !gridData.TryDamageWall(
                         localCoordinate,
                         equippedTool.MiningPower,
                         out _,
                         out wasDestroyed))
            {
                return false;
            }

            if (wasDestroyed && wallBefore.ResourceType != WorldResourceType.None)
            {
                // 보상은 실제로 벽을 파괴한 입력에서만 한 번 지급합니다.
                // 청크 재생성은 변경 데이터만 복원하므로 생성 과정에서 보상이 다시 지급되지 않습니다.
                if (resourceInventory.TryAdd(wallBefore.ResourceType, wallBefore.ResourceAmount) &&
                    EnableMiningRewardLogs)
                {
                    int totalAmount = resourceInventory.GetAmount(wallBefore.ResourceType);
                    Debug.Log(
                        $"[MiningReward] 자원: {wallBefore.ResourceType}, 획득 수량: {wallBefore.ResourceAmount}, 현재 보유량: {totalAmount}",
                        this);
                }
            }

            return true;
        }
    }
}
