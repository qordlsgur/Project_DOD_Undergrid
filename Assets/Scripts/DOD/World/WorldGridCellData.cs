using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 고정 바닥 셀의 종류를 구분합니다. None은 빈 셀에서만 사용합니다.
    /// </summary>
    public enum FloorType
    {
        /// <summary>바닥이 없는 셀을 나타내는 기본값입니다.</summary>
        None,
        /// <summary>기본 이동 가능 지형입니다.</summary>
        Ground,
        /// <summary>흙 계열 바닥입니다.</summary>
        Soil,
        /// <summary>돌 계열 바닥입니다.</summary>
        Stone
    }

    /// <summary>
    /// 바닥 셀이 배치되었는지 나타냅니다.
    /// </summary>
    public enum FloorState
    {
        /// <summary>바닥이 배치되지 않은 상태입니다.</summary>
        Empty,
        /// <summary>바닥이 배치된 상태입니다.</summary>
        Present
    }

    /// <summary>
    /// 파괴 가능한 벽 셀의 종류를 구분합니다. None은 벽이 없는 셀입니다.
    /// </summary>
    public enum WallType
    {
        /// <summary>벽이 없는 셀을 나타내는 기본값입니다.</summary>
        None,
        /// <summary>흙 계열 파괴 가능 벽입니다.</summary>
        Soil,
        /// <summary>돌 계열 파괴 가능 벽입니다.</summary>
        Stone,
        /// <summary>구리 광맥이 포함된 파괴 가능 벽입니다.</summary>
        CopperOre,
        /// <summary>철 광맥이 포함된 파괴 가능 벽입니다.</summary>
        IronOre
    }

    /// <summary>광맥을 파괴했을 때 플레이어가 획득하는 자원의 종류입니다.</summary>
    public enum WorldResourceType
    {
        /// <summary>자원 보상이 없는 일반 벽입니다.</summary>
        None,
        /// <summary>구리 광맥에서 획득하는 자원입니다.</summary>
        Copper,
        /// <summary>철 광맥에서 획득하는 자원입니다.</summary>
        Iron
    }

    /// <summary>
    /// 광물 벽의 생성 규칙과 채굴 보상을 전달하는 Unity 비의존 설정 값입니다.
    /// 노이즈 채널을 정의에 포함해 광물 종류를 추가해도 생성 결과가 호출 순서에 좌우되지 않습니다.
    /// </summary>
    public readonly struct WorldOreDefinition
    {
        /// <summary>생성할 광맥 벽의 종류입니다.</summary>
        public WallType WallType { get; }
        /// <summary>광맥 노이즈를 검사할 바탕 벽의 종류입니다.</summary>
        public WallType HostWallType { get; }
        /// <summary>광맥 파괴 시 지급할 자원입니다.</summary>
        public WorldResourceType ResourceType { get; }
        /// <summary>다른 노이즈 채널과 구분되는 결정적 입력값입니다.</summary>
        public int NoiseChannel { get; }
        /// <summary>광맥 분포 노이즈의 좌표 주파수입니다.</summary>
        public float NoiseFrequency { get; }
        /// <summary>노이즈가 이 값 이상인 바탕 벽 셀에 광맥을 배치합니다.</summary>
        public float SpawnThreshold { get; }
        /// <summary>생성되는 광맥 벽의 최대 내구도입니다.</summary>
        public int MaxDurability { get; }
        /// <summary>광맥 파괴 한 번에 획득할 자원 수량입니다.</summary>
        public int DropAmount { get; }
        /// <summary>생성 및 보상에 필요한 모든 값이 유효한지 반환합니다.</summary>
        public bool IsValid => IsOreWallType(WallType) && IsBaseWallType(HostWallType) &&
                               IsSupportedResourceType(ResourceType) &&
                               NoiseFrequency > 0f &&
                               !float.IsNaN(NoiseFrequency) && !float.IsInfinity(NoiseFrequency) &&
                               SpawnThreshold >= 0f && SpawnThreshold <= 1f &&
                               !float.IsNaN(SpawnThreshold) && !float.IsInfinity(SpawnThreshold) &&
                               MaxDurability > 0 && DropAmount > 0;

        /// <summary>광맥 종류, 바탕 벽, 노이즈 배치 규칙 및 파괴 보상을 설정합니다.</summary>
        public WorldOreDefinition(
            WallType wallType,
            WallType hostWallType,
            WorldResourceType resourceType,
            int noiseChannel,
            float noiseFrequency,
            float spawnThreshold,
            int maxDurability,
            int dropAmount)
        {
            WallType = wallType;
            HostWallType = hostWallType;
            ResourceType = resourceType;
            NoiseChannel = noiseChannel;
            NoiseFrequency = noiseFrequency;
            SpawnThreshold = spawnThreshold;
            MaxDurability = maxDurability;
            DropAmount = dropAmount;

            if (!IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(wallType), "광물 생성 및 보상 설정이 유효하지 않습니다.");
            }
        }

        internal static bool IsOreWallType(WallType type)
        {
            return (int)type > (int)WallType.Stone && Enum.IsDefined(typeof(WallType), type);
        }

        internal static bool IsBaseWallType(WallType type)
        {
            return type == WallType.Soil || type == WallType.Stone;
        }

        internal static bool IsSupportedResourceType(WorldResourceType type)
        {
            return type != WorldResourceType.None && Enum.IsDefined(typeof(WorldResourceType), type);
        }
    }

    /// <summary>
    /// 변경이 발생한 논리적 타일 계층입니다.
    /// </summary>
    public enum WorldGridLayer
    {
        /// <summary>바닥 데이터 계층입니다.</summary>
        Floor,
        /// <summary>벽 데이터 계층입니다.</summary>
        Wall
    }

    /// <summary>
    /// 타일 표현 계층이 셀 변경을 적용할 때 참고할 변경 종류입니다.
    /// </summary>
    public enum WorldGridChangeKind
    {
        /// <summary>빈 셀에 데이터가 배치된 변경입니다.</summary>
        Placed,
        /// <summary>벽 내구도처럼 기존 데이터 값이 갱신된 변경입니다.</summary>
        Damaged,
        /// <summary>해당 계층의 데이터가 제거된 변경입니다.</summary>
        Removed
    }

    /// <summary>
    /// 바닥 종류와 상태를 함께 보관하는 읽기 전용 값입니다.
    /// 배열의 기본값은 바닥이 없는 Empty 상태입니다.
    /// </summary>
    public readonly struct WorldFloorData
    {
        /// <summary>셀에 배치된 바닥 종류입니다.</summary>
        public FloorType Type { get; }
        /// <summary>바닥의 배치 상태입니다.</summary>
        public FloorState State { get; }
        /// <summary>바닥이 현재 존재하면 true입니다.</summary>
        public bool HasFloor => State == FloorState.Present;

        internal WorldFloorData(FloorType type, FloorState state)
        {
            Type = type;
            State = state;
        }
    }

    /// <summary>
    /// 벽 종류, 내구도와 채굴 보상 정보를 보관하는 읽기 전용 값입니다.
    /// 종류가 None이면 비어 있는 셀을 뜻합니다.
    /// </summary>
    public readonly struct WorldWallData
    {
        /// <summary>셀에 배치된 벽 종류입니다.</summary>
        public WallType Type { get; }
        /// <summary>벽이 배치될 때 설정된 최대 내구도입니다.</summary>
        public int MaxDurability { get; }
        /// <summary>피해 적용 후 남은 현재 내구도입니다.</summary>
        public int CurrentDurability { get; }
        /// <summary>이 벽을 파괴했을 때 지급할 자원 종류입니다. 일반 벽은 None입니다.</summary>
        public WorldResourceType ResourceType { get; }
        /// <summary>이 벽을 파괴했을 때 지급할 자원 수량입니다.</summary>
        public int ResourceAmount { get; }
        /// <summary>벽 종류가 None이 아니면 true입니다.</summary>
        public bool HasWall => Type != WallType.None;

        internal WorldWallData(
            WallType type,
            int maxDurability,
            int currentDurability,
            WorldResourceType resourceType,
            int resourceAmount)
        {
            Type = type;
            MaxDurability = maxDurability;
            CurrentDurability = currentDurability;
            ResourceType = resourceType;
            ResourceAmount = resourceAmount;
        }
    }

    /// <summary>
    /// 특정 좌표의 어떤 타일 계층이 어떻게 바뀌었는지 알리는 값입니다.
    /// 외부 표현 계층은 이 알림을 받은 셀만 다시 조회하면 됩니다.
    /// </summary>
    public readonly struct WorldGridChange
    {
        /// <summary>변경된 셀의 정수 좌표입니다.</summary>
        public WorldGridCoordinate Coordinate { get; }
        /// <summary>변경이 발생한 바닥 또는 벽 계층입니다.</summary>
        public WorldGridLayer Layer { get; }
        /// <summary>배치, 내구도 갱신 또는 제거의 구분입니다.</summary>
        public WorldGridChangeKind Kind { get; }

        internal WorldGridChange(
            WorldGridCoordinate coordinate,
            WorldGridLayer layer,
            WorldGridChangeKind kind)
        {
            Coordinate = coordinate;
            Layer = layer;
            Kind = kind;
        }
    }
}
