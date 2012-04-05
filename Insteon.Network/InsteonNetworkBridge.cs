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
using System.IO;
using System.Text;
using System.Threading;
using Insteon.Network.Serial;

namespace Insteon.Network
{
    // This class is responsible for bridging the logical network to the physical INSTEON network via the serial interface.
    // The responsibilities of the bridge include:
    //  - Owning the serial connection to the INSTEON controller device.
    //  - Directing all serial communications through the serial interface to the INSTEON controller device.
    //  - Detecting the beginning of each raw message by identifying the MESSAGE START header byte (02).
    //  - Delegating to the messenger the interpretation of the bytes following the header byte.
    //    Note: The messenger is responsible for reporting back to the bridge whether or not each message is valid, and if valid the size in bytes of the message.
    //  - Verifying the message integrity by checking the trailer byte for ACK (06) or NAK (15), and informing the messenger of the result.
    internal class InsteonNetworkBridge : IDisposable
    {
        private readonly List<byte> buffer = new List<byte>(); // buffer of received data to be processed
        private ISerialPort port = null; // serial port connection to the INSTEON controller
        private readonly IMessageProcessor messageProcessor; // reference to creator that handles processing of raw binary data into higher level messages
        
        public InsteonNetworkBridge(IMessageProcessor messageProcessor)
        {
            if (messageProcessor == null)
                throw new ArgumentNullException("messageProcessor");
            this.messageProcessor = messageProcessor;
        }

        public Dictionary<PropertyKey, int> Connect(InsteonConnection connection)
        {
            if (port != null)
                port.Close();

            port = SerialPortCreator.Create(connection);
            port.Open();

            byte[] input = new byte[] { 0x02, 0x60 };
            Dictionary<PropertyKey, int> properties = new Dictionary<PropertyKey, int>();
            List<byte> response = new List<byte>();

            try
            {
                for (int i = 1; i <= Constants.retryCount; ++i)
                {
                    Log.WriteLine("TX: {0}", Utilities.ByteArrayToString(input));
                    port.Write(input);


                    port.Wait(Constants.openTimeout);
                    byte[] output = port.ReadAll();
                    if (output.Length <= 0)
                    {
                        Thread.Sleep(100);
                        continue; // try again
                    }

                    response.Clear();
                    response.AddRange(output);

                    while (output.Length > 0 && response.Count < 9)
                    {
                        port.Wait(Constants.openTimeout);
                        output = port.ReadAll();
                        response.AddRange(output);
                    }

                    Log.WriteLine("RX: {0}", Utilities.ByteArrayToString(response.ToArray()));

                    int offset = 0;
                    for (int j = 0; j < response.Count; ++j)
                        if (response[j] == 0x02)
                            offset = j;

                    if (response.Count >= offset + 9 && response[offset] == 0x02 && response[offset + 1] == 0x60 && response[offset + 8] == 0x06)
                    {
                        properties[PropertyKey.Address] = response[offset + 2] << 16 | response[offset + 3] << 8 | response[offset + 4];
                        properties[PropertyKey.DevCat] = response[offset + 5];
                        properties[PropertyKey.SubCat] = response[offset + 6];
                        properties[PropertyKey.FirmwareVersion] = response[offset + 7];
                        break; // found
                    }
                }
            }
            finally
            {
                if (response.Count == 0)
                    throw new IOException("Failed to open port, timeout waiting for response from port.");

                if (properties.Keys.Count == 0)
                {
                    port.Close();
                    port = null;
                    throw new IOException("Failed to open port, unable to negotiate with INSTEON controller.");
                }
            }

            Log.WriteLine("Successfully negotiated with INSTEON controller on connection '{0}'...", connection);
            port.SetNotify(DataAvailable);
            return properties;
        }

        public void Close()
        {
            if (port != null)
            {
                port.SetNotify(null);
                port.Close();
                port = null;
            }
        }

        public EchoStatus Send(byte[] message, bool retryOnNak)
        {
            if (port == null)
                throw new InvalidOperationException();

            port.SetNotify(null);
            EchoStatus status = EchoStatus.Unknown;
            try
            {
                ProcessData(); // process any pending data before sending a new command

                byte[] input = new byte[message.Length + 1];
                input[0] = 0x02;
                message.CopyTo(input, 1);

                int retry = -1;
                while (retry++ <= Constants.retryCount)
                {
                    if (retry <= 0)
                        Log.WriteLine("TX: {0}", Utilities.ByteArrayToString(input));
                    else
                        Log.WriteLine("TX: {0} - RETRY {1} of {2}", Utilities.ByteArrayToString(input), retry, Constants.retryCount);
                    port.Write(input);
                    status = ProcessEcho();
                    if (status == EchoStatus.ACK)
                        return status;
                    if (status == EchoStatus.NAK && !retryOnNak)
                        return status;
                }

                Log.WriteLine("Send failed after {0} retries", Constants.retryCount);
                return status;
            }
            finally
            {
                port.SetNotify(DataAvailable);
            }
        }

