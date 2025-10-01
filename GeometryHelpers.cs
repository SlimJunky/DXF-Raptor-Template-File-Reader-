using netDxf;
using netDxf.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.Remoting.Messaging;
using System.Diagnostics;
using System.Configuration;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
// Avoid ambiguity
using SysVec2 = System.Numerics.Vector2;
using DxfVec2 = netDxf.Vector2;

namespace DXF_Raptor_Template_Reader
{
    public static class GeometryHelpers
    {
        // Hold tolerance settings loaded from App.config for geometry operations in DXF processing
        public static class ToleranceSettings
        {
            public static readonly float AreClose; // Tolerance for comparing closeness of points
            public static readonly float PointOnEdge;  // Tolerance for point on edge checks
            public static readonly float SegmentIntersection;  // Tolerance for segment intersection checks

            static ToleranceSettings()
            {
                // Load from App.config, with fallback defaults
                AreClose = GetFloatFromConfig("Tolerance_Closeness", 0.2f);
                PointOnEdge = GetFloatFromConfig("Tolerance_PointNearLine", 2f);
                SegmentIntersection = GetFloatFromConfig("Tolerance_SegmentIntersection", 0.01f);
            }

            private static float GetFloatFromConfig(string key, float defaultValue)
            {
                string value = ConfigurationManager.AppSettings[key];
                return float.TryParse(value, out float result) ? result : defaultValue;
            }
        }

        // Helper: Convert netDxf numerics to System.Numerics
        public static SysVec2 ToVec2(netDxf.Vector2 v) => new SysVec2((float)v.X, (float)v.Y);
        public static SysVec2 ToVec2(netDxf.Vector3 v) => new SysVec2((float)v.X, (float)v.Y);


        // Helper: check if point p is within tolerance distance from segment a-b
        public static bool IsPointNearSegment(SysVec2 p, SysVec2 a, SysVec2 b, float tolerance)
        {
            var ab = b - a;
            var ap = p - a;

            float abLengthSquared = ab.LengthSquared();

            if (abLengthSquared == 0)
                return (p - a).Length() < tolerance;

            float t = SysVec2.Dot(ap, ab) / abLengthSquared;

            t = Clamp(t, 0f, 1f);

            var closest = a + t * ab;

            return (p - closest).Length() < tolerance;
        }

        // Helper: clamp a value between min and max
        public static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            else if (value > max) return max;
            else return value;
        }

        // Helper: get the bounding box of a list of points
        public static (float minX, float maxX, float minY, float maxY) GetBounds(List<SysVec2> points)
        {
            var xs = points.Select(p => p.X);
            var ys = points.Select(p => p.Y);
            return (xs.Min(), xs.Max(), ys.Min(), ys.Max()); //Min() Max() functions may cause some slight discrepancy in rounding here
        }

        // Helper: check if two edges are equal (considering direction)
        public static bool EdgesAreEqual((SysVec2 a, SysVec2 b) e1, (SysVec2 a, SysVec2 b) e2) =>
            (AreClose(e1.a, e2.a, ToleranceSettings.AreClose) && AreClose(e1.b, e2.b, ToleranceSettings.AreClose)) ||
            (AreClose(e1.a, e2.b, ToleranceSettings.AreClose) && AreClose(e1.b, e2.a, ToleranceSettings.AreClose));

