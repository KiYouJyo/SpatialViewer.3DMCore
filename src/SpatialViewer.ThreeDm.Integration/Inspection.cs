using System.Globalization;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Integration;

public sealed record ThreeDmDocumentSummary(
    string SourcePath,
    BoundingBox3d Bounds,
    string? ModelUnitSystem,
    int ObjectCount,
    int LayerCount,
    int MaterialCount,
    int NamedViewCount,
    int InstanceDefinitionCount,
    int InformationDiagnosticCount,
    int WarningDiagnosticCount,
    int ErrorDiagnosticCount);

public static class ThreeDmInspection
{
    public static ThreeDmDocumentSummary CreateDocumentSummary(ThreeDmSceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new ThreeDmDocumentSummary(
            document.SourcePath,
            document.Bounds,
            document.Properties?.ModelUnitSystem,
            document.Objects.Count,
            document.Layers.Count,
            document.Materials.Count,
            document.NamedViews.Count,
            document.InstanceDefinitions.Count,
            document.Diagnostics.Count(item => item.Severity == ThreeDmDiagnosticSeverity.Information),
            document.Diagnostics.Count(item => item.Severity == ThreeDmDiagnosticSeverity.Warning),
            document.Diagnostics.Count(item => item.Severity == ThreeDmDiagnosticSeverity.Error));
    }

    public static IReadOnlyDictionary<string, string> CreateGeometryDetails(ThreeDmGeometryData? geometry)
    {
        if (geometry is null) return new Dictionary<string, string>();

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        switch (geometry)
        {
            case ThreeDmPointCloudGeometryData pointCloud:
                values["PointCount"] = Format(pointCloud.Points.Count);
                break;
            case ThreeDmCurveGeometryData curve:
                values["CurveForm"] = curve.Form.ToString();
                values["ControlPointCount"] = Format(curve.Nurbs.ControlPoints.Count);
                values["Degree"] = Format(curve.Nurbs.Degree);
                values["Closed"] = curve.Nurbs.IsClosed.ToString();
                break;
            case ThreeDmNurbsSurfaceGeometryData surface:
                values["DegreeU"] = Format(surface.DegreeU);
                values["DegreeV"] = Format(surface.DegreeV);
                values["ControlPointCountU"] = Format(surface.ControlPointCountU);
                values["ControlPointCountV"] = Format(surface.ControlPointCountV);
                break;
            case ThreeDmMeshGeometryData mesh:
                values["VertexCount"] = Format(mesh.Vertices.Count);
                values["FaceCount"] = Format(mesh.Faces.Count);
                values["Closed"] = mesh.IsClosed.ToString();
                break;
            case ThreeDmBrepGeometryData brep:
                values["VertexCount"] = Format(brep.Vertices.Count);
                values["EdgeCount"] = Format(brep.Edges.Count);
                values["FaceCount"] = Format(brep.Faces.Count);
                values["Solid"] = brep.IsSolid.ToString();
                values["RenderMeshCount"] = Format(brep.RenderMeshes.Count);
                break;
            case ThreeDmExtrusionGeometryData extrusion:
                values["ProfileCount"] = Format(extrusion.Profiles.Count);
                values["Solid"] = extrusion.IsSolid.ToString();
                values["RenderMeshCount"] = Format(extrusion.RenderMeshes.Count);
                break;
            case ThreeDmSubDGeometryData subD:
                values["VertexCount"] = Format(subD.Vertices.Count);
                values["FaceCount"] = Format(subD.Faces.Count);
                break;
            case ThreeDmInstanceReferenceGeometryData instance:
                values["InstanceDefinitionId"] = instance.InstanceDefinitionId.ToString("D");
                break;
            case ThreeDmAnnotationGeometryData annotation:
                values["AnnotationType"] = annotation.AnnotationType;
                values["Text"] = annotation.PlainText;
                break;
            case ThreeDmTextDotGeometryData textDot:
                values["Text"] = textDot.Text;
                break;
            case ThreeDmHatchGeometryData hatch:
                values["OuterBoundaryCount"] = Format(hatch.OuterBoundaries.Count);
                values["InnerBoundaryCount"] = Format(hatch.InnerBoundaries.Count);
                break;
        }

        return values;
    }

    private static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}

public sealed partial class ThreeDmSession
{
    public ThreeDmDocumentSummary GetDocumentSummary() =>
        ThreeDmInspection.CreateDocumentSummary(RequireOpenDocument());

    public IReadOnlyList<ThreeDmImportDiagnostic> GetDiagnostics() =>
        RequireOpenDocument().Diagnostics.ToArray();
}
