using SpatialViewer.ThreeDm.Integration;

namespace SpatialViewer.ThreeDm.Integration.Tests;

public sealed class KernelPackageContractTests
{
    [Fact]
    public void PackageContractMatchesStableHostBoundary()
    {
        Assert.Equal(1, ThreeDmKernelPackageContract.ManifestSchemaVersion);
        Assert.Equal("win-x64", ThreeDmKernelPackageContract.Runtime);
        Assert.Equal("net10.0", ThreeDmKernelPackageContract.Framework);
        Assert.Contains("SpatialViewer.ThreeDm.Integration.dll", ThreeDmKernelPackageContract.RequiredAssemblies);
        Assert.True(ThreeDmIntegrationContract.SupportsHost(new Version(1, 0, 0)));
    }
}
