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

        /// <summary>
        /// Gets the command that is currently pending on the device, or null if no command is pending.
        /// </summary>
        public InsteonDeviceCommands? PendingCommand { get; private set; }

        private readonly InsteonNetwork network;
        private readonly Timer pendingTimer;
        private readonly AutoResetEvent pendingEvent = new AutoResetEvent(false);

        private enum DimmerDirection { None, Up, Down }
        private DimmerDirection dimmerDirection = DimmerDirection.None;

        internal InsteonDevice(InsteonNetwork network, InsteonAddress address, InsteonIdentity identity)
        {
            this.network = network;
            this.Address = address;
            this.Identity = identity;
            this.pendingTimer = new Timer(new TimerCallback(PendingCommandTimerCallback), null, Timeout.Infinite, Constants.messageTimeout);
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
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending. Check the <see cref="PendingCommand">PendingCommand</see> property to determine whether a command is pending.
        /// The <see cref="DeviceStatusChanged">DeviceStatusChanged</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        public void Command(InsteonDeviceCommands command)
        {
            if (command == InsteonDeviceCommands.On)
                Command(command, 0xFF);
            else
                Command(command, 0x00);
        }

        /// <summary>
        /// Sends an INSTEON command to the device.
        /// </summary>
        /// <remarks>
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending. Check the <see cref="PendingCommand">PendingCommand</see> property to determine whether a command is pending.
        /// The <see cref="DeviceStatusChanged">DeviceStatusChanged</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        /// <param name="value">A parameter value required by some commands.</param>
        public void Command(InsteonDeviceCommands command, byte value)
        {
            WaitAndSetPendingCommand(command);
            byte[] message = GetStandardMessage(Address, (byte)command, value);
            Log.WriteLine("Device.Command(command:{0}, value:{1:X2})", command.ToString(), value);
            network.Messenger.Send(message);
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
            List<InsteonDeviceCommands> commands = new List<InsteonDeviceCommands>();
            switch (Identity.DevCat)
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
        /// This is a blocking method that sends an INSTEON message to the target device and waits for a reply, or until a timeout of 2 seconds has passed.
        /// </remarks>
        public byte GetOnLevel()
        {
            byte value;
            if (!TryGetOnLevel(out value))
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
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending. Check the <see cref="PendingCommand">PendingCommand</see> property to determine whether a command is pending.
        /// The <see cref="DeviceIdentified">DeviceIdentified</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public void Identify()
        {
            this.Identity = new InsteonIdentity();
            Command(InsteonDeviceCommands.IDRequest);
        }

        private void OnDeviceCommandTimeout()
        {
            if (DeviceCommandTimeout != null)
                DeviceCommandTimeout(this, new InsteonDeviceEventArgs(this));
            network.Devices.OnDeviceCommandTimeout(this);
        }

        private void OnDeviceIdentified()
        {
            if (DeviceIdentified != null)
                DeviceIdentified(this, new InsteonDeviceEventArgs(this));
            network.Devices.OnDeviceIdentified(this);
        }

        private void OnDeviceStatusChanged(InsteonDeviceStatus status)
        {
            if (DeviceStatusChanged != null)
                DeviceStatusChanged(this, new InsteonDeviceStatusChangedEventArgs(this, status));
            network.Devices.OnDeviceStatusChanged(this, status);
        }

        private void OnSetButtonPressed(InsteonMessage message)
        {
            if (this.Identity.IsEmpty)
            {
                byte devCat = (byte)message.Properties[PropertyKey.DevCat];
                byte subCat = (byte)message.Properties[PropertyKey.DevCat];
                byte firmwareVersion = (byte)message.Properties[PropertyKey.DevCat];
                this.Identity = new InsteonIdentity(devCat, subCat, firmwareVersion);
            }
            OnDeviceIdentified();
        }

        internal void OnMessage(InsteonMessage message)
        {
            switch (message.MessageType)
            {
                case InsteonMessageType.Ack:
                    PendingCommandAck(message);
                    break;

                case InsteonMessageType.OnCleanup:
                    OnDeviceStatusChanged(InsteonDeviceStatus.On);
                    break;

                case InsteonMessageType.OffCleanup:
                    OnDeviceStatusChanged(InsteonDeviceStatus.Off);
                    break;

                case InsteonMessageType.FastOnCleanup:
                    OnDeviceStatusChanged(InsteonDeviceStatus.On);
                    OnDeviceStatusChanged(InsteonDeviceStatus.FastOn);
                    break;

                case InsteonMessageType.FastOffCleanup:
                    OnDeviceStatusChanged(InsteonDeviceStatus.Off);
                    OnDeviceStatusChanged(InsteonDeviceStatus.FastOff);
                    break;

                case InsteonMessageType.IncrementBeginBroadcast:
                    dimmerDirection = message.Properties[PropertyKey.IncrementDirection] != 0 ? DimmerDirection.Up : DimmerDirection.Down;
                    break;

                case InsteonMessageType.IncrementEndBroadcast:
                    if (dimmerDirection == DimmerDirection.Up)
                    {
                        OnDeviceStatusChanged(InsteonDeviceStatus.Brighten);
                    }
                    else if (dimmerDirection == DimmerDirection.Down)
                    {
                        OnDeviceStatusChanged(InsteonDeviceStatus.Dim);
                    }
                    break;

                case InsteonMessageType.SetButtonPressed:
                    OnSetButtonPressed(message);
                    break;
            }
        }

        // if a command is pending determines whether the current message completes the pending command
        private void PendingCommandAck(InsteonMessage message)
        {
            lock (pendingEvent)
            {
                if (PendingCommand != null)
                {
                    int cmd1 = message.Properties[PropertyKey.Cmd1];
                    if (Enum.IsDefined(typeof(InsteonDeviceCommands), cmd1))
                    {
                        InsteonDeviceCommands command = (InsteonDeviceCommands)cmd1;
                        if (PendingCommand.Value == command)
                        {
                            PendingCommand = null;
                            pendingTimer.Change(Timeout.Infinite, Timeout.Infinite); // stop timeout timer
                            pendingEvent.Set(); // unblock any thread that may be waiting on the pending command
                        }
                    }
                }
            }
        }

        // invoked when a pending command times out
        private void PendingCommandTimerCallback(object state)
        {
            string latchedPendingCommand = null;
            lock (pendingEvent)
            {
                if (PendingCommand != null)
                {
                    latchedPendingCommand = PendingCommand.ToString();
                    PendingCommand = null;
                    pendingTimer.Change(Timeout.Infinite, Timeout.Infinite); // stop timeout timer
                    pendingEvent.Set(); // unblock any thread that may be waiting on the pending command                
                }
            }
            if (latchedPendingCommand != null)
            {
                Log.WriteLine("ERROR: Device {0} command {1} timed out", Address.ToString(), latchedPendingCommand);
                OnDeviceCommandTimeout();
            }
        }

        /// <summary>
        /// Sends an INSTEON command to the device.
        /// </summary>
        /// <remarks>
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending. Check the <see cref="PendingCommand">PendingCommand</see> property to determine whether a command is pending.
        /// The <see cref="DeviceStatusChanged">DeviceStatusChanged</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        public bool TryCommand(InsteonDeviceCommands command)
        {
            if (command == InsteonDeviceCommands.On)
                return TryCommand(command, 0xFF);
            else
                return TryCommand(command, 0x00);
        }

        /// <summary>
        /// Sends an INSTEON command to the device.
        /// </summary>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        /// <param name="value">A parameter value required by some commands.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending. Check the <see cref="PendingCommand">PendingCommand</see> property to determine whether a command is pending.
        /// The <see cref="DeviceStatusChanged">DeviceStatusChanged</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public bool TryCommand(InsteonDeviceCommands command, byte value)
        {
            WaitAndSetPendingCommand(command);
            byte[] message = GetStandardMessage(Address, (byte)command, value);
            Log.WriteLine("Device.Command(command:{0}, value:{1:X2})", command.ToString(), value);
            return network.Messenger.TrySend(message) == EchoStatus.ACK;
        }

        /// <summary>
        /// Gets a value that indicates the on-level of the device.
        /// </summary>
        /// <returns>
        /// A value indicating the on-level of the device. For a dimmer a value between 0 and 255 will be returned. For a non-dimmer a value 0 or 255 will be returned.
        /// </returns>
        /// <remarks>
        /// This is a blocking method that sends an INSTEON message to the target device and waits for a reply, or until a timeout of 2 seconds has passed.
        /// </remarks>
        public bool TryGetOnLevel(out byte value)
        {
            Log.WriteLine("Device.GetOnLevel");
            byte[] message = GetStandardMessage(Address, 0x19, 0);
            Dictionary<PropertyKey, int> properties;
            EchoStatus status = network.Messenger.TrySendReceive(message, true, 0x50, out properties); // on-level returned in cmd2 of ACK
            if (status == EchoStatus.ACK && properties != null)
            {
                value = (byte)properties[PropertyKey.Cmd2];
                Log.WriteLine("Device.GetOnLevel returning {0:X2}", value);
                return true;
            }
            else
            {
                value = 0;
                return false;
            }
        }

        /// <summary>
        /// Determines the type of INSTEON device by querying the device.
        /// </summary>
        /// <remarks>
        /// This method does not throw an exception.
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending. Check the <see cref="PendingCommand">PendingCommand</see> property to determine whether a command is pending.
        /// The <see cref="DeviceIdentified">DeviceIdentified</see> event will be invoked if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public bool TryIdentify()
        {
            this.Identity = new InsteonIdentity();
            return TryCommand(InsteonDeviceCommands.IDRequest);
        }

        /// <summary>
        /// Removes links within both the INSTEON device and the INSTEON controller for the specified group.
        /// </summary>
        /// <param name="group">The specified group within which links are to be removed.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending. Check the <see cref="PendingCommand">PendingCommand</see> property to determine whether a command is pending.
        /// A <see cref="InsteonController.DeviceLinked">DeviceLinked</see> event will be invoked on the controller if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public bool TryUnlink(byte group)
        {
            if (network.Controller.TryEnterLinkMode(InsteonLinkMode.Delete, group))
                return TryCommand(InsteonDeviceCommands.EnterLinkingMode, group);
            else
                return false;
        }

        // blocks the current thread if a command is pending, then sets the current command as the pending command (note does not apply to all commands)
        private void WaitAndSetPendingCommand(InsteonDeviceCommands command)
        {           
            InsteonDeviceCommands latchedPendingCommand;

            lock (pendingEvent)
            {
                if (PendingCommand == null)
                {
                    PendingCommand = command;
                    pendingTimer.Change(Constants.messageTimeout, Timeout.Infinite); // start timeout timer
                    return;
                }
                latchedPendingCommand = PendingCommand.Value;
            }

            // block current thread if a command is pending
            Log.WriteLine("Blocking command {0} for pending command {1}", command.ToString(), latchedPendingCommand.ToString());
            pendingEvent.Reset();
            if (!pendingEvent.WaitOne(Constants.messageTimeout)) // wait at most 2 seconds
            {
                lock (pendingEvent)
                    PendingCommand = null; // break deadlock and warn
                Log.WriteLine("WARNING: Unblocking command {0} for pending command {1}", command.ToString(), latchedPendingCommand.ToString());
            }

            WaitAndSetPendingCommand(command); // try again
        }

        /// <summary>
        /// Removes links within both the INSTEON device and the INSTEON controller for the specified group.
        /// </summary>
        /// <param name="group">The specified group within which links are to be removed.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// This is a non-blocking method that sends an INSTEON message to the target device and returns immediately (as long as another command is not already pending for the device). Only one command can be pending to an INSTEON device at a time. This method will block if a second command is sent while a first command is still pending. Check the <see cref="PendingCommand">PendingCommand</see> property to determine whether a command is pending.
        /// A <see cref="InsteonController.DeviceLinked">DeviceLinked</see> event will be invoked on the controller if the command is successful.
        /// The <see cref="DeviceCommandTimeout">DeviceCommandTimeout</see> event will be invoked if the device does not respond within the expected timeout period.
        /// </remarks>
        public void Unlink(byte group)
        {
            this.network.Controller.EnterLinkMode(InsteonLinkMode.Delete, group);
            Command(InsteonDeviceCommands.EnterUnlinkingMode, group);
        }
    }
}
