using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Vortice.Wpf;

namespace BBGameLauncher.Controls;

/// <summary>
/// Hardware cube renderer. Unlike a WPF bitmap effect, this samples a texture
/// cube with reflected and refracted ray directions for every rendered pixel.
/// </summary>
public sealed class Direct3DGlassCubeSurface : DrawingSurface
{
    private GlassCubeFrame[] _cubes = [];
    private ID3D11Buffer? _vertices;
    private ID3D11Buffer? _frameConstants;
    private ID3D11Buffer? _objectConstants;
    private ID3D11VertexShader? _vertexShader;
    private ID3D11PixelShader? _pixelShader;
    private ID3D11PixelShader? _backPositionShader;
    private ID3D11InputLayout? _inputLayout;
    private ID3D11Buffer? _stars;
    private ID3D11VertexShader? _backgroundVertexShader;
    private ID3D11PixelShader? _backgroundPixelShader;
    private ID3D11PixelShader? _compositePixelShader;
    private ID3D11VertexShader? _starVertexShader;
    private ID3D11PixelShader? _starPixelShader;
    private ID3D11InputLayout? _starInputLayout;
    private ID3D11BlendState? _starBlend;
    private ID3D11Texture2D? _sceneColor;
    private ID3D11RenderTargetView? _sceneColorView;
    private ID3D11ShaderResourceView? _sceneColorResource;
    private ID3D11SamplerState? _sceneSampler;
    private ID3D11Texture2D? _environment;
    private ID3D11ShaderResourceView? _environmentView;
    private ID3D11SamplerState? _environmentSampler;
    private byte[]? _backdropPixels;
    private int _backdropWidth;
    private int _backdropHeight;
    private bool _backdropDirty;
    private ID3D11Texture2D? _backdrop;
    private ID3D11ShaderResourceView? _backdropView;
    private ID3D11SamplerState? _backdropSampler;
    private ID3D11Texture2D? _exitPosition;
    private ID3D11RenderTargetView? _exitPositionView;
    private ID3D11ShaderResourceView? _exitPositionResource;
    private ID3D11Texture2D? _exitDepth;
    private ID3D11DepthStencilView? _exitDepthView;
    private ID3D11SamplerState? _exitSampler;
    private ID3D11BlendState? _glassBlend;
    private ID3D11RasterizerState? _rasterizer;

    public Direct3DGlassCubeSurface()
    {
        AlwaysRefresh = true;
        LoadContent += OnLoadContent;
        Draw += OnDraw;
        UnloadContent += OnUnloadContent;
    }

    public void SetCubes(GlassCubeFrame[] cubes)
    {
        _cubes = cubes;
        Invalidate();
    }

    public void SetBackdrop(byte[] pixels, int width, int height)
    {
        _backdropPixels = pixels;
        _backdropWidth = width;
        _backdropHeight = height;
        _backdropDirty = true;
    }

