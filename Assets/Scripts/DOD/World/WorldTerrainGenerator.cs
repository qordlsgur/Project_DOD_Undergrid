using System;

namespace Prohect_DOD_Undergird.DOD.World
{
    /// <summary>
    /// Seed와 셀 좌표만으로 재현 가능한 벽 지형을 생성하는 순수 C# 유틸리티입니다.
    /// 바닥 및 Unity 표현은 변경하지 않고 WorldGrid의 벽 배치 API만 사용합니다.
    /// </summary>
    public static class WorldTerrainGenerator
    {
        private const int WallChannel = 0x2C9277B5;
        private const int MaterialChannel = 0x5F356495;
        private const int CaveChannel = 0x36D9F2A1;
        private const int CaveDetailChannel = 0x71A4C35B;
        private const int ChunkEntranceChannel = 0x4A7C159D;

        /// <summary>
        /// 지정된 노이즈 설정으로 안전 영역을 제외한 셀에 바탕 벽과 설정된 광맥을 배치합니다.
        /// </summary>
        public static int GenerateWalls(
            WorldGrid grid,
            int seed,
            float noiseFrequency,
            float wallDensityThreshold,
            float stoneWallRatio,
            int maxDurability,
            WorldGridCoordinate playerStart,
            float safeRadius,
            WorldGridCoordinate[] additionalSafeCenters = null,
            WorldOreDefinition[] oreDefinitions = null)
        {
            if (grid == null || !IsFinite(noiseFrequency) || noiseFrequency <= 0f ||
                !IsFinite(wallDensityThreshold) || !IsFinite(stoneWallRatio) ||
                !IsFinite(safeRadius) || maxDurability <= 0 || safeRadius < 0f)
            {
                return 0;
            }

            wallDensityThreshold = Clamp01(wallDensityThreshold);
            stoneWallRatio = Clamp01(stoneWallRatio);
            double safeRadiusSquared = (double)safeRadius * safeRadius;
            int placedCount = 0;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var coordinate = new WorldGridCoordinate(x, y);
                    if (IsProtected(coordinate, playerStart, safeRadiusSquared) ||
                        IsProtectedByAdditionalCenter(grid, coordinate, additionalSafeCenters, safeRadiusSquared))
                    {
                        continue;
                    }

                    float wallValue = SampleValueNoise(x, y, seed, WallChannel, noiseFrequency);
                    if (wallValue < wallDensityThreshold)
                    {
                        continue;
                    }

                    float materialValue = SampleValueNoise(x, y, seed, MaterialChannel, noiseFrequency);
                    WallType hostWallType = materialValue < stoneWallRatio ? WallType.Stone : WallType.Soil;
                    if (TryPlaceGeneratedWall(
                            grid,
                            coordinate,
                            x,
                            y,
                            seed,
                            hostWallType,
                            maxDurability,
                            oreDefinitions))
                    {
                        placedCount++;
                    }
                }
            }

