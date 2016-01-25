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
    /// <summary>
    /// Represents an individual INSTEON device on the network.
    /// </summary>
    public class InsteonDevice
    {
        /// <summary>
        /// Invoked when a device fails to respond to a command within the timeout period of 2 seconds.
        /// </summary>
        public event InsteonDeviceEventHandler DeviceCommandTimeout;

        /// <summary>
        /// Invoked when the device has been identified.
        /// </summary>
        public event InsteonDeviceEventHandler DeviceIdentified;

        /// <summary>
        /// Invoked when a status message is received from the INSTEON device, for example when the device turns on or off.
        /// </summary>
        public event InsteonDeviceStatusChangedEventHandler DeviceStatusChanged;

        private readonly InsteonNetwork network;
        private readonly Timer ackTimer; // timeout to receive ACK from device
        private InsteonDeviceCommands? pendingCommand = null; // Gets the command that is currently pending on the device, or null if no command is pending.
        private readonly AutoResetEvent pendingEvent = new AutoResetEvent(false);
        private byte pendingValue = 0;
        private int pendingRetry = 0; // retry count for pending command

        private enum DimmerDirection { None, Up, Down }
        private DimmerDirection dimmerDirection = DimmerDirection.None;

        internal InsteonDevice(InsteonNetwork network, InsteonAddress address, InsteonIdentity identity)
        {
            this.network = network;
            this.Address = address;
            this.Identity = identity;
            this.ackTimer = new Timer(new TimerCallback(this.PendingCommandTimerCallback), null, Timeout.Infinite, Constants.deviceAckTimeout);
        }

        /// <summary>
        /// The INSTEON address of the device.
        /// </summary>
        public InsteonAddress Address { get; private set; }

        /// <summary>
        /// Indicates the type of the INSTEON device.
        /// </summary>
        public InsteonIdentity Identity { get; private set; }

        /// <summary>
        /// Sends an INSTEON command to the device.
        /// </summary>
        /// <remarks>
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending.
        /// The <see cref="DeviceStatusChanged">DeviceStatusChanged</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        public void Command(InsteonDeviceCommands command)
        {
            if (command == InsteonDeviceCommands.On)
                this.Command(command, 0xFF);
            else
                this.Command(command, 0x00);
        }

        /// <summary>
        /// Sends an INSTEON command to the device.
        /// </summary>
        /// <remarks>
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending.
        /// The <see cref="DeviceStatusChanged">DeviceStatusChanged</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        /// <param name="value">A parameter value required by some commands.</param>
        public void Command(InsteonDeviceCommands command, byte value)
        {
            if (!this.TryCommand(command, value))
                throw new IOException(string.Format("Failed to send command '{0}' for device '{1}'", command.ToString(), this.Address.ToString()));
        }

        /// <summary>
        /// Returns the list of INSTEON commands supported by this device.
        /// </summary>
        /// <returns>An array of supported INSTEON commands.</returns>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public InsteonDeviceCommands[] GetCommands()
        {
            var commands = new List<InsteonDeviceCommands>();
            switch (this.Identity.DevCat)
            {
                case 0x01: // SwitchLinc Dimmer, LampLinc, OutletLinc Dimmer, KeypadLinc Dimmer, ...
                    commands.Add(InsteonDeviceCommands.On);
                    commands.Add(InsteonDeviceCommands.Off);
                    commands.Add(InsteonDeviceCommands.FastOn);
                    commands.Add(InsteonDeviceCommands.FastOff);
                    commands.Add(InsteonDeviceCommands.Brighten);
                    commands.Add(InsteonDeviceCommands.Dim);
                    commands.Add(InsteonDeviceCommands.StartDimming);
                    commands.Add(InsteonDeviceCommands.StopDimming);
                    break;

                case 0x02: // SwitchLinc On/Off, ApplianceLinc, OutletLinc, KeypadLinc On/Off, ...
                case 0x09: // Load Controller, ...
                    commands.Add(InsteonDeviceCommands.On);
                    commands.Add(InsteonDeviceCommands.Off);
                    break;

                case 0x00: // RemoteLinc, ...
                case 0x03: // PowerLinc, SmartLinc, ...
                case 0x05: // TempLinc, ...
                case 0x07: // IOLinc, ...
                case 0x10: // TriggerLinc, Motion sensors
                default:
                    break;
            }
            return commands.ToArray();
        }

        /// <summary>
        /// Gets a value that indicates the on-level of the device.
        /// </summary>
        /// <returns>
        /// A value indicating the on-level of the device. For a dimmer a value between 0 and 255 will be returned. For a non-dimmer a value 0 or 255 will be returned.
        /// </returns>
        /// <remarks>
        /// This is a blocking method that sends an INSTEON message to the target device and waits for a reply, or until the device command times out.
        /// </remarks>
        public byte GetOnLevel()
        {
            byte value;
            if (!this.TryGetOnLevel(out value))
                throw new IOException();
            return value;
        }

        private static byte[] GetStandardMessage(InsteonAddress address, byte cmd1, byte cmd2)
        {
            byte[] message = { 0x62, address[2], address[1], address[0], 0x0F, cmd1, cmd2 };
            return message;
        }

        /// <summary>
        /// Determines the type of INSTEON device by querying the device.
        /// </summary>
        /// <remarks>
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending.
        /// The <see cref="DeviceIdentified">DeviceIdentified</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public void Identify()
        {
            this.Identity = new InsteonIdentity();
            this.Command(InsteonDeviceCommands.IDRequest);
        }

        private void OnDeviceCommandTimeout()
        {
            if (this.DeviceCommandTimeout != null)
                this.DeviceCommandTimeout(this, new InsteonDeviceEventArgs(this));
            this.network.Devices.OnDeviceCommandTimeout(this);
        }

        private void OnDeviceIdentified()
        {
            if (this.DeviceIdentified != null)
                this.DeviceIdentified(this, new InsteonDeviceEventArgs(this));
            this.network.Devices.OnDeviceIdentified(this);
        }

        private void OnDeviceStatusChanged(InsteonDeviceStatus status)
        {
            if (this.DeviceStatusChanged != null)
                this.DeviceStatusChanged(this, new InsteonDeviceStatusChangedEventArgs(this, status));
            this.network.Devices.OnDeviceStatusChanged(this, status);
        }

        private void OnSetButtonPressed(InsteonMessage message)
        {
            if (this.Identity.IsEmpty)
            {
                var devCat = (byte)message.Properties[PropertyKey.DevCat];
                var subCat = (byte)message.Properties[PropertyKey.DevCat];
                var firmwareVersion = (byte)message.Properties[PropertyKey.DevCat];
                this.Identity = new InsteonIdentity(devCat, subCat, firmwareVersion);
            }
            this.OnDeviceIdentified();
        }

        internal void OnMessage(InsteonMessage message)
        {
            switch (message.MessageType)
            {
                case InsteonMessageType.Ack:
                    var cmd = this.PendingCommandAck(message);

                    if (cmd.HasValue)
                    {
                        // This feels wrong, but I'm not seeing the 'DeviceStatusChanged' as a result of setting the status.
                        // Could probably use some sort of 'map' here to map command to status.
                        switch (cmd.Value)
                        {
                            case InsteonDeviceCommands.On:
                                this.OnDeviceStatusChanged(InsteonDeviceStatus.On);
                                break;
                            case InsteonDeviceCommands.Off:
                                this.OnDeviceStatusChanged(InsteonDeviceStatus.Off);
                                break;
                        }
                    }
                    break;

                case InsteonMessageType.OnCleanup:
                    this.OnDeviceStatusChanged(InsteonDeviceStatus.On);
                    break;

                case InsteonMessageType.OffCleanup:
                    this.OnDeviceStatusChanged(InsteonDeviceStatus.Off);
                    break;

                case InsteonMessageType.FastOnCleanup:
                    this.OnDeviceStatusChanged(InsteonDeviceStatus.On);
                    this.OnDeviceStatusChanged(InsteonDeviceStatus.FastOn);
                    break;

                case InsteonMessageType.FastOffCleanup:
                    this.OnDeviceStatusChanged(InsteonDeviceStatus.Off);
                    this.OnDeviceStatusChanged(InsteonDeviceStatus.FastOff);
                    break;

                case InsteonMessageType.IncrementBeginBroadcast:
                    this.dimmerDirection = message.Properties[PropertyKey.IncrementDirection] != 0 ? DimmerDirection.Up : DimmerDirection.Down;
                    break;

                case InsteonMessageType.IncrementEndBroadcast:
                    if (this.dimmerDirection == DimmerDirection.Up)
                    {
                        this.OnDeviceStatusChanged(InsteonDeviceStatus.Brighten);
                    }
                    else if (this.dimmerDirection == DimmerDirection.Down)
                    {
                        this.OnDeviceStatusChanged(InsteonDeviceStatus.Dim);
                    }
                    break;

                case InsteonMessageType.SetButtonPressed:
                    this.OnSetButtonPressed(message);
                    break;
            }
        }

        // if a command is pending determines whether the current message completes the pending command
        private InsteonDeviceCommands? PendingCommandAck(InsteonMessage message)
        {
            lock (this.pendingEvent)
            {
                if (this.pendingCommand != null)
                {
                    var cmd1 = message.Properties[PropertyKey.Cmd1];
                    if (Enum.IsDefined(typeof(InsteonDeviceCommands), cmd1))
                    {
                        var command = (InsteonDeviceCommands)cmd1;
                        if (this.pendingCommand.Value == command)
                        {
                            this.pendingCommand = null;
                            this.pendingValue = 0;
                            this.ackTimer.Change(Timeout.Infinite, Timeout.Infinite); // stop ACK timeout timer
                            this.pendingEvent.Set(); // unblock any thread that may be waiting on the pending command
                            return command;
                        }
                    }
                }
            }

            return null;
        }

        private void ClearPendingCommand()
        {
            lock (this.pendingEvent)
            {
                this.pendingCommand = null;
                this.pendingValue = 0;
                this.ackTimer.Change(Timeout.Infinite, Timeout.Infinite); // stop ACK timeout timer
                this.pendingEvent.Set(); // unblock any thread that may be waiting on the pending command
            }
        }

        // invoked when a pending command times out
        private void PendingCommandTimerCallback(object state)
        {
            this.ackTimer.Change(Timeout.Infinite, Timeout.Infinite); // stop ACK timeout timer

            var retry = false;
            var command = InsteonDeviceCommands.On;
            byte value = 0;
            var retryCount = 0;
            
            lock (this.pendingEvent)
            {
                if (this.pendingCommand == null)
                    return;

                this.pendingRetry += 1;
                if (this.pendingRetry <= Constants.deviceCommandRetries)
                {
                    retry = true;
                    value = this.pendingValue;
                    retryCount = this.pendingRetry;
                }
                else
                {
                    retry = false;
                    command = this.pendingCommand.Value;
                    this.pendingCommand = null;
                    this.pendingValue = 0;
                    this.pendingEvent.Set(); // unblock any thread that may be waiting on the pending command                
                }
            }
            
            if (retry)
            {
                Log.WriteLine("WARNING: Device {0} Command {1} timed out, retry {2} of {3}...", this.Address.ToString(), command, retryCount, Constants.deviceCommandRetries);
                this.TryCommandInternal(command, value);
            }
            else
            {
                Log.WriteLine("ERROR: Device {0} Command {1} timed out", this.Address.ToString(), command);
                this.OnDeviceCommandTimeout();
            }
        }

        /// <summary>
        /// Sends an INSTEON command to the device.
        /// </summary>
        /// <remarks>
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending.
        /// The <see cref="DeviceStatusChanged">DeviceStatusChanged</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        public bool TryCommand(InsteonDeviceCommands command)
        {
            if (command == InsteonDeviceCommands.On)
                return this.TryCommand(command, 0xFF);
            else
                return this.TryCommand(command, 0x00);
        }

        /// <summary>
        /// Sends an INSTEON command to the device.
        /// </summary>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        /// <param name="value">A parameter value required by some commands.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending.
        /// The <see cref="DeviceStatusChanged">DeviceStatusChanged</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public bool TryCommand(InsteonDeviceCommands command, byte value)
        {
            this.WaitAndSetPendingCommand(command, value);
            return this.TryCommandInternal(command, value);
        }

        private bool TryCommandInternal(InsteonDeviceCommands command, byte value)
        {
            var message = GetStandardMessage(this.Address, (byte)command, value);
            Log.WriteLine("Device {0} Command(command:{1}, value:{2:X2})", this.Address.ToString(), command.ToString(), value);

            var status = this.network.Messenger.TrySend(message);
            if (status == EchoStatus.ACK)
            {
                this.ackTimer.Change(Constants.deviceAckTimeout, Timeout.Infinite); // start ACK timeout timer   
                return true;
            }
            else
            {
                this.ClearPendingCommand();
                return false;
            }
        }

        /// <summary>
        /// Gets a value that indicates the on-level of the device.
        /// </summary>
        /// <returns>
        /// A value indicating the on-level of the device. For a dimmer a value between 0 and 255 will be returned. For a non-dimmer a value 0 or 255 will be returned.
        /// </returns>
        /// <remarks>
        /// This is a blocking method that sends an INSTEON message to the target device and waits for a reply, or until the device command times out.
        /// </remarks>
        public bool TryGetOnLevel(out byte value)
        {
            var command = InsteonDeviceCommands.StatusRequest;
            this.WaitAndSetPendingCommand(command, 0);
            Log.WriteLine("Device {0} GetOnLevel", this.Address.ToString());
            var message = GetStandardMessage(this.Address, (byte)command, 0);
            Dictionary<PropertyKey, int> properties;
            var status = this.network.Messenger.TrySendReceive(message, true, 0x50, out properties); // on-level returned in cmd2 of ACK
            if (status == EchoStatus.ACK && properties != null)
            {
                value = (byte)properties[PropertyKey.Cmd2];
                Log.WriteLine("Device {0} GetOnLevel returning {1:X2}", this.Address.ToString(), value);
                return true;
            }
            else
            {
                this.ClearPendingCommand();
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// Determines the type of INSTEON device by querying the device.
        /// </summary>
        /// <remarks>
        /// This method does not throw an exception.
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending.
        /// The <see cref="DeviceIdentified">DeviceIdentified</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public bool TryIdentify()
        {
            this.Identity = new InsteonIdentity();
            return this.TryCommand(InsteonDeviceCommands.IDRequest);
        }

        /// <summary>
        /// Removes links within both the INSTEON device and the INSTEON controller for the specified group.
        /// </summary>
        /// <param name="group">The specified group within which links are to be removed.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending.
        /// A <see cref="InsteonController.DeviceLinked">DeviceLinked</see> event will be invoked on the controller if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public bool TryUnlink(byte group)
        {
            if (this.network.Controller.TryEnterLinkMode(InsteonLinkMode.Delete, group))
                return this.TryCommand(InsteonDeviceCommands.EnterLinkingMode, group);
            else
                return false;
        }

        // blocks the current thread if a command is pending, then sets the current command as the pending command (note does not apply to all commands)
        private void WaitAndSetPendingCommand(InsteonDeviceCommands command, byte value)
        {
            InsteonDeviceCommands latchedPendingCommand;

            lock (this.pendingEvent)
            {
                if (this.pendingCommand == null)
                {
                    this.pendingCommand = command;
                    this.pendingValue = value;
                    this.pendingRetry = 0;
                    return;
                }
                latchedPendingCommand = this.pendingCommand.Value;
            }

            // block current thread if a command is pending
            Log.WriteLine("Device {0} blocking command {1} for pending command {2}", this.Address.ToString(), command.ToString(), latchedPendingCommand.ToString());
            this.pendingEvent.Reset();
            if (!this.pendingEvent.WaitOne(Constants.deviceAckTimeout)) // wait at most deviceAckTimeout seconds
            {
                this.ClearPendingCommand(); // break deadlock and warn
                Log.WriteLine("WARNING: Device {0} unblocking command {1} for pending command {2}", this.Address.ToString(), command.ToString(), latchedPendingCommand.ToString());
            }

            this.WaitAndSetPendingCommand(command, value); // try again
        }

        /// <summary>
        /// Removes links within both the INSTEON device and the INSTEON controller for the specified group.
        /// </summary>
        /// <param name="group">The specified group within which links are to be removed.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending.
        /// A <see cref="InsteonController.DeviceLinked">DeviceLinked</see> event will be invoked on the controller if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public void Unlink(byte group)
        {
            this.network.Controller.EnterLinkMode(InsteonLinkMode.Delete, group);
            this.Command(InsteonDeviceCommands.EnterUnlinkingMode, group);
        }
    }
}
