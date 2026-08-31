using System.Drawing;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.FileIO;
using Rhino.Geometry;
using SpatialViewer.Formats.ThreeDm.Rhino3dm;
using SpatialViewer.ThreeDm.Core;
using SpatialViewer.ThreeDm.Rendering;

namespace SpatialViewer.ThreeDm.Tests;

public sealed class VisualFidelityImportTests
{
    [Fact]
    public async Task ImportAsyncRoundTripsLayerMaterialAndInheritedVisibility()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase5-appearance-{Guid.NewGuid():N}.3dm");
        try
        {
            Guid parentLayerId;
            Guid childLayerId;
            Guid materialId;
            Guid objectId;

            using (var model = new File3dm())
            {
                var material = new Material
                {
                    Name = "Facade Glass",
                    DiffuseColor = Color.FromArgb(255, 30, 120, 180),
                    SpecularColor = Color.FromArgb(255, 240, 245, 250),
                    EmissionColor = Color.FromArgb(255, 2, 3, 4),
                    Transparency = 0.35,
                    Shine = 72,
                    Reflectivity = 0.2,
                };
                model.AllMaterials.Add(material);
                var storedMaterial = model.AllMaterials.Single(item => item.Name == "Facade Glass");
                materialId = storedMaterial.Id;

                var parentLayer = new Layer
                {
                    Name = "Hidden Parent",
                    Color = Color.FromArgb(255, 100, 100, 100),
                    IsVisible = false,
                };
                model.AllLayers.Add(parentLayer);
                var storedParent = model.AllLayers.Single(item => item.Name == "Hidden Parent");
                parentLayerId = storedParent.Id;

                var childLayer = new Layer
                {
                    Name = "Visible Child",
                    ParentLayerId = parentLayerId,
                    Color = Color.FromArgb(255, 12, 80, 160),
                    IsVisible = true,
                    RenderMaterialIndex = storedMaterial.Index,
                };
                model.AllLayers.Add(childLayer);
                var storedChild = model.AllLayers.Single(item => item.Name == "Visible Child");
                childLayerId = storedChild.Id;

                var attributes = new ObjectAttributes
                {
                    Name = "Facade Panel",
                    LayerIndex = storedChild.Index,
                    ColorSource = ObjectColorSource.ColorFromLayer,
                    MaterialSource = ObjectMaterialSource.MaterialFromLayer,
                    ObjectColor = Color.Magenta,
                    Visible = true,
                };
                var mesh = new Mesh();
                mesh.Vertices.Add(0, 0, 0);
                mesh.Vertices.Add(1, 0, 0);
                mesh.Vertices.Add(0, 1, 0);
                mesh.Faces.AddFace(0, 1, 2);
                objectId = model.Objects.Add(mesh, attributes);

                Assert.NotEqual(Guid.Empty, objectId);
                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(
                path,
                new ThreeDmImportOptions(IncludeHiddenObjects: true));

            var parent = Assert.Single(document.Layers, item => item.Id == parentLayerId);
            var child = Assert.Single(document.Layers, item => item.Id == childLayerId);
            Assert.False(parent.IsVisible);
            Assert.True(child.IsVisible);
            Assert.Equal(parentLayerId, child.ParentLayerId);
            Assert.Equal(materialId, child.RenderMaterialId);
            Assert.Equal(0xFF0C50A0u, child.ColorArgb);

            var importedMaterial = Assert.Single(document.Materials, item => item.Id == materialId);
            Assert.Equal(0xFF1E78B4u, importedMaterial.DiffuseColorArgb);
            Assert.Equal(0.35, importedMaterial.Transparency, 12);
            Assert.Equal(0xFFF0F5FAu, importedMaterial.SpecularColorArgb);
            Assert.Equal(0xFF020304u, importedMaterial.EmissionColorArgb);
            Assert.Equal(72, importedMaterial.Shine, 12);
            Assert.Equal(0.2, importedMaterial.Reflectivity, 12);
            Assert.Null(importedMaterial.PhysicallyBased);

            var sceneObject = Assert.Single(document.Objects, item => item.Id == objectId);
            Assert.Equal(childLayerId, sceneObject.LayerId);
            Assert.Equal("ColorFromLayer", sceneObject.ColorSource);
            Assert.Equal("MaterialFromLayer", sceneObject.MaterialSource);
            Assert.False(sceneObject.IsVisible);
            Assert.True(sceneObject.SourceObjectVisible);

            var builder = new ThreeDmVisualRenderSceneBuilder();
            Assert.Empty(builder.Build(document).Meshes);

            var visibleDocument = document with
            {
                Layers = document.Layers
                    .Select(layer => layer.Id == parentLayerId ? layer with { IsVisible = true } : layer)
                    .ToArray(),
            };
            var visibleMesh = Assert.Single(builder.Build(visibleDocument).Meshes);
            Assert.Equal(objectId, visibleMesh.SourceObjectId);
            Assert.Equal(0xFF0C50A0u, visibleMesh.Appearance.ColorArgb);
            Assert.Equal(materialId, visibleMesh.Appearance.MaterialId);
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
    public async Task ImportAsyncRoundTripsPhysicallyBasedMaterialAndTextureMetadata()
    {
        var path = Path.Combine(Path.GetTempPath(), $"spatialviewer-phase5-pbr-{Guid.NewGuid():N}.3dm");
        try
        {
            Guid materialId;
            using (var model = new File3dm())
            {
                var material = new Material
                {
                    Name = "PBR Facade",
                    DiffuseColor = Color.FromArgb(255, 40, 80, 120),
                };
                material.ToPhysicallyBased();
                var pbr = material.PhysicallyBased;
                pbr.BaseColor = new Color4f(0.2f, 0.4f, 0.6f, 0.8f);
                pbr.Metallic = 0.65;
                pbr.Roughness = 0.35;
                pbr.Alpha = 0.75;
                pbr.Opacity = 0.55;
                pbr.Clearcoat = 0.45;
                pbr.ClearcoatRoughness = 0.25;

                var texture = new Texture
                {
                    FileName = "textures/facade-albedo.png",
                };
                Assert.True(material.SetTexture(texture, TextureType.Bitmap));

                model.AllMaterials.Add(material);
                var storedMaterial = model.AllMaterials.Single(item => item.Name == "PBR Facade");
                materialId = storedMaterial.Id;

                var attributes = new ObjectAttributes
                {
                    MaterialIndex = storedMaterial.Index,
                    MaterialSource = ObjectMaterialSource.MaterialFromObject,
                };
                model.Objects.Add(new Rhino.Geometry.Point(new Rhino.Geometry.Point3d(1, 2, 3)), attributes);
                Assert.True(model.Write(path, 8));
            }

            var document = await new Rhino3dmThreeDmImporter().ImportAsync(path);
            var importedMaterial = Assert.Single(document.Materials, item => item.Id == materialId);
            var pbrInfo = Assert.IsType<ThreeDmPhysicallyBasedMaterialInfo>(importedMaterial.PhysicallyBased);

            Assert.Equal(0.2, pbrInfo.BaseColorR, 5);
            Assert.Equal(0.4, pbrInfo.BaseColorG, 5);
            Assert.Equal(0.6, pbrInfo.BaseColorB, 5);
            Assert.Equal(0.8, pbrInfo.BaseColorA, 5);
            Assert.Equal(0.65, pbrInfo.Metallic, 8);
            Assert.Equal(0.35, pbrInfo.Roughness, 8);
            Assert.Equal(0.75, pbrInfo.Alpha, 8);
            Assert.Equal(0.55, pbrInfo.Opacity, 8);
            Assert.Equal(0.45, pbrInfo.Clearcoat, 8);
            Assert.Equal(0.25, pbrInfo.ClearcoatRoughness, 8);

            var importedTexture = Assert.Single(importedMaterial.Textures, item => item.TextureType == "Bitmap");
            Assert.EndsWith("facade-albedo.png", importedTexture.FileName, StringComparison.OrdinalIgnoreCase);
            Assert.True(importedTexture.IsEnabled);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
