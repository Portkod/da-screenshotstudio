using DaScreenshotStudio.Engine.Rendering;
using DaScreenshotStudio.Engine.Text;

namespace DaScreenshotStudio.Engine.Ui;

public sealed partial class GuiBackPane
{
    private GuiTextPane? _id;

    private GuiTextPane? _hp;

    private GuiTextPane? _mp;

    private GuiTextPane? _zone;

    private GuiTextPane? _position;

    private GuiTextPane? _weightLabel;

    private GuiTextPane? _server;

    private GuiTextPane? _descriptionLabel;

    private OrbPane? _healthOrb;

    private OrbPane? _manaOrb;

    private ImagePane? _ping;

    private readonly Pixmap[] _pingFrames = new Pixmap[NetworkIndicatorStages];

    private uint _latencyMs;

    private readonly byte[] _name = new byte[64];

    private readonly byte[] _serverName = new byte[64];

    private readonly byte[] _zoneName = new byte[64];

    private readonly byte[] _description = new byte[256];

    private int _nameLength;

    private int _serverLength;

    private int _zoneLength;

    private int _descriptionLength;

    private int _labelChannel;

    private uint _health;

    private uint _maxHealth;

    private uint _mana;

    private uint _maxMana;

    private int _weight;

    private int _maxWeight;

    private int _positionX;

    private int _positionY;

    private bool _positionAgrees = true;

    public const int NetworkIndicatorStages = 4;

    public static ReadOnlySpan<uint> NetworkIndicatorBounds => [250, 350, 450];

    public ReadOnlySpan<byte> Description => _description.AsSpan(0, _descriptionLength);

    public void SetName(scoped ReadOnlySpan<byte> name)
    {
        _nameLength = Copy(name, _name);
        _id?.SetText(name);
    }

    public void SetServer(scoped ReadOnlySpan<byte> name)
    {
        _serverLength = Copy(name, _serverName);
        _server?.SetText(name);
    }

    public void SetDescription(scoped ReadOnlySpan<byte> text)
    {
        WriteDescriptionLine(text);
        _labelChannel = 0;
    }

    public void SetDescription(int channel, scoped ReadOnlySpan<byte> text)
    {
        SetDescription(text);
        _labelChannel = channel;
    }

    public void SetDescription(int channel, scoped ReadOnlySpan<byte> text, int x, int y)
    {
        SetDescription(text);

        if (LayoutIndex != GuiLayoutIndex.Compact)
        {
            _tooltip.Show(null, this, x, y - HoverLabelOffsetY, text);
        }

        _labelChannel = channel;
    }

    public void ClearDescription(int channel)
    {
        if (channel != 0 && _labelChannel != channel)
        {
            return;
        }

        _labelChannel = 0;

        if (LayoutIndex != GuiLayoutIndex.Compact)
        {
            _tooltip.HideAny();
        }

        WriteDescriptionLine(default);
    }

    private void WriteDescriptionLine(scoped ReadOnlySpan<byte> text)
    {
        _descriptionLength = Copy(text, _description);
        _descriptionLabel?.SetText(text);
    }

    public void SetVitals(uint health, uint maxHealth, uint mana, uint maxMana)
    {
        _health = health;
        _maxHealth = maxHealth;
        _mana = mana;
        _maxMana = maxMana;

        Span<byte> buffer = stackalloc byte[48];

        var text = new TextBuffer(buffer);
        text.Append(health);

        if (_hp is not null)
        {
            _hp.SetText(text.Written);
        }
        else
        {
            _healthOrb?.SetLabel(text.Written);
        }

        text.Clear();
        text.Append(mana);

        if (_mp is not null)
        {
            _mp.SetText(text.Written);
        }
        else
        {
            _manaOrb?.SetLabel(text.Written);
        }

        _healthOrb?.SetRatio(health, maxHealth);
        _manaOrb?.SetRatio(mana, maxMana);
    }

    public void SetPosition(int x, int y, bool serverAgrees)
    {
        _positionX = x;
        _positionY = y;
        _positionAgrees = serverAgrees;

        Span<byte> buffer = stackalloc byte[24];
        var text = new TextBuffer(buffer);
        text.Append(x);
        text.Append(',');
        text.Append(' ');
        text.Append(y);

        if (_position is null)
        {
            return;
        }

        _position.PaletteIndex = serverAgrees ? TextPalette.InterfaceIndex : TextPalette.OutOfSyncIndex;
        _position.SetText(text.Written);
    }

    public void SetZone(scoped ReadOnlySpan<byte> name)
    {
        _zoneLength = Copy(name, _zoneName);
        _zone?.SetText(name);
    }

    public static int NetworkIndicatorStage(uint milliseconds)
    {
        var bounds = NetworkIndicatorBounds;

        for (var i = 0; i < bounds.Length; i++)
        {
            if (milliseconds < bounds[i])
            {
                return NetworkIndicatorStages - 1 - i;
            }
        }

        return 0;
    }

    public void SetNetworkLatency(uint milliseconds)
    {
        if (_latencyMs == milliseconds)
        {
            return;
        }

        _latencyMs = milliseconds;
        ApplyNetworkIndicator();
    }

    private void ApplyNetworkIndicator()
    {
        if (_ping is null)
        {
            return;
        }

        var frame = _pingFrames[NetworkIndicatorStage(_latencyMs)];
        if (!frame.IsEmpty)
        {
            _ping.Image = frame;
        }
    }

    public void SetWeight(int weight, int maxWeight)
    {
        _weight = weight;
        _maxWeight = maxWeight;

        Span<byte> buffer = stackalloc byte[24];
        var text = new TextBuffer(buffer);
        text.Append(weight);

        if (LayoutIndex == GuiLayoutIndex.Compact)
        {
            text.Append(' ');
            text.Append('/');
            text.Append(' ');
        }
        else
        {
            text.Append('/');
        }

        text.Append(maxWeight);
        _weightLabel?.SetText(text.Written);
    }

    private void ReapplyValues()
    {
        _id?.SetText(_name.AsSpan(0, _nameLength));
        _server?.SetText(_serverName.AsSpan(0, _serverLength));
        _zone?.SetText(_zoneName.AsSpan(0, _zoneLength));
        _descriptionLabel?.SetText(_description.AsSpan(0, _descriptionLength));

        SetVitals(_health, _maxHealth, _mana, _maxMana);
        SetWeight(_weight, _maxWeight);
        SetPosition(_positionX, _positionY, _positionAgrees);
        ApplyNetworkIndicator();
    }

    private static int Copy(scoped ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var length = Math.Min(source.Length, destination.Length);
        source[..length].CopyTo(destination);
        return length;
    }
}
