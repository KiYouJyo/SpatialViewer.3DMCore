using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Rendering.Windows;

public sealed record WindowsThreeDmRenderingCapabilities(
    bool SupportsExpandedMeshes,
    bool SupportsSharedMeshInstances,
    bool SupportsCurves,
    bool SupportsPointSets,
    bool SupportsPbrMetadata,
    bool SupportsTextureMetadata,
    bool SupportsLargeCoordinateRebasing);

public sealed record WindowsPreparedRenderUpload(
    ThreeDmRenderDisplayMode DisplayMode,
    WindowsSharedMeshSceneUpload SharedMeshes,
    IReadOnlyList<WindowsRenderCurveUpload> Curves,
    IReadOnlyList<WindowsRenderPointSetUpload> PointSets,
    IReadOnlyList<ThreeDmRenderDiagnostic> Diagnostics)
{
    public IReadOnlyList<ThreeDmPreparedMeshDrawPolicy> MeshDrawPolicies { get; init; } =
        Array.Empty<ThreeDmPreparedMeshDrawPolicy>();
}

public interface IWindowsThreeDmRenderingBackend
{
    string Name { get; }
    int ApiVersion { get; }
    WindowsThreeDmRenderingCapabilities Capabilities { get; }

    WindowsRenderSceneUpload Project(ThreeDmRenderScene scene, WindowsRenderOrigin? origin = null);
    WindowsSharedMeshSceneUpload Project(ThreeDmSharedMeshScene scene, WindowsRenderOrigin? origin = null);
    WindowsPreparedRenderUpload Project(ThreeDmPreparedRenderScene scene, WindowsRenderOrigin? origin = null);
}

public sealed class WindowsThreeDmRenderingBackend : IWindowsThreeDmRenderingBackend
{
    public string Name => "SpatialViewer.3DMCore.Windows";
    public int ApiVersion => 1;

    public WindowsThreeDmRenderingCapabilities Capabilities { get; } = new(
        SupportsExpandedMeshes: true,
        SupportsSharedMeshInstances: true,
        SupportsCurves: true,
        SupportsPointSets: true,
        SupportsPbrMetadata: true,
        SupportsTextureMetadata: true,
        SupportsLargeCoordinateRebasing: true);

    public WindowsRenderSceneUpload Project(ThreeDmRenderScene scene, WindowsRenderOrigin? origin = null) =>
        WindowsThreeDmUploadProjection.Project(scene, origin);

    public WindowsSharedMeshSceneUpload Project(ThreeDmSharedMeshScene scene, WindowsRenderOrigin? origin = null) =>
        WindowsThreeDmSharedUploadProjection.Project(scene, origin);

    public WindowsPreparedRenderUpload Project(ThreeDmPreparedRenderScene scene, WindowsRenderOrigin? origin = null)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var shared = WindowsThreeDmSharedUploadProjection.Project(scene.SharedMeshes, origin);
        var nonMeshScene = new ThreeDmRenderScene(Array.Empty<ThreeDmRenderMesh>())
        {
            Curves = scene.Curves,
            PointSets = scene.PointSets,
        };
        var primitives = WindowsThreeDmUploadProjection.Project(nonMeshScene, shared.Origin);

        return new WindowsPreparedRenderUpload(
            scene.DisplayMode,
            shared,
            primitives.Curves,
            primitives.PointSets,
            scene.Diagnostics)
        {
            MeshDrawPolicies = scene.MeshDrawPolicies.ToArray(),
        };
    }
}
