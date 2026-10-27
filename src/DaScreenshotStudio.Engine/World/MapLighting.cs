using DaScreenshotStudio.Assets.Maps;
using DaScreenshotStudio.Assets.Metadata;
using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Core;

namespace DaScreenshotStudio.Engine.World;

public interface IMapLightMasks
{
    HeaLightMask? LoadLightMask(int mapId);
}

public sealed class MapLighting
{
    public const string MetafileName = "Light";

    private readonly WorldLighting _lighting;

    public MapLighting(WorldLighting lighting)
    {
        ArgumentNullException.ThrowIfNull(lighting);
        _lighting = lighting;
    }

    public LightProfileTable Profiles { get; private set; } = LightProfileTable.Empty;

    public bool ProfilesLoaded { get; private set; }

    public void SetProfiles(LightProfileTable profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        if (ProfilesLoaded)
        {
            return;
        }

        ProfilesLoaded = true;
        Profiles = profiles;
        Update();
    }

    public IMapLightMasks? Masks { get; set; }

    public PixelPacking Packing { get; set; } = PixelPacking.Rgb565;

    public int MapId { get; private set; }

    public byte TimeStep { get; private set; }

    public MapFlags Mode { get; private set; }

    public int SetupCount { get; private set; }

    public int ProfileTime => LightProfileTable.ToProfileTime(TimeStep);

    public bool HasProfile { get; private set; }

    public void SetTime(byte timeStep)
    {
        TimeStep = timeStep;
        Update();
    }

    public void SetMap(int mapId, MapFlags flags)
    {
        MapId = mapId;
        Mode = flags.GetMode();
        SetupCount++;

        if (Mode == MapFlags.Darkness)
        {
            _lighting.AmbientIntensity = 0;
            _lighting.AmbientColor = 0;
        }

        Update();
    }

    public void Update()
    {
        HasProfile = Profiles.TryResolve(MapId, ProfileTime, out var entry);

        if (HasProfile)
        {
            _lighting.AmbientIntensity = entry.Intensity;
            _lighting.AmbientColor = Packing.Pack(entry.Red, entry.Green, entry.Blue);

            if (entry.UsesHea)
            {
                _lighting.Mask = Masks?.LoadLightMask(MapId);
            }

            return;
        }

        if (Mode == MapFlags.Darkness)
        {
            return;
        }

        _lighting.AmbientIntensity = LightProfileTable.FallbackIntensity;
        _lighting.AmbientColor = 0;
        _lighting.Mask = null;
    }
}
