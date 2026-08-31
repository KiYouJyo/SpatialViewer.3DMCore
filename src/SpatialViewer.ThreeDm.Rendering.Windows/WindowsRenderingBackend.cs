namespace SpatialViewer.ThreeDm.Rendering.Windows;

public interface IWindowsThreeDmRenderingBackend
{
    string Name { get; }
}

public sealed class WindowsThreeDmRenderingBackend : IWindowsThreeDmRenderingBackend
{
    public string Name => "SpatialViewer.3DMCore.Windows";
}
