namespace SpatialViewer.Formats.ThreeDm;

public sealed record ThreeDmLayer(
    Guid Id,
    string Name,
    Guid? ParentLayerId,
    bool IsVisible,
    bool IsLocked);

public sealed record ThreeDmMaterial(
    Guid Id,
    string Name,
    uint? BaseColorArgb,
    double Transparency);

public sealed record ThreeDmObjectAttributes(
    Guid Id,
    string? Name,
    Guid? LayerId,
    Guid? MaterialId,
    bool IsVisible);

public sealed record ThreeDmSourceDocument(
    int ArchiveVersion,
    string? ApplicationName,
    IReadOnlyList<ThreeDmLayer> Layers,
    IReadOnlyList<ThreeDmMaterial> Materials,
    IReadOnlyList<ThreeDmObjectAttributes> Objects);
