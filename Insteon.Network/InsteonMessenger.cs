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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Insteon.Network
{
    // This class is responsible for processing raw messages into structured property lists and dispatching the result to individual device objects.
    // The responsibilities of the messenger include:
    //  - Owning the network bridge to the physical INSTEON network.
    //  - Providing the ability to send messages to the controller for other classes in the module.
    //  - Processing raw message bytes into structured property lists.
    //  - Determining the logical device object to which the message is directed and dispatching the message to that object.
    //  - Reporting back to the bridge whether or not each message is valid, and if valid the size in bytes of the message.
    internal class InsteonMessenger : InsteonNetworkBridge.IMessageProcessor
    {
        private readonly InsteonNetwork network;
        private readonly InsteonNetworkBridge bridge;
        private readonly List<WaitItem> waitList = new List<WaitItem>();
        private byte[] sentMessage = null; // bytes of last sent message, used to match the echo

        public Dictionary<PropertyKey, int> ControllerProperties { get; private set; }

        public InsteonMessenger(InsteonNetwork network)
        {
            if (network == null)
                throw new ArgumentNullException("network");

            this.network = network;
            bridge = new InsteonNetworkBridge(this);
            ControllerProperties = new Dictionary<PropertyKey, int>();
        }

        public void Close()
        {
            lock (bridge)
            {
                bridge.Close();
            }
            network.Disconnect();
        }

        public void Connect(InsteonConnection connection)
        {
            lock (bridge)
            {
                ControllerProperties = bridge.Connect(connection);
            }
            Log.WriteLine("Connected to '{0}'", connection);
        }

        public bool TryConnect(InsteonConnection connection)
        {
            try
            {
                Log.WriteLine("Trying connection '{0}'...", connection.ToString());
                lock (bridge)
                {
                    ControllerProperties = bridge.Connect(connection);
                }
                Log.WriteLine("Connected to '{0}'", connection);
                return true;
            }
            catch (Exception ex)
            {
                Log.WriteLine("Error connecting to '{0}'. {1}", connection.ToString(), ex.Message);
            }
            return false;
        }

        public void Send(byte[] message)
        {
            if (TrySend(message, true) != EchoStatus.ACK)
                throw new IOException(string.Format("Failed to send message '{0}'", Utilities.ByteArrayToString(message)));
        }

        public EchoStatus TrySend(byte[] message)
        {
            return TrySend(message, true);
        }

        public EchoStatus TrySend(byte[] message, bool retryOnNak)
        {
            EchoStatus status = EchoStatus.Unknown;
            lock (bridge)
            {
                sentMessage = message;
                try
                {
                    status = bridge.Send(message, retryOnNak);
                }
                catch (InvalidOperationException)
                {
                    bridge.Close();
                    network.Disconnect();
                }
                catch (Exception ex)
                {
                    if (Debugger.IsAttached)
                        throw;
                    Log.WriteLine("UNEXPECTED ERROR: {0}", ex.Message);
                }
                finally
                {
                    sentMessage = null;
                }
            }
            return status;
        }

        public void SendReceive(byte[] message, byte receiveMessageId, out Dictionary<PropertyKey, int> properties)
        {
            if (TrySendReceive(message, true, receiveMessageId, out properties) != EchoStatus.ACK)
                throw new IOException(string.Format("Failed to send message '{0}'.", Utilities.ByteArrayToString(message)));
        }

        public EchoStatus TrySendReceive(byte[] message, bool retryOnNak, byte receiveMessageId, out Dictionary<PropertyKey, int> properties)
        {
            properties = null;
            WaitItem item = new WaitItem(receiveMessageId);
            
            lock (waitList)
                waitList.Add(item);

            EchoStatus status = TrySend(message, retryOnNak);
            if (status == EchoStatus.ACK)
            {
                if (item.Message == null)
                    item.MessageEvent.WaitOne(Constants.messageTimeout);
                if (item.Message != null)
                    properties = item.Message.Properties;
            }

            lock (waitList)
                waitList.Remove(item);

            return status;
        }

        private void OnMessage(InsteonMessage message)
        {
            if (message.Properties.ContainsKey(PropertyKey.FromAddress))
            {
                int address = message.Properties[PropertyKey.FromAddress];
                if (network.Devices.ContainsKey(address))
                {
                    Log.WriteLine("Device {0} received message '{1}'", InsteonAddress.Format(address), message.ToString());
                    InsteonDevice device = network.Devices.Find(address);
                    device.OnMessage(message);
                }
                else if (message.MessageType == InsteonMessageType.SetButtonPressed)
                {
                    // don't warn about SetButtonPressed message from unknown devices, because it may be from a device about to be added
                }
                else if (network.AutoAdd)
                {
                    Log.WriteLine("Device {0} received message '{1}', adding unknown device", InsteonAddress.Format(address), message.ToString());
                    InsteonDevice device = network.Devices.Add(new InsteonAddress(address), new InsteonIdentity());
                    device.OnMessage(message);
                }
                else
                {
                    Log.WriteLine("WARNING: Unknown device {0} received message '{1}'", InsteonAddress.Format(address), message.ToString());
                }
            }
            else
            {
                Log.WriteLine("Controller received message '{0}'", message.ToString());
                network.Controller.OnMessage(message);
            }
        }

        private void UpdateWaitItems(InsteonMessage message)
        {
            lock (waitList)
            {
                for (int i = 0; i < waitList.Count; ++i)
                {
                    WaitItem item = waitList[i];
                    if (message.MessageId == item.MessageId)
                        if (item.Message == null)
                        {
                            item.Message = message;
                            item.MessageEvent.Set();
                        }
                }
            }
        }

        #region InsteonNetworkBridge.IMessageProcessor

        bool InsteonNetworkBridge.IMessageProcessor.ProcessMessage(byte[] data, int offset, out int count)
        {
            InsteonMessage message;
            if (InsteonMessageProcessor.ProcessMessage(data, offset, out count, out message))
            {
                OnMessage(message);
                UpdateWaitItems(message);
                return true;
            }
            else
            {
                return false;
            }
        }

        bool InsteonNetworkBridge.IMessageProcessor.ProcessEcho(byte[] data, int offset, out int count)
        {
            byte[] message = Utilities.ArraySubset(data, offset, sentMessage.Length);
            if (Utilities.ArraySequenceEquals(sentMessage, message))
            {
                count = sentMessage.Length;
                return true;
            }
            else
            {
                count = 0;
                return false;
            }
        }

        void InsteonNetworkBridge.IMessageProcessor.SetEchoStatus(EchoStatus status)
        {
        }

        #endregion

        private class WaitItem
        {
            public WaitItem(byte messageId)
            {
                this.MessageId = messageId;
                this.MessageEvent = new AutoResetEvent(false);
                this.Message = null;
            }
            public byte MessageId { get; private set; }
            public AutoResetEvent MessageEvent { get; private set; }
            public InsteonMessage Message { get; set; }
        }
    }
}