            return placedCount;
        }

        /// <summary>
        /// 지정 청크와 겹치는 셀만 생성합니다. Noise와 보호 영역에는 절대 월드 셀 좌표를 사용합니다.
        /// 호출 순서와 무관하게 같은 Seed·청크 주소는 같은 결과를 냅니다.
        /// </summary>
        public static int GenerateChunkWalls(
            WorldGrid targetGrid,
            WorldCellCoordinate targetGridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            int seed,
            float noiseFrequency,
            float wallDensityThreshold,
            float stoneWallRatio,
            int maxDurability,
            WorldCellCoordinate playerStart,
            float safeRadius,
            WorldCellCoordinate[] additionalSafeCenters = null,
            WorldOreDefinition[] oreDefinitions = null)
        {
            if (!IsValidChunkRequest(targetGrid, chunkSize) ||
                !TryGetChunkGridIntersection(
                    targetGrid, targetGridWorldOrigin, chunk, chunkSize,
                    out long minimumX, out long minimumY, out long maximumXExclusive, out long maximumYExclusive) ||
                !IsFinite(noiseFrequency) ||
                noiseFrequency <= 0f || !IsFinite(wallDensityThreshold) ||
                !IsFinite(stoneWallRatio) || !IsFinite(safeRadius) ||
                maxDurability <= 0 || safeRadius < 0f)
            {
                return 0;
            }

            wallDensityThreshold = Clamp01(wallDensityThreshold);
            stoneWallRatio = Clamp01(stoneWallRatio);
            double safeRadiusSquared = (double)safeRadius * safeRadius;
            int placedCount = 0;

            for (long worldY = minimumY; worldY < maximumYExclusive; worldY++)
            {
                for (long worldX = minimumX; worldX < maximumXExclusive; worldX++)
                {
                    if (!TryMapToGrid(targetGrid, targetGridWorldOrigin, worldX, worldY, out WorldGridCoordinate gridCell))
                    {
                        continue;
                    }

                    int cellX = (int)worldX;
                    int cellY = (int)worldY;
                    var worldCell = new WorldCellCoordinate(cellX, cellY);
                    if (IsProtected(worldCell, playerStart, safeRadiusSquared) ||
                        IsProtectedByAdditionalCenter(worldCell, additionalSafeCenters, safeRadiusSquared))
                    {
                        continue;
                    }

                    float wallValue = SampleValueNoise(
                        cellX,
                        cellY,
                        seed,
                        WallChannel,
                        noiseFrequency);
                    if (wallValue < wallDensityThreshold)
                    {
                        continue;
                    }

                    float materialValue = SampleValueNoise(
                        cellX,
                        cellY,
                        seed,
                        MaterialChannel,
                        noiseFrequency);
                    WallType hostWallType = materialValue < stoneWallRatio ? WallType.Stone : WallType.Soil;
                    if (TryPlaceGeneratedWall(
                            targetGrid,
                            gridCell,
                            cellX,
                            cellY,
                            seed,
                            hostWallType,
                            maxDurability,
                            oreDefinitions))
                    {
                        placedCount++;
                    }
                }
            }

            return placedCount;
        }

        private static bool TryPlaceGeneratedWall(
            WorldGrid grid,
            WorldGridCoordinate gridCell,
            int worldX,
            int worldY,
            int seed,
            WallType hostWallType,
            int wallDurability,
            WorldOreDefinition[] oreDefinitions)
        {
            if (oreDefinitions != null)
            {
                for (int i = 0; i < oreDefinitions.Length; i++)
                {
                    WorldOreDefinition definition = oreDefinitions[i];
                    if (!definition.IsValid || definition.HostWallType != hostWallType)
                    {
                        continue;
                    }

                    float oreValue = SampleValueNoise(
                        worldX,
                        worldY,
                        seed,
                        definition.NoiseChannel,
                        definition.NoiseFrequency);
                    if (oreValue >= Clamp01(definition.SpawnThreshold))
                    {
                        return grid.TryPlaceOreWall(gridCell, definition);
                    }
                }
            }

            return grid.TryPlaceWall(gridCell, hostWallType, wallDurability);
        }

        /// <summary>
        /// 서로 다른 공간 규모의 노이즈를 합성해 벽 데이터만 제거하고 불규칙한 동굴을 만듭니다.
        /// 바닥과 안전 영역은 변경하지 않습니다.
        /// </summary>
        public static int CarveCaves(
            WorldGrid grid,
            int seed,
            float caveNoiseFrequency,
            float carveThreshold)
        {
            float detailFrequency = caveNoiseFrequency * 2.7f;
            if (grid == null || !IsFinite(caveNoiseFrequency) || caveNoiseFrequency <= 0f ||
                !IsFinite(detailFrequency) || !IsFinite(carveThreshold))
            {
                return 0;
            }

            carveThreshold = Clamp01(carveThreshold);
            int removedWallCount = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var coordinate = new WorldGridCoordinate(x, y);
                    if (!grid.TryGetWall(coordinate, out WorldWallData wall))
                    {
                        continue;
                    }

                    float broadNoise = SampleValueNoise(x, y, seed, CaveChannel, caveNoiseFrequency);
                    float detailNoise = SampleValueNoise(x, y, seed, CaveDetailChannel, detailFrequency);
                    float caveNoise = broadNoise * 0.75f + detailNoise * 0.25f;
                    if (caveNoise < carveThreshold)
                    {
                        continue;
                    }

                    if (grid.TryDamageWall(
                        coordinate,
                        wall.CurrentDurability,
                        out _,
                        out bool wasDestroyed) && wasDestroyed)
                    {
                        removedWallCount++;
                    }
                }
            }

            return removedWallCount;
        }

        /// <summary>지정 청크와 겹치는 셀의 동굴만 절대 좌표 노이즈로 생성합니다.</summary>
        public static int CarveChunkCaves(
            WorldGrid targetGrid,
            WorldCellCoordinate targetGridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            int seed,
            float caveNoiseFrequency,
            float carveThreshold)
        {
            float detailFrequency = caveNoiseFrequency * 2.7f;
            if (!IsValidChunkRequest(targetGrid, chunkSize) ||
                !TryGetChunkGridIntersection(
                    targetGrid, targetGridWorldOrigin, chunk, chunkSize,
                    out long minimumX, out long minimumY, out long maximumXExclusive, out long maximumYExclusive) ||
                !IsFinite(caveNoiseFrequency) || caveNoiseFrequency <= 0f ||
                !IsFinite(detailFrequency) || !IsFinite(carveThreshold))
            {
                return 0;
            }

            carveThreshold = Clamp01(carveThreshold);
            int removedWallCount = 0;
            for (long worldY = minimumY; worldY < maximumYExclusive; worldY++)
            {
                for (long worldX = minimumX; worldX < maximumXExclusive; worldX++)
                {
                    if (!TryMapToGrid(targetGrid, targetGridWorldOrigin, worldX, worldY, out WorldGridCoordinate gridCell) ||
                        !targetGrid.TryGetWall(gridCell, out WorldWallData wall))
                    {
                        continue;
                    }

                    float broadNoise = SampleValueNoise(
                        (int)worldX,
                        (int)worldY,
                        seed,
                        CaveChannel,
                        caveNoiseFrequency);
                    float detailNoise = SampleValueNoise(
                        (int)worldX,
                        (int)worldY,
                        seed,
                        CaveDetailChannel,
                        detailFrequency);
                    float caveNoise = broadNoise * 0.75f + detailNoise * 0.25f;
                    if (caveNoise >= carveThreshold &&
                        targetGrid.TryDamageWall(
                            gridCell,
                            wall.CurrentDurability,
                            out _,
                            out bool wasDestroyed) && wasDestroyed)
                    {
                        removedWallCount++;
                    }
                }
            }

            return removedWallCount;
        }

        /// <summary>
        /// 청크 내부의 활성 경계 입구와 주요 동굴을 BFS로 확인하고 필요한 바닥 셀만 통로로 엽니다.
        /// 이웃 청크의 데이터가 없어도 월드 그리드에 양쪽 경계가 포함되어 있으면 현재 청크만 처리합니다.
        /// </summary>
        public static bool ConnectChunkEntrances(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldCellBounds worldBounds,
            WorldChunkCoordinate chunk,
            int chunkSize,
            int seed,
            int minimumMajorCaveSize,
            int corridorWidth,
            out int activeEntranceCount)
        {
            activeEntranceCount = 0;
            if (!TryGetChunkBounds(chunk, chunkSize, out long minimumX, out long minimumY, out long maximumXExclusive, out long maximumYExclusive) ||
                grid == null || minimumMajorCaveSize < 1 || corridorWidth < 1 || corridorWidth > chunkSize ||
                (long)chunkSize * chunkSize > int.MaxValue)
            {
                return false;
            }

            int area = chunkSize * chunkSize;
            var entranceStarts = new WorldCellCoordinate[4];
            var entranceActive = new bool[4];
            for (int sideIndex = 0; sideIndex < 4; sideIndex++)
            {
                WorldChunkSide side = (WorldChunkSide)sideIndex;
                if (!TryGetChunkBoundaryEntranceStart(chunk, chunkSize, seed, side, corridorWidth, out WorldCellCoordinate start) ||
                    !IsActiveEntranceWithinWorldBounds(
                        grid, gridWorldOrigin, worldBounds, side, start, corridorWidth))
                {
                    continue;
                }

                entranceStarts[activeEntranceCount] = start;
                entranceActive[activeEntranceCount] = true;
                activeEntranceCount++;
                CarveBoundaryOpening(grid, gridWorldOrigin, chunk, chunkSize, side, start, corridorWidth);
            }

            var connected = new bool[area];
            var componentVisited = new bool[area];
            var queue = new int[area];
            var representatives = new int[area];
            int[] parent = new int[area];
            int[] visitStamp = new int[area];
            int searchGeneration = 0;
            int connectedSeed = -1;

            // 입구가 있는 청크는 첫 입구를 기준으로 모든 입구를 하나의 영역에 합칩니다.
            if (activeEntranceCount > 0 &&
                TryGetChunkLocalIndex(entranceStarts[0], chunk, chunkSize, out int firstEntranceIndex))
            {
                connectedSeed = firstEntranceIndex;
                FloodChunkWalkableRegion(
                    grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                    connectedSeed, connected, queue);
            }

            int representativeCount = FindMajorChunkComponents(
                grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                minimumMajorCaveSize, componentVisited, representatives, queue);

            // 경계가 없는 월드 외곽 청크는 가장 먼저 발견한 주요 동굴을 내부 연결의 기준으로 삼습니다.
            if (connectedSeed < 0 && representativeCount > 0)
            {
                connectedSeed = representatives[0];
                FloodChunkWalkableRegion(
                    grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                    connectedSeed, connected, queue);
            }

            if (connectedSeed < 0)
            {
                return true;
            }

            for (int entranceIndex = 0; entranceIndex < activeEntranceCount; entranceIndex++)
            {
                if (!entranceActive[entranceIndex] ||
                    !TryGetChunkLocalIndex(entranceStarts[entranceIndex], chunk, chunkSize, out int entranceLocalIndex))
                {
                    return false;
                }

                if (!connected[entranceLocalIndex] &&
                    !ConnectChunkCellToRegion(
                        grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                        entranceLocalIndex, corridorWidth, connected, visitStamp, parent, ref searchGeneration, queue))
                {
                    return false;
                }
            }

            for (int componentIndex = 0; componentIndex < representativeCount; componentIndex++)
            {
                int representative = representatives[componentIndex];
                if (!connected[representative] &&
                    !ConnectChunkCellToRegion(
                        grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                        representative, corridorWidth, connected, visitStamp, parent, ref searchGeneration, queue))
                {
                    return false;
                }
            }

            // 최종 검증도 청크 범위 안에서 수행해 활성 입구나 주요 동굴이 고립되지 않게 합니다.
            ArrayClear(componentVisited);
            FloodChunkWalkableRegion(
                grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                connectedSeed, componentVisited, queue);
            for (int entranceIndex = 0; entranceIndex < activeEntranceCount; entranceIndex++)
            {
                if (!TryGetChunkLocalIndex(entranceStarts[entranceIndex], chunk, chunkSize, out int entranceLocalIndex) ||
                    !componentVisited[entranceLocalIndex])
                {
                    return false;
                }
            }

            for (int componentIndex = 0; componentIndex < representativeCount; componentIndex++)
            {
                if (!componentVisited[representatives[componentIndex]])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 인접한 두 청크가 공유하는 경계에서 동일한 입구 시작 셀을 계산합니다.
        /// 반환 좌표는 요청한 청크 안의 경계 셀이며, 반대편 청크는 같은 축 좌표를 계산합니다.
        /// </summary>
        public static bool TryGetChunkBoundaryEntranceStart(
            WorldChunkCoordinate chunk,
            int chunkSize,
            int seed,
            WorldChunkSide side,
            int corridorWidth,
            out WorldCellCoordinate entranceStart)
        {
            entranceStart = default;
            if (!TryGetChunkBounds(chunk, chunkSize, out long minimumX, out long minimumY, out _, out _) ||
                corridorWidth < 1 || corridorWidth > chunkSize)
            {
                return false;
            }

            bool verticalEdge = side == WorldChunkSide.West || side == WorldChunkSide.East;
            long boundaryX = verticalEdge
                ? minimumX + (side == WorldChunkSide.East ? chunkSize : 0L)
                : minimumX;
            long boundaryY = verticalEdge
                ? minimumY
                : minimumY + (side == WorldChunkSide.North ? chunkSize : 0L);
            if (!IsValidCellCoordinate(boundaryX, boundaryY))
            {
                return false;
            }

            int channel = verticalEdge ? ChunkEntranceChannel : ChunkEntranceChannel ^ 0x5A5A5A5A;
            int hash = Hash(seed, (int)boundaryX, (int)boundaryY, channel);
            int availableStarts = chunkSize - corridorWidth + 1;
            int offset = (int)((uint)hash % (uint)availableStarts);
            long tangentStart = verticalEdge ? minimumY + offset : minimumX + offset;
            long tangentEnd = tangentStart + corridorWidth - 1L;
            if (tangentStart < int.MinValue || tangentEnd > int.MaxValue)
            {
                return false;
            }

            switch (side)
            {
                case WorldChunkSide.West:
                    entranceStart = new WorldCellCoordinate((int)minimumX, (int)tangentStart);
                    return true;
                case WorldChunkSide.East:
                    entranceStart = new WorldCellCoordinate((int)(minimumX + chunkSize - 1L), (int)tangentStart);
                    return true;
                case WorldChunkSide.South:
                    entranceStart = new WorldCellCoordinate((int)tangentStart, (int)minimumY);
                    return true;
                case WorldChunkSide.North:
                    entranceStart = new WorldCellCoordinate((int)tangentStart, (int)(minimumY + chunkSize - 1L));
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 시작 지점과 분리된 최소 크기 이상의 이동 가능 영역을 BFS로 찾아 벽 제거 통로로 연결합니다.
        /// 통로를 만든 뒤 시작 지점에서 다시 BFS를 수행해 실제 연결을 확인합니다.
        /// </summary>
        public static bool ConnectMajorCaves(
            WorldGrid grid,
            WorldGridCoordinate start,
            int minimumMajorCaveSize,
            int corridorWidth,
            out int majorCaveCount)
        {
            majorCaveCount = 0;
            if (grid == null || !grid.IsInBounds(start) || !grid.IsWalkable(start) ||
                minimumMajorCaveSize < 1 || corridorWidth < 1)
            {
                return false;
            }

            int cellCount = grid.CellCount;
            int width = grid.Width;
            var queue = new System.Collections.Generic.Queue<int>(cellCount);
            var startRegion = new bool[cellCount];
            var componentVisited = new bool[cellCount];
            var pathVisited = new bool[cellCount];
            var parent = new int[cellCount];
            var majorRepresentatives = new int[cellCount];

            int startIndex = GetIndex(start, width);
            FloodWalkableRegion(grid, startIndex, startRegion, queue);
            FloodWalkableRegion(grid, startIndex, componentVisited, queue);

            for (int index = 0; index < cellCount; index++)
            {
                if (componentVisited[index] || !IsWalkableIndex(grid, index))
                {
                    continue;
                }

                int componentSize = FloodWalkableRegion(grid, index, componentVisited, queue);
                if (componentSize >= minimumMajorCaveSize)
                {
                    majorRepresentatives[majorCaveCount++] = index;
                }
            }

            if (majorCaveCount == 0)
            {
                return true;
            }

            // 모든 바닥 셀을 통과 가능한 탐색 영역으로 두어 각 주요 공간까지의 최단 격자 경로를 한 번 계산합니다.
            BuildFloorPathTree(grid, startIndex, pathVisited, parent, queue);
            for (int caveIndex = 0; caveIndex < majorCaveCount; caveIndex++)
            {
                int currentIndex = majorRepresentatives[caveIndex];
                if (!pathVisited[currentIndex])
                {
                    continue;
                }

                while (currentIndex >= 0 && !startRegion[currentIndex])
                {
                    WorldGridCoordinate coordinate = GetCoordinate(currentIndex, width);
                    CarveCorridorCell(grid, coordinate, corridorWidth);
                    startRegion[currentIndex] = true;
                    currentIndex = parent[currentIndex];
                }
            }

            ArrayClear(componentVisited);
            FloodWalkableRegion(grid, startIndex, componentVisited, queue);
            for (int caveIndex = 0; caveIndex < majorCaveCount; caveIndex++)
            {
                if (!componentVisited[majorRepresentatives[caveIndex]])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>두 셀이 벽 없이 상하좌우로 이어지는지 초기 연결성 검사에 사용합니다.</summary>
        public static bool AreConnected(
            WorldGrid grid,
            WorldGridCoordinate start,
            WorldGridCoordinate target)
        {
            if (grid == null || !grid.IsInBounds(start) || !grid.IsInBounds(target) ||
                !grid.IsWalkable(start) || !grid.IsWalkable(target))
            {
                return false;
            }

            int cellCount = grid.CellCount;
            var visited = new bool[cellCount];
            var queue = new System.Collections.Generic.Queue<int>(cellCount);
            int targetIndex = GetIndex(target, grid.Width);
            FloodWalkableRegion(grid, GetIndex(start, grid.Width), visited, queue);
            return visited[targetIndex];
        }

        private static int FloodWalkableRegion(
            WorldGrid grid,
            int startIndex,
            bool[] visited,
            System.Collections.Generic.Queue<int> queue)
        {
            if (visited[startIndex] || !IsWalkableIndex(grid, startIndex))
            {
                return 0;
            }

            int width = grid.Width;
            queue.Clear();
            queue.Enqueue(startIndex);
            visited[startIndex] = true;
            int regionSize = 0;

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                regionSize++;
                int x = index % width;
                int y = index / width;

                VisitWalkableNeighbor(grid, x, y + 1, visited, queue);
                VisitWalkableNeighbor(grid, x, y - 1, visited, queue);
                VisitWalkableNeighbor(grid, x - 1, y, visited, queue);
                VisitWalkableNeighbor(grid, x + 1, y, visited, queue);
            }

            return regionSize;
        }

        private static void VisitWalkableNeighbor(
            WorldGrid grid,
            int x,
            int y,
            bool[] visited,
            System.Collections.Generic.Queue<int> queue)
        {
            if (x < 0 || x >= grid.Width || y < 0 || y >= grid.Height)
            {
                return;
            }

            int index = y * grid.Width + x;
            if (visited[index] || !grid.IsWalkable(new WorldGridCoordinate(x, y)))
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }

        private static void BuildFloorPathTree(
            WorldGrid grid,
            int startIndex,
            bool[] visited,
            int[] parent,
            System.Collections.Generic.Queue<int> queue)
        {
            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = -1;
            }

            queue.Clear();
            queue.Enqueue(startIndex);
            visited[startIndex] = true;
            int width = grid.Width;

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;
                VisitFloorNeighbor(grid, x - 1, y, index, visited, parent, queue);
                VisitFloorNeighbor(grid, x + 1, y, index, visited, parent, queue);
                VisitFloorNeighbor(grid, x, y - 1, index, visited, parent, queue);
                VisitFloorNeighbor(grid, x, y + 1, index, visited, parent, queue);
            }
        }

        private static void VisitFloorNeighbor(
            WorldGrid grid,
            int x,
            int y,
            int parentIndex,
            bool[] visited,
            int[] parent,
            System.Collections.Generic.Queue<int> queue)
        {
            if (x < 0 || x >= grid.Width || y < 0 || y >= grid.Height)
            {
                return;
            }

            int index = y * grid.Width + x;
            if (visited[index] || !HasFloorIndex(grid, index))
            {
                return;
            }

            visited[index] = true;
            parent[index] = parentIndex;
            queue.Enqueue(index);
        }

        private static void CarveCorridorCell(WorldGrid grid, WorldGridCoordinate center, int corridorWidth)
        {
            int minimumOffset = -(corridorWidth / 2);
            int maximumOffset = minimumOffset + corridorWidth - 1;
            for (int offsetY = minimumOffset; offsetY <= maximumOffset; offsetY++)
            {
                for (int offsetX = minimumOffset; offsetX <= maximumOffset; offsetX++)
                {
                    var coordinate = new WorldGridCoordinate(center.X + offsetX, center.Y + offsetY);
                    if (grid.TryGetWall(coordinate, out WorldWallData wall))
                    {
                        grid.TryDamageWall(coordinate, wall.CurrentDurability, out _, out _);
                    }
                }
            }
        }

        private static bool IsWalkableIndex(WorldGrid grid, int index)
        {
            return grid.IsWalkable(GetCoordinate(index, grid.Width));
        }

        private static bool HasFloorIndex(WorldGrid grid, int index)
        {
            return grid.TryGetFloor(GetCoordinate(index, grid.Width), out _);
        }

        private static int GetIndex(WorldGridCoordinate coordinate, int width)
        {
            return coordinate.Y * width + coordinate.X;
        }

        private static int FindMajorChunkComponents(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            long minimumX,
            long minimumY,
            long maximumXExclusive,
            long maximumYExclusive,
            int minimumMajorCaveSize,
            bool[] visited,
            int[] representatives,
            int[] queue)
        {
            int representativeCount = 0;
            for (int localY = 0; localY < chunkSize; localY++)
            {
                for (int localX = 0; localX < chunkSize; localX++)
                {
                    long worldX = minimumX + localX;
                    long worldY = minimumY + localY;
                    int localIndex = localY * chunkSize + localX;
                    if (visited[localIndex] ||
                        worldX >= maximumXExclusive || worldY >= maximumYExclusive ||
                        !TryMapToGrid(grid, gridWorldOrigin, worldX, worldY, out WorldGridCoordinate gridCell) ||
                        !grid.IsWalkable(gridCell))
                    {
                        continue;
                    }

                    int componentSize = FloodChunkWalkableRegion(
                        grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                        localIndex, visited, queue);
                    if (componentSize >= minimumMajorCaveSize)
                    {
                        representatives[representativeCount++] = localIndex;
                    }
                }
            }

            return representativeCount;
        }

        private static int FloodChunkWalkableRegion(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            long minimumX,
            long minimumY,
            long maximumXExclusive,
            long maximumYExclusive,
            int startIndex,
            bool[] visited,
            int[] queue)
        {
            if (visited[startIndex])
            {
                return 0;
            }

            int startX = startIndex % chunkSize;
            int startY = startIndex / chunkSize;
            if (!IsChunkCellWalkable(
                    grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                    startX, startY))
            {
                return 0;
            }

            int head = 0;
            int tail = 0;
            queue[tail++] = startIndex;
            visited[startIndex] = true;
            while (head < tail)
            {
                int index = queue[head++];
                int localX = index % chunkSize;
                int localY = index / chunkSize;
                EnqueueChunkWalkableNeighbor(grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive, localX - 1, localY, visited, queue, ref tail);
                EnqueueChunkWalkableNeighbor(grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive, localX + 1, localY, visited, queue, ref tail);
                EnqueueChunkWalkableNeighbor(grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive, localX, localY - 1, visited, queue, ref tail);
                EnqueueChunkWalkableNeighbor(grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive, localX, localY + 1, visited, queue, ref tail);
            }

            return tail;
        }

        private static void EnqueueChunkWalkableNeighbor(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            long minimumX,
            long minimumY,
            long maximumXExclusive,
            long maximumYExclusive,
            int localX,
            int localY,
            bool[] visited,
            int[] queue,
            ref int tail)
        {
            if (localX < 0 || localX >= chunkSize || localY < 0 || localY >= chunkSize)
            {
                return;
            }

            int index = localY * chunkSize + localX;
            if (visited[index] || !IsChunkCellWalkable(
                    grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                    localX, localY))
            {
                return;
            }

            visited[index] = true;
            queue[tail++] = index;
        }

        private static bool IsChunkCellWalkable(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            long minimumX,
            long minimumY,
            long maximumXExclusive,
            long maximumYExclusive,
            int localX,
            int localY)
        {
            long worldX = minimumX + localX;
            long worldY = minimumY + localY;
            return worldX >= minimumX && worldX < maximumXExclusive &&
                   worldY >= minimumY && worldY < maximumYExclusive &&
                   TryMapToGrid(grid, gridWorldOrigin, worldX, worldY, out WorldGridCoordinate gridCell) &&
                   grid.IsWalkable(gridCell);
        }

        private static bool ConnectChunkCellToRegion(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            long minimumX,
            long minimumY,
            long maximumXExclusive,
            long maximumYExclusive,
            int targetIndex,
            int corridorWidth,
            bool[] connected,
            int[] visitStamp,
            int[] parent,
            ref int searchGeneration,
            int[] queue)
        {
            if (connected[targetIndex])
            {
                return true;
            }

            searchGeneration++;
            if (searchGeneration == int.MaxValue)
            {
                ArrayClear(visitStamp);
                searchGeneration = 1;
            }

            int head = 0;
            int tail = 0;
            queue[tail++] = targetIndex;
            visitStamp[targetIndex] = searchGeneration;
            parent[targetIndex] = -1;
            int meetingIndex = -1;

            while (head < tail)
            {
                int index = queue[head++];
                if (connected[index])
                {
                    meetingIndex = index;
                    break;
                }

                int localX = index % chunkSize;
                int localY = index / chunkSize;
                if (!EnqueueChunkFloorNeighbor(grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive, localX - 1, localY, index, visitStamp, parent, searchGeneration, queue, ref tail) ||
                    !EnqueueChunkFloorNeighbor(grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive, localX + 1, localY, index, visitStamp, parent, searchGeneration, queue, ref tail) ||
                    !EnqueueChunkFloorNeighbor(grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive, localX, localY - 1, index, visitStamp, parent, searchGeneration, queue, ref tail) ||
                    !EnqueueChunkFloorNeighbor(grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive, localX, localY + 1, index, visitStamp, parent, searchGeneration, queue, ref tail))
                {
                    return false;
                }
            }

            if (meetingIndex < 0)
            {
                return false;
            }

            int currentIndex = meetingIndex;
            while (currentIndex >= 0)
            {
                CarveChunkCorridorCell(
                    grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                    currentIndex, corridorWidth);
                if (currentIndex == targetIndex)
                {
                    break;
                }

                currentIndex = parent[currentIndex];
            }

            FloodChunkWalkableRegion(
                grid, gridWorldOrigin, chunk, chunkSize, minimumX, minimumY, maximumXExclusive, maximumYExclusive,
                targetIndex, connected, queue);
            return connected[targetIndex];
        }

        private static bool EnqueueChunkFloorNeighbor(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            long minimumX,
            long minimumY,
            long maximumXExclusive,
            long maximumYExclusive,
            int localX,
            int localY,
            int parentIndex,
            int[] visitStamp,
            int[] parent,
            int searchGeneration,
            int[] queue,
            ref int tail)
        {
            if (localX < 0 || localX >= chunkSize || localY < 0 || localY >= chunkSize)
            {
                return true;
            }

            int index = localY * chunkSize + localX;
            if (visitStamp[index] == searchGeneration)
            {
                return true;
            }

            long worldX = minimumX + localX;
            long worldY = minimumY + localY;
            if (worldX >= maximumXExclusive || worldY >= maximumYExclusive ||
                !TryMapToGrid(grid, gridWorldOrigin, worldX, worldY, out WorldGridCoordinate gridCell) ||
                !grid.TryGetFloor(gridCell, out _))
            {
                return true;
            }

            visitStamp[index] = searchGeneration;
            parent[index] = parentIndex;
            queue[tail++] = index;
            return true;
        }

        private static void CarveChunkCorridorCell(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            long minimumX,
            long minimumY,
            long maximumXExclusive,
            long maximumYExclusive,
            int centerIndex,
            int corridorWidth)
        {
            int centerX = centerIndex % chunkSize;
            int centerY = centerIndex / chunkSize;
            int minimumOffset = -(corridorWidth / 2);
            int maximumOffset = minimumOffset + corridorWidth - 1;
            for (int offsetY = minimumOffset; offsetY <= maximumOffset; offsetY++)
            {
                int localY = centerY + offsetY;
                long worldY = minimumY + localY;
                if (localY < 0 || localY >= chunkSize || worldY >= maximumYExclusive)
                {
                    continue;
                }

                for (int offsetX = minimumOffset; offsetX <= maximumOffset; offsetX++)
                {
                    int localX = centerX + offsetX;
                    long worldX = minimumX + localX;
                    if (localX < 0 || localX >= chunkSize || worldX >= maximumXExclusive ||
                        !TryMapToGrid(
                            grid,
                            gridWorldOrigin,
                            worldX,
                            worldY,
                            out WorldGridCoordinate gridCell) ||
                        !grid.TryGetWall(gridCell, out WorldWallData wall))
                    {
                        continue;
                    }

                    grid.TryDamageWall(gridCell, wall.CurrentDurability, out _, out _);
                }
            }
        }

        private static void CarveBoundaryOpening(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            WorldChunkSide side,
            WorldCellCoordinate start,
            int corridorWidth)
        {
            for (int offset = 0; offset < corridorWidth; offset++)
            {
                long x = side == WorldChunkSide.West || side == WorldChunkSide.East ? start.X : (long)start.X + offset;
                long y = side == WorldChunkSide.South || side == WorldChunkSide.North ? start.Y : (long)start.Y + offset;
                if (TryMapToGrid(grid, gridWorldOrigin, x, y, out WorldGridCoordinate gridCell) &&
                    grid.TryGetWall(gridCell, out WorldWallData wall))
                {
                    grid.TryDamageWall(gridCell, wall.CurrentDurability, out _, out _);
                }
            }
        }

        private static bool IsActiveEntranceWithinWorldBounds(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldCellBounds worldBounds,
            WorldChunkSide side,
            WorldCellCoordinate entranceStart,
            int corridorWidth)
        {
            for (int offset = 0; offset < corridorWidth; offset++)
            {
                long x = side == WorldChunkSide.West ? (long)entranceStart.X - 1 :
                    side == WorldChunkSide.East ? (long)entranceStart.X + 1 : (long)entranceStart.X + offset;
                long y = side == WorldChunkSide.South ? (long)entranceStart.Y - 1 :
                    side == WorldChunkSide.North ? (long)entranceStart.Y + 1 : (long)entranceStart.Y + offset;
                long currentX = side == WorldChunkSide.West || side == WorldChunkSide.East ? entranceStart.X : (long)entranceStart.X + offset;
                long currentY = side == WorldChunkSide.South || side == WorldChunkSide.North ? entranceStart.Y : (long)entranceStart.Y + offset;
                if (!worldBounds.Contains(currentX, currentY) || !worldBounds.Contains(x, y) ||
                    !TryMapToGrid(grid, gridWorldOrigin, currentX, currentY, out WorldGridCoordinate currentCell) ||
                    !grid.TryGetFloor(currentCell, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetChunkLocalIndex(
            WorldCellCoordinate worldCell,
            WorldChunkCoordinate chunk,
            int chunkSize,
            out int localIndex)
        {
            localIndex = 0;
            if (chunkSize <= 0)
            {
                return false;
            }

            long localX = (long)worldCell.X - (long)chunk.X * chunkSize;
            long localY = (long)worldCell.Y - (long)chunk.Y * chunkSize;
            if (localX < 0 || localX >= chunkSize || localY < 0 || localY >= chunkSize)
            {
                return false;
            }

            long index = localY * chunkSize + localX;
            if (index > int.MaxValue)
            {
                return false;
            }

            localIndex = (int)index;
            return true;
        }

        private static bool TryGetChunkBounds(
            WorldChunkCoordinate chunk,
            int chunkSize,
            out long minimumX,
            out long minimumY,
            out long maximumXExclusive,
            out long maximumYExclusive)
        {
            minimumX = 0L;
            minimumY = 0L;
            maximumXExclusive = 0L;
            maximumYExclusive = 0L;
            if (chunkSize <= 0)
            {
                return false;
            }

            minimumX = (long)chunk.X * chunkSize;
            minimumY = (long)chunk.Y * chunkSize;
            maximumXExclusive = minimumX + chunkSize;
            maximumYExclusive = minimumY + chunkSize;
            if (maximumXExclusive <= int.MinValue || maximumYExclusive <= int.MinValue ||
                minimumX > int.MaxValue || minimumY > int.MaxValue)
            {
                return false;
            }

            return true;
        }

        private static bool TryGetChunkGridIntersection(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldChunkCoordinate chunk,
            int chunkSize,
            out long minimumX,
            out long minimumY,
            out long maximumXExclusive,
            out long maximumYExclusive)
        {
            minimumX = 0L;
            minimumY = 0L;
            maximumXExclusive = 0L;
            maximumYExclusive = 0L;
            if (grid == null || !TryGetChunkBounds(
                    chunk, chunkSize,
                    out long chunkMinimumX, out long chunkMinimumY,
                    out long chunkMaximumX, out long chunkMaximumY))
            {
                return false;
            }

            long gridMaximumX = (long)gridWorldOrigin.X + grid.Width;
            long gridMaximumY = (long)gridWorldOrigin.Y + grid.Height;
            minimumX = Math.Max(Math.Max(chunkMinimumX, gridWorldOrigin.X), int.MinValue);
            minimumY = Math.Max(Math.Max(chunkMinimumY, gridWorldOrigin.Y), int.MinValue);
            maximumXExclusive = Math.Min(Math.Min(chunkMaximumX, gridMaximumX), (long)int.MaxValue + 1L);
            maximumYExclusive = Math.Min(Math.Min(chunkMaximumY, gridMaximumY), (long)int.MaxValue + 1L);
            return minimumX < maximumXExclusive && minimumY < maximumYExclusive;
        }

        private static bool IsValidChunkRequest(WorldGrid grid, int chunkSize)
        {
            return grid != null && chunkSize > 0;
        }

        private static bool TryMapToGrid(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            WorldCellCoordinate worldCell,
            out WorldGridCoordinate gridCoordinate)
        {
            return TryMapToGrid(grid, gridWorldOrigin, worldCell.X, worldCell.Y, out gridCoordinate);
        }

        private static bool TryMapToGrid(
            WorldGrid grid,
            WorldCellCoordinate gridWorldOrigin,
            long worldX,
            long worldY,
            out WorldGridCoordinate gridCoordinate)
        {
            long x = worldX - gridWorldOrigin.X;
            long y = worldY - gridWorldOrigin.Y;
            if (worldX < int.MinValue || worldX > int.MaxValue ||
                worldY < int.MinValue || worldY > int.MaxValue ||
                x < 0 || x >= grid.Width || y < 0 || y >= grid.Height)
            {
                gridCoordinate = default;
                return false;
            }

            gridCoordinate = new WorldGridCoordinate((int)x, (int)y);
            return true;
        }

        private static WorldGridCoordinate GetCoordinate(int index, int width)
        {
            return new WorldGridCoordinate(index % width, index / width);
        }

        private static void ArrayClear(bool[] values)
        {
            System.Array.Clear(values, 0, values.Length);
        }

        private static void ArrayClear(int[] values)
        {
            System.Array.Clear(values, 0, values.Length);
        }

        private static bool IsProtected(
            WorldGridCoordinate coordinate,
            WorldGridCoordinate center,
            double radiusSquared)
        {
            long deltaX = (long)coordinate.X - center.X;
            long deltaY = (long)coordinate.Y - center.Y;
            return (double)deltaX * deltaX + (double)deltaY * deltaY <= radiusSquared;
        }

        private static bool IsProtected(
            WorldCellCoordinate coordinate,
            WorldCellCoordinate center,
            double radiusSquared)
        {
            long deltaX = (long)coordinate.X - center.X;
            long deltaY = (long)coordinate.Y - center.Y;
            return (double)deltaX * deltaX + (double)deltaY * deltaY <= radiusSquared;
        }

        private static bool IsProtectedByAdditionalCenter(
            WorldGrid grid,
            WorldGridCoordinate coordinate,
            WorldGridCoordinate[] centers,
            double radiusSquared)
        {
            if (centers == null)
            {
                return false;
            }

            for (int i = 0; i < centers.Length; i++)
            {
                if (grid.IsInBounds(centers[i]) && IsProtected(coordinate, centers[i], radiusSquared))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsProtectedByAdditionalCenter(
            WorldCellCoordinate coordinate,
            WorldCellCoordinate[] centers,
            double radiusSquared)
        {
            if (centers == null)
            {
                return false;
            }

            for (int i = 0; i < centers.Length; i++)
            {
                if (IsProtected(coordinate, centers[i], radiusSquared))
                {
                    return true;
                }
            }

            return false;
        }

        private static float SampleValueNoise(int x, int y, int seed, int channel, float frequency)
        {
            float sampleX = x * frequency;
            float sampleY = y * frequency;
            int cellX = FloorToInt(sampleX);
            int cellY = FloorToInt(sampleY);
            float blendX = SmoothStep(sampleX - cellX);
            float blendY = SmoothStep(sampleY - cellY);

            float bottom = Lerp(
                HashToValue(Hash(seed, cellX, cellY, channel)),
                HashToValue(Hash(seed, cellX + 1, cellY, channel)),
                blendX);
            float top = Lerp(
                HashToValue(Hash(seed, cellX, cellY + 1, channel)),
                HashToValue(Hash(seed, cellX + 1, cellY + 1, channel)),
                blendX);
            return Lerp(bottom, top, blendY);
        }

        private static int Hash(int seed, int x, int y, int channel)
        {
            unchecked
            {
                uint value = (uint)seed ^ (uint)channel;
                value ^= (uint)x * 0x8DA6B343u;
                value ^= (uint)y * 0xD8163841u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (int)value;
            }
        }

        private static float HashToValue(int hash)
        {
            return (hash & 0x00FFFFFF) / 16777215f;
        }

        private static int FloorToInt(float value)
        {
            int integer = (int)value;
            return value < integer ? integer - 1 : integer;
        }

        private static float SmoothStep(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static float Lerp(float from, float to, float amount)
        {
            return from + (to - from) * amount;
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsValidCellCoordinate(long x, long y)
        {
            return x >= int.MinValue && x <= int.MaxValue &&
                   y >= int.MinValue && y <= int.MaxValue;
        }
    }
}
