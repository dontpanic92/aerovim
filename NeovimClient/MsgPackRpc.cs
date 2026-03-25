// <copyright file="MsgPackRpc.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.NeovimClient
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The RPC client using MsgPackRpc Protocol, through <see cref="Stream"/>s.
    /// </summary>
    public sealed class MsgPackRpc : IDisposable
    {
        private readonly Stream writer;
        private readonly Stream reader;
        private readonly Thread readTask;
        private uint nextRequestId = 0;

        private ConcurrentDictionary<uint, TaskCompletionSource<(bool, object)>> responseSignals
            = new ConcurrentDictionary<uint, TaskCompletionSource<(bool, object)>>();

        /// <summary>
        /// Initializes a new instance of the <see cref="MsgPackRpc"/> class.
        /// </summary>
        /// <param name="writer">The stream for sending data to remote.</param>
        /// <param name="reader">The stream for receiving data from remote.</param>
        /// <param name="handler">Notification handler.</param>
        public MsgPackRpc(Stream writer, Stream reader, NotificationHandler handler)
        {
            this.writer = writer;
            this.reader = reader;
            this.NotificationHandlers += handler;
            this.readTask = new Thread(() => this.ReadTask());
            this.readTask.Start();
        }

        /// <summary>
        /// The RequestHandler type to process requests.
        /// </summary>
        /// <param name="method">The name of the method in the request.</param>
        /// <param name="args">The args of the method.</param>
        /// <returns>If error occured, returns null; otherwise returns the response.</returns>
        public delegate object RequestHandler(string method, IList<MsgPack.MessagePackObject> args);

        /// <summary>
        /// The RequestHandler type to process notifications.
        /// </summary>
        /// <param name="method">The name of the method in the request.</param>
        /// <param name="args">The args of the method.</param>
        public delegate void NotificationHandler(string method, IList<MsgPack.MessagePackObject> args);

        /// <summary>
        /// Gets or sets the handlers to process requests.
        /// </summary>
        public RequestHandler RequestHandlers { get; set; }

        /// <summary>
        /// Gets or sets the handlers to process notifications.
        /// </summary>
        public NotificationHandler NotificationHandlers { get; set; }

        private uint NextRequestId
        {
            get
            {
                uint ret = this.nextRequestId;
                this.nextRequestId = (this.nextRequestId + 1) % uint.MaxValue;
                return ret;
            }
        }

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
        public Task<(bool, object)> SendRequest(string name, IList<object> args)
        {
            var requestId = this.NextRequestId;
            var responseSignal = new TaskCompletionSource<(bool, object)>();
            this.responseSignals.TryAdd(requestId, responseSignal);

            var request = new List<object>() { 0, requestId, name, args };
            var packer = MsgPack.Serialization.MessagePackSerializer.Get<List<object>>();
            packer.Pack(this.writer, request);
            this.writer.Flush();
            return responseSignal.Task;
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            this.reader.Close();
            this.writer.Close();
            this.readTask.Abort();
        }

        private void ReadTask()
        {
            var bufferedStream = new BufferedStream(this.reader);

            while (true)
            {
                IList<MsgPack.MessagePackObject> list;
                try
                {
                   list = MsgPack.Unpacking.UnpackArray(bufferedStream);
                }
                catch (MsgPack.UnpackException)
                {
                    break;
                }

                var type = list[0].AsUInt32();
                switch (type)
                {
                    case 0:
                        // Request
                        if (list.Count != 4)
                        {
                            throw new Exception("Wrong MsgPackRpc format: Request must have 4 elements but " + list.Count + "received");
                        }

                        break;
                    case 1:
                        // Response
                        if (list.Count != 4)
                        {
                            throw new Exception("Wrong MsgPackRpc format: Response must have 3 elements but " + list.Count + "received");
                        }

                        this.OnResponse(list[1].AsUInt32(), list[2], list[3]);
                        break;
                    case 2:
                        // Notification
                        if (list.Count != 3)
                        {
                            throw new Exception("Wrong MsgPackRpc format: Notification must have 3 elements but " + list.Count + "received");
                        }

                        this.OnNotification(list[1].AsString(), list[2].AsList());

                        break;
                    default:
                        throw new Exception("Unknown type of message received. Type: " + type);
                }
            }
        }

        private void OnNotification(string name, IList<MsgPack.MessagePackObject> args)
        {
            this.NotificationHandlers.Invoke(name, args);
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
    }
}
