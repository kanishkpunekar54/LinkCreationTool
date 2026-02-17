using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace Live.Api.Services
{
    // Lightweight broadcaster that captures Console writes and fan-out to subscribers.
    // - Keeps a bounded history (default 1000 lines) for new subscribers.
    // - Broadcasts each complete line to all active subscribers.
    // - Uses a custom TextWriter you can install via Console.SetOut(ConsoleBroadcaster.Writer).
    public static class ConsoleBroadcaster
    {
        private static readonly ConcurrentQueue<string> _history = new();
        private static readonly List<Channel<string>> _subscribers = new();
        private static readonly object _lock = new();
        private const int MaxHistory = 1000;

        private static readonly TextWriter _original = Console.Out;

        public static TextWriter Writer { get; } = new BroadcasterTextWriter();

        // Subscribe returns a Channel<string> whose Writer will receive future log lines.
        // The subscriber should read from returnedChannel.Reader and call Unsubscribe(returnedChannel) when done.
        public static Channel<string> Subscribe(bool sendHistory = true)
        {
            var ch = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            lock (_lock)
            {
                _subscribers.Add(ch);
                if (sendHistory)
                {
                    foreach (var h in _history)
                        ch.Writer.TryWrite(h);
                }
            }
            return ch;
        }

        public static void Unsubscribe(Channel<string> channel)
        {
            if (channel == null) return;
            lock (_lock)
            {
                if (_subscribers.Remove(channel))
                {
                    channel.Writer.TryComplete();
                }
            }
        }

        private static void Broadcast(string line)
        {
            if (line == null) return;

            // keep bounded history
            _history.Enqueue(line);
            while (_history.Count > MaxHistory && _history.TryDequeue(out _)) { }

            lock (_lock)
            {
                foreach (var ch in _subscribers.ToArray())
                {
                    // Best-effort; drop if can't write
                    try
                    {
                        ch.Writer.TryWrite(line);
                    }
                    catch
                    {
                        // ignore per-subscriber failures; cleanup happens on Unsubscribe
                    }
                }
            }
        }

        private class BroadcasterTextWriter : TextWriter
        {
            private readonly StringBuilder _buffer = new();

            public override Encoding Encoding => _original?.Encoding ?? Encoding.UTF8;

            public override void Write(char value)
            {
                _original?.Write(value);
                if (value == '\n')
                {
                    FlushBufferAsLine();
                }
                else
                {
                    _buffer.Append(value);
                }
            }

            public override void Write(string? value)
            {
                _original?.Write(value);
                if (string.IsNullOrEmpty(value)) return;

                int start = 0;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c == '\n')
                    {
                        _buffer.Append(value.Substring(start, i - start));
                        FlushBufferAsLine();
                        start = i + 1;
                    }
                }

                if (start < value.Length)
                    _buffer.Append(value.Substring(start));
            }

            public override void WriteLine(string? value)
            {
                _original?.WriteLine(value);
                if (value != null)
                {
                    if (_buffer.Length > 0)
                    {
                        _buffer.Append(value);
                        FlushBufferAsLine();
                    }
                    else
                    {
                        Broadcast(value);
                    }
                }
                else
                {
                    Broadcast(string.Empty);
                }
            }

            public override Task WriteAsync(char value)
            {
                Write(value);
                return Task.CompletedTask;
            }

            public override Task WriteAsync(string? value)
            {
                Write(value);
                return Task.CompletedTask;
            }

            public override Task WriteLineAsync(string? value)
            {
                WriteLine(value);
                return Task.CompletedTask;
            }

            public override void Flush()
            {
                _original?.Flush();
            }

            private void FlushBufferAsLine()
            {
                var line = _buffer.ToString();
                _buffer.Clear();
                Broadcast(line);
            }
        }
    }
}