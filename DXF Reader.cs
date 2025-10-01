// Last Updated: Mikolaj Wyrzykowski - 06/08/2025 
using netDxf;
using netDxf.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.Remoting.Messaging;
using static netDxf.Entities.HatchBoundaryPath;
// Avoid ambiguity
using SysVec2 = System.Numerics.Vector2;
using DxfVec2 = netDxf.Vector2;


namespace DXF_Raptor_Template_Reader
{
    public static class DXF_Reader
    {
       
        // Parse the DXF file and convert it to JSON format, Collect segments and text entities
        public static string ParseDxfToJson(string filePath, Action<string> log, out List<List<SysVec2>> debugLoops)
        {
            var doc = DxfDocument.Load(filePath);
            log("DXF document loaded.");

            var segments = new List<(SysVec2 start, SysVec2 end)>();
            var segmentInfoList = new List<SegmentInfo>();


            foreach (var poly in doc.Entities.Polylines2D)
            {
                if (poly.Layer != null && LayerSettings.TargetLayers.Contains(poly.Layer.Name))
                {
                    for (int i = 0; i < poly.Vertexes.Count - 1; i++)
                    {   
                        var start = GeometryHelpers.ToVec2(poly.Vertexes[i].Position);
                        var end = GeometryHelpers.ToVec2(poly.Vertexes[i + 1].Position);

                        segments.Add((start, end));
                        segmentInfoList.Add(new SegmentInfo
                        {
                            Start = start,
                            End = end,
                            Layer = poly.Layer?.Name,
                            SourcePolylineHandle = poly.Handle.ToString(),
                            Thickness = (float)(poly.Thickness)
                        });
                    }

                    if (poly.IsClosed && poly.Vertexes.Count > 1)
                    {
                        segments.Add((GeometryHelpers.ToVec2(poly.Vertexes.Last().Position), GeometryHelpers.ToVec2(poly.Vertexes.First().Position)));
                    }
                }
            }

            log($"Total segments collected from target layers: {segments.Count}");

   
            // Build lookup of segmentlayer for fast access and during traversal
            var segmentLayers = new Dictionary<(SysVec2, SysVec2), string>(new GeometryHelpers.EdgeComparer());
            foreach (var info in segmentInfoList)
            {
                segmentLayers[(info.Start, info.End)] = info.Layer;
                //segmentLayers[(info.End, info.Start)] = info.Layer; 
            }

            // Assign the text entities in dxf file to list which will be assigned to polygons later based on position
            var textEntities = new List<(SysVec2 position, string value)>();

            // Only keep texts from "TEXT" layer to prevent invisible texts from being assigned to pieces
            foreach (var t in doc.Entities.Texts)
            {
                if (t.Layer != null && t.Layer.Name == "TEXT" && !string.IsNullOrWhiteSpace(t.Value))
                {
                    textEntities.Add((GeometryHelpers.ToVec2(t.Position), t.Value));
                }
            }
            foreach (var m in doc.Entities.MTexts)
            {
                if (m.Layer != null && m.Layer.Name == "TEXT" && !string.IsNullOrWhiteSpace(m.PlainText()))
                {
                    textEntities.Add((GeometryHelpers.ToVec2(m.Position), m.PlainText()));
                }
            }

            log($"Total text entities collected: {textEntities.Count}");



            var (pieces, loops) = GroupSegmentsIntoClosedLoopsAndStructurePieces(segments, segmentInfoList, segmentLayers, textEntities, log, doc);
            debugLoops = loops;




            log($"Total closed shapes detected (including nested): {loops.Count}");

            return JsonConvert.SerializeObject(pieces, Formatting.Indented);
        }
        


