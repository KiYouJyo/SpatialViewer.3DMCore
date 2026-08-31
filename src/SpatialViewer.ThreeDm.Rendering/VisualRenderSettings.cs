namespace SpatialViewer.ThreeDm.Rendering;

public enum ThreeDmRenderDisplayMode
{
    Shaded,
    ShadedWithEdges,
    Wireframe,
}

public sealed record ThreeDmVisualRenderSettings(
    ThreeDmRenderDisplayMode DisplayMode = ThreeDmRenderDisplayMode.ShadedWithEdges,
    ThreeDmTessellationSettings? Tessellation = null);
