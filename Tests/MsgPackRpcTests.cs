// <copyright file="MsgPackRpcTests.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.Tests;

using System.Buffers;
using System.Collections;
using AeroVim.Editor.Diagnostics;
using AeroVim.NeovimClient;
using AeroVim.Tests.Helpers;
using MessagePack;
using NUnit.Framework;

/// <summary>
/// Tests MsgPack-RPC transport behavior.
/// </summary>
public class MsgPackRpcTests
{
    /// <summary>
    /// A successful response should complete the matching request task.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SendRequest_WritesRequestAndCompletesOnResponse()
    {
        using var streams = new DuplexStreamPair();
        using var rpc = new MsgPackRpc(streams.ClientWriter, streams.ClientReader, (_, _) => { }, NullLogger.Instance);

        Task<(bool Success, object Value)> requestTask = rpc.SendRequest("nvim_input", new object[] { "abc", 42 });
        var request = await ReadRequestAsync(streams.ServerReader);

        Assert.That(request.Type, Is.EqualTo(0));
        Assert.That(request.Method, Is.EqualTo("nvim_input"));
        Assert.That(request.Args, Is.EqualTo(new object?[] { "abc", 42L }));

        await WriteRpcArrayAsync(streams.ServerWriter, 1, request.RequestId, null, "ok");

        var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(response.Success, Is.True);
        Assert.That(((MsgPack.MessagePackObject)response.Value).AsString(), Is.EqualTo("ok"));
    }

    /// <summary>
    /// An error response should complete the request as unsuccessful.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SendRequest_ErrorResponseReturnsFailureTuple()
    {
        using var streams = new DuplexStreamPair();
        using var rpc = new MsgPackRpc(streams.ClientWriter, streams.ClientReader, (_, _) => { }, NullLogger.Instance);

        Task<(bool Success, object Value)> requestTask = rpc.SendRequest("nvim_eval", new object[] { "g:var" });
        var request = await ReadRequestAsync(streams.ServerReader);

        await WriteRpcArrayAsync(streams.ServerWriter, 1, request.RequestId, "boom", null);

        var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(response.Success, Is.False);
        Assert.That(((MsgPack.MessagePackObject)response.Value).AsString(), Is.EqualTo("boom"));
    }

    /// <summary>
    /// Notifications should be dispatched to the configured handler.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NotificationMessage_InvokesNotificationHandler()
    {
        using var streams = new DuplexStreamPair();
        var handlerCalled = new TaskCompletionSource<(string Name, IList<MsgPack.MessagePackObject> Args)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var rpc = new MsgPackRpc(
            streams.ClientWriter,
            streams.ClientReader,
            (name, args) => handlerCalled.TrySetResult((name, args)),
            NullLogger.Instance);

        await WriteRpcArrayAsync(
            streams.ServerWriter,
            2,
            "redraw",
            new object?[]
            {
                "grid_line",
                1,
            });

        var result = await handlerCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(result.Name, Is.EqualTo("redraw"));
        Assert.That(result.Args[0].AsString(), Is.EqualTo("grid_line"));
        Assert.That(result.Args[1].AsInt32(), Is.EqualTo(1));
    }

