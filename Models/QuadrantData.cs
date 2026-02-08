// Models/QuadrantData.cs
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace GoodFences.Models
{
    /// <summary>Identifies which quadrant of the Four Corners farm.</summary>
    public enum Quadrant
    {
        /// <summary>Northeast - Shared farmhouse quadrant.</summary>
        NE,
        /// <summary>Northwest - Private (forest-style with hardwood stump).</summary>
        NW,
        /// <summary>Southwest - Private (large fishing pond).</summary>
        SW,
        /// <summary>Southeast - Private (quarry ore nodes).</summary>
        SE
    }

    /// <summary>Represents a passage point between quadrants that needs enforcement.</summary>
    public class BoundaryPassage
    {
        /// <summary>The quadrant this passage leads into.</summary>
        public Quadrant TargetQuadrant { get; set; }

        /// <summary>The tiles that form this passage.</summary>
        public List<Vector2> Tiles { get; set; } = new();

        /// <summary>Description of this passage for debugging.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Whether this passage is blocked by an obstacle at game start.</summary>
        public bool InitiallyBlocked { get; set; } = false;
    }

    /// <summary>Cabin placement data for each quadrant.</summary>
    public class CabinData
    {
        /// <summary>The quadrant this cabin is in.</summary>
        public Quadrant Quadrant { get; set; }

        /// <summary>Cabin type name (Log, Stone, Plank).</summary>
        public string CabinType { get; set; } = string.Empty;

        /// <summary>Upper-left tile position.</summary>
        public Vector2 Position { get; set; }

        /// <summary>Cabin size in tiles (width x height).</summary>
        public Vector2 Size { get; set; } = new(5, 3);
    }

    /// <summary>Static coordinate data for the Four Corners farm boundaries.</summary>
    public static class QuadrantCoordinates
    {
        /*********
        ** Cabin Coordinates (deterministic with "Separate" placement)
        *********/
        public static readonly CabinData LogCabin = new()
        {
            Quadrant = Quadrant.SW,
            CabinType = "Log",
            Position = new Vector2(17, 43),
            Size = new Vector2(5, 3)
        };

        public static readonly CabinData StoneCabin = new()
        {
            Quadrant = Quadrant.SE,
            CabinType = "Stone",
            Position = new Vector2(59, 43),
            Size = new Vector2(5, 3)
        };

        public static readonly CabinData PlankCabin = new()
        {
            Quadrant = Quadrant.NW,
            CabinType = "Plank",
            Position = new Vector2(17, 8),
            Size = new Vector2(5, 3)
        };

        /*********
        ** Shipping Bin Coordinates
        ** NE has a common shipping bin for selling common goods (split proceeds)
        *********/
        public static readonly Dictionary<Quadrant, Vector2> ShippingBins = new()
        {
            { Quadrant.NE, new Vector2(71, 14) }, // Common shipping bin near farmhouse
            { Quadrant.NW, new Vector2(22, 10) },
            { Quadrant.SW, new Vector2(22, 45) },
            { Quadrant.SE, new Vector2(64, 45) }
        };

        /*********
        ** Common Chest (Distribution Chest) Coordinates
        ** NE always gets a common chest for shared production
        ** Note: Must be OUTSIDE cabin footprints (cabins are 5x3 tiles)
        *********/
        public static readonly Dictionary<Quadrant, Vector2> CommonChests = new()
        {
            { Quadrant.NE, new Vector2(69, 14) }, // Common chest near farmhouse
            { Quadrant.NW, new Vector2(17, 11) }, // Below NW cabin (cabin ends at y10)
            { Quadrant.SW, new Vector2(17, 46) }, // Below SW cabin (cabin ends at y45) - was y44 inside cabin!
            { Quadrant.SE, new Vector2(59, 46) }  // Below SE cabin (cabin ends at y45)
        };

        /*********
        ** Boundary Passage Coordinates
        ** These are the tiles where players can cross between quadrants
        ** Enforcement blocks entry to passages leading to other players' quadrants
        *********/
        public static readonly List<BoundaryPassage> SwPassages = new()
        {
            new BoundaryPassage
            {
                TargetQuadrant = Quadrant.SW,
                Description = "SW Main Passage",
                Tiles = GenerateTileRange(29, 33, 43, 43) // x29-x33, y43 (5 tiles wide)
            },
            new BoundaryPassage
            {
                TargetQuadrant = Quadrant.SW,
                Description = "SW South Passage",
                Tiles = new List<Vector2> { new Vector2(38, 57) } // 1 tile
            },
            new BoundaryPassage
            {
                TargetQuadrant = Quadrant.SW,
                Description = "SW West Passage",
                Tiles = GenerateTileRange(11, 12, 42, 42), // x11-x12, y42 (2 tiles)
                InitiallyBlocked = true
            }
        };

        public static readonly List<BoundaryPassage> SePassages = new()
        {
            new BoundaryPassage
            {
                TargetQuadrant = Quadrant.SE,
                Description = "SE Main Passage",
                Tiles = GenerateTileRange(47, 50, 43, 43) // x47-x50, y43 (4 tiles wide)
            },
            new BoundaryPassage
            {
                TargetQuadrant = Quadrant.SE,
                Description = "SE South Passage",
                Tiles = new List<Vector2> { new Vector2(42, 57) } // 1 tile
            },
            new BoundaryPassage
            {
                TargetQuadrant = Quadrant.SE,
                Description = "SE East Passage",
                Tiles = GenerateTileRange(70, 71, 42, 42), // x70-x71, y42 (2 tiles)
                InitiallyBlocked = true
            }
        };

        public static readonly List<BoundaryPassage> NwPassages = new()
        {
            new BoundaryPassage
            {
                TargetQuadrant = Quadrant.NW,
                Description = "NW Main Passage",
                Tiles = GenerateTileRange(32, 36, 29, 30) // x32-x36, y29-y30 (5x2 tiles)
            },
            new BoundaryPassage
            {
                TargetQuadrant = Quadrant.NW,
                Description = "NW North Passage",
                Tiles = GenerateTileRange(39, 39, 14, 15) // x39, y14-y15 (1x2 tiles)
            },
            new BoundaryPassage
            {
                TargetQuadrant = Quadrant.NW,
                Description = "NW West Passage",
                Tiles = GenerateTileRange(11, 12, 35, 35), // x11-x12, y35 (2 tiles)
                InitiallyBlocked = true
            }
        };

        /// <summary>Get all passages for a specific quadrant.</summary>
        public static List<BoundaryPassage> GetPassagesForQuadrant(Quadrant quadrant)
        {
            return quadrant switch
            {
                Quadrant.SW => SwPassages,
                Quadrant.SE => SePassages,
                Quadrant.NW => NwPassages,
                _ => new List<BoundaryPassage>()
            };
        }

        /// <summary>Get all boundary passages.</summary>
        public static List<BoundaryPassage> GetAllPassages()
        {
            var all = new List<BoundaryPassage>();
            all.AddRange(SwPassages);
            all.AddRange(SePassages);
            all.AddRange(NwPassages);
            return all;
        }

        /*********
        ** Helper Methods
        *********/
        /// <summary>Generate a list of tiles within a rectangular range.</summary>
        private static List<Vector2> GenerateTileRange(int x1, int x2, int y1, int y2)
        {
            var tiles = new List<Vector2>();
            for (int x = x1; x <= x2; x++)
            {
                for (int y = y1; y <= y2; y++)
                {
                    tiles.Add(new Vector2(x, y));
                }
            }
            return tiles;
        }
    }
}
