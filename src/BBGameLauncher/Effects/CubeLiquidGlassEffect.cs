using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace BBGameLauncher.Effects;

/// <summary>Backdrop-refraction effect used by the transparent menu cubes.</summary>
public sealed class CubeLiquidGlassEffect : ShaderEffect
{
    private static readonly PixelShader Shader = new()
    {
        UriSource = new Uri("pack://application:,,,/BBGameLauncher;component/Shaders/CubeLiquidGlass.ps", UriKind.Absolute)
    };

    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(nameof(Input), typeof(CubeLiquidGlassEffect), 0);
    public static readonly DependencyProperty TextureSizeProperty = DependencyProperty.Register(
        nameof(TextureSize), typeof(Point), typeof(CubeLiquidGlassEffect),
        new UIPropertyMetadata(new Point(1, 1), PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty GlassCenterProperty = DependencyProperty.Register(
        nameof(GlassCenter), typeof(Point), typeof(CubeLiquidGlassEffect),
        new UIPropertyMetadata(new Point(), PixelShaderConstantCallback(1)));
    public static readonly DependencyProperty GlassSizeProperty = DependencyProperty.Register(
        nameof(GlassSize), typeof(Point), typeof(CubeLiquidGlassEffect),
        new UIPropertyMetadata(new Point(80, 80), PixelShaderConstantCallback(2)));
    public static readonly DependencyProperty BlurIntensityProperty = DependencyProperty.Register(
        nameof(BlurIntensity), typeof(float), typeof(CubeLiquidGlassEffect),
        new UIPropertyMetadata(.72f, PixelShaderConstantCallback(3)));

    public CubeLiquidGlassEffect()
    {
        PixelShader = Shader;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(TextureSizeProperty);
        UpdateShaderValue(GlassCenterProperty);
        UpdateShaderValue(GlassSizeProperty);
        UpdateShaderValue(BlurIntensityProperty);
    }

    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }
    public Point TextureSize { get => (Point)GetValue(TextureSizeProperty); set => SetValue(TextureSizeProperty, value); }
    public Point GlassCenter { get => (Point)GetValue(GlassCenterProperty); set => SetValue(GlassCenterProperty, value); }
    public Point GlassSize { get => (Point)GetValue(GlassSizeProperty); set => SetValue(GlassSizeProperty, value); }
    public float BlurIntensity { get => (float)GetValue(BlurIntensityProperty); set => SetValue(BlurIntensityProperty, value); }
}