    /// <summary>
    /// Invalid payloads should fail any pending requests.
    /// </summary>
    [Test]
    public void InvalidPayload_FailsPendingRequests()
    {
        using var streams = new DuplexStreamPair();
        using var rpc = new MsgPackRpc(streams.ClientWriter, streams.ClientReader, (_, _) => { }, NullLogger.Instance);

        Task<(bool Success, object Value)> requestTask = rpc.SendRequest("nvim_input", new object[] { "abc" });
        WriteStandaloneString(streams.ServerWriter, "not-an-array");

        var ex = Assert.ThrowsAsync<MsgPack.UnpackException>(async () => await requestTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(ex, Is.Not.Null);
    }

    /// <summary>
    /// Disposing the transport should fault any pending requests.
    /// </summary>
    [Test]
    public void Dispose_FailsPendingRequests()
    {
        using var streams = new DuplexStreamPair();
        var rpc = new MsgPackRpc(streams.ClientWriter, streams.ClientReader, (_, _) => { }, NullLogger.Instance);
        Task<(bool Success, object Value)> requestTask = rpc.SendRequest("nvim_input", new object[] { "abc" });

        rpc.Dispose();

        var ex = Assert.ThrowsAsync<ObjectDisposedException>(async () => await requestTask.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(ex, Is.Not.Null);
    }

    /// <summary>
    /// Fire-and-forget should raise RpcErrorOccurred when the server returns an error response.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SendRequestFireAndForget_ErrorResponse_RaisesRpcErrorOccurred()
    {
        using var streams = new DuplexStreamPair();
        using var rpc = new MsgPackRpc(streams.ClientWriter, streams.ClientReader, (_, _) => { }, NullLogger.Instance);

        var errorReceived = new TaskCompletionSource<(string Method, string Error)>(TaskCreationOptions.RunContinuationsAsynchronously);
        rpc.RpcErrorOccurred += (method, error) => errorReceived.TrySetResult((method, error));

        rpc.SendRequestFireAndForget("nvim_command", new object[] { "badcmd" });

        var request = await ReadRequestAsync(streams.ServerReader);
        await WriteRpcArrayAsync(streams.ServerWriter, 1, request.RequestId, "E492: Not an editor command", null);

        var result = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(result.Method, Is.EqualTo("nvim_command"));
        Assert.That(result.Error, Does.Contain("E492"));
    }

    /// <summary>
    /// Fire-and-forget should raise RpcErrorOccurred when the transport fails.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SendRequestFireAndForget_TransportFailure_RaisesRpcErrorOccurred()
    {
        using var streams = new DuplexStreamPair();
        using var rpc = new MsgPackRpc(streams.ClientWriter, streams.ClientReader, (_, _) => { }, NullLogger.Instance);

        var errorReceived = new TaskCompletionSource<(string Method, string Error)>(TaskCreationOptions.RunContinuationsAsynchronously);
        rpc.RpcErrorOccurred += (method, error) => errorReceived.TrySetResult((method, error));

        rpc.SendRequestFireAndForget("nvim_input", new object[] { "abc" });

        // Read the request, then send garbage to trigger a parse failure that faults pending requests.
        await ReadRequestAsync(streams.ServerReader);
        WriteStandaloneString(streams.ServerWriter, "not-an-array");

        var result = await errorReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(result.Method, Is.EqualTo("nvim_input"));
    }

    /// <summary>
    /// Fire-and-forget should not raise RpcErrorOccurred on a successful response.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SendRequestFireAndForget_SuccessResponse_DoesNotRaiseRpcErrorOccurred()
    {
        using var streams = new DuplexStreamPair();
        using var rpc = new MsgPackRpc(streams.ClientWriter, streams.ClientReader, (_, _) => { }, NullLogger.Instance);

        bool errorFired = false;
        rpc.RpcErrorOccurred += (_, _) => errorFired = true;

        rpc.SendRequestFireAndForget("nvim_input", new object[] { "abc" });

        var request = await ReadRequestAsync(streams.ServerReader);
        await WriteRpcArrayAsync(streams.ServerWriter, 1, request.RequestId, null, 3);

        // Give the continuation a moment to run.
        await Task.Delay(100);

        Assert.That(errorFired, Is.False);
    }

    /// <summary>
    /// Fire-and-forget should not throw when the transport is disposed mid-flight.
    /// </summary>
    [Test]
    public void SendRequestFireAndForget_DisposeDuringFlight_DoesNotThrow()
    {
        using var streams = new DuplexStreamPair();
        var rpc = new MsgPackRpc(streams.ClientWriter, streams.ClientReader, (_, _) => { }, NullLogger.Instance);

        rpc.SendRequestFireAndForget("nvim_input", new object[] { "abc" });

        Assert.DoesNotThrow(() => rpc.Dispose());
    }

    /// <summary>
    /// Concurrent SendRequest calls should each receive a unique request ID.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConcurrentSendRequests_ProduceUniqueRequestIds()
    {
        using var streams = new DuplexStreamPair();
        using var rpc = new MsgPackRpc(streams.ClientWriter, streams.ClientReader, (_, _) => { }, NullLogger.Instance);

        const int count = 50;
        var tasks = new Task<(bool Success, object Value)>[count];
        var barrier = new Barrier(count);

        // Launch N requests concurrently, synchronized at a barrier.
        for (int i = 0; i < count; i++)
        {
            int index = i;
            tasks[index] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                return rpc.SendRequest($"method_{index}", new object[] { index });
            });
        }

        // Read all N requests from the server side and collect their IDs.
        var ids = new HashSet<uint>();
        var serverReader = new MessagePackStreamReader(streams.ServerReader);
        for (int i = 0; i < count; i++)
        {
            var payload = await serverReader.ReadAsync(CancellationToken.None);
            Assert.That(payload.HasValue, Is.True);
            var unpacker = new MessagePackReader(payload.Value);
            Assert.That(unpacker.ReadArrayHeader(), Is.EqualTo(4));
            unpacker.ReadInt32(); // type
            uint requestId = unpacker.ReadUInt32();
            ids.Add(requestId);
        }

        Assert.That(ids.Count, Is.EqualTo(count), "All request IDs must be unique.");
    }