        // Group segments into closed loops and find text entities inside or near them
        private static (StructuredOutput output, List<List<SysVec2>> loops) GroupSegmentsIntoClosedLoopsAndStructurePieces(
            List<(SysVec2, SysVec2)> segments,
            List<SegmentInfo> segmentInfoList,
            Dictionary<(SysVec2, SysVec2), string> segmentLayers,
            List<(SysVec2 position, string value)> textEntities,
            Action<string> log,
            DxfDocument doc)
        {
            var graph = new Dictionary<SysVec2, List<SysVec2>>(new Vec2Comparer());

            foreach (var (start, end) in segments)
            {
                if (!graph.ContainsKey(start)) graph[start] = new List<SysVec2>();
                if (!graph.ContainsKey(end)) graph[end] = new List<SysVec2>();
                graph[start].Add(end);
                graph[end].Add(start);
            }

            var loops = new List<List<SysVec2>>();
            var found = new HashSet<string>();

            foreach (var node in graph.Keys)
            {
                FindCycles(node, node, new List<SysVec2> { node }, found, loops, graph, segmentLayers);
            }

            log($"Cycles found (before deduplication): {loops.Count}");

            // Create PolygonShape for each loop
            var polygons = loops
                .Select(loop =>
                {
                    var polygon = new PolygonShape
                    {
                        Points = loop,
                        Bounds = GeometryHelpers.GetBounds(loop),
                        Area = GeometryHelpers.CalculatePolygonArea(loop),
                        Segments = new List<SegmentInfo>(),
                        Holes = new List<PolygonShape>(),
                        Scribe = null, 
                    };
                    for (int i = 0; i < loop.Count; i++)
                    {
                        var a = loop[i];
                        var b = loop[(i + 1) % loop.Count];

                        // Match original segment info in either direction
                        var match = segmentInfoList.FirstOrDefault(s =>
                            (GeometryHelpers.AreClose(s.Start, a, GeometryHelpers.ToleranceSettings.AreClose) && GeometryHelpers.AreClose(s.End, b, GeometryHelpers.ToleranceSettings.AreClose)) ||
                            (GeometryHelpers.AreClose(s.Start, b, GeometryHelpers.ToleranceSettings.AreClose) && GeometryHelpers.AreClose(s.End, a, GeometryHelpers.ToleranceSettings.AreClose)));

                        if (match != null)
                        {
                            polygon.Segments.Add(match);
                        }
                    }

                    // Assign dominant SourceLayer from segments for holes / cutouts
                    polygon.SourceLayer = polygon.Segments
                        .GroupBy(s => s.Layer)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key;

                    return polygon;
                })
                //.Where(polygon => !GeometryHelpers.PolygonHasCrossingSegments(polygon.Points, segments))
                .OrderByDescending(p => p.Area)
                .ToList();

            log($"Filtered polygons count (after crossing check): {polygons.Count}");


            // Assign scribes (text) to polygons based on textEntities inside or near them
            foreach (var polygon in polygons)
            {
                // Find text inside polygon
                var textsInside = textEntities.Where(te => GeometryHelpers.PointInPolygon(te.position, polygon.Points)).ToList();

                (SysVec2 position, string value)? nearestText = null;

                if (textsInside.Count > 0)
                {
                    var centroid = GeometryHelpers.GetCentroid(polygon.Points);
                    nearestText = textsInside.OrderBy(te => SysVec2.Distance(te.position, centroid)).First();
                }
                else
                {
                    // Check for text near polygon edges otherwise
                    var edges = GeometryHelpers.GetEdges(polygon.Points);
                    var nearbyTexts = new List<(SysVec2 position, string value)>();

                    foreach (var te in textEntities)
                    {
                        foreach (var edge in edges)
                        {
                            if (GeometryHelpers.IsPointNearSegment(te.position, edge.a, edge.b, GeometryHelpers.ToleranceSettings.SegmentIntersection))
                            {
                                nearbyTexts.Add(te);
                                break;
                            }
                        }
                    }

                    if (nearbyTexts.Count > 0)
                    {
                        var centroid = GeometryHelpers.GetCentroid(polygon.Points);
                        nearestText = nearbyTexts.OrderBy(te => SysVec2.Distance(te.position, centroid)).First();
                    }
                }

                polygon.Scribe = nearestText?.value?.Trim();
            }

            // ------------- WARNING POOR SCAD DEVELOPER(s) DODGY LOGIC BELOW THAT SOMEHOW WORKS ---------------
            // This code is a very stupid way to ensure that we only keep the smallest polygon per scribe. Essentially deculttering the over assigned polygons. 
            // DFS in the find cycles method finds too many possible piece shapes not accounting for "cutout" paths as unvalid pieces. I have tried to code around this but I am just a silly intern.
            // //This relies on the text assignemnt functionality to find only valid pieces of smallest area
            // It also relies on templater to name the pieces correctly and uniquely. This appears to be the standard for the templater so it works suprisingly most of the time.

            // Keep only polygons with assigned scribe (piece name)
            var polygonsWithScribe = polygons.Where(p => !string.IsNullOrWhiteSpace(p.Scribe)).ToList();


            // Group polygons by scribe and keep the smallest area polygon per scribe
            var uniquePolygons = polygonsWithScribe
                .GroupBy(p => p.Scribe)
                .Select(g => g.OrderBy(p => p.Area).First())
                .ToList();


            // polygons fully contained inside another polygon become holes of that polygon, this should add holes to valid polygon shapes.
            // Get all other polygons that were filtered out
            var topLevelPieces = new List<PolygonShape>();

            var possibleHolePolygons = polygons
                .Where(p => !uniquePolygons.Contains(p))
                .ToList();


            // Go through each discarded polygon and check if it fits inside a valid piece to assign cutout holes to the pieces
            foreach (var hole in possibleHolePolygons)
            {
                foreach (var valid in uniquePolygons)
                {
                    if (GeometryHelpers.IsContained(hole, valid))
                    {
                        valid.Holes.Add(hole);
                        log($"Added polygon with area {hole.Area} as cutout inside piece with scribe '{valid.Scribe}'.");
                        break; 
                    }
                }
            }

            topLevelPieces = uniquePolygons;

            log($"Final top-level pieces count: {topLevelPieces.Count}");

            var drillHoles = FindDrillHolesFromEntities(doc, log);

            // Add drill holes to valid top pieces
            foreach (var hole in drillHoles)
            {
                foreach (var piece in topLevelPieces)
                {
                    if (GeometryHelpers.IsContained(hole, piece))
                    {
                        piece.DrillHoles.Add(hole);
                        break;
                    }
                }
            }

            // Build flat list of output pieces
            var output = topLevelPieces
                .Select(piece => BuildPieceOutputJSON(piece, GetCategoryAndProcessFromEdgeStyles(piece)))
                .ToList();

            // Group by Category
            var groupedByCategory = output
                .GroupBy(piece =>
                {
                    var categoryProp = piece.GetType().GetProperty("Category");
                    return categoryProp?.GetValue(piece)?.ToString() ?? "Unknown";
                })
                .ToDictionary(
                    g => g.Key,
                    g => g.ToList()
                );

            // Count how many in each category
            var categoryCounts = groupedByCategory
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Count
                );