        public static float CalculatePolygonArea(List<SysVec2> points)
        {
            float area = 0;
            int n = points.Count;
            for (int i = 0; i < n; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % n];
                area += p1.X * p2.Y - p2.X * p1.Y;
            }
            return Math.Abs(area) / 2f;
        }

        //Helper: get edges of a polygon as pairs of points
        public static List<(SysVec2 a, SysVec2 b)> GetEdges(List<SysVec2> points)
        {
            var edges = new List<(SysVec2, SysVec2)>();
            for (int i = 0; i < points.Count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                edges.Add((p1, p2));
            }
            return edges;
        }

        // Helper: check if a polygon is fully contained within another polygon
        public static bool IsContained(DXF_Reader.PolygonShape candidate, DXF_Reader.PolygonShape container)
        {
            foreach (var pt in candidate.Points)
            {
                if (!PointInPolygon(pt, container.Points))
                    return false;
            }

            var cEdges = GetEdges(candidate.Points);
            var pEdges = GetEdges(container.Points);

            foreach (var ce in cEdges)
            {
                foreach (var pe in pEdges)
                {
                    if (EdgesAreEqual(ce, pe))
                        return false; // shared edge means not fully inside
                }
            }

            return true;
        }

        //Helper: check if a point is inside a polygon using the ray-casting algorithm
        public static bool PointInPolygon(SysVec2 point, List<SysVec2> polygon)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = polygon[i];
                var pj = polygon[j];

                if ((pi.Y > point.Y) != (pj.Y > point.Y) &&
                    point.X < (pj.X - pi.X) * (point.Y - pi.Y) / ((pj.Y - pi.Y) + 1e-6f) + pi.X)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // Helper: check if two vectors are close enough to be considered equal
        public static bool AreClose(SysVec2 a, SysVec2 b, float tolerance) =>
            SysVec2.Distance(a, b) < tolerance;


        //Helper: Calculate the centroid of a list of points (Middle of the polygon)
        public static SysVec2 GetCentroid(List<SysVec2> points)
        {
            float x = 0;
            float y = 0;
            int count = points.Count;
            foreach (var p in points)
            {
                x += p.X;
                y += p.Y;
            }
            return new SysVec2(x / count, y / count);
        }


        //Helper: Check if a segment passes through the interior of a polygon - halfway through a midpoint of shape
        public static bool DoesSegmentPassThroughPolygon(SysVec2 a, SysVec2 b, List<SysVec2> polygon)
        {
            var midpoint = (a + b) / 2;
            return PointInPolygon(midpoint, polygon);
        }

        public static bool HasCrossingSegment(List<SysVec2> polygon, List<(SysVec2 start, SysVec2 end)> allSegments, float tolerance)
        {
            var polygonEdges = GetEdges(polygon);

            foreach (var (segStart, segEnd) in allSegments)
            {
                // Skip if this segment is part of the polygon's perimeter
                if (IsEdgeInPolygon((segStart, segEnd), polygonEdges, tolerance))
                    continue;

                bool startInside = PointInPolygon(segStart, polygon);
                bool endInside = PointInPolygon(segEnd, polygon);

                if (startInside || endInside)
                    return true;

                // Count how many times this segment intersects polygon edges
                int intersections = 0;
                foreach (var (a, b) in polygonEdges)
                {
                    if (SegmentsIntersect(segStart, segEnd, a, b, ToleranceSettings.SegmentIntersection))
                        intersections++;
                    if (intersections >= 2)
                        return true;
                }
               
            }

            return false;
        }

        // Checks if the polygon has any crossing segments inside its interior (excluding polygon edges)
        public static bool PolygonHasCrossingSegments(List<SysVec2> polygonPoints, List<(SysVec2 start, SysVec2 end)> allSegments)
        {
            var polygonEdges = GetEdges(polygonPoints); // your helper to get polygon edges

            foreach (var seg in allSegments)
            {
                // Ignore segments that are polygon edges themselves
                if (polygonEdges.Any(edge => EdgesAreEqual(edge,(seg.start, seg.end))))
                    continue;

                // Check if segment intersects polygon edges or lies inside polygon crossing other segments
                if (DoesSegmentCrossPolygonInterior(seg, polygonPoints))
                {
                    return true; // crossing segment found inside polygon
                }
            }

            return false; // no crossing segments inside polygon
        }


        // Helper: Check if a point is on the edge of a polygon within a tolerance
        public static bool IsPointOnPolygonEdge(SysVec2 point, List<SysVec2> polygon, float tolerance)
        {
            var edges = GetEdges(polygon);

            foreach (var (a, b) in edges)
            {
                if (DistanceToSegment(point, a, b) <= tolerance)
                    return true;
            }

            return false;
        }

        // Check if two line segments intersect
        public static bool SegmentsIntersect(SysVec2 p1, SysVec2 p2, SysVec2 p3, SysVec2 p4, float tolerance)
        {
            // Using 2D line segment intersection algorithm
            float denominator = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);

            if (Math.Abs(denominator) < tolerance)
            {
                // Lines are parallel or coincident
                return false;
            }

            float ua = ((p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X)) / denominator;
            float ub = ((p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X)) / denominator;

            return (ua >= 0 && ua <= 1) && (ub >= 0 && ub <= 1);
        }



        // Helper: Check if two segments intersect
        public static bool IsEdgeInPolygon((SysVec2 a, SysVec2 b) edge, List<(SysVec2, SysVec2)> polygonEdges, float tolerance)
        {
            foreach (var (x, y) in polygonEdges)
            {
                if ((AreClose(edge.a, x, tolerance) && AreClose(edge.b, y, tolerance)) ||
                    (AreClose(edge.a, y, tolerance) && AreClose(edge.b, x, tolerance)))
                    return true;
            }
            return false;
        }

        public static bool DoesSegmentCrossPolygonInterior((SysVec2 start, SysVec2 end) segment, List<SysVec2> polygon)
        {
            var polygonEdges = GetEdges(polygon);

            SysVec2 segStart = segment.start;
            SysVec2 segEnd = segment.end;

            //Check if segment intersects any polygon edge (except sharing endpoints)
            foreach (var (edgeStart, edgeEnd) in polygonEdges)
            {
                // Skip if they share endpoints (touching edges)
                bool sharesEndpoint = AreClose(segStart, edgeStart, ToleranceSettings.AreClose) || AreClose(segStart, edgeEnd, ToleranceSettings.AreClose) ||
                                      AreClose(segEnd, edgeStart, ToleranceSettings.AreClose) || AreClose(segEnd, edgeEnd, ToleranceSettings.AreClose);
                if (sharesEndpoint)
                    continue;

                // Check actual intersection of line segments
                if (SegmentsIntersect(segStart, segEnd, edgeStart, edgeEnd, ToleranceSettings.SegmentIntersection))
                {
                    return true;
                }
            }

            //Check if midpoint of the segment lies inside polygon (passes through interior)
            var midpoint = (segStart + segEnd) / 2f;
            if (PointInPolygon(midpoint, polygon))
            {
                return true;
            }

            return false;
        }

        // Helper: Approximate a circle with a given number of segments
        public static List<SysVec2> ApproximateCircle(SysVec2 center, float radius, int segments)
        {
            var points = new List<SysVec2>();
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)(2 * Math.PI * i / segments);
                float x = center.X + radius * (float)Math.Cos(angle);
                float y = center.Y + radius * (float)Math.Sin(angle);
                points.Add(new SysVec2(x, y));
            }
            return points;
        }

        // Helper: Calculate permieter of polygon shapes, useful for drill holes.
        public static float CalculatePerimeter(List<SysVec2> points)
        {
            float perimeter = 0f;

            for (int i = 0; i < points.Count; i++)
            {
                var current = points[i];
                var next = points[(i + 1) % points.Count]; // wraps around to form closed loop
                perimeter += SysVec2.Distance(current, next);
            }

            return perimeter;
        }



        // Helper: Calculate the distance from a point to a line segment
        public static float DistanceToSegment(SysVec2 p, SysVec2 a, SysVec2 b)
        {
            var ab = b - a;
            var ap = p - a;
            float t = Clamp(SysVec2.Dot(ap, ab) / ab.LengthSquared(), 0f, 1f);
            var projection = a + t * ab;
            return SysVec2.Distance(p, projection);
        }


        // Custom comparer for edges that ignores order
        public class EdgeComparer : IEqualityComparer<(SysVec2, SysVec2)>
        {
            public bool Equals((SysVec2, SysVec2) e1, (SysVec2, SysVec2) e2)
            {
                return (AreClose(e1.Item1, e2.Item1, ToleranceSettings.AreClose) && AreClose(e1.Item2, e2.Item2, ToleranceSettings.AreClose)) ||
                       (AreClose(e1.Item1, e2.Item2, ToleranceSettings.AreClose) && AreClose(e1.Item2, e2.Item1, ToleranceSettings.AreClose));
            }

            // Generate a hash code that is order-independent -- Used indirectly in collections like HashSet 
            public int GetHashCode((SysVec2, SysVec2) edge)
            {
                unchecked
                {
                    // Order-independent hash
                    int hash1 = edge.Item1.GetHashCode();
                    int hash2 = edge.Item2.GetHashCode();
                    return hash1 ^ hash2;
                }
            }

        }

    }
}
