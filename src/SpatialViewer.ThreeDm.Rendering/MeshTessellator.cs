using SpatialViewer.ThreeDm.Core;

namespace SpatialViewer.ThreeDm.Rendering;

public static class ThreeDmMeshTessellator
{
    public static ThreeDmRenderMesh Tessellate(Guid sourceObjectId, ThreeDmMeshGeometryData mesh, Guid? materialId = null, uint? colorArgb = null)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        var vertices = mesh.Vertices.Select(point => new ThreeDmRenderVertex(point.X, point.Y, point.Z)).ToArray();
        var indices = new List<int>(mesh.Faces.Count * 6);
        foreach (var face in mesh.Faces)
        {
            ValidateIndex(face.A, vertices.Length);
            ValidateIndex(face.B, vertices.Length);
            ValidateIndex(face.C, vertices.Length);

            indices.Add(face.A);
            indices.Add(face.B);
            indices.Add(face.C);

            if (face.D is int d)
            {
                ValidateIndex(d, vertices.Length);
                indices.Add(face.A);
                indices.Add(face.C);
                indices.Add(d);
            }
        }

        var result = new ThreeDmRenderMesh(sourceObjectId, vertices, indices)
        {
            MaterialId = materialId,
            ColorArgb = colorArgb,
        };

        if (mesh.Normals.Count == vertices.Length)
        {
            result = result with
            {
                Normals = mesh.Normals.Select(normal => new ThreeDmRenderNormal(normal.X, normal.Y, normal.Z)).ToArray(),
            };
        }

        if (mesh.TextureCoordinates.Count == vertices.Length)
        {
            result = result with
            {
                TextureCoordinates = mesh.TextureCoordinates
                    .Select(textureCoordinate => new ThreeDmRenderTextureCoordinate(textureCoordinate.U, textureCoordinate.V))
                    .ToArray(),
            };
        }

        return result;
    }

    private static void ValidateIndex(int index, int vertexCount)
    {
        if (index < 0 || index >= vertexCount)
        {
            throw new InvalidDataException($"Mesh face index {index} is outside vertex range [0, {vertexCount}).");
        }
    }
}
