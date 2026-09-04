namespace SpatialViewer.ThreeDm.Integration;

public static class ThreeDmKernelPackageContract
{
    public const int ManifestSchemaVersion = 1;
    public const string Runtime = "win-x64";
    public const string Framework = "net10.0";
    public const string SourceRepository = "KiYouJyo/SpatialViewer.3DMCore";

    public static IReadOnlyList<string> RequiredAssemblies { get; } =
    [
        "SpatialViewer.ThreeDm.Core.dll",
        "SpatialViewer.Formats.ThreeDm.dll",
        "SpatialViewer.Formats.ThreeDm.Rhino3dm.dll",
        "SpatialViewer.ThreeDm.Rendering.dll",
        "SpatialViewer.ThreeDm.Rendering.Windows.dll",
        "SpatialViewer.ThreeDm.Integration.dll",
    ];
}