            // Return combined object in desired structure count of categories and pieces grouped by category
            var outputJson = new StructuredOutput
            {
                CategoryCounts = categoryCounts,
                Pieces = groupedByCategory
            };

            return (outputJson, loops);
        }


        // Recursive function to find cycles in the graph similar to depth-first search. Completes a cycle when it returns to the start point, forming a loop or closed shape.
        private static void FindCycles(SysVec2 current, SysVec2 start, List<SysVec2> path,
            HashSet<string> foundCycles, List<List<SysVec2>> loops, Dictionary<SysVec2, List<SysVec2>> graph, Dictionary<(SysVec2, SysVec2), string> segmentLayers, int maxDepth = 30)
        {
            if (path.Count > maxDepth)
                return;

            foreach (var neighbor in graph[current])
            {
                if (GeometryHelpers.AreClose(neighbor, start, GeometryHelpers.ToleranceSettings.AreClose) && path.Count >= 3)
                {
                    var cycle = new List<SysVec2>(path);
                    string key = GetLoopKey(cycle);

                    if (!foundCycles.Contains(key))
                    {
                        loops.Add(cycle);
                        foundCycles.Add(key);
                    }

                    // Don't continue DFS from this completed loop
                    return;
                }

                if (!path.Contains(neighbor, new Vec2Comparer()))
                {
                    path.Add(neighbor);
                    FindCycles(neighbor, start, path, foundCycles, loops, graph, segmentLayers, maxDepth);
                    path.RemoveAt(path.Count - 1);
                }
            }

        }


        //Generate a unique key for a loop by normalizing its points to a consistent format
        private static string GetLoopKey(List<SysVec2> loop)
        {
            var rounded = loop.Select(v => $"{v.X:F3},{v.Y:F3}").ToList();

            string GetMinRotation(List<string> pts)
            {
                int n = pts.Count;
                string minKey = null;
                for (int i = 0; i < n; i++)
                {
                    var rotated = pts.Skip(i).Concat(pts.Take(i)).ToList();
                    string key = string.Join("|", rotated);
                    if (minKey == null || string.Compare(key, minKey, StringComparison.Ordinal) < 0)
                        minKey = key;
                }
                return minKey;
            }

            var keyFwd = GetMinRotation(rounded);
            var keyRev = GetMinRotation(rounded.AsEnumerable().Reverse().ToList());

            return string.Compare(keyFwd, keyRev, StringComparison.Ordinal) < 0 ? keyFwd : keyRev;
        }