    private void OnLoadContent(object? sender, DrawingSurfaceEventArgs e)
    {
        var vertices = CreateCubeVertices();
        _vertices = e.Device.CreateBuffer(vertices, BindFlags.VertexBuffer);
        _stars = e.Device.CreateBuffer(CreateStarVertices(), BindFlags.VertexBuffer);
        _frameConstants = e.Device.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<FrameConstants>(), BindFlags.ConstantBuffer));
        _objectConstants = e.Device.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<ObjectConstants>(), BindFlags.ConstantBuffer));

        var shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "GlassCube.hlsl");
        var shaderSource = File.ReadAllText(shaderPath);
        var vertexBytecode = Compiler.Compile(shaderSource, "VSMain", shaderPath, "vs_4_0");
        var pixelBytecode = Compiler.Compile(shaderSource, "PSMain", shaderPath, "ps_4_0");
        _vertexShader = e.Device.CreateVertexShader(vertexBytecode.Span);
        _pixelShader = e.Device.CreatePixelShader(pixelBytecode.Span);
        _backPositionShader = e.Device.CreatePixelShader(Compiler.Compile(shaderSource, "PSBack", shaderPath, "ps_4_0").Span);
        _backgroundVertexShader = e.Device.CreateVertexShader(Compiler.Compile(shaderSource, "VSFullscreen", shaderPath, "vs_4_0").Span);
        _backgroundPixelShader = e.Device.CreatePixelShader(Compiler.Compile(shaderSource, "PSBackground", shaderPath, "ps_4_0").Span);
        _compositePixelShader = e.Device.CreatePixelShader(Compiler.Compile(shaderSource, "PSComposite", shaderPath, "ps_4_0").Span);
        _starVertexShader = e.Device.CreateVertexShader(Compiler.Compile(shaderSource, "VSStar", shaderPath, "vs_4_0").Span);
        _starPixelShader = e.Device.CreatePixelShader(Compiler.Compile(shaderSource, "PSStar", shaderPath, "ps_4_0").Span);
        _inputLayout = e.Device.CreateInputLayout(
        [
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0)
        ], vertexBytecode.Span);
        _starInputLayout = e.Device.CreateInputLayout(
        [
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32_Float, 12, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
            new InputElementDescription("TEXCOORD", 1, Format.R32_Float, 32, 0)
        ], Compiler.Compile(shaderSource, "VSStar", shaderPath, "vs_4_0").Span);

        _environment = CreateEnvironmentMap(e.Device, e.Context);
        _environmentView = e.Device.CreateShaderResourceView(_environment);
        _environmentSampler = e.Device.CreateSamplerState(SamplerDescription.LinearClamp);
        _backdropSampler = e.Device.CreateSamplerState(SamplerDescription.LinearClamp);
        _sceneSampler = e.Device.CreateSamplerState(SamplerDescription.LinearClamp);
        _exitSampler = e.Device.CreateSamplerState(SamplerDescription.PointClamp);
        _glassBlend = e.Device.CreateBlendState(BlendDescription.AlphaBlend);
        _starBlend = e.Device.CreateBlendState(BlendDescription.Additive);
        _rasterizer = e.Device.CreateRasterizerState(RasterizerDescription.CullNone);
    }

    private void OnDraw(object? sender, DrawEventArgs e)
    {
        if (_vertices == null || _stars == null || _frameConstants == null || _objectConstants == null || _environmentView == null ||
            _environmentSampler == null || _backgroundVertexShader == null || _backgroundPixelShader == null ||
            _compositePixelShader == null || _starVertexShader == null || _starPixelShader == null || _starInputLayout == null ||
            _sceneSampler == null ||
            _vertexShader == null || _pixelShader == null || _backPositionShader == null || _inputLayout == null ||
            _exitSampler == null)
            return;

        var width = Math.Max(1, e.Surface.TextureWidth);
        var height = Math.Max(1, e.Surface.TextureHeight);
        const float fieldOfView = MathF.PI / 4f;
        var cameraDistance = height / (2f * MathF.Tan(fieldOfView / 2f));
        var camera = new Vector3(0, 0, -cameraDistance);
        var view = Matrix4x4.CreateLookAt(camera, Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, width / (float)height, 1f, 8000f);
        EnsureSceneTarget(e.Device, width, height);
        EnsureExitTargets(e.Device, width, height);
        e.Context.UpdateSubresource(new FrameConstants
        {
            ViewProjection = view * projection,
            Camera = new Vector4(camera, 0),
            Viewport = new Vector4(width, height, 1f / width, 1f / height)
        }, _frameConstants);

        // The complete space scene is rendered in D3D before any cube pass.
        // This texture is both the visible background and the glass refraction source.
        e.Context.PSSetShaderResource(1, null);
        e.Context.PSSetShaderResource(3, null);
        e.Context.OMSetRenderTargets(_sceneColorView, null);
        e.Context.ClearRenderTargetView(_sceneColorView!, new Color4(0.003f, 0.012f, 0.035f, 1));
        e.Context.OMSetBlendState(null);
        e.Context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        e.Context.IASetInputLayout(null);
        e.Context.VSSetShader(_backgroundVertexShader);
        e.Context.PSSetShader(_backgroundPixelShader);
        e.Context.Draw(3, 0);
        e.Context.OMSetBlendState(_starBlend);
        e.Context.IASetInputLayout(_starInputLayout);
        e.Context.IASetVertexBuffer(0, _stars, (uint)Marshal.SizeOf<StarVertex>());
        e.Context.VSSetConstantBuffer(0, _frameConstants);
        e.Context.VSSetShader(_starVertexShader);
        e.Context.PSSetShader(_starPixelShader);
        e.Context.Draw(1440, 0);

        e.Context.OMSetRenderTargets(e.Surface.ColorTextureView!, e.Surface.DepthStencilView);
        e.Context.ClearRenderTargetView(e.Surface.ColorTextureView!, new Color4(0, 0, 0, 1));
        if (e.Surface.DepthStencilView != null) e.Context.ClearDepthStencilView(e.Surface.DepthStencilView, DepthStencilClearFlags.Depth, 1, 0);
        e.Context.OMSetBlendState(null);
        e.Context.IASetInputLayout(null);
        e.Context.VSSetShader(_backgroundVertexShader);
        e.Context.PSSetShader(_compositePixelShader);
        e.Context.PSSetShaderResource(3, _sceneColorResource);
        e.Context.PSSetSampler(3, _sceneSampler);
        e.Context.Draw(3, 0);

        e.Context.OMSetBlendState(_glassBlend);
        e.Context.OMSetDepthStencilState(null);
        e.Context.RSSetState(_rasterizer);
        e.Context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        e.Context.IASetInputLayout(_inputLayout);
        e.Context.IASetVertexBuffer(0, _vertices, (uint)Marshal.SizeOf<CubeVertex>());
        e.Context.VSSetShader(_vertexShader);
        e.Context.PSSetShader(_pixelShader);
        e.Context.VSSetConstantBuffer(0, _frameConstants);
        e.Context.VSSetConstantBuffer(1, _objectConstants);
        e.Context.PSSetConstantBuffer(1, _objectConstants);
        e.Context.PSSetShaderResource(0, _environmentView);
        e.Context.PSSetSampler(0, _environmentSampler);
        e.Context.PSSetShaderResource(1, _sceneColorResource);
        e.Context.PSSetSampler(1, _sceneSampler);
        e.Context.PSSetSampler(2, _exitSampler);

        // Keeping one stable, frame-level order avoids the face-bucket pop the
        // old WPF implementation exhibited when rotating through a face plane.
        foreach (var cube in _cubes.OrderByDescending(cube => cube.Size))
        {
            if (cube.Size <= .01f || cube.Opacity <= .001f) continue;
            var position = new Vector3(cube.X - width * .5f, height * .5f - cube.Y, 0);
            var rotation = Matrix4x4.CreateRotationX(cube.AngleX) * Matrix4x4.CreateRotationY(cube.AngleY) * Matrix4x4.CreateRotationZ(cube.AngleZ);
            var world = Matrix4x4.CreateScale(cube.Size) * rotation * Matrix4x4.CreateTranslation(position);
            Matrix4x4.Invert(world, out var inverseWorld);
            e.Context.UpdateSubresource(new ObjectConstants
            {
                World = world,
                InverseWorld = inverseWorld,
                Material = new Vector4(cube.Selected ? 1 : 0, cube.Opacity, cube.Size, 0)
            }, _objectConstants);

            // Pass 1: record the actual rear surface for this cube. The front
            // pass samples this texture, rather than estimating a box exit from
            // interpolated data that has already been projected to the screen.
            e.Context.PSSetShaderResource(2, null);
            e.Context.OMSetRenderTargets(_exitPositionView, _exitDepthView);
            e.Context.ClearRenderTargetView(_exitPositionView!, new Color4(0, 0, 0, 0));
            e.Context.ClearDepthStencilView(_exitDepthView!, DepthStencilClearFlags.Depth, 1, 0);
            e.Context.OMSetBlendState(null);
            e.Context.OMSetDepthStencilState(null);
            e.Context.PSSetShader(_backPositionShader);
            e.Context.Draw(36, 0);

            // Pass 2: refract through the front surface into the recorded rear
            // surface, then alpha-composite the resulting solid glass volume.
            e.Context.OMSetRenderTargets(e.Surface.ColorTextureView!, e.Surface.DepthStencilView);
            e.Context.OMSetBlendState(_glassBlend);
            e.Context.OMSetDepthStencilState(null);
            e.Context.PSSetShader(_pixelShader);
            e.Context.PSSetShaderResource(2, _exitPositionResource);
            e.Context.Draw(36, 0);
        }
    }

    private void OnUnloadContent(object? sender, DrawingSurfaceEventArgs e)
    {
        _vertices?.Dispose(); _vertices = null;
        _stars?.Dispose(); _stars = null;
        _frameConstants?.Dispose(); _frameConstants = null;
        _objectConstants?.Dispose(); _objectConstants = null;
        _vertexShader?.Dispose(); _vertexShader = null;
        _pixelShader?.Dispose(); _pixelShader = null;
        _backPositionShader?.Dispose(); _backPositionShader = null;
        _inputLayout?.Dispose(); _inputLayout = null;
        _backgroundVertexShader?.Dispose(); _backgroundVertexShader = null;
        _backgroundPixelShader?.Dispose(); _backgroundPixelShader = null;
        _compositePixelShader?.Dispose(); _compositePixelShader = null;
        _starVertexShader?.Dispose(); _starVertexShader = null;
        _starPixelShader?.Dispose(); _starPixelShader = null;
        _starInputLayout?.Dispose(); _starInputLayout = null;
        _starBlend?.Dispose(); _starBlend = null;
        _sceneSampler?.Dispose(); _sceneSampler = null;
        _sceneColorResource?.Dispose(); _sceneColorResource = null;
        _sceneColorView?.Dispose(); _sceneColorView = null;
        _sceneColor?.Dispose(); _sceneColor = null;
        _environmentSampler?.Dispose(); _environmentSampler = null;
        _environmentView?.Dispose(); _environmentView = null;
        _environment?.Dispose(); _environment = null;
        _backdropSampler?.Dispose(); _backdropSampler = null;
        _backdropView?.Dispose(); _backdropView = null;
        _backdrop?.Dispose(); _backdrop = null;
        _exitSampler?.Dispose(); _exitSampler = null;
        _exitPositionResource?.Dispose(); _exitPositionResource = null;
        _exitPositionView?.Dispose(); _exitPositionView = null;
        _exitPosition?.Dispose(); _exitPosition = null;
        _exitDepthView?.Dispose(); _exitDepthView = null;
        _exitDepth?.Dispose(); _exitDepth = null;
        _glassBlend?.Dispose(); _glassBlend = null;
        _rasterizer?.Dispose(); _rasterizer = null;
    }

    private static ID3D11Texture2D CreateEnvironmentMap(ID3D11Device device, ID3D11DeviceContext context)
    {
        const int size = 128;
        var texture = device.CreateTexture2D(new Texture2DDescription
        {
            Width = size, Height = size, ArraySize = 6, MipLevels = 1,
            Format = Format.B8G8R8A8_UNorm, BindFlags = BindFlags.ShaderResource,
            Usage = ResourceUsage.Default, CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.TextureCube, SampleDescription = new SampleDescription(1, 0)
        });
        for (var face = 0; face < 6; face++)
        {
            var pixels = new uint[size * size];
            var random = new Random(9601 + face * 37);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)(size - 1) * 2 - 1;
                var v = y / (float)(size - 1) * 2 - 1;
                // A quiet sky-like environment: point lights stay localized in
                // reflections instead of becoming broad bands across a face.
                var haze = MathF.Exp(-((u - (face == 2 ? -.32f : .44f)) * (u - (face == 2 ? -.32f : .44f)) + (v + .16f) * (v + .16f)) * 8f);
                var star = random.NextDouble() > .9987 ? 1f : 0f;
                var red = (byte)Math.Clamp(2 + haze * 8 + star * 176, 0, 255);
                var green = (byte)Math.Clamp(7 + haze * 25 + star * 198, 0, 255);
                var blue = (byte)Math.Clamp(18 + haze * 52 + star * 220, 0, 255);
                pixels[y * size + x] = 0xff000000u | ((uint)red << 16) | ((uint)green << 8) | blue;
            }
            context.UpdateSubresource(pixels, texture, (uint)face, size * sizeof(uint));
        }
        return texture;
    }

    private void UploadBackdrop(ID3D11Device device, ID3D11DeviceContext context)
    {
        var width = Math.Max(1, _backdropWidth);
        var height = Math.Max(1, _backdropHeight);
        if (_backdrop == null || _backdrop.Description.Width != (uint)width || _backdrop.Description.Height != (uint)height)
        {
            _backdropView?.Dispose();
            _backdrop?.Dispose();
            _backdrop = device.CreateTexture2D(new Texture2DDescription
            {
                Width = (uint)width, Height = (uint)height, ArraySize = 1, MipLevels = 1,
                Format = Format.B8G8R8A8_UNorm, BindFlags = BindFlags.ShaderResource,
                Usage = ResourceUsage.Default, CPUAccessFlags = CpuAccessFlags.None,
                SampleDescription = new SampleDescription(1, 0)
            });
            _backdropView = device.CreateShaderResourceView(_backdrop);
        }

        var pixels = _backdropPixels ?? new byte[] { 0, 0, 0, 255 };
        context.UpdateSubresource(pixels, _backdrop, 0, (uint)(width * 4));
        _backdropDirty = false;
    }

    private void EnsureExitTargets(ID3D11Device device, int width, int height)
    {
        if (_exitPosition != null && _exitPosition.Description.Width == (uint)width && _exitPosition.Description.Height == (uint)height)
            return;

        _exitPositionResource?.Dispose(); _exitPositionResource = null;
        _exitPositionView?.Dispose(); _exitPositionView = null;
        _exitPosition?.Dispose(); _exitPosition = null;
        _exitDepthView?.Dispose(); _exitDepthView = null;
        _exitDepth?.Dispose(); _exitDepth = null;

        _exitPosition = device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width, Height = (uint)height, ArraySize = 1, MipLevels = 1,
            Format = Format.R16G16B16A16_Float, BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            Usage = ResourceUsage.Default, SampleDescription = new SampleDescription(1, 0)
        });
        _exitPositionView = device.CreateRenderTargetView(_exitPosition);
        _exitPositionResource = device.CreateShaderResourceView(_exitPosition);
        _exitDepth = device.CreateTexture2D(Format.D32_Float, (uint)width, (uint)height, 1, 1, null, BindFlags.DepthStencil);
        _exitDepthView = device.CreateDepthStencilView(_exitDepth, new DepthStencilViewDescription(_exitDepth, DepthStencilViewDimension.Texture2D));
    }

    private void EnsureSceneTarget(ID3D11Device device, int width, int height)
    {
        if (_sceneColor != null && _sceneColor.Description.Width == (uint)width && _sceneColor.Description.Height == (uint)height)
            return;

        _sceneColorResource?.Dispose(); _sceneColorResource = null;
        _sceneColorView?.Dispose(); _sceneColorView = null;
        _sceneColor?.Dispose(); _sceneColor = null;
        _sceneColor = device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width, Height = (uint)height, ArraySize = 1, MipLevels = 1,
            Format = Format.B8G8R8A8_UNorm, BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            Usage = ResourceUsage.Default, SampleDescription = new SampleDescription(1, 0)
        });
        _sceneColorView = device.CreateRenderTargetView(_sceneColor);
        _sceneColorResource = device.CreateShaderResourceView(_sceneColor);
    }

    private static CubeVertex[] CreateCubeVertices()
    {
        var p = new[]
        {
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(1,1,-1), new Vector3(-1,1,-1),
            new Vector3(-1,-1,1), new Vector3(1,-1,1), new Vector3(1,1,1), new Vector3(-1,1,1)
        };
        var faces = new[]
        {
            (new[] { 0, 1, 2, 3 }, new Vector3(0,0,-1)), (new[] { 5, 4, 7, 6 }, new Vector3(0,0,1)),
            (new[] { 4, 0, 3, 7 }, new Vector3(-1,0,0)), (new[] { 1, 5, 6, 2 }, new Vector3(1,0,0)),
            (new[] { 3, 2, 6, 7 }, new Vector3(0,1,0)), (new[] { 4, 5, 1, 0 }, new Vector3(0,-1,0))
        };
        return faces.SelectMany(face => new[] { face.Item1[0], face.Item1[1], face.Item1[2], face.Item1[0], face.Item1[2], face.Item1[3] }
            .Select(index => new CubeVertex { Position = p[index], Normal = face.Item2 })).ToArray();
    }

    private static StarVertex[] CreateStarVertices()
    {
        var random = new Random(4821);
        var vertices = new List<StarVertex>(1440);
        var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        for (var i = 0; i < 240; i++)
        {
            var depth = 120f + (float)random.NextDouble() * 4600f;
            var spread = 820f + depth * .92f;
            var position = new Vector3(((float)random.NextDouble() * 2 - 1) * spread,
                ((float)random.NextDouble() * 2 - 1) * spread * .55f, depth);
            var shade = .55f + (float)random.NextDouble() * .45f;
            var color = new Vector3(.45f * shade, .72f * shade, shade);
            var size = 1.4f + depth * .0011f + (float)random.NextDouble() * 2f;
            foreach (var corner in corners)
                vertices.Add(new StarVertex { Position = position, Color = color, Corner = corner, Size = size });
        }
        return vertices.ToArray();
    }

    [StructLayout(LayoutKind.Sequential)] private struct CubeVertex { public Vector3 Position; public Vector3 Normal; }
    [StructLayout(LayoutKind.Sequential)] private struct StarVertex { public Vector3 Position; public Vector3 Color; public Vector2 Corner; public float Size; }
    [StructLayout(LayoutKind.Sequential)] private struct FrameConstants { public Matrix4x4 ViewProjection; public Vector4 Camera; public Vector4 Viewport; }
    [StructLayout(LayoutKind.Sequential)] private struct ObjectConstants { public Matrix4x4 World; public Matrix4x4 InverseWorld; public Vector4 Material; }
}

public readonly record struct GlassCubeFrame(float X, float Y, float Size, float AngleX, float AngleY, float AngleZ, bool Selected, float Opacity);
