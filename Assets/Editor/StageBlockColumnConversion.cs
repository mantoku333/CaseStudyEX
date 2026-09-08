using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace EditorTools
{
    /// <summary>Converts eligible grass cells to column faces without retiling other scene content.</summary>
    public static class StageBlockColumnConversion
    {
        public const string FixCapcomScenePath = "Assets/Scenes/FixScenes/Fix_CAPCOM.unity";
        private const string PalettePath = "Assets/Editor/StageEditorPalette.asset";

        public readonly struct Result
        {
            public int ConvertedColumns { get; }
            public int ConvertedTiles { get; }

            public Result(int convertedColumns, int convertedTiles)
            {
                ConvertedColumns = convertedColumns;
                ConvertedTiles = convertedTiles;
            }
        }

        /// <summary>Changes only eligible cells, preserving each cell's color, transform, and flags.</summary>
        public static Result ConvertTilemap(Tilemap tilemap, StageBlockTileSet stage2, bool recordUndo = true)
        {
            if (tilemap == null || stage2 == null || !stage2.HasAllTiles() || !stage2.HasColumnTiles())
            {
                return default;
            }

            List<BoundsInt> columns = StageBlockColumnUtility.CollectColumns(
                tilemap.cellBounds, cell => stage2.Contains(tilemap.GetTile(cell)));
            List<(TileChangeData change, TileFlags flags)> changes = new List<(TileChangeData, TileFlags)>();
            int convertedColumns = 0;

            foreach (BoundsInt column in columns)
            {
                int changesBeforeColumn = changes.Count;
                foreach (Vector3Int cell in column.allPositionsWithin)
                {
                    if (!StageBlockAutoTileResolver.TryResolveColumn(
                        StageBlockColumnUtility.GetNeighborState(cell, tilemap.HasTile), out StageBlockFace face))
                    {
                        continue;
                    }

                    TileBase tile = stage2.GetTile(face);
                    if (tilemap.GetTile(cell) == tile)
                    {
                        continue;
                    }

                    changes.Add((new TileChangeData
                    {
                        position = cell,
                        tile = tile,
                        color = tilemap.GetColor(cell),
                        transform = tilemap.GetTransformMatrix(cell)
                    }, tilemap.GetTileFlags(cell)));
                }

                if (changes.Count > changesBeforeColumn)
                {
                    convertedColumns++;
                }
            }

            if (changes.Count > 0 && recordUndo)
            {
                Undo.RegisterCompleteObjectUndo(tilemap, "Convert Grass Column Tiles");
            }

            foreach (var entry in changes)
            {
                tilemap.SetTile(entry.change, true);
                tilemap.SetTileFlags(entry.change.position, entry.flags);
            }

            return new Result(convertedColumns, changes.Count);
        }

        [MenuItem("Tools/Level/Grass Columns/Convert Fix_CAPCOM")]
        public static void ApplyFixCapcom()
        {
            StageEditorPalette palette = AssetDatabase.LoadAssetAtPath<StageEditorPalette>(PalettePath);
            StageBlockTileSet stage2 = palette != null ? palette.Stage2Blocks : null;
            if (stage2 == null || !stage2.HasAllTiles() || !stage2.HasColumnTiles())
            {
                throw new InvalidOperationException("Assign the Stage2 A-I and J/K/M tiles before converting Fix_CAPCOM.");
            }

            Scene scene = SceneManager.GetSceneByPath(FixCapcomScenePath);
            bool openedScene = !scene.IsValid() || !scene.isLoaded;
            if (openedScene)
            {
                scene = EditorSceneManager.OpenScene(FixCapcomScenePath, OpenSceneMode.Additive);
            }

            try
            {
                int convertedColumns = 0;
                int convertedTiles = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Tilemap tilemap in root.GetComponentsInChildren<Tilemap>(true))
                    {
                        Result result = ConvertTilemap(tilemap, stage2);
                        convertedColumns += result.ConvertedColumns;
                        convertedTiles += result.ConvertedTiles;
                    }
                }

                if (convertedTiles > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        throw new InvalidOperationException("Could not save the converted Fix_CAPCOM scene.");
                    }
                }

                Debug.Log($"[Grass Columns] Fix_CAPCOM: converted {convertedColumns} columns / {convertedTiles} tiles. " +
                          (convertedTiles > 0 ? "Scene saved." : "Scene was not rewritten."));
            }
            finally
            {
                // Keep a scene with unsaved changes open if saving failed.
                if (openedScene && !scene.isDirty)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
