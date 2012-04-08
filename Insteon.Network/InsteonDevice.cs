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

namespace Insteon.Network
{
    /// <summary>
    /// Represents an individual INSTEON device on the network.
    /// </summary>
    public class InsteonDevice
    {
        /// <summary>
        /// Invoked when a status message is received from the INSTEON device, for example when the device turns on or off.
        /// </summary>
        public event InsteonDeviceStatusChangedEventHandler DeviceStatusChanged;

        /// <summary>
        /// Invoked when the device has been identified.
        /// </summary>
        public event InsteonDeviceEventHandler DeviceIdentified;

        private enum DimmerDirection { None, Up, Down }

        private readonly InsteonNetwork network;
        private DimmerDirection dimmerDirection = DimmerDirection.None;

        internal InsteonDevice(InsteonNetwork network, InsteonAddress address, InsteonIdentity identity)
        {
            this.network = network;
            this.Address = address;
            this.Identity = identity;
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
        /// This method is a non-blocking operation.
        /// The device status changed event will be invoked if the command is successful.
        /// </summary>
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
        /// This method is a non-blocking operation.
        /// The device status changed event will be invoked if the command is successful.
        /// </summary>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        /// <param name="value">A parameter value required by some commands.</param>
        public void Command(InsteonDeviceCommands command, byte value)
        {
            byte flags = 0x0F;
            byte cmd1 = (byte)command;
            byte cmd2 = value;
            byte[] message = { 0x62, Address[2], Address[1], Address[0], flags, cmd1, cmd2 };
            network.Messenger.Send(message);
        }

        /// <summary>
        /// Determines the type of INSTEON device by querying the device.
        /// This method is a non-blocking operation.
        /// The device identified event will be invoked if the command is successful.
        /// </summary>
        public void Identify()
        {
            this.Identity = new InsteonIdentity();
            Command(InsteonDeviceCommands.IDRequest);
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

        /// <summary>
        /// Sends an INSTEON command to the device.
        /// This method is a non-blocking operation.
        /// The device status changed event will be invoked if the command is successful.
        /// </summary>
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
        /// This method is a non-blocking operation.
        /// The device status changed event will be invoked if the command is successful.
        /// </summary>
        /// <param name="command">Specifies the INSTEON device command to be invoked.</param>
        /// <param name="value">A parameter value required by some commands.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryCommand(InsteonDeviceCommands command, byte value)
        {
            byte flags = 0x0F;
            byte cmd1 = (byte)command;
            byte cmd2 = value;
            byte[] message = { 0x62, Address[2], Address[1], Address[0], flags, cmd1, cmd2 };
            return network.Messenger.TrySend(message) == EchoStatus.ACK;
        }

        /// <summary>
        /// Determines the type of INSTEON device by querying the device.
        /// This method is a non-blocking operation.
        /// The device identified event will be invoked if the command is successful.
        /// </summary>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryIdentify()
        {
            this.Identity = new InsteonIdentity();
            return TryCommand(InsteonDeviceCommands.IDRequest);
        }

        /// <summary>
        /// Removes links within both the INSTEON device and the INSTEON controller for the specified group.
        /// This method is a non-blocking operation.
        /// A link event will be invoked if the command is successful.
        /// </summary>
        /// <param name="group">The specified group within which links are to be removed.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryUnlink(byte group)
        {
            if (network.Controller.TryEnterLinkMode(InsteonLinkMode.Delete, group))
                return TryCommand(InsteonDeviceCommands.EnterLinkingMode, group);
            else
                return false;
        }

        /// <summary>
        /// Removes links within both the INSTEON device and the INSTEON controller for the specified group.
        /// This method is a non-blocking operation.
        /// A link event will be invoked if the command is successful.
        /// </summary>
        /// <param name="group">The specified group within which links are to be removed.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public void Unlink(byte group)
        {
            this.network.Controller.EnterLinkMode(InsteonLinkMode.Delete, group);
            Command(InsteonDeviceCommands.EnterUnlinkingMode, group);
        }
    }
}