    private static async Task<(int Type, uint RequestId, string Method, object?[] Args)> ReadRequestAsync(Stream stream)
    {
        var reader = new MessagePackStreamReader(stream);
        ReadOnlySequence<byte>? payload = await reader.ReadAsync(CancellationToken.None);
        Assert.That(payload.HasValue, Is.True);

        var unpacker = new MessagePackReader(payload.Value);
        Assert.That(unpacker.ReadArrayHeader(), Is.EqualTo(4));

        int type = unpacker.ReadInt32();
        uint requestId = unpacker.ReadUInt32();
        string method = unpacker.ReadString() ?? string.Empty;

        int argsCount = unpacker.ReadArrayHeader();
        var args = new object?[argsCount];
        for (int i = 0; i < argsCount; i++)
        {
            args[i] = ReadObject(ref unpacker);
        }

        return (type, requestId, method, args);
    }

    private static async Task WriteRpcArrayAsync(Stream stream, params object?[] values)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(values.Length);
        for (int i = 0; i < values.Length; i++)
        {
            WriteObject(ref writer, values[i]);
        }

        writer.Flush();
        await stream.WriteAsync(buffer.WrittenMemory);
        await stream.FlushAsync();
    }

    private static void WriteStandaloneString(Stream stream, string value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.Write(value);
        writer.Flush();
        stream.Write(buffer.WrittenSpan);
        stream.Flush();
    }

    private static object? ReadObject(ref MessagePackReader reader)
    {
        if (reader.TryReadNil())
        {
            return null;
        }

        switch (reader.NextMessagePackType)
        {
            case MessagePackType.Boolean:
                return reader.ReadBoolean();
            case MessagePackType.Integer:
                return reader.ReadInt64();
            case MessagePackType.Float:
                return reader.ReadDouble();
            case MessagePackType.String:
                return reader.ReadString();
            case MessagePackType.Array:
                {
                    int count = reader.ReadArrayHeader();
                    var values = new object?[count];
                    for (int i = 0; i < count; i++)
                    {
                        values[i] = ReadObject(ref reader);
                    }

                    return values;
                }

            default:
                throw new InvalidOperationException($"Unsupported test payload type {reader.NextMessagePackType}.");
        }
    }

    private static void WriteObject(ref MessagePackWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNil();
                return;
            case string text:
                writer.Write(text);
                return;
            case bool boolean:
                writer.Write(boolean);
                return;
            case int intValue:
                writer.Write(intValue);
                return;
            case uint uintValue:
                writer.Write(uintValue);
                return;
            case long longValue:
                writer.Write(longValue);
                return;
            case object?[] array:
                writer.WriteArrayHeader(array.Length);
                for (int i = 0; i < array.Length; i++)
                {
                    WriteObject(ref writer, array[i]);
                }

                return;
            case IEnumerable enumerable:
                {
                    var items = enumerable.Cast<object?>().ToList();
                    writer.WriteArrayHeader(items.Count);
                    for (int i = 0; i < items.Count; i++)
                    {
                        WriteObject(ref writer, items[i]);
                    }

                    return;
                }

            default:
                throw new InvalidOperationException($"Unsupported test payload value type {value.GetType().FullName}.");
        }
    }
}
