using Rhino.Geometry;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.Formats.ThreeDm.Rhino3dm;

internal static class Rhino3dmGeometryConverter
{
    public static ThreeDmGeometryData? Convert(GeometryBase geometry)
    {
        return geometry switch
        {
            Rhino.Geometry.Point point => ConvertPointGeometry(point),
            PointCloud pointCloud => ConvertPointCloud(pointCloud),
            Brep brep => ConvertBrep(brep),
            Extrusion extrusion => ConvertExtrusion(extrusion),
            Mesh mesh => ConvertMesh(mesh),
            SubD subD => ConvertSubD(subD),
            InstanceReferenceGeometry instanceReference => ConvertInstanceReference(instanceReference),
            TextDot textDot => ConvertTextDot(textDot),
            AnnotationBase annotation => ConvertAnnotation(annotation),
            Hatch hatch => ConvertHatch(hatch),
            Curve curve => ConvertCurve(curve),
            Surface surface => ConvertSurface(surface),
            _ => null,
        };
    }

    private static ThreeDmPointGeometryData ConvertPointGeometry(Rhino.Geometry.Point point)
    {
        var bounds = ConvertBounds(point.GetBoundingBox(true));
        return new ThreeDmPointGeometryData(ConvertPoint(point.Location), bounds);
    }

    private static ThreeDmPointCloudGeometryData ConvertPointCloud(PointCloud pointCloud)
    {
        var points = pointCloud.GetPoints().Select(ConvertPoint).ToArray();
        return new ThreeDmPointCloudGeometryData(points, ConvertBounds(pointCloud.GetBoundingBox(true)));
    }

    private static ThreeDmBrepGeometryData ConvertBrep(Brep brep)
    {
        var vertices = new List<ThreeDmBrepVertexData>(brep.Vertices.Count);
        foreach (var vertex in brep.Vertices)
        {
            vertices.Add(new ThreeDmBrepVertexData(
                vertex.VertexIndex,
                ConvertPoint(vertex.Location),
                vertex.Tolerance));
        }

        var edges = new List<ThreeDmBrepEdgeData>(brep.Edges.Count);
        foreach (var edge in brep.Edges)
        {
            edges.Add(new ThreeDmBrepEdgeData(
                edge.EdgeIndex,
                edge.StartVertex.VertexIndex,
                edge.EndVertex.VertexIndex,
                edge.Tolerance,
                ConvertCurve(edge)));
        }

        var trims = new List<ThreeDmBrepTrimData>(brep.Trims.Count);
        foreach (var trim in brep.Trims)
        {
            trim.GetTolerances(out var toleranceU, out var toleranceV);
            ThreeDmCurveGeometryData? parameterCurve = null;
            var trimCurve = trim.TrimCurve;
            if (trimCurve is not null)
            {
                parameterCurve = ConvertCurve(trimCurve);
            }

            trims.Add(new ThreeDmBrepTrimData(
                trim.TrimIndex,
                trim.Face.FaceIndex,
                trim.Loop.LoopIndex,
                trim.Edge?.EdgeIndex,
                trim.StartVertex.VertexIndex,
                trim.EndVertex.VertexIndex,
                trim.TrimType.ToString(),
                trim.IsoStatus.ToString(),
                trim.IsReversed(),
                toleranceU,
                toleranceV,
                parameterCurve));
        }

        var loops = new List<ThreeDmBrepLoopData>(brep.Loops.Count);
        foreach (var loop in brep.Loops)
        {
            loops.Add(new ThreeDmBrepLoopData(
                loop.LoopIndex,
                loop.Face.FaceIndex,
                loop.LoopType.ToString(),
                loop.Trims.Select(trim => trim.TrimIndex).ToArray()));
        }

        var faces = new List<ThreeDmBrepFaceData>(brep.Faces.Count);
        foreach (var face in brep.Faces)
        {
            faces.Add(new ThreeDmBrepFaceData(
                face.FaceIndex,
                face.SurfaceIndex,
                face.OrientationIsReversed,
                face.Loops.Select(loop => loop.LoopIndex).ToArray(),
                ConvertSurface(face)));
        }

        return new ThreeDmBrepGeometryData(
            vertices,
            edges,
            trims,
            loops,
            faces,
            brep.IsSolid,
            ConvertBounds(brep.GetBoundingBox(true)));
    }