        // Maintain ruleset conditions for obtaining TQS piece categories & processes dependent on edge style layers that make up the pieces. Should this be moved to a config file for mapping?
        // Not efficient, but works for the current template structure. Order of if statements matters here, as it checks for specific conditions in a certain order.
        private static (string Category, string Processes) GetCategoryAndProcessFromEdgeStyles(PolygonShape piece)
        {
            // Collect all layers from piece shapes, holes (cutouts) & drill holes
            var pieceLayers = piece.Segments.Select(s => s.Layer).ToHashSet();
            var holeLayers = piece.Holes.Select(h => h.SourceLayer).Where(l => l != null); 
            var drillHoleLayers = piece.DrillHoles.Select(h => h.SourceLayer).Where(l => l != null);
            var allLayers = pieceLayers.Concat(holeLayers).Concat(drillHoleLayers).ToHashSet();

            // If piece contains unpolished cutout layer, it is a hob cutout
            if (drillHoleLayers.Contains("UNPOLISHED_CUTOUT"))
                return ("Processes", "Hob Cutout");

            // If contains drainer grooves then cutout processes drainer grooves
            if (pieceLayers.Contains("DRAINER_GROOVES"))
                return ("Processes", "Sink Groove Set");

            // Worktop process with sink cutout process?
            if (pieceLayers.Contains("PRIMARY") && drillHoleLayers.Contains("DRILL_HOLE"))
                return ("Worktop", "Sink Run");

            // Sink cutouts part of worktop? 
            if (holeLayers.Contains("POLISHED_CUTOUT"))
                return ("Processes", "Belfast Sink Cut Out");

            // Shaping with handfinished is worktop notch
            if (pieceLayers.Contains("HAND_FINISHED_NOTCH_"))
                return ("Worktop", "Worktop Notch");

            // Worktop / worksurface contains primary edge layer
            if (pieceLayers.Contains("PRIMARY"))
                return ("Worktop", "Cnr run");

            // Splashback process on upstand with only these edge styles?
            if (pieceLayers.Contains("SPLASH") && pieceLayers.Contains("FLAT_POLISH")) 
                return("Upstand", "Splash Back");
            
            if (pieceLayers.Contains("SPLASH") && pieceLayers.Contains("POLISHED_CUTOUT"))
                return ("Upstand", "Splash Back with Cutout");

            // Not sure what piece with flat polish and sawn edge style is in terms of category and process
            if (pieceLayers.Contains("FLAT_POLISH") && pieceLayers.Contains("SAWN"))
                return ("Upstand", "Flat Polish");

            // Fallback if no conditions are met
            return ("Unknown", "Unknown");
        }


        // Find drill holes in the loops based on segment layers. Looks in DXF document entities for CIRCLE and ARC entities on the "DRILL_HOLE" layer.
        private static List<PolygonShape> FindDrillHolesFromEntities(DxfDocument doc, Action<string> log)
        {
            var drillHoles = new List<PolygonShape>();

            // Handle CIRCLES, manual polygon shape assignment to handle circle stuff
            foreach (var circle in doc.Entities.Circles)
            {
                if (circle.Layer?.Name != "DRILL_HOLE" && circle.Layer?.Name != "UNPOLISHED_CUTOUT")
                    continue;

                var center = GeometryHelpers.ToVec2(circle.Center);
                var radius = (float)circle.Radius;
                var points = GeometryHelpers.ApproximateCircle(center, radius, 20);

                // Circle is treated as a polygon with approximate points
                var shape = new PolygonShape
                {
                    Points = points,
                    Bounds = GeometryHelpers.GetBounds(points),
                    Area = GeometryHelpers.CalculatePolygonArea(points),
                    Segments = new List<SegmentInfo>(), // no segments for raw circle
                    Holes = new List<PolygonShape>(),
                    Scribe = null,
                    SourceLayer = circle.Layer?.Name,

                };

                drillHoles.Add(shape);
            }

            // Handle ARC entities , treat as full circle if it's a full 360 arc
            foreach (var arc in doc.Entities.Arcs)
            {
                if (arc.Layer?.Name != "DRILL_HOLE" && arc.Layer?.Name != "UNPOLISHED_CUTOUT")
                    continue;

                double sweep = arc.EndAngle - arc.StartAngle;
                if (Math.Abs(sweep) >= 359.9)
                {
                    var center = GeometryHelpers.ToVec2(arc.Center);
                    var radius = (float)arc.Radius;
                    var points = GeometryHelpers.ApproximateCircle(center, radius, 20);

                    var shape = new PolygonShape
                    {
                        Points = points,
                        Bounds = GeometryHelpers.GetBounds(points),
                        Area = GeometryHelpers.CalculatePolygonArea(points),
                        Segments = new List<SegmentInfo>(),
                        Holes = new List<PolygonShape>(),
                        Scribe = null,
                        SourceLayer = arc.Layer?.Name,
                    };

                    drillHoles.Add(shape);
                }
            }

            log($"Drill holes found from entities: {drillHoles.Count}");
            return drillHoles;
        }



