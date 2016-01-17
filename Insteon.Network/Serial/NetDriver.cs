// <copyright company="INSTEON">
// Copyright (c) 2012 All Right Reserved, http://www.insteon.net
//
// This source is subject to the Common Development and Distribution License (CDDL). 
// Please see the LICENSE.txt file for more information.
// All other rights reserved.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
// KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// </copyright>
// <author>Dave Templin</author>
// <email>info@insteon.net</email>

namespace Insteon.Network.Serial
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;

    // Provides an implementation of the serial communication interface adapting to an INSTEON controller device over a remote socket connection.
    // Rewrite as an async client, see: http://msdn.microsoft.com/en-us/library/bew39x2a(v=vs.110).aspx
    internal class NetDriver : ISerialPort, IDisposable
    {
        private const int Port = 9761;
        private readonly string host = string.Empty;
        private readonly IPAddress address = IPAddress.None;
        private DataAvailable notify;
        private Thread thread;
        private bool running;
        private readonly List<byte> sendBuffer = new List<byte>();        
        private readonly List<byte> receiveBuffer = new List<byte>();
        private readonly AutoResetEvent wait = new AutoResetEvent(false);

        public NetDriver(string host)
        {
            if (!IPAddress.TryParse(host, out this.address))
            {
                this.host = host;
            }
        }

        public void Close()
        {
            Log.WriteLine("NetDriver closing");
            this.running = false;
            this.thread.Interrupt();
            this.thread.Join();
            this.notify = null;
            Log.WriteLine("NetDriver closed");
        }

        public void Dispose()
        {
            this.Close();            
        }

        public void Open()
        {
            this.thread = new Thread(this.ThreadProc);
            this.thread.Start();
            Thread.Sleep(100); // yield
        }

        public byte[] ReadAll()
        {
            if (!this.running)
            {
                Log.WriteLine("NetDriver thread no longer running, restarting...");
                this.Open();
            }

            var data = this.receiveBuffer.ToArray();
            this.receiveBuffer.Clear();
            return data;
        }

        public void SetNotify(DataAvailable notify)
        {
            this.notify = notify;
        }

        private void ThreadProc()
        {
            Log.WriteLine("NetDriver thread start");
            this.running = true;
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.NoDelay = true;

            try
            {
                if (!string.IsNullOrEmpty(this.host))
                {
                    socket.Connect(this.host, Port);
                }
                else
                {
                    socket.Connect(this.address, Port);
                }

                while (this.running)
                {
                    if (this.sendBuffer.Count > 0)
                    {
                        var data = this.sendBuffer.ToArray();
                        this.sendBuffer.Clear();
                        socket.Send(data, SocketFlags.None);

                        Log.WriteLine("NET send: ({1}) {0}", Utilities.ByteArrayToString(data), data.Length);
                    }

                    if (socket.Poll(100, SelectMode.SelectRead) && socket.Available > 0)
                    {
                        //Log.WriteLine("NetDriver data available...");
                        while (socket.Available > 0)
                        {
                            var data = new byte[socket.Available];
                            socket.Receive(data, SocketFlags.Partial);
                            this.receiveBuffer.AddRange(data);

                            Log.WriteLine("NET recv: ({1}) {0}", Utilities.ByteArrayToString(data), data.Length);
                        }

                        if (this.notify != null)
                        {
                            this.notify();
                        }

                        this.wait.Set();
                    }
                }

                socket.Shutdown(SocketShutdown.Both);
            }
            catch (ThreadInterruptedException)
            {
                //Log.WriteLine("NetDriver thread connect interrupted");
            }
            catch (SocketException ex)
            {
                Log.WriteLine("NetDriver socket error: {0}", ex.Message);
            }

            socket.Close();
            this.running = false;
            Log.WriteLine("NetDriver thread exit");
        }

        public void Write(byte[] data)
        {
            if (!this.running)
            {
                Log.WriteLine("NetDriver thread no longer running, restarting...");
                this.Open();
            }

            //Log.WriteLine("NetDriver send buffer: {0}", Utilities.ByteArrayToString(data));
            this.sendBuffer.AddRange(data);
            Thread.Sleep(1); // yield
        }

        public void Wait(int timeout)
        {
            if (!this.running)
            {
                Log.WriteLine("NetDriver thread no longer running, restarting...");
                this.Open();
            }

            this.wait.WaitOne(timeout);
        }
    }
}