    private static ThreeDmInstanceReferenceGeometryData ConvertInstanceReference(InstanceReferenceGeometry instanceReference) =>
        new(
            instanceReference.ParentIdefId,
            ConvertTransform(instanceReference.Xform),
            ConvertBounds(instanceReference.GetBoundingBox(true)));

    private static ThreeDmSubDGeometryData ConvertSubD(SubD subD)
    {
        var vertices = new List<ThreeDmSubDVertexData>(subD.Vertices.Count);
        foreach (var vertex in subD.Vertices)
        {
            vertices.Add(new ThreeDmSubDVertexData(
                vertex.Id,
                ConvertPoint(vertex.ControlNetPoint),
                vertex.Tag.ToString()));
        }

        var faces = new List<ThreeDmSubDFaceData>(subD.Faces.Count);
        foreach (var face in subD.Faces)
        {
            var vertexIds = new uint[face.VertexCount];
            for (var i = 0; i < vertexIds.Length; i++)
            {
                vertexIds[i] = face.VertexAt(i).Id;
            }

            faces.Add(new ThreeDmSubDFaceData(
                face.Id,
                vertexIds,
                0,
                face.PerFaceColor.IsEmpty ? null : ToArgb(face.PerFaceColor)));
        }

        return new ThreeDmSubDGeometryData(
            vertices,
            faces,
            ConvertBounds(subD.GetBoundingBox(true)));
    }

    private static ThreeDmTextDotGeometryData ConvertTextDot(TextDot textDot) =>
        new(
            textDot.Text ?? string.Empty,
            textDot.SecondaryText ?? string.Empty,
            ConvertPoint(textDot.Point),
            textDot.FontFace ?? string.Empty,
            textDot.FontHeight,
            ConvertBounds(textDot.GetBoundingBox(true)));

    private static ThreeDmAnnotationGeometryData ConvertAnnotation(AnnotationBase annotation) =>
        new(
            annotation.AnnotationType.ToString(),
            annotation.PlainText ?? string.Empty,
            annotation.RichText ?? string.Empty,
            annotation.DimensionStyleId,
            ConvertPlane(annotation.Plane),
            annotation.TextHeight,
            annotation.TextRotationRadians,
            ConvertBounds(annotation.GetBoundingBox(true)));

    private static ThreeDmHatchGeometryData ConvertHatch(Hatch hatch)
    {
        var outer = ConvertBoundaryCurves(hatch.Get3dCurves(true));
        var inner = ConvertBoundaryCurves(hatch.Get3dCurves(false));
        return new ThreeDmHatchGeometryData(
            hatch.PatternIndex,
            hatch.PatternScale,
            hatch.PatternRotation,
            ConvertPoint(hatch.BasePoint),
            outer,
            inner,
            ConvertBounds(hatch.GetBoundingBox(true)));
    }

    private static List<ThreeDmCurveGeometryData> ConvertBoundaryCurves(IEnumerable<Curve> curves)
    {
        var result = new List<ThreeDmCurveGeometryData>();
        foreach (var curve in curves)
        {
            using (curve)
            {
                result.Add(ConvertCurve(curve));
            }
        }

        return result;
    }

