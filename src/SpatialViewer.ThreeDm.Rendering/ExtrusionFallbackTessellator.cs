using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

internal static class ThreeDmExtrusionFallbackTessellator
{
    public static ThreeDmRenderMesh? Tessellate(
        Guid sourceObjectId,
        ThreeDmExtrusionGeometryData extrusion,
        ThreeDmTessellationSettings settings,
        double modelAbsoluteTolerance,
        Guid? materialId,
        uint? colorArgb)
    {
        ArgumentNullException.ThrowIfNull(extrusion);
        ArgumentNullException.ThrowIfNull(settings);
        if (extrusion.Profiles.Count == 0)
        {
            return null;
        }

        var delta = new ThreeDmRenderVertex(
            extrusion.PathEnd.X - extrusion.PathStart.X,
            extrusion.PathEnd.Y - extrusion.PathStart.Y,
            extrusion.PathEnd.Z - extrusion.PathStart.Z);
        var vertices = new List<ThreeDmRenderVertex>();
        var indices = new List<int>();
        var profileRanges = new List<(int Start, int Count)>();

        foreach (var profile in extrusion.Profiles)
        {
            var rendered = ThreeDmCurveTessellator.Tessellate(
                sourceObjectId,
                profile,
                settings,
                modelAbsoluteTolerance);
            var points = RemoveDuplicateClosingPoint(rendered.Points);
            if (points.Count < 2)
            {
                continue;
            }

            var start = vertices.Count;
            vertices.AddRange(points);
            foreach (var point in points)
            {
                vertices.Add(new ThreeDmRenderVertex(
                    point.X + delta.X,
                    point.Y + delta.Y,
                    point.Z + delta.Z));
            }

            profileRanges.Add((start, points.Count));
            var topStart = start + points.Count;
            for (var i = 0; i < points.Count; i++)
            {
                var next = (i + 1) % points.Count;
                var bottomA = start + i;
                var bottomB = start + next;
                var topA = topStart + i;
                var topB = topStart + next;

                indices.Add(bottomA);
                indices.Add(bottomB);
                indices.Add(topB);
                indices.Add(bottomA);
                indices.Add(topB);
                indices.Add(topA);
            }
        }

        if (extrusion.IsSolid && profileRanges.Count == 1)
        {
            var (start, count) = profileRanges[0];
            var topStart = start + count;
            if (extrusion.IsCappedAtBottom)
            {
                for (var i = 1; i < count - 1; i++)
                {
                    indices.Add(start);
                    indices.Add(start + i + 1);
                    indices.Add(start + i);
                }
            }

            if (extrusion.IsCappedAtTop)
            {
                for (var i = 1; i < count - 1; i++)
                {
                    indices.Add(topStart);
                    indices.Add(topStart + i);
                    indices.Add(topStart + i + 1);
                }
            }
        }

        if (vertices.Count == 0 || indices.Count < 3)
        {
            return null;
        }

        return new ThreeDmRenderMesh(sourceObjectId, vertices.ToArray(), indices.ToArray())
        {
            MaterialId = materialId,
            ColorArgb = colorArgb,
        };
    }

    private static IReadOnlyList<ThreeDmRenderVertex> RemoveDuplicateClosingPoint(
        IReadOnlyList<ThreeDmRenderVertex> points)
    {
        if (points.Count < 2)
        {
            return points;
        }

        var first = points[0];
        var last = points[^1];
        var dx = first.X - last.X;
        var dy = first.Y - last.Y;
        var dz = first.Z - last.Z;
        return (dx * dx) + (dy * dy) + (dz * dz) <= 1e-18
            ? points.Take(points.Count - 1).ToArray()
            : points;
    }
}
