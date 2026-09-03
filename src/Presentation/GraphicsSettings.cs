using Godot;

namespace EpochAeterna.Presentation;

/// <summary>Quality tiers, from "runs on anything" to "looks its best".</summary>
public enum GraphicsPreset
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Applies a graphics preset to the environment, the sun and the viewport.
/// </summary>
/// <remarks>
/// An RTS draws hundreds of units at once, so the expensive effects are exactly
/// the ones that have to be switchable. Global illumination in particular is
/// worth its cost on a screenshot and rarely worth it in a battle, which is why
/// it only appears in the High tier.
///
/// Everything is set from code rather than baked into the scene: a preset that
/// lives in the .tscn cannot be changed at runtime, and the options menu in
/// phase 6 will need exactly this.
/// </remarks>
public sealed partial class GraphicsSettings : Node
{
    public GraphicsPreset Preset { get; private set; } = GraphicsPreset.Medium;

    private WorldEnvironment? _worldEnvironment;
    private DirectionalLight3D? _sun;

    public void Attach(WorldEnvironment worldEnvironment, DirectionalLight3D sun)
    {
        _worldEnvironment = worldEnvironment;
        _sun = sun;
        Apply(Preset);
    }

    /// <summary>Steps to the next tier and returns it — bound to a key for quick comparison.</summary>
    public GraphicsPreset Cycle()
    {
        GraphicsPreset next = Preset switch
        {
            GraphicsPreset.Low => GraphicsPreset.Medium,
            GraphicsPreset.Medium => GraphicsPreset.High,
            _ => GraphicsPreset.Low,
        };

        Apply(next);
        return next;
    }

    public void Apply(GraphicsPreset preset)
    {
        Preset = preset;

        ApplyEnvironment(preset);
        ApplySun(preset);
        ApplyViewport(preset);
    }

    private void ApplyEnvironment(GraphicsPreset preset)
    {
        Godot.Environment? environment = _worldEnvironment?.Environment;
        if (environment is null) return;

        bool medium = preset >= GraphicsPreset.Medium;
        bool high = preset == GraphicsPreset.High;

        // Contact shadows in every crevice — the single biggest gain in how
        // "solid" a scene reads, and cheap enough to keep from Medium upwards.
        environment.SsaoEnabled = medium;
        environment.SsaoIntensity = 1.4f;
        environment.SsaoRadius = 1.6f;

        // Bounced light. Noticeably softer shading, noticeably more expensive.
        environment.SsilEnabled = high;
        environment.SsilIntensity = 0.6f;

        // Global illumination. Only worth it on the top tier.
        // Cascade count stays at Godot's default of four, which already covers
        // the camera's zoom range.
        environment.SdfgiEnabled = high;
        environment.SdfgiUseOcclusion = true;

        // Slight bloom on bright surfaces. Keeps metal and sunlit thatch from
        // looking flat without turning the whole image hazy.
        environment.GlowEnabled = medium;
        environment.GlowIntensity = 0.35f;
        environment.GlowBloom = 0.05f;
        environment.GlowHdrThreshold = 1.1f;

        environment.TonemapMode = Godot.Environment.ToneMapper.Aces;
        environment.TonemapWhite = 6f;
    }

    private void ApplySun(GraphicsPreset preset)
    {
        if (_sun is null) return;

        _sun.ShadowEnabled = preset >= GraphicsPreset.Medium;

        // More splits means sharper shadows near the camera at the cost of extra
        // passes. Two is enough for the zoom range an RTS camera actually uses.
        _sun.DirectionalShadowMode = preset == GraphicsPreset.High
            ? DirectionalLight3D.ShadowMode.Parallel4Splits
            : DirectionalLight3D.ShadowMode.Parallel2Splits;

        _sun.DirectionalShadowMaxDistance = preset == GraphicsPreset.High ? 260f : 180f;
        _sun.ShadowBlur = preset == GraphicsPreset.High ? 1.0f : 1.4f;
    }

    private void ApplyViewport(GraphicsPreset preset)
    {
        Viewport viewport = GetViewport();

        viewport.Msaa3D = preset switch
        {
            GraphicsPreset.Low => Viewport.Msaa.Disabled,
            GraphicsPreset.Medium => Viewport.Msaa.Msaa2X,
            _ => Viewport.Msaa.Msaa4X,
        };

        // Temporal anti-aliasing smooths the shimmering that thin geometry —
        // spear shafts, roof ridges, selection rings — produces in motion.
        viewport.UseTaa = preset >= GraphicsPreset.Medium;

        viewport.ScreenSpaceAA = preset >= GraphicsPreset.Medium
            ? Viewport.ScreenSpaceAAEnum.Fxaa
            : Viewport.ScreenSpaceAAEnum.Disabled;
    }
}
