// <copyright file="MsgPackRpc.cs">
// Copyright (c) aerovim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace AeroVim.NeovimClient;

using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using AeroVim.Editor.Diagnostics;
using MessagePack;

/// <summary>
/// The RPC client using MsgPackRpc Protocol, through <see cref="Stream"/>s.
/// </summary>
public sealed class MsgPackRpc : IDisposable
{
    private readonly Stream writer;
    private readonly Stream reader;
    private readonly IComponentLogger log;
    private readonly CancellationTokenSource disposeCancellation = new();
    private readonly Task readTask;
    private readonly object writeLock = new();
    private readonly ArrayBufferWriter<byte> sendBuffer = new();
    private int nextRequestId;
    private bool disposed;

    private ConcurrentDictionary<uint, TaskCompletionSource<(bool Success, object Value)>> responseSignals
        = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MsgPackRpc"/> class.
    /// </summary>
    /// <param name="writer">The stream for sending data to remote.</param>
    /// <param name="reader">The stream for receiving data from remote.</param>
    /// <param name="handler">Notification handler.</param>
    /// <param name="logger">Application logger.</param>
    public MsgPackRpc(Stream writer, Stream reader, NotificationHandler handler, IAppLogger logger)
    {
        this.writer = writer;
        this.reader = reader;
        this.log = logger.For<MsgPackRpc>();
        this.NotificationHandlers += handler;
        this.readTask = Task.Run(() => this.ReadTaskAsync(this.disposeCancellation.Token));
    }

    /// <summary>
    /// The RequestHandler type to process notifications.
    /// </summary>
    /// <param name="method">The name of the method in the request.</param>
    /// <param name="args">The args of the method.</param>
    public delegate void NotificationHandler(string method, IList<MsgPack.MessagePackObject> args);

    /// <summary>
    /// Handler for RPC errors observed from fire-and-forget requests.
    /// </summary>
    /// <param name="method">The RPC method that failed.</param>
    /// <param name="errorDescription">A human-readable description of the error.</param>
    public delegate void RpcErrorHandler(string method, string errorDescription);

    /// <summary>
    /// Gets or sets the handlers to process notifications.
    /// </summary>
    public NotificationHandler NotificationHandlers { get; set; }

    /// <summary>
    /// Gets or sets the handler invoked when a fire-and-forget RPC request
    /// completes with an error response or a transport failure.
    /// </summary>
    public RpcErrorHandler? RpcErrorOccurred { get; set; }

    private uint NextRequestId => unchecked((uint)Interlocked.Increment(ref this.nextRequestId));

    /// <summary>
    /// Send a request to remote.
    /// </summary>
    /// <param name="name">method name.</param>
    /// <param name="args">method args.</param>
    /// <returns>
    /// Returns a tuple of bool and MessagePackObject; bool represents whether
    /// the request is successfully completed. If true, the object is the return value;
    /// otherwise the object represents the error that returns from remote.
    /// </returns>
    public Task<(bool Success, object Value)> SendRequest(string name, IList<object> args)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        var requestId = this.NextRequestId;
        var responseSignal = new TaskCompletionSource<(bool Success, object Value)>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.responseSignals.TryAdd(requestId, responseSignal);

