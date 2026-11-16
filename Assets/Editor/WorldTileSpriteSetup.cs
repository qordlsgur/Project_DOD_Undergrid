using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Prohect_DOD_Undergird.EditorTools
{
    /// <summary>
    /// 내장 Background Sprite를 측정하고 1×1 임시 타일 Sprite를 만들어 기존 Tile 에셋에 연결합니다.
    /// </summary>
    public static class WorldTileSpriteSetup
    {
        private const string SpritePath = "Assets/Art/WorldTiles/TemporaryWorldTileSprite.png";
        private const int SpritePixelSize = 8;
        private const float SpritePixelsPerUnit = 8f;

        private static readonly TileSetup[] TileSetups =
        {
            new TileSetup("Assets/Art/WorldTiles/GroundFloorTile.asset", Tile.ColliderType.None),
            new TileSetup("Assets/Art/WorldTiles/SoilFloorTile.asset", Tile.ColliderType.None),
            new TileSetup("Assets/Art/WorldTiles/StoneFloorTile.asset", Tile.ColliderType.None),
            new TileSetup("Assets/Art/WorldTiles/SoilWallTile.asset", Tile.ColliderType.Grid),
            new TileSetup("Assets/Art/WorldTiles/StoneWallTile.asset", Tile.ColliderType.Grid)
        };

        /// <summary>
        /// 임시 Sprite 에셋을 생성 또는 검증하고 Tile의 Sprite 참조만 교체합니다.
        /// </summary>
        [MenuItem("Tools/World/Setup Temporary Tile Sprites")]
        private static void SetupTemporaryTileSprites()
        {
            Tile[] tiles = LoadAndValidateTiles();
            if (tiles == null)
            {
                return;
            }

            LogCurrentSpriteSettings(tiles);

            Sprite replacementSprite = LoadOrCreateReplacementSprite();
            if (replacementSprite == null)
            {
                return;
            }

            for (int i = 0; i < tiles.Length; i++)
            {
                // Sprite만 교체하므로 기존 색상 구분과 Collider Type은 그대로 보존됩니다.
                tiles[i].sprite = replacementSprite;
                EditorUtility.SetDirty(tiles[i]);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"임시 타일 Sprite 연결을 완료했습니다. 크기: {replacementSprite.rect.width}×{replacementSprite.rect.height}px, " +
                $"PPU: {replacementSprite.pixelsPerUnit}, 월드 크기: {replacementSprite.bounds.size.x}×{replacementSprite.bounds.size.y}.");
        }

        private static Tile[] LoadAndValidateTiles()
        {
            Tile[] tiles = new Tile[TileSetups.Length];
            for (int i = 0; i < TileSetups.Length; i++)
            {
                TileSetup setup = TileSetups[i];
                tiles[i] = AssetDatabase.LoadAssetAtPath<Tile>(setup.AssetPath);
                if (tiles[i] == null)
                {
                    Debug.LogError($"Tile 에셋을 불러오지 못했습니다: {setup.AssetPath}");
                    return null;
                }

                if (tiles[i].colliderType != setup.ExpectedColliderType)
                {
                    Debug.LogError(
                        $"Tile Collider Type이 예상 설정과 다릅니다. 에셋: {setup.AssetPath}, " +
                        $"현재: {tiles[i].colliderType}, 예상: {setup.ExpectedColliderType}");
                    return null;
                }
            }

            return tiles;
        }

        private static void LogCurrentSpriteSettings(Tile[] tiles)
        {
            for (int i = 0; i < tiles.Length; i++)
            {
                Sprite currentSprite = tiles[i].sprite;
                if (currentSprite == null)
                {
                    Debug.LogWarning($"현재 Sprite가 비어 있습니다: {TileSetups[i].AssetPath}");
                    continue;
                }

                Texture2D texture = currentSprite.texture;
                Debug.Log(
                    $"현재 Tile Sprite 설정: {TileSetups[i].AssetPath}; " +
                    $"이름={currentSprite.name}; 텍스처={texture.width}×{texture.height}px; " +
                    $"Sprite 영역={currentSprite.rect.width}×{currentSprite.rect.height}px; " +
                    $"PPU={currentSprite.pixelsPerUnit}; " +
                    $"월드 크기={currentSprite.bounds.size.x}×{currentSprite.bounds.size.y}");
            }
        }

        private static Sprite LoadOrCreateReplacementSprite()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null && File.Exists(SpritePath))
            {
                Debug.LogError($"경로에 파일이 있지만 Sprite로 불러오지 못했습니다. 기존 파일을 덮어쓰지 않습니다: {SpritePath}");
                return null;
            }

            if (sprite == null)
            {
                CreateOpaqueWhiteTexture();
                AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureSpriteImporter();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            }

            if (!HasExpectedDimensions(sprite))
            {
                Debug.LogError(
                    $"임시 Sprite가 1×1 월드 셀 설정과 일치하지 않습니다. " +
                    $"필요: {SpritePixelSize}×{SpritePixelSize}px, PPU {SpritePixelsPerUnit}. " +
                    $"기존 Sprite 에셋은 변경하지 않았습니다.");
                return null;
            }

            return sprite;
        }

        private static void CreateOpaqueWhiteTexture()
        {
            Texture2D texture = new Texture2D(SpritePixelSize, SpritePixelSize, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[SpritePixelSize * SpritePixelSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureSpriteImporter()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"TextureImporter를 찾지 못했습니다: {SpritePath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SpritePixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 32;
            importer.SaveAndReimport();
        }

        private static bool HasExpectedDimensions(Sprite sprite)
        {
            if (sprite == null)
            {
                return false;
            }

            return Mathf.Approximately(sprite.rect.width, SpritePixelSize) &&
                   Mathf.Approximately(sprite.rect.height, SpritePixelSize) &&
                   Mathf.Approximately(sprite.pixelsPerUnit, SpritePixelsPerUnit) &&
                   Mathf.Approximately(sprite.bounds.size.x, 1f) &&
                   Mathf.Approximately(sprite.bounds.size.y, 1f);
        }

        private sealed class TileSetup
        {
            public string AssetPath { get; }
            public Tile.ColliderType ExpectedColliderType { get; }

            public TileSetup(string assetPath, Tile.ColliderType expectedColliderType)
            {
                AssetPath = assetPath;
                ExpectedColliderType = expectedColliderType;
            }
        }
    }
}
