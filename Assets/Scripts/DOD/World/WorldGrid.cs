using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// 고정 크기 월드의 바닥과 벽을 분리된 연속 배열로 관리하는 Unity 비의존 데이터 시스템입니다.
    /// 좌표 조회는 배열 인덱스 계산만 사용하며, 변경 이벤트는 실제로 바뀐 셀에 대해서만 발생합니다.
    /// </summary>
    public sealed class WorldGrid
    {
        private readonly int width;
        private readonly int height;
        private readonly float cellWidth;
        private readonly float cellHeight;
        private readonly float originX;
        private readonly float originY;
        private readonly WorldFloorData[] floors;
        private readonly WorldWallData[] walls;
        private int floorCount;
        private int wallCount;

        /// <summary>그리드의 X축 셀 수입니다.</summary>
        public int Width => width;
        /// <summary>그리드의 Y축 셀 수입니다.</summary>
        public int Height => height;
        /// <summary>바닥·벽 배열이 공통으로 보유하는 전체 셀 수입니다.</summary>
        public int CellCount => floors.Length;
        /// <summary>현재 배치된 바닥 셀의 수입니다.</summary>
        public int FloorCount => floorCount;
        /// <summary>현재 배치된 벽 셀의 수입니다.</summary>
        public int WallCount => wallCount;
        /// <summary>월드 좌표에서 한 셀의 가로 길이입니다.</summary>
        public float CellWidth => cellWidth;
        /// <summary>월드 좌표에서 한 셀의 세로 길이입니다.</summary>
        public float CellHeight => cellHeight;
        /// <summary>셀 (0, 0)의 왼쪽 경계 월드 X 좌표입니다.</summary>
        public float OriginX => originX;
        /// <summary>셀 (0, 0)의 아래쪽 경계 월드 Y 좌표입니다.</summary>
        public float OriginY => originY;

        /// <summary>
        /// 실제 셀 변경이 일어난 뒤 좌표와 계층을 구독자에게 전달합니다.
        /// 구독자는 이 알림을 이용해 Tilemap이나 충돌 표현의 변경 셀만 갱신할 수 있습니다.
        /// </summary>
        public event Action<WorldGridChange> CellChanged;

        /// <summary>
        /// 직사각형 월드의 크기와 셀-월드 좌표 변환 기준을 설정합니다.
        /// 원점은 셀 (0, 0)의 왼쪽 아래 경계이며, 최대 경계는 그리드 밖으로 처리합니다.
        /// </summary>
        public WorldGrid(
            int width,
            int height,
            float cellWidth,
            float cellHeight,
            float originX = 0f,
            float originY = 0f)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "월드 너비는 1 이상이어야 합니다.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "월드 높이는 1 이상이어야 합니다.");
            }

            if (!IsFinitePositive(cellWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(cellWidth), "셀 너비는 유한한 양수여야 합니다.");
            }

            if (!IsFinitePositive(cellHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(cellHeight), "셀 높이는 유한한 양수여야 합니다.");
            }

            if (!IsFinite(originX))
            {
                throw new ArgumentOutOfRangeException(nameof(originX), "월드 X 원점은 유한한 좌표여야 합니다.");
            }

            if (!IsFinite(originY))
            {
                throw new ArgumentOutOfRangeException(nameof(originY), "월드 Y 원점은 유한한 좌표여야 합니다.");
            }

            double maximumWorldX = (double)originX + (double)width * cellWidth;
            double maximumWorldY = (double)originY + (double)height * cellHeight;
            if (maximumWorldX > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(cellWidth), "월드 경계가 지원되는 좌표 범위를 초과합니다.");
            }

            if (maximumWorldY > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(cellHeight), "월드 경계가 지원되는 좌표 범위를 초과합니다.");
            }

            long cellCount = (long)width * height;
            if (cellCount > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "월드 셀 수가 배열에서 지원하는 범위를 초과합니다.");
            }

            this.width = width;
            this.height = height;
            this.cellWidth = cellWidth;
            this.cellHeight = cellHeight;
            this.originX = originX;
            this.originY = originY;
            floors = new WorldFloorData[(int)cellCount];
            walls = new WorldWallData[(int)cellCount];
        }

        /// <summary>
        /// 정수 좌표가 월드 배열 범위 안에 있는지 검사합니다.
        /// </summary>
        public bool IsInBounds(WorldGridCoordinate coordinate)
        {
            return coordinate.X >= 0 && coordinate.X < width &&
                   coordinate.Y >= 0 && coordinate.Y < height;
        }

        /// <summary>
        /// 월드 위치를 셀 좌표로 변환합니다. 원점과 셀 크기를 기준으로 각 축을 내림 처리합니다.
        /// </summary>
        public bool TryWorldToCell(float worldX, float worldY, out WorldGridCoordinate coordinate)
        {
            coordinate = default;
            if (!IsFinite(worldX) || !IsFinite(worldY))
            {
                return false;
            }

            double cellX = Math.Floor(((double)worldX - originX) / cellWidth);
            double cellY = Math.Floor(((double)worldY - originY) / cellHeight);
            if (cellX < 0 || cellX >= width || cellY < 0 || cellY >= height)
            {
                return false;
            }

            coordinate = new WorldGridCoordinate((int)cellX, (int)cellY);
            return true;
        }

        /// <summary>
        /// 셀의 중심 월드 좌표를 반환합니다. 유효하지 않은 좌표이면 false를 반환합니다.
        /// </summary>
        public bool TryGetCellCenterWorldPosition(
            WorldGridCoordinate coordinate,
            out float worldX,
            out float worldY)
        {
            worldX = 0f;
            worldY = 0f;
            if (!IsInBounds(coordinate))
            {
                return false;
            }

            // 중간 곱셈이 float 범위를 넘어도 최종 좌표가 유효할 수 있으므로 double로 계산합니다.
            worldX = (float)((double)originX + ((double)coordinate.X + 0.5d) * cellWidth);
            worldY = (float)((double)originY + ((double)coordinate.Y + 0.5d) * cellHeight);
            return true;
        }

        /// <summary>
        /// 빈 셀에 고정 바닥을 배치합니다. 바닥은 벽 피해 API의 대상이 되지 않습니다.
        /// </summary>
        public bool TryPlaceFloor(WorldGridCoordinate coordinate, FloorType type)
        {
            if (!IsInBounds(coordinate) || !IsSupportedFloorType(type))
            {
                return false;
            }

            int index = GetIndex(coordinate);
            if (floors[index].HasFloor)
            {
                return false;
            }

            floors[index] = new WorldFloorData(type, FloorState.Present);
            floorCount++;
            NotifyCellChanged(coordinate, WorldGridLayer.Floor, WorldGridChangeKind.Placed);
            return true;
        }

        /// <summary>
        /// 지정 좌표의 바닥 데이터를 복사해 반환합니다. 빈 셀 또는 경계 밖이면 false입니다.
        /// </summary>
        public bool TryGetFloor(WorldGridCoordinate coordinate, out WorldFloorData floor)
        {
            floor = default;
            if (!IsInBounds(coordinate))
            {
                return false;
            }

            floor = floors[GetIndex(coordinate)];
            return floor.HasFloor;
        }

        /// <summary>
        /// 바닥이 있고 벽이 없는 셀에 내구도를 가진 벽을 배치합니다.
        /// 동일 좌표의 중복 배치와 잘못된 내구도는 거부합니다.
        /// </summary>
        public bool TryPlaceWall(
            WorldGridCoordinate coordinate,
            WallType type,
            int maxDurability)
        {
            if (!WorldOreDefinition.IsBaseWallType(type))
            {
                return false;
            }

            return TryPlaceWallState(
                coordinate,
                type,
                maxDurability,
                WorldResourceType.None,
                0);
        }

        /// <summary>광물 정의의 벽 종류, 내구도, 채굴 보상을 한 셀에 함께 배치합니다.</summary>
        public bool TryPlaceOreWall(WorldGridCoordinate coordinate, WorldOreDefinition definition)
        {
            return definition.IsValid && TryPlaceWallState(
                coordinate,
                definition.WallType,
                definition.MaxDurability,
                definition.ResourceType,
                definition.DropAmount);
        }

        /// <summary>
        /// 기존 지형 변경을 재생성할 때 벽 데이터와 보상 메타데이터를 함께 복원합니다.
        /// 일반 API에서 광물 벽이 보상 데이터 없이 생성되는 것을 막기 위해 내부에서만 사용합니다.
        /// </summary>
        internal bool TryPlaceWallState(
            WorldGridCoordinate coordinate,
            WallType type,
            int maxDurability,
            WorldResourceType resourceType,
            int resourceAmount)
        {
            bool isBaseWall = WorldOreDefinition.IsBaseWallType(type) &&
                              resourceType == WorldResourceType.None && resourceAmount == 0;
            bool isOreWall = WorldOreDefinition.IsOreWallType(type) &&
                             WorldOreDefinition.IsSupportedResourceType(resourceType) &&
                             resourceAmount > 0;
            if (!IsInBounds(coordinate) || (!isBaseWall && !isOreWall) || maxDurability <= 0)
            {
                return false;
            }

            int index = GetIndex(coordinate);
            if (!floors[index].HasFloor || walls[index].HasWall)
            {
                return false;
            }

            walls[index] = new WorldWallData(
                type,
                maxDurability,
                maxDurability,
                resourceType,
                resourceAmount);
            wallCount++;
            NotifyCellChanged(coordinate, WorldGridLayer.Wall, WorldGridChangeKind.Placed);
            return true;
        }

        /// <summary>
        /// 지정 좌표의 벽 종류와 내구도를 복사해 반환합니다.
        /// </summary>
        public bool TryGetWall(WorldGridCoordinate coordinate, out WorldWallData wall)
        {
            wall = default;
            if (!IsInBounds(coordinate))
            {
                return false;
            }

            wall = walls[GetIndex(coordinate)];
            return wall.HasWall;
        }

        /// <summary>
        /// 벽에 피해를 적용합니다. 내구도가 0에 도달하면 벽 데이터만 비우고 바닥은 유지합니다.
        /// </summary>
        public bool TryDamageWall(
            WorldGridCoordinate coordinate,
            int damage,
            out int remainingDurability,
            out bool wasDestroyed)
        {
            remainingDurability = 0;
            wasDestroyed = false;
            if (!IsInBounds(coordinate) || damage <= 0)
            {
                return false;
            }

            int index = GetIndex(coordinate);
            WorldWallData wall = walls[index];
            if (!wall.HasWall)
            {
                return false;
            }

            if (damage >= wall.CurrentDurability)
            {
                walls[index] = default;
                wallCount--;
                wasDestroyed = true;
                NotifyCellChanged(coordinate, WorldGridLayer.Wall, WorldGridChangeKind.Removed);
                return true;
            }

            remainingDurability = wall.CurrentDurability - damage;
            walls[index] = new WorldWallData(
                wall.Type,
                wall.MaxDurability,
                remainingDurability,
                wall.ResourceType,
                wall.ResourceAmount);
            NotifyCellChanged(coordinate, WorldGridLayer.Wall, WorldGridChangeKind.Damaged);
            return true;
        }

        /// <summary>
        /// 바닥이 존재하고 벽이 없는 셀만 이동 가능한 것으로 판정합니다.
        /// </summary>
        public bool IsWalkable(WorldGridCoordinate coordinate)
        {
            if (!IsInBounds(coordinate))
            {
                return false;
            }

            int index = GetIndex(coordinate);
            return floors[index].HasFloor && !walls[index].HasWall;
        }

        private int GetIndex(WorldGridCoordinate coordinate)
        {
            return coordinate.Y * width + coordinate.X;
        }

        private void NotifyCellChanged(
            WorldGridCoordinate coordinate,
            WorldGridLayer layer,
            WorldGridChangeKind kind)
        {
            CellChanged?.Invoke(new WorldGridChange(coordinate, layer, kind));
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsSupportedFloorType(FloorType type)
        {
            return type == FloorType.Ground || type == FloorType.Soil || type == FloorType.Stone;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
