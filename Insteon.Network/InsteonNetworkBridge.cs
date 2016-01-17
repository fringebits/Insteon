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

namespace Insteon.Network
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using Serial;

    // This class is responsible for bridging the logical network to the physical INSTEON network via the serial interface.
    // The responsibilities of the bridge include:
    //  - Owning the serial connection to the INSTEON controller device.
    //  - Directing all serial communications through the serial interface to the INSTEON controller device.
    //  - Detecting the beginning of each raw message by identifying the MESSAGE START byte (02).
    //  - Delegating to the messenger the interpretation of the bytes following the header byte.
    //    Note: The messenger is responsible for reporting back to the bridge whether or not each message is valid, and if valid the size in bytes of the message.
    //  - Verifying the message integrity by checking the trailer byte for ACK (06) or NAK (15), and informing the messenger of the result.
    internal class InsteonNetworkBridge : IDisposable
    {
        private readonly List<byte> buffer = new List<byte>(); // buffer of received data to be processed
        private ISerialPort port; // serial port connection to the INSTEON controller
        private readonly IMessageProcessor messageProcessor; // reference to creator that handles processing of raw binary data into higher level messages
        
        public InsteonNetworkBridge(IMessageProcessor messageProcessor)
        {
            if (messageProcessor == null)
            {
                throw new ArgumentNullException("messageProcessor");
            }
            this.messageProcessor = messageProcessor;
        }

        public void Close()
        {
            if (this.port != null)
            {
                this.port.SetNotify(null);
                this.port.Close();
                this.port = null;
            }
        }

        public Dictionary<PropertyKey, int> Connect(InsteonConnection connection)
        {
            if (this.port != null)
                this.port.Close();

            this.port = SerialPortCreator.Create(connection);
            this.port.Open();

            var input = new byte[] { 0x02, 0x60 };
            var properties = new Dictionary<PropertyKey, int>();
            var response = new List<byte>();

            try
            {
                for (var i = 1; i <= Constants.negotiateRetries; ++i)
                {
                    Log.WriteLine("TX: {0}", Utilities.ByteArrayToString(input));
                    this.port.Write(input);

                    this.port.Wait(Constants.openTimeout);
                    var output = this.port.ReadAll();
                    if (output.Length <= 0)
                    {
                        Thread.Sleep(100);
                        continue; // try again
                    }

                    response.Clear();
                    response.AddRange(output);

                    while (output.Length > 0 && response.Count < 9)
                    {
                        this.port.Wait(Constants.openTimeout);
                        output = this.port.ReadAll();
                        response.AddRange(output);
                    }

                    Log.WriteLine("RX: {0}", Utilities.ByteArrayToString(response.ToArray()));

                    var offset = 0;
                    for (var j = 0; j < response.Count; ++j)
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
                    this.port.Close();
                    this.port = null;
                    throw new IOException("Failed to open port, unable to negotiate with INSTEON controller.");
                }
            }

            Log.WriteLine("Successfully negotiated with INSTEON controller on connection '{0}'...", connection);
            this.port.SetNotify(this.DataAvailable);
            return properties;
        }

        private void DataAvailable()
        {
            this.ProcessData();
        }

        public bool IsConnected { get { return this.port != null; } }

        private void ProcessData()
        {
            if (this.port == null || this.messageProcessor == null)
            {
                throw new InvalidOperationException();
            }

            var data = this.ReadData(0, false);
            if (data.Length > 0)
            {
                Log.WriteLine("RX: {0}", Utilities.ByteArrayToString(data));
            }

            lock (this.buffer)
            {
                if (data.Length > 0)
                {
                    this.buffer.AddRange(data);
                }
                data = this.buffer.ToArray();
                this.buffer.Clear();
            }

            if (data.Length > 0)
            {
                var count = 0;
                var offset = 0;
                var last = 0;
                while (offset < data.Length)
                {
                    if (data[offset++] == 0x02)
                    {
                        if (last != offset - 1)
                        {
                            Log.WriteLine("WARNING: Skipping {0} bytes to '{1}', discarded '{2}'", offset - last, Utilities.ByteArrayToString(data, offset - 1), Utilities.ByteArrayToString(data, 0, offset - 1));
                        }

                        // loop until message successfully processed or until there is no more data available on the serial port...
                        while (true) 
                        {
                            if (this.messageProcessor.ProcessMessage(data, offset, out count))
                            {
                                offset += count;
                                last = offset;
                                break; // break out of the loop when message successfully processed
                            }

                            var appendData = this.ReadData(1, false); // try to read at least one more byte, waits up to Constants.readTime milliseconds
                            if (appendData.Length == 0)
                            {
                                Log.WriteLine("WARNING: Could not process data '{0}'", Utilities.ByteArrayToString(data));
                                break; // break out of the loop when there is no more data available on the serial port
                            }

                            var list = new List<byte>(data);
                            list.AddRange(appendData);
                            data = list.ToArray();
                            Log.WriteLine("RX: {0} (appended)", Utilities.ByteArrayToString(data));
                        }
                    }
                }

                if (last != offset)
                {
                    Log.WriteLine("WARNING: Discarding {0} bytes '{1}'", offset - last, Utilities.ByteArrayToString(data, last));
                }
            }
        }

        private EchoStatus ProcessEcho(int echoLength)
        {
            if (this.port == null || this.messageProcessor == null)
            {
                throw new InvalidOperationException();
            }

            var data = this.ReadData(echoLength, true);
            if (data.Length == 0)
            {
                Log.WriteLine("ERROR: No data read from port");
                return EchoStatus.None;
            }

            // if the first byte is a NAK (15) then return a NAK and add whatever additional data was read to the buffer
            if (data[0] == 0x15)
            {
                Log.WriteLine("RX: {0}", Utilities.ByteArrayToString(data));
                if (data.Length > 1)
                {
                    var remainingCount = data.Length - 1;
                    var remainingData = new byte[remainingCount];
                    Array.Copy(data, 1, remainingData, 0, remainingCount);
                    lock (this.buffer)
                    {
                        this.buffer.AddRange(remainingData);
                    }
                    this.ProcessData(); //process the rest of the data stream
                }
                return EchoStatus.NAK;
            }

            // scan until a MESSAGE START byte (02) is detected, which should be the first byte
            var offset = 0;
            while (offset < data.Length)
            {
                if (data[offset++] == 0x02)
                    break;
            }

            // exit if no MESSAGE START byte detected
            if (offset >= data.Length)
            {
                Log.WriteLine("RX: {0} ERROR - Failed to find MESSAGE START byte (02)", Utilities.ByteArrayToString(data));
                return EchoStatus.Unknown;
            }

            Log.WriteLine("RX: {0}", Utilities.ByteArrayToString(data));

            // warn about any skipped bytes
            if (offset > 1)
            {
                Log.WriteLine("WARNING: Skipping {0} bytes to '{1}', discarded '{2}'", offset - 1, Utilities.ByteArrayToString(data, offset - 1), Utilities.ByteArrayToString(data, 0, offset - 1));
            }

            // process the echo and decode the trailing status byte
            int count;
            if (this.messageProcessor.ProcessEcho(data, offset, out count))
            {
                var j = offset + count;
                var result = j < data.Length ? data[j] : (byte)0x00;
                j += 1;

                if (data.Length > j) // if there's data beyond the echo then add it to the buffer
                {
                    var remainingCount = data.Length - j;
                    var remainingData = new byte[remainingCount];
                    Array.Copy(data, j, remainingData, 0, remainingCount);
                    lock (this.buffer)
                        this.buffer.AddRange(remainingData);
                    this.ProcessData(); //process the rest of the data stream
                }

                if (result == 0x06)
                {
                    this.messageProcessor.SetEchoStatus(EchoStatus.ACK);
                    Log.WriteLine("PLM: {0} ACK", Utilities.ByteArrayToString(data, offset - 1, count + 2)); // +1 for MESSAGE START byte (02), +1 for ACK byte (06)
                    return EchoStatus.ACK;
                }

                if (result == 0x15)
                {
                    Log.WriteLine("PLM: {0} NAK", Utilities.ByteArrayToString(data, offset - 1, count + 2)); // +1 for MESSAGE START byte (02), +1 for NAK byte (15)
                    this.messageProcessor.SetEchoStatus(EchoStatus.NAK);
                    return EchoStatus.NAK;
                }

                Log.WriteLine("PLM: {0} Unknown trailing byte", Utilities.ByteArrayToString(data, offset - 1, count + 2)); // +1 for MESSAGE START byte (02), +1 for unknown byte
                this.messageProcessor.SetEchoStatus(EchoStatus.Unknown);
                return EchoStatus.Unknown;
            }

            Log.WriteLine("PLM: {0} Echo mismatch", Utilities.ByteArrayToString(data, offset - 1));
            return EchoStatus.Unknown;
        }

        private byte[] ReadData(int expectedBytes, bool isEcho)
        {
            var list = new List<byte>();
            var data = this.port.ReadAll();
            list.AddRange(data);

            // if we are expecting an echo response and the first byte received was a NAK (15) then don't bother waiting for more data
            if (isEcho && data.Length > 0 && data[0] == 0x15)
                return list.ToArray();  // caller will log the NAK

            // if we didn't get the expected number of bytes then try to read more data within a timeout period
            if (expectedBytes > 0)
            {
                var retryCount = 0;
                while (list.Count == 0 && ++retryCount <= Constants.readDataRetries)
                {
                    this.port.Wait(Constants.readDataRetryTime);
                    data = this.port.ReadAll();
                    list.AddRange(data);
                }

                if (list.Count < expectedBytes)
                {
                    do
                    {
                        this.port.Wait(Constants.readDataTimeout);
                        data = this.port.ReadAll();
                        list.AddRange(data);
                    } while (data.Length > 0);
                }

                // if we are expecting an echo response and the first byte received was a NAK (15) then don't bother waiting for more data
                if (isEcho && list.Count > 0 && list[0] == 0x15)
                    return list.ToArray(); // caller will log the NAK

                if (list.Count < expectedBytes)
                {
                    if (list.Count > 0)
                        Log.WriteLine("WARNING: Could not read the expected number of bytes from the serial port; BytesRead='{0}', Expected={1}, Received={2}, Timeout={3}ms", Utilities.ByteArrayToString(list.ToArray()), expectedBytes, list.Count, Constants.readDataTimeout);
                    else
                        Log.WriteLine("WARNING: Could not read the expected number of bytes from the serial port; Expected={0}, Received=0, Timeout={1}ms", expectedBytes, Constants.readDataTimeout);
                }
            }
                
            return list.ToArray();
        }

        public EchoStatus Send(byte[] message, bool retryOnNak, int echoLength)
        {
            if (this.port == null)
            {
                throw new InvalidOperationException();
            }

            this.port.SetNotify(null);
            var status = EchoStatus.None;
            try
            {
                this.ProcessData(); // process any pending data before sending a new command

                var input = new byte[message.Length + 1];
                input[0] = 0x02;
                message.CopyTo(input, 1);

                var retry = -1;
                while (retry++ < Constants.sendMessageRetries)
                {
                    if (retry <= 0)
                    {
                        Log.WriteLine("TX: {0}", Utilities.ByteArrayToString(input));
                    }
                    else
                    {
                        Thread.Sleep(retry * Constants.sendMessageWaitTime);
                        Log.WriteLine("TX: {0} - RETRY {1} of {2}", Utilities.ByteArrayToString(input), retry, Constants.sendMessageRetries);
                    }

                    this.port.Write(input);
                    status = this.ProcessEcho(echoLength + 2); // +1 for leading 02 byte, +1 for trailing ACK/NAK byte
                    if (status == EchoStatus.ACK)
                        return status;

                    if (status == EchoStatus.NAK && !retryOnNak)
                        return status;
                }

                Log.WriteLine("Send failed after {0} retries", Constants.sendMessageRetries);
                return status;
            }
            finally
            {
                this.port.SetNotify(this.DataAvailable);
            }
        }

        void IDisposable.Dispose()
        {
            this.Close();
        }

        public interface IMessageProcessor
        {
            bool ProcessMessage(byte[] data, int offset, out int count);
            bool ProcessEcho(byte[] data, int offset, out int count);
            void SetEchoStatus(EchoStatus status);
        }
    }
}
