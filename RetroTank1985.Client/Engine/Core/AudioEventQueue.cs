using RetroTank1985.Client.Engine.Enums;

namespace RetroTank1985.Client.Engine.Core;

public interface IAudioEventQueue
{
    void Enqueue(AudioSoundEffect sfx);
    List<byte> Flush();
    void Clear();
}

public class AudioEventQueue : IAudioEventQueue
{
    private static readonly List<byte> EmptyList = new(0);
    private List<byte> _currentQueue = new(8);
    private List<byte> _flushBuffer = new(8);
    private readonly object _lock = new();

    public void Enqueue(AudioSoundEffect sfx)
    {
        if (sfx == AudioSoundEffect.None) return;
        lock (_lock)
        {
            _currentQueue.Add((byte)sfx);
        }
    }

    public List<byte> Flush()
    {
        lock (_lock)
        {
            if (_currentQueue.Count == 0) return EmptyList;

            // Swap buffers to avoid heap allocations
            _flushBuffer.Clear();
            var temp = _flushBuffer;
            _flushBuffer = _currentQueue;
            _currentQueue = temp;

            return _flushBuffer;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _currentQueue.Clear();
            _flushBuffer.Clear();
        }
    }
}
