using System.Drawing;
using Rhino;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class Rhino3dmImporterContractTests
{
    [Theory]
    [InlineData("model.3dm")]
    [InlineData("MODEL.3DM")]
    public void CanImportRecognizesThreeDmFiles(string path)
    {
        var importer = new Rhino3dmThreeDmImporter();

        Assert.True(importer.CanImport(path));
        Assert.False(importer.CanImport("model.dwg"));
    }

    [Fact]
    public async Task ImportAsyncReadsDocumentMetadataLayersMaterialsViewsAndObjectAttributes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase1-{Guid.NewGuid():N}.3dm");
        try
        {
            using (var model = new File3dm())
            {
                model.ApplicationName = "SpatialViewer Phase 1 Fixture";
                model.ApplicationUrl = "https://github.com/KiYouJyo/SpatialViewer.3DMCore";
                model.ApplicationDetails = "Document ingestion regression fixture";
                model.Settings.ModelUnitSystem = UnitSystem.Millimeters;
                model.Settings.ModelAbsoluteTolerance = 0.01;
                model.Settings.ModelRelativeTolerance = 0.01;
                model.Settings.ModelAngleToleranceRadians = Math.PI / 180.0;

                var layer = new Layer
                {
                    Name = "Architecture",
                    Color = Color.FromArgb(255, 45, 90, 135),
                    IsVisible = true,
                };
                model.AllLayers.Add(layer);
                var storedLayer = model.AllLayers.Single(item => item.Name == "Architecture");

                var material = new Material
                {
                    Name = "Concrete",
                    DiffuseColor = Color.FromArgb(255, 180, 180, 170),
                    Transparency = 0.2,
                };
                model.AllMaterials.Add(material);
                var storedMaterial = model.AllMaterials.Single(item => item.Name == "Concrete");

                var attributes = new ObjectAttributes
                {
                    Name = "Survey Origin",
                    LayerIndex = storedLayer.Index,
                    MaterialIndex = storedMaterial.Index,
                    MaterialSource = ObjectMaterialSource.MaterialFromObject,
                    ObjectColor = Color.FromArgb(255, 200, 30, 40),
                    ColorSource = ObjectColorSource.ColorFromObject,
                };
                model.Objects.Add(new Rhino.Geometry.Point(new Rhino.Geometry.Point3d(10, 20, 30)), attributes);

                model.AllNamedViews.Add(new ViewInfo { Name = "Review View" });

                Assert.True(model.Write(path, 8));
            }

            var importer = new Rhino3dmThreeDmImporter();
            var document = await importer.ImportAsync(path);

            Assert.NotNull(document.Properties);
            Assert.Equal("SpatialViewer Phase 1 Fixture", document.Properties.ApplicationName);
            Assert.Equal(nameof(UnitSystem.Millimeters), document.Properties.ModelUnitSystem);
            Assert.Equal(0.01, document.Properties.ModelAbsoluteTolerance, 8);
            Assert.Contains(document.Layers, item => item.Name == "Architecture");
            Assert.Contains(document.Materials, item => item.Name == "Concrete" && Math.Abs(item.Transparency - 0.2) < 1e-8);
            Assert.Contains(document.NamedViews, item => item.Name == "Review View");

            var sceneObject = Assert.Single(document.Objects);
            Assert.Equal("Survey Origin", sceneObject.Name);
            Assert.True(sceneObject.Bounds.IsValid);
            Assert.Equal(ThreeDmGeometryKind.Point, sceneObject.GeometryKind);
            Assert.NotNull(sceneObject.LayerId);
            Assert.NotNull(sceneObject.MaterialId);
            Assert.Empty(document.Diagnostics);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task ImportAsyncHonorsCancellationBeforeReading()
    {
        var importer = new Rhino3dmThreeDmImporter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await importer.ImportAsync("never-read.3dm", cancellationToken: cancellation.Token));
    }
}