    private static ThreeDmCurveGeometryData ConvertCurve(Curve curve)
    {
        var bounds = ConvertBounds(curve.GetBoundingBox(true));
        using var nurbs = curve.ToNurbsCurve();
        if (nurbs is null)
        {
            throw new InvalidDataException($"Curve type '{curve.GetType().Name}' does not expose a NURBS representation.");
        }

        var controlPoints = new List<ThreeDmWeightedPoint3d>(nurbs.Points.Count);
        for (var i = 0; i < nurbs.Points.Count; i++)
        {
            if (!nurbs.Points.GetPoint(i, out Rhino.Geometry.Point3d point))
            {
                throw new InvalidDataException($"Failed to read NURBS curve control point {i}.");
            }

            controlPoints.Add(new ThreeDmWeightedPoint3d(ConvertPoint(point), nurbs.Points.GetWeight(i)));
        }

        var knots = new double[nurbs.Knots.Count];
        for (var i = 0; i < knots.Length; i++)
        {
            knots[i] = nurbs.Knots[i];
        }

        var nurbsData = new ThreeDmNurbsCurveData(
            nurbs.Degree,
            nurbs.IsRational,
            nurbs.IsClosed,
            nurbs.IsPeriodic,
            controlPoints,
            knots);

        var form = ThreeDmCurveForm.Other;
        var polylinePoints = Array.Empty<SpatialViewer.ThreeDm.Core.Point3d>();
        ThreeDmArcGeometryData? arcData = null;
        ThreeDmEllipseGeometryData? ellipseData = null;

        if (curve is LineCurve)
        {
            form = ThreeDmCurveForm.Line;
        }
        else if (curve.TryGetPolyline(out var polyline))
        {
            form = ThreeDmCurveForm.Polyline;
            polylinePoints = polyline.Select(ConvertPoint).ToArray();
        }
        else if (curve.TryGetCircle(out var circle))
        {
            form = ThreeDmCurveForm.Circle;
            arcData = new ThreeDmArcGeometryData(
                ConvertPoint(circle.Center),
                ConvertVector(circle.Plane.Normal),
                circle.Radius,
                0,
                Math.PI * 2);
        }
        else if (curve.TryGetArc(out var arc))
        {
            form = ThreeDmCurveForm.Arc;
            arcData = new ThreeDmArcGeometryData(
                ConvertPoint(arc.Center),
                ConvertVector(arc.Plane.Normal),
                arc.Radius,
                arc.StartAngle,
                arc.EndAngle);
        }
        else if (curve.TryGetEllipse(out var ellipse))
        {
            form = ThreeDmCurveForm.Ellipse;
            ellipseData = new ThreeDmEllipseGeometryData(
                ConvertPoint(ellipse.Plane.Origin),
                ConvertVector(ellipse.Plane.Normal),
                ellipse.Radius1,
                ellipse.Radius2);
        }
        else if (curve is NurbsCurve)
        {
            form = ThreeDmCurveForm.Nurbs;
        }

        return new ThreeDmCurveGeometryData(
            form,
            nurbsData,
            polylinePoints,
            arcData,
            ellipseData,
            bounds);
    }

    private static ThreeDmNurbsSurfaceGeometryData ConvertSurface(Surface surface)
    {
        var bounds = ConvertBounds(surface.GetBoundingBox(true));
        using var nurbs = surface.ToNurbsSurface();
        if (nurbs is null)
        {
            throw new InvalidDataException($"Surface type '{surface.GetType().Name}' does not expose a NURBS representation.");
        }

        var controlPoints = new List<ThreeDmWeightedPoint3d>(nurbs.Points.CountU * nurbs.Points.CountV);
        for (var u = 0; u < nurbs.Points.CountU; u++)
        {
            for (var v = 0; v < nurbs.Points.CountV; v++)
            {
                if (!nurbs.Points.GetPoint(u, v, out Rhino.Geometry.Point3d point))
                {
                    throw new InvalidDataException($"Failed to read NURBS surface control point ({u}, {v}).");
                }

                controlPoints.Add(new ThreeDmWeightedPoint3d(ConvertPoint(point), nurbs.Points.GetWeight(u, v)));
            }
        }

        var knotsU = new double[nurbs.KnotsU.Count];
        for (var i = 0; i < knotsU.Length; i++)
        {
            knotsU[i] = nurbs.KnotsU[i];
        }

        var knotsV = new double[nurbs.KnotsV.Count];
        for (var i = 0; i < knotsV.Length; i++)
        {
            knotsV[i] = nurbs.KnotsV[i];
        }

        return new ThreeDmNurbsSurfaceGeometryData(
            nurbs.Degree(0),
            nurbs.Degree(1),
            nurbs.Points.CountU,
            nurbs.Points.CountV,
            nurbs.IsRational,
            nurbs.IsClosed(0),
            nurbs.IsClosed(1),
            nurbs.IsPeriodic(0),
            nurbs.IsPeriodic(1),
            controlPoints,
            knotsU,
            knotsV,
            bounds);
    }