        private EchoStatus ProcessEcho()
        {
            if (port == null || messageProcessor == null)
                throw new InvalidOperationException();

            byte[] data = port.ReadAll();

            int retryCount = 0;
            while (data.Length == 0 && retryCount <= Constants.retryCount)
            {
                Thread.Sleep(Constants.retryTime);
                data = port.ReadAll();
            }

            if (data.Length == 0)
            {
                Log.WriteLine("ERROR: No data read from port");
                return EchoStatus.Unknown;
            }

            int offset = 0;
            while (offset < data.Length) // scan until a 02 is detected
                if (data[offset++] == 0x02)
                    break;

            if (offset >= data.Length)
            {
                Log.WriteLine("RX: {0} ERROR - Failed to find leading 02 byte", Utilities.ByteArrayToString(data));
                return EchoStatus.Unknown;
            }

            if (offset > 1)
                Log.WriteLine("SKIPPING {0} BYTES TO: {1}", offset - 1, Utilities.ByteArrayToString(data, offset - 1));

            int count;
            if (messageProcessor.ProcessEcho(data, offset, out count))
            {
                int j = offset + count;
                byte result = j < data.Length ? data[j] : (byte)0x00;
                j += 1;
                if (data.Length > j) // if there's data beyond the echo then add it to the buffer
                {
                    int remainingCount = data.Length - j;
                    byte[] remainingData = new byte[remainingCount];
                    Array.Copy(data, j, remainingData, 0, remainingCount);
                    lock (buffer)
                        buffer.AddRange(remainingData);
                    ProcessData(); //process the rest of the data stream
                }
                if (result == 0x06)
                {
                    messageProcessor.SetEchoStatus(EchoStatus.ACK);
                    Log.WriteLine("RX: {0} [ACK]", Utilities.ByteArrayToString(data, offset - 1, count + 2)); // +1 for 02, +1 for 06
                    return EchoStatus.ACK;
                }
                else if (result == 0x15)
                {
                    Log.WriteLine("RX: {0} [NAK]", Utilities.ByteArrayToString(data, offset - 1, count + 2)); // +1 for 02, +1 for 15
                    messageProcessor.SetEchoStatus(EchoStatus.NAK);
                    return EchoStatus.NAK;
                }
                else
                {
                    Log.WriteLine("RX: {0} ERROR - Unknown trailing byte", Utilities.ByteArrayToString(data, offset - 1, count + 2)); // +1 for 02, +1 for ??
                    messageProcessor.SetEchoStatus(EchoStatus.Unknown);
                    return EchoStatus.Unknown;
                }
            }
            else
            {
                Log.WriteLine("RX: {0} ERROR - Echo mismatch", Utilities.ByteArrayToString(data, offset - 1));
                return EchoStatus.Unknown;
            }
        }

        private void ProcessData()
        {
            if (port == null || messageProcessor == null)
                throw new InvalidOperationException();

            byte[] data = port.ReadAll();
            if (data.Length > 0)
                Log.WriteLine("RX: {0}", Utilities.ByteArrayToString(data));

            lock (buffer)
            {
                if (data.Length > 0)
                    buffer.AddRange(data);
                data = buffer.ToArray();
                buffer.Clear();
            }

            if (data.Length > 0)
            {
                int count = 0;
                int offset = 0;
                int last = 0;
                while (offset < data.Length)
                {
                    if (data[offset++] == 0x02)
                    {
                        if (last != offset - 1)
                            Log.WriteLine("SKIPPING {0} BYTES TO: {1}", offset - last, Utilities.ByteArrayToString(data, offset - 1));
                        int retry = 0;
                        while (++retry < Constants.retryCount)
                        {
                            if (messageProcessor.ProcessMessage(data, offset, out count))
                            {
                                offset += count;
                                last = offset;
                                break;
                            }
                            else
                            {
                                Thread.Sleep(Constants.retryTime);
                                byte[] more = port.ReadAll();
                                if (more.Length > 0)
                                {
                                    List<byte> list = new List<byte>(data);
                                    list.AddRange(more);
                                    data = list.ToArray();
                                    Log.WriteLine("RX: {0} - RETRY {1} of {2} - got more data", Utilities.ByteArrayToString(data), retry, Constants.retryCount);
                                }
                                else
                                {
                                    Log.WriteLine("RX: {0} - RETRY {1} of {2} - waiting for more data", Utilities.ByteArrayToString(data), retry, Constants.retryCount);
                                }
                            }
                        }
                    }
                }
                if (last != offset)
                    Log.WriteLine("DISCARDING {0} BYTES: {1}", offset - last, Utilities.ByteArrayToString(data, last));
            }
        }

        private void DataAvailable()
        {
            ProcessData();
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        public interface IMessageProcessor
        {
            bool ProcessMessage(byte[] data, int offset, out int count);
            bool ProcessEcho(byte[] data, int offset, out int count);
            void SetEchoStatus(EchoStatus status);
        }
    }
}