        // Build the output JSON for each piece with its properties and metadata
        // Category grouping for this output happens in GroupSegmentsIntoClosedLoopsAndStructurePieces method. 
        private static object BuildPieceOutputJSON(PolygonShape piece, (string category, string processes) categoryAndProcesses)
        {
            var (category, processes) = categoryAndProcesses;

            /*** Get thickness from the first segment that has it, doesnt work with dxf file since each segment does not have thickness value
            var thickness = piece.Segments
                .Select(s => s.Thickness)
                .FirstOrDefault(t => t.HasValue) ?? 0f;
            ***/

            return new
            {
                Piece = piece.Scribe,
                Length = piece.Bounds.maxX - piece.Bounds.minX,
                Width = piece.Bounds.maxY - piece.Bounds.minY,
                MaxSquareMeterage = Math.Round(piece.Area / 1_000_000.0, 6),
                Category = category,
                Processes = processes,
                EdgeStyles = piece.Segments
                    .GroupBy(s => s.Layer)
                    .ToDictionary(g => g.Key, g => g.Count()),
                Cutouts = piece.Holes.Select(h => new
                {
                    Length = h.Bounds.maxX - h.Bounds.minX,
                    Width = h.Bounds.maxY - h.Bounds.minY,
                    MaxSquareMeterage = Math.Round(h.Area / 1_000_000.0, 6)
                }).ToList(),
                DrillHoles = piece.DrillHoles.Select(h => new
                {
                    Area = Math.Round(h.Area / 1_000_000.0, 6),
                    Circumference = Math.Round(GeometryHelpers.CalculatePerimeter(h.Points), 3),
                    Diameter = Math.Round(Math.Sqrt((h.Area / Math.PI)) * 2, 3)
                }).ToList(),
            };
        }




        // Strucutred output class to hold the final JSON structure return type
        public class StructuredOutput
        {
            public Dictionary<string, int> CategoryCounts { get; set; }
            public Dictionary<string, List<object>> Pieces { get; set; }
        }


        // LayerSettings class to hold layer settings for the DXF reader
        // Any layer that is used to make up individual pieces in the template is this target layer, other layers are optional and can be used for other purposes
        public static class LayerSettings
        {
            public static HashSet<string> TargetLayers { get; private set; }
            public static HashSet<string> OtherLayers { get; private set; }

            static LayerSettings()
            {
                string target = ConfigurationManager.AppSettings["Template_TargetLayers"];
                string other = ConfigurationManager.AppSettings["Template_OtherLayers"];

                TargetLayers = string.IsNullOrWhiteSpace(target)
                    ? new HashSet<string>()
                    : target.Split(',').Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

                OtherLayers = string.IsNullOrWhiteSpace(other)
                    ? new HashSet<string>()
                    : other.Split(',').Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }



        // --- GEOMETRY HELPER CLASS STRUCUTRES, SHOULD REALLY BE IN GEOMETRY HELPERS ----


        // Helper class to represent a polygon shape with its properties
        public class PolygonShape
        {
            public List<SysVec2> Points { get; set; }
            public (float minX, float maxX, float minY, float maxY) Bounds { get; set; }
            public float Area { get; set; }
            public List<PolygonShape> Holes { get; set; } = new List<PolygonShape>();
            public List<SegmentInfo> Segments { get; set; } = new List<SegmentInfo>();
            public string Scribe { get; set; }
            public List<PolygonShape> DrillHoles { get; set; } = new List<PolygonShape>();
            public string SourceLayer { get; set; }  // Layer from which this polygon was derived, if applicable useful for cutouts and drill holes. They are always single layer
        }

        // Helper class to represent a segment or polyline with optional metadata, change this to handle circles or arcs?
        public class SegmentInfo
        {
            public SysVec2 Start { get; set; }
            public SysVec2 End { get; set; }
            public string Layer { get; set; }
            public string SourcePolylineHandle { get; set; }  // DXF entity handle for traceability
            public float? Thickness { get; set; }
        }



        //Custom comparer for SysVec2 to use in dictionaries and sets
        public class Vec2Comparer : IEqualityComparer<SysVec2>
        {
            public bool Equals(SysVec2 a, SysVec2 b)
            {
                return GeometryHelpers.AreClose(a, b, GeometryHelpers.ToleranceSettings.AreClose);
            }

            public int GetHashCode(SysVec2 obj)
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + obj.X.GetHashCode();
                    hash = hash * 23 + obj.Y.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