    private static ThreeDmExtrusionGeometryData ConvertExtrusion(Extrusion extrusion)
    {
        var profiles = new List<ThreeDmCurveGeometryData>(extrusion.ProfileCount);
        for (var i = 0; i < extrusion.ProfileCount; i++)
        {
            using var profile = extrusion.Profile3d(i, 0.0);
            if (profile is not null)
            {
                profiles.Add(ConvertCurve(profile));
            }
        }

        return new ThreeDmExtrusionGeometryData(
            ConvertPoint(extrusion.PathStart),
            ConvertPoint(extrusion.PathEnd),
            ConvertVector(extrusion.PathTangent),
            extrusion.IsSolid,
            extrusion.IsCappedAtBottom,
            extrusion.IsCappedAtTop,
            profiles,
            ConvertBounds(extrusion.GetBoundingBox(true)));
    }

    private static ThreeDmMeshGeometryData ConvertMesh(Mesh mesh)
    {
        var vertices = new SpatialViewer.ThreeDm.Core.Point3d[mesh.Vertices.Count];
        for (var i = 0; i < vertices.Length; i++)
        {
            var vertex = mesh.Vertices[i];
            vertices[i] = new SpatialViewer.ThreeDm.Core.Point3d(vertex.X, vertex.Y, vertex.Z);
        }

        var faces = new ThreeDmMeshFace[mesh.Faces.Count];
        for (var i = 0; i < faces.Length; i++)
        {
            var face = mesh.Faces.GetFace(i);
            faces[i] = new ThreeDmMeshFace(face.A, face.B, face.C, face.IsQuad ? face.D : null);
        }

        var normals = new SpatialViewer.ThreeDm.Core.Vector3d[mesh.Normals.Count];
        for (var i = 0; i < normals.Length; i++)
        {
            var normal = mesh.Normals[i];
            normals[i] = new SpatialViewer.ThreeDm.Core.Vector3d(normal.X, normal.Y, normal.Z);
        }

        var textureCoordinates = new ThreeDmTextureCoordinate[mesh.TextureCoordinates.Count];
        for (var i = 0; i < textureCoordinates.Length; i++)
        {
            var textureCoordinate = mesh.TextureCoordinates[i];
            textureCoordinates[i] = new ThreeDmTextureCoordinate(textureCoordinate.X, textureCoordinate.Y);
        }

        return new ThreeDmMeshGeometryData(
            vertices,
            faces,
            normals,
            textureCoordinates,
            mesh.IsClosed,
            ConvertBounds(mesh.GetBoundingBox(true)));
    }

    private static Plane3d ConvertPlane(Plane plane) =>
        new(ConvertPoint(plane.Origin), ConvertVector(plane.XAxis), ConvertVector(plane.YAxis), ConvertVector(plane.ZAxis));

    private static Transform3d ConvertTransform(Transform transform) =>
        new(
            transform.M00, transform.M01, transform.M02, transform.M03,
            transform.M10, transform.M11, transform.M12, transform.M13,
            transform.M20, transform.M21, transform.M22, transform.M23,
            transform.M30, transform.M31, transform.M32, transform.M33);

    private static BoundingBox3d ConvertBounds(Rhino.Geometry.BoundingBox bounds)
    {
        if (!bounds.IsValid)
        {
            return BoundingBox3d.Invalid;
        }

        return new BoundingBox3d(ConvertPoint(bounds.Min), ConvertPoint(bounds.Max));
    }

    private static SpatialViewer.ThreeDm.Core.Point3d ConvertPoint(Rhino.Geometry.Point3d point) =>
        new(point.X, point.Y, point.Z);

    private static SpatialViewer.ThreeDm.Core.Vector3d ConvertVector(Rhino.Geometry.Vector3d vector) =>
        new(vector.X, vector.Y, vector.Z);

    private static uint ToArgb(System.Drawing.Color color) => unchecked((uint)color.ToArgb());
}
