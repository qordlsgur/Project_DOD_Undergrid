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
        Stone
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
    /// 벽 종류와 내구도를 보관하는 읽기 전용 값입니다.
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
        /// <summary>벽 종류가 None이 아니면 true입니다.</summary>
        public bool HasWall => Type != WallType.None;

        internal WorldWallData(WallType type, int maxDurability, int currentDurability)
        {
            Type = type;
            MaxDurability = maxDurability;
            CurrentDurability = currentDurability;
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
