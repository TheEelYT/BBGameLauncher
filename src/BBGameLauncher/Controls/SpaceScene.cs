using System.Windows;
using System.Windows.Media;

namespace BBGameLauncher.Controls;

/// <summary>Procedural starfield with a Direct3D-backed liquid-glass cube layer.</summary>
public sealed class SpaceScene : FrameworkElement
{
    private readonly List<Cube> _cubes = [];
    private readonly VisualCollection _visuals;
    private readonly Direct3DGlassCubeSurface _glassSurface;
    private TimeSpan _lastFrame;
    private double _elapsed;
    private double _motionElapsed;
    private int _selectedCube;
    private int _pendingCubeCount;
    private int _pendingSelectedCube;
    private bool _hasPendingCubeSet;
    private CubeMotion _motion;
    private bool _forwardTransition;

    public SpaceScene()
    {
        _visuals = new VisualCollection(this);
        _glassSurface = new Direct3DGlassCubeSurface { IsHitTestVisible = false };
        _visuals.Add(_glassSurface);
        Loaded += (_, _) => CompositionTarget.Rendering += RenderFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= RenderFrame;
        ClipToBounds = true;
        SetMenuCubes(4, 0);
    }

    public void SetMenuCubes(int menuItemCount, int selectedIndex)
    {
        menuItemCount = Math.Max(0, menuItemCount);
        var rebuilding = _cubes.Count != menuItemCount;
        var selectionChanged = selectedIndex != _selectedCube;
        _selectedCube = Math.Clamp(selectedIndex, 0, Math.Max(0, menuItemCount - 1));
        if (rebuilding) RebuildCubes(menuItemCount);
        if ((selectionChanged || rebuilding) && _cubes.Count > 0) AddFlick(_cubes[_selectedCube]);
        InvalidateVisual();
    }

    public void BeginCubeTransition(bool forward, int incomingCubeCount, int incomingSelectedIndex)
    {
        _forwardTransition = forward;
        _pendingCubeCount = Math.Max(0, incomingCubeCount);
        _pendingSelectedCube = Math.Clamp(incomingSelectedIndex, 0, Math.Max(0, _pendingCubeCount - 1));
        _hasPendingCubeSet = true;
        _motion = CubeMotion.Exiting;
        _motionElapsed = 0;
    }

    protected override int VisualChildrenCount => _visuals.Count;
    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        _glassSurface.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        _glassSurface.SetCubes(_cubes.Select((cube, index) =>
        {
            var (center, size, opacity) = GetCubePresentation(cube, index == _selectedCube);
            return new GlassCubeFrame((float)center.X, (float)center.Y, (float)size,
                (float)cube.AngleX, (float)cube.AngleY, (float)cube.AngleZ,
                index == _selectedCube, (float)opacity);
        }).ToArray());

    }

    private (Point Center, double Size, double Opacity) GetCubePresentation(Cube cube, bool highlighted)
    {
        var transition = Math.Clamp(_motionElapsed / .42, 0, 1);
        var eased = 1 - Math.Pow(1 - transition, 3);
        var opacity = _motion switch { CubeMotion.Exiting => 1 - eased, CubeMotion.Entering => eased, _ => 1 };
        var bob = Math.Sin(_elapsed * cube.Speed + cube.Phase) * 18;
        var center = new Point(cube.X * RenderSize.Width + Math.Cos(_elapsed * cube.Speed + cube.Phase) * 18,
            cube.Y * RenderSize.Height + bob);
        var zoom = _motion switch
        {
            CubeMotion.Exiting when _forwardTransition => 1 + eased * 7,
            CubeMotion.Exiting => 1 - eased * .94,
            CubeMotion.Entering when _forwardTransition => .045 + eased * .955,
            CubeMotion.Entering => 7 * (1 - eased) + eased,
            _ => 1
        };
        var size = cube.Size * (1 + Math.Sin(_elapsed * .6 + cube.Phase) * .08) * (highlighted ? 1.12 : 1) * zoom;
        return (center, Math.Max(0, size), opacity);
    }

    private void RebuildCubes(int menuItemCount)
    {
        _cubes.Clear();
        var placements = new[] { (.66, .19, 46d), (.85, .36, 36d), (.78, .61, 44d), (.61, .76, 32d), (.90, .72, 54d), (.50, .22, 28d), (.72, .88, 31d) };
        for (var i = 0; i < menuItemCount; i++)
        {
            var p = placements[i % placements.Length];
            _cubes.Add(new Cube(p.Item1, p.Item2, p.Item3, i * .73, .24 + (i % 4) * .05));
        }
    }

    private static void AddFlick(Cube cube) { cube.SpinX += 2.4; cube.SpinY += 4.6; cube.SpinZ += .85; }

    private static void UpdateCubeRotations(IEnumerable<Cube> cubes, double delta)
    {
        var damping = Math.Exp(-delta * 1.4);
        foreach (var cube in cubes)
        {
            cube.AngleX += (.075 + cube.Speed * .045 + cube.SpinX) * delta;
            cube.AngleY += (.052 + cube.Speed * .035 + cube.SpinY) * delta;
            cube.AngleZ += (.014 + cube.Speed * .012 + cube.SpinZ) * delta;
            cube.SpinX *= damping; cube.SpinY *= damping; cube.SpinZ *= damping;
        }
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        if (e is RenderingEventArgs args)
        {
            if (_lastFrame != default)
            {
                var delta = Math.Min(.05, (args.RenderingTime - _lastFrame).TotalSeconds);
                _elapsed += delta;
                UpdateCubeRotations(_cubes, delta);
                if (_motion != CubeMotion.None)
                {
                    _motionElapsed += delta;
                    if (_motionElapsed >= .42 && _motion == CubeMotion.Exiting)
                    {
                        if (_hasPendingCubeSet)
                        {
                            _selectedCube = _pendingSelectedCube; RebuildCubes(_pendingCubeCount);
                            if (_cubes.Count > 0) AddFlick(_cubes[_selectedCube]);
                            _hasPendingCubeSet = false;
                        }
                        _motion = CubeMotion.Entering; _motionElapsed = 0;
                    }
                    else if (_motionElapsed >= .42) _motion = CubeMotion.None;
                }
            }
            _lastFrame = args.RenderingTime;
        }
        InvalidateVisual();
    }

    private sealed class Cube
    {
        public Cube(double x, double y, double size, double phase, double speed)
        { X = x; Y = y; Size = size; Phase = phase; Speed = speed; AngleX = phase * .8; AngleY = phase * 1.7; AngleZ = phase * .25; }
        public double X { get; } public double Y { get; } public double Size { get; } public double Phase { get; } public double Speed { get; }
        public double AngleX { get; set; } public double AngleY { get; set; } public double AngleZ { get; set; }
        public double SpinX { get; set; } public double SpinY { get; set; } public double SpinZ { get; set; }
    }
    private enum CubeMotion { None, Exiting, Entering }
}
