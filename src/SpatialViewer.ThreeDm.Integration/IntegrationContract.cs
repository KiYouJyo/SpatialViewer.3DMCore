namespace SpatialViewer.ThreeDm.Integration;

public static class ThreeDmIntegrationContract
{
    public const string Name = "SpatialViewer.ThreeDmHost";

    public const int ApiVersion = 1;

    public static Version ContractVersion { get; } = new(1, 0, 0);

    public static Version MinimumCompatibleHostVersion { get; } = new(1, 0, 0);

    public static Version MaximumCompatibleHostVersionExclusive { get; } = new(2, 0, 0);

    public static Version AssemblyVersion =>
        typeof(ThreeDmIntegrationContract).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    public static bool SupportsHost(Version hostVersion)
    {
        ArgumentNullException.ThrowIfNull(hostVersion);
        return hostVersion >= MinimumCompatibleHostVersion &&
               hostVersion < MaximumCompatibleHostVersionExclusive;
    }
}