        try
        {
            lock (this.writeLock)
            {
                this.sendBuffer.Clear();
                var packer = new MessagePackWriter(this.sendBuffer);
                packer.WriteArrayHeader(4);
                packer.Write(0);
                packer.Write(requestId);
                packer.Write(name);
                this.WriteObject(ref packer, args);
                packer.Flush();

                this.writer.Write(this.sendBuffer.WrittenSpan);
                this.writer.Flush();
            }

            return responseSignal.Task;
        }
        catch (Exception ex)
        {
            this.responseSignals.TryRemove(requestId, out _);
            responseSignal.TrySetException(ex);
            throw;
        }
    }

    /// <summary>
    /// Send a request to remote without requiring the caller to observe the result.
    /// Errors are logged and surfaced via <see cref="RpcErrorOccurred"/>.
    /// </summary>
    /// <param name="name">Method name.</param>
    /// <param name="args">Method args.</param>
    public void SendRequestFireAndForget(string name, IList<object> args)
    {
        Task<(bool Success, object Value)> task;
        try
        {
            task = this.SendRequest(name, args);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (Exception ex)
        {
            this.log.Error($"RPC call '{name}' failed to send.", ex);
            this.RpcErrorOccurred?.Invoke(name, ex.Message);
            return;
        }

        _ = this.ObserveResponse(name, task);
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.disposeCancellation.Cancel();
        this.FailPendingRequests(new ObjectDisposedException(nameof(MsgPackRpc)));

        try
        {
            this.readTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            this.reader.Dispose();
            this.writer.Dispose();
            this.disposeCancellation.Dispose();
        }
    }

    private static IList<MsgPack.MessagePackObject> ReadArray(ref MessagePackReader unpacker)
    {
        int count = unpacker.ReadArrayHeader();
        var items = new List<MsgPack.MessagePackObject>(count);
        for (int i = 0; i < count; i++)
        {
            items.Add(ReadObject(ref unpacker));
        }

        return items;
    }

    private static MsgPack.MessagePackObjectDictionary ReadMap(ref MessagePackReader unpacker)
    {
        int count = unpacker.ReadMapHeader();
        var items = new MsgPack.MessagePackObjectDictionary(count);
        for (int i = 0; i < count; i++)
        {
            var key = ReadObject(ref unpacker);
            var value = ReadObject(ref unpacker);
            items[key] = value;
        }

        return items;
    }

    private static MsgPack.MessagePackObject ReadObject(ref MessagePackReader unpacker)
    {
        if (unpacker.TryReadNil())
        {
            return new MsgPack.MessagePackObject(null);
        }

        switch (unpacker.NextMessagePackType)
        {
            case MessagePackType.Boolean:
                return new MsgPack.MessagePackObject(unpacker.ReadBoolean());
            case MessagePackType.Integer:
                return new MsgPack.MessagePackObject(unpacker.ReadInt64());
            case MessagePackType.Float:
                return new MsgPack.MessagePackObject(unpacker.ReadDouble());
            case MessagePackType.String:
                return new MsgPack.MessagePackObject(unpacker.ReadString());
            case MessagePackType.Array:
                return new MsgPack.MessagePackObject(ReadArray(ref unpacker));
            case MessagePackType.Map:
                return new MsgPack.MessagePackObject(ReadMap(ref unpacker));
            case MessagePackType.Binary:
                {
                    var bytes = unpacker.ReadBytes();
                    return new MsgPack.MessagePackObject(bytes.HasValue ? bytes.Value.ToArray() : Array.Empty<byte>());
                }

            case MessagePackType.Extension:
                {
                    var extension = unpacker.ReadExtensionFormat();
                    return new MsgPack.MessagePackObject(extension.Data.ToArray());
                }

            default:
                throw new InvalidDataException("Unsupported MsgPack value type.");
        }
    }

    private async Task ReadTaskAsync(CancellationToken cancellationToken)
    {
        var bufferedStream = new BufferedStream(this.reader);
        var streamReader = new MessagePackStreamReader(bufferedStream);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                IList<MsgPack.MessagePackObject> list;
                try
                {
                    var payload = await streamReader.ReadAsync(cancellationToken).ConfigureAwait(false);
                    if (!payload.HasValue)
                    {
                        this.FailPendingRequests(new EndOfStreamException("The msgpack-rpc stream closed before pending responses completed."));
                        break;
                    }

                    var unpacker = new MessagePackReader(payload.Value);
                    list = ReadArray(ref unpacker);
                }
                catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or MessagePackSerializationException)
                {
                    var unpackException = new MsgPack.UnpackException("Failed to unpack msgpack-rpc payload.", ex);
                    this.FailPendingRequests(unpackException);
                    this.log.Error("Failed to unpack msgpack-rpc payload.", unpackException);
                    break;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    this.FailPendingRequests(new ObjectDisposedException(nameof(MsgPackRpc)));
                    break;
                }
                catch (InvalidOperationException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                this.ProcessMessage(list);
            }
        }
        finally
        {
            bufferedStream.Dispose();
        }
    }

    private void ProcessMessage(IList<MsgPack.MessagePackObject> list)
    {
        var type = list[0].AsUInt32();
        switch (type)
        {
            case 0:
                this.log.Warning("Received an incoming request but request handling is not supported.");
                break;
            case 1:
                if (list.Count != 4)
                {
                    throw new InvalidDataException($"Wrong MsgPackRpc format: Response must have 4 elements but {list.Count} received.");
                }

                this.OnResponse(list[1].AsUInt32(), list[2], list[3]);
                break;
            case 2:
                if (list.Count != 3)
                {
                    throw new InvalidDataException($"Wrong MsgPackRpc format: Notification must have 3 elements but {list.Count} received.");
                }

                this.OnNotification(list[1].AsString(), list[2].AsList());
                break;
            default:
                throw new InvalidDataException($"Unknown type of message received. Type: {type}");
        }
    }

    private void OnNotification(string name, IList<MsgPack.MessagePackObject> args)
    {
        this.NotificationHandlers?.Invoke(name, args);
    }

    private async Task ObserveResponse(string name, Task<(bool Success, object Value)> task)
    {
        try
        {
            var (success, value) = await task.ConfigureAwait(false);
            if (!success)
            {
                var description = value?.ToString() ?? "unknown error";
                this.log.Warning($"RPC call '{name}' returned error: {description}");
                this.RpcErrorOccurred?.Invoke(name, description);
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            this.log.Error($"RPC call '{name}' failed.", ex);
            this.RpcErrorOccurred?.Invoke(name, ex.Message);
        }
    }

    private void OnResponse(uint requestId, MsgPack.MessagePackObject error, MsgPack.MessagePackObject result)
    {
        if (!this.responseSignals.TryRemove(requestId, out var signal))
        {
            return;
        }

        if (!error.IsNil)
        {
            signal.SetResult((false, error));
        }
        else
        {
            signal.SetResult((true, result));
        }
    }

    private void FailPendingRequests(Exception exception)
    {
        foreach (var pair in this.responseSignals)
        {
            if (this.responseSignals.TryRemove(pair.Key, out var signal))
            {
                signal.TrySetException(exception);
            }
        }
    }

    private void WriteObject(ref MessagePackWriter packer, object? value)
    {
        switch (value)
        {
            case null:
                packer.WriteNil();
                return;
            case MsgPack.MessagePackObject messagePackObject:
                this.WriteObject(ref packer, messagePackObject.GetRawValue());
                return;
            case string text:
                packer.Write(text);
                return;
            case bool boolean:
                packer.Write(boolean);
                return;
            case byte byteValue:
                packer.Write(byteValue);
                return;
            case sbyte signedByteValue:
                packer.Write(signedByteValue);
                return;
            case short shortValue:
                packer.Write(shortValue);
                return;
            case ushort unsignedShortValue:
                packer.Write(unsignedShortValue);
                return;
            case int intValue:
                packer.Write(intValue);
                return;
            case uint uintValue:
                packer.Write(uintValue);
                return;
            case long longValue:
                packer.Write(longValue);
                return;
            case ulong unsignedLongValue:
                packer.Write(unsignedLongValue);
                return;
            case float floatValue:
                packer.Write(floatValue);
                return;
            case double doubleValue:
                packer.Write(doubleValue);
                return;
            case byte[] bytes:
                packer.Write(bytes);
                return;
            case IDictionary dictionary:
                packer.WriteMapHeader(dictionary.Count);
                foreach (DictionaryEntry entry in dictionary)
                {
                    this.WriteObject(ref packer, entry.Key);
                    this.WriteObject(ref packer, entry.Value);
                }

                return;
            case IEnumerable enumerable:
                {
                    var items = enumerable.Cast<object>().ToList();
                    packer.WriteArrayHeader(items.Count);
                    foreach (var item in items)
                    {
                        this.WriteObject(ref packer, item);
                    }

                    return;
                }

            default:
                throw new NotSupportedException($"Unsupported msgpack value type: {value.GetType().FullName}");
        }
    }
}
