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
        [SerializeField] private PlayerInventory playerInventory;

        private WorldGrid gridData;
        private WorldGridWorld chunkWorld;
        private WorldResourceInventory fallbackResourceInventory;
        private float nextMiningTime;
        private bool miningActionClaimed;

        public WorldMiningToolData EquippedTool => equippedTool;
        /// <summary>슬롯 수량을 월드 자원 종류로 조회하는 공유 호환 계층입니다.</summary>
        public WorldResourceInventory ResourceInventory
        {
            get
            {
                EnsureInventoryReference();
                return playerInventory != null
                    ? playerInventory.ResourceInventory
                    : fallbackResourceInventory;
            }
        }

        private void Awake()
        {
            EnsureInventoryReference();

            if (miningCamera == null)
            {
                miningCamera = Camera.main;
            }
        }

        /// <summary>
        /// Unity 생명주기 호출 여부와 무관하게 공유 인벤토리 참조를 준비합니다.
        /// EditMode 테스트처럼 Awake 실행이 보장되지 않는 호출 경로도 같은 데이터를 사용합니다.
        /// </summary>
        private void EnsureInventoryReference()
        {
            if (playerInventory == null && fallbackResourceInventory == null)
            {
                playerInventory = GetComponent<PlayerInventory>();
                if (playerInventory == null)
                {
                    // PlayerInventory가 없는 기존 테스트·도구 경로도 같은 슬롯 자료구조를 사용합니다.
                    fallbackResourceInventory = new WorldResourceInventory();
                }
            }

            if (playerInventory != null && !playerInventory.IsInitialized)
            {
                playerInventory.Initialize();
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
            if (chunkWorld != null)
            {
                if (!chunkWorld.TryGetWall(worldCoordinate, out wallBefore, out _))
                {
                    return false;
                }
            }
            else if (!gridData.TryGetWall(localCoordinate, out wallBefore))
            {
                return false;
            }

            bool expectsOreReward = wallBefore.ResourceType != WorldResourceType.None &&
                                    wallBefore.CurrentDurability <= equippedTool.MiningPower;
            WorldResourceInventory resourceInventory = ResourceInventory;
            if (expectsOreReward &&
                (resourceInventory == null ||
                 !resourceInventory.TryReserveAdd(wallBefore.ResourceType, wallBefore.ResourceAmount)))
            {
                return false;
            }

            bool damageApplied;
            bool wasDestroyed;
            if (chunkWorld != null)
            {
                damageApplied = chunkWorld.TryDamageWall(
                    worldCoordinate,
                    equippedTool.MiningPower,
                    out _,
                    out wasDestroyed,
                    out _);
            }
            else
            {
                damageApplied = gridData.TryDamageWall(
                    localCoordinate,
                    equippedTool.MiningPower,
                    out _,
                    out wasDestroyed);
            }

            if (!damageApplied)
            {
                if (expectsOreReward)
                {
                    resourceInventory.CancelReservedAdd(wallBefore.ResourceType, wallBefore.ResourceAmount);
                }

                return false;
            }

            if (expectsOreReward && wasDestroyed)
            {
                // 예약은 월드 피해 이벤트 도중 다른 호출이 보상 공간을 소비하지 않도록 하며,
                // 실제 슬롯 반영은 WorldGrid가 파괴를 확정한 뒤에만 수행합니다.
                if (resourceInventory.CommitReservedAdd(wallBefore.ResourceType, wallBefore.ResourceAmount) &&
                    EnableMiningRewardLogs)
                {
                    int totalAmount = resourceInventory.GetAmount(wallBefore.ResourceType);
                    Debug.Log(
                        $"[MiningReward] 자원: {wallBefore.ResourceType}, 획득 수량: {wallBefore.ResourceAmount}, 현재 보유량: {totalAmount}",
                        this);
                }
            }
            else if (expectsOreReward)
            {
                resourceInventory.CancelReservedAdd(wallBefore.ResourceType, wallBefore.ResourceAmount);
            }

            return true;
        }
    }
}
