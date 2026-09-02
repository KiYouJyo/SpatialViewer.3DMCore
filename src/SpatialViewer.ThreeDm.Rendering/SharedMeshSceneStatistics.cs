namespace SpatialViewer.ThreeDm.Rendering;

public readonly record struct ThreeDmSharedMeshStatistics(
    int UniqueGeometryCount,
    int InstanceCount,
    long UniqueVertexCount,
    long ExpandedVertexCount,
    long UniqueIndexCount,
    long ExpandedIndexCount)
{
    public double VertexReuseRatio => ExpandedVertexCount == 0
        ? 1
        : (double)UniqueVertexCount / ExpandedVertexCount;

    public double IndexReuseRatio => ExpandedIndexCount == 0
        ? 1
        : (double)UniqueIndexCount / ExpandedIndexCount;
}

public static class ThreeDmSharedMeshSceneStatistics
{
    public static ThreeDmSharedMeshStatistics Measure(ThreeDmSharedMeshScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var geometryByIndex = scene.Geometries.ToDictionary(item => item.GeometryIndex);
        long uniqueVertices = 0;
        long uniqueIndices = 0;
        foreach (var geometry in scene.Geometries)
        {
            uniqueVertices += geometry.Vertices.Count;
            uniqueIndices += geometry.Indices.Count;
        }

        long expandedVertices = 0;
        long expandedIndices = 0;
        foreach (var instance in scene.Instances)
        {
            if (!geometryByIndex.TryGetValue(instance.GeometryIndex, out var geometry))
            {
                throw new InvalidDataException(
                    $"Shared mesh instance references missing geometry index {instance.GeometryIndex}.");
            }

            expandedVertices += geometry.Vertices.Count;
            expandedIndices += geometry.Indices.Count;
        }

        return new ThreeDmSharedMeshStatistics(
            scene.Geometries.Count,
            scene.Instances.Count,
            uniqueVertices,
            expandedVertices,
            uniqueIndices,
            expandedIndices);
    }
}
