namespace SpatialViewer.ThreeDm.Integration;

public static class ThreeDmIntegrationContract
{
    public const int ApiVersion = 1;

    public static Version AssemblyVersion =>
        typeof(ThreeDmIntegrationContract).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
}
