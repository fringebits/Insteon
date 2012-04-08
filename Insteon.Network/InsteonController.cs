﻿// <copyright company="INSTEON">
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
using System.Timers;

namespace Insteon.Network
{
    /// <summary>
    /// Represents the controller device, which interfaces with the variuos other INSTEON devices on the network.
    /// </summary>
    public class InsteonController
    {
        private readonly InsteonNetwork network;
        private readonly System.Timers.Timer timer = new System.Timers.Timer();
        private InsteonLinkMode? linkingMode = null;

        /// <summary>
        /// Invoked when an INSTEON device is linked to the controller device.
        /// </summary>
        public event InsteonDeviceEventHandler DeviceLinked;
        
        /// <summary>
        /// Invoked when an initiated link operation has timed out after 4 minutes.
        /// </summary>
        public event EventHandler DeviceLinkTimeout;
        
        /// <summary>
        /// Invoked when an INSTEON device is unlinked from the controller device.
        /// </summary>
        public event InsteonDeviceEventHandler DeviceUnlinked;

        /// <summary>
        /// The INSTEON address of the controller device.
        /// </summary>
        public InsteonAddress Address { get; private set; }

        /// <summary>
        /// Indicates the type of the INSTEON device.
        /// </summary>
        public InsteonIdentity Identity { get; private set; }

        internal InsteonController(InsteonNetwork network)
        : this(
            network,
            new InsteonAddress(network.Messenger.ControllerProperties[PropertyKey.Address]),
            new InsteonIdentity(
                (byte)network.Messenger.ControllerProperties[PropertyKey.DevCat],
                (byte)network.Messenger.ControllerProperties[PropertyKey.SubCat],
                (byte)network.Messenger.ControllerProperties[PropertyKey.FirmwareVersion]
                )
            )
        {
        }

        private InsteonController(InsteonNetwork network, InsteonAddress address, InsteonIdentity identity)
        {
            this.network = network;
            this.Address = address;
            this.Identity = identity;
            
            this.timer.Interval = 4 * 60* 1000; // 4 minutes
            this.timer.AutoReset = false;
            this.timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);
        }

        /// <summary>
        /// Cancels linking mode in the controller.
        /// </summary>
        public void CancelLinkMode()
        {
            if (!TryCancelLinkMode())
                throw new IOException();
        }

        /// <summary>
        /// Places the INSTEON controller into linking mode in order to link or unlink a device.
        /// </summary>
        /// <param name="mode">Determines the linking mode as controller, responder, either, or delete.</param>
        /// <param name="group">Specifies the INSTEON group number to which the device will be linked.</param>
        /// <remarks>
        /// The DeviceLinked event will be raised when a device has been linked to the controller.
        /// The DeviceUnlinked event will be raised when a device has been unklinked from the controller.
        /// The DeviceLinkTimeout event will be raised if a device is not added within the 4 minute timeout period.
        /// </remarks>
        public void EnterLinkMode(InsteonLinkMode mode, byte group)
        {
            if (!TryEnterLinkMode(mode, group))
                throw new IOException();
        }

        /// <summary>
        /// Returns an array of device links in the INSTEON controller.
        /// </summary>
        /// <returns>An array of objects representing each device link.</returns>
        public InsteonDeviceLinkRecord[] GetLinks()
        {
            InsteonDeviceLinkRecord[] links;
            if (!TryGetLinks(out links))
                throw new IOException();
            return links;
        }

        /// <summary>
        /// Sends an INSTEON group broadcast command to the controller.
        /// This method is a non-blocking operation.
        /// Status changed events will be invoked for each INSTEON device linked within the specified group that responds to the command.
        /// </summary>
        /// <param name="command">Specifies the INSTEON controller group command to be invoked.</param>
        /// <param name="group">Specifies the group number for the command.</param>
        public void GroupCommand(InsteonControllerGroupCommands command, byte group)
        {
            if (command == InsteonControllerGroupCommands.StopDimming)
                throw new ArgumentNullException();
            GroupCommand(command, group, 0);
        }

        /// <summary>
        /// Sends an INSTEON group broadcast command to the controller.
        /// This method is a non-blocking operation.
        /// Status changed events will be invoked for each INSTEON device linked within the specified group that responds to the command.
        /// </summary>
        /// <param name="command">Specifies the INSTEON controller group command to be invoked.</param>
        /// <param name="group">Specifies the group number for the command.</param>
        /// <param name="value">A parameter value required by some group commands.</param>
        public void GroupCommand(InsteonControllerGroupCommands command, byte group, byte value)
        {
            byte cmd = (byte)command;
            byte[] message = { 0x61, group, cmd, value };
            network.Messenger.Send(message);
        }

        internal void OnMessage(InsteonMessage message)
        {
            if (message.MessageType == InsteonMessageType.DeviceLink)
            {
                InsteonAddress address = new InsteonAddress(message.Properties[PropertyKey.Address]);
                InsteonIdentity identity = new InsteonIdentity((byte)message.Properties[PropertyKey.DevCat], (byte)message.Properties[PropertyKey.SubCat], (byte)message.Properties[PropertyKey.FirmwareVersion]);
                InsteonDevice device = network.Devices.Add(address, identity);
                timer.Stop();
                IsInLinkingMode = false;
                if (linkingMode.HasValue)
                {
                    if (linkingMode != InsteonLinkMode.Delete)
                        OnDeviceLinked(device);
                    else
                        OnDeviceUnlinked(device);
                }
                else
                {
                    OnDeviceLinked(device);
                }
            }
        }

        /// <summary>
        /// Determines whether the controller is in linking mode.
        /// </summary>
        public bool IsInLinkingMode { get; private set; }

        private void OnDeviceLinked(InsteonDevice device)
        {
            if (DeviceLinked != null)
                DeviceLinked(this, new InsteonDeviceEventArgs(device));
        }

        private void OnDeviceLinkTimeout()
        {
            if (DeviceLinkTimeout != null)
                DeviceLinkTimeout(this, EventArgs.Empty);
        }

        private void OnDeviceUnlinked(InsteonDevice device)
        {
            if (DeviceUnlinked != null)
                DeviceUnlinked(this, new InsteonDeviceEventArgs(device));
        }

        private void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            IsInLinkingMode = false;
            OnDeviceLinkTimeout();
        }

        /// <summary>
        /// Cancels linking mode in the controller.
        /// </summary>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryCancelLinkMode()
        {
            timer.Stop();
            IsInLinkingMode = false;
            linkingMode = null;
            byte[] message = { 0x65 };
            return network.Messenger.TrySend(message) == EchoStatus.ACK;
        }

        /// <summary>
        /// Places the INSTEON controller into linking mode in order to link or unlink a device.
        /// </summary>
        /// <param name="mode">Determines the linking mode as controller, responder, either, or delete.</param>
        /// <param name="group">Specifies the INSTEON group number to which the device will be linked.</param>
        /// <remarks>
        /// The DeviceLinked event will be raised when a device has been linked to the controller.
        /// The DeviceUnlinked event will be raised when a device has been unklinked from the controller.
        /// The DeviceLinkTimeout event will be raised if a device is not added within the 4 minute timeout period.
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryEnterLinkMode(InsteonLinkMode mode, byte group)
        {
            linkingMode = mode;
            byte[] message = { 0x64, (byte)mode, group };
            if (network.Messenger.TrySend(message) != EchoStatus.ACK)
                return false;
            timer.Start();
            IsInLinkingMode = true;
            return true;
        }

        /// <summary>
        /// Returns an array of device links in the INSTEON controller.
        /// </summary>
        /// <param name="links">An array of objects representing each device link.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryGetLinks(out InsteonDeviceLinkRecord[] links)
        {
            links = null;
            List<InsteonDeviceLinkRecord> list = new List<InsteonDeviceLinkRecord>();
            Dictionary<PropertyKey, int> properties;
            EchoStatus status = EchoStatus.None;

            byte[] message1 = { 0x69 };
            status = network.Messenger.TrySendReceive(message1, false, 0x57, out properties);
            if (status == EchoStatus.NAK)
            {
                links = new InsteonDeviceLinkRecord[0];
                return true;
            }
            else if (status == EchoStatus.ACK)
            {
                list.Add(new InsteonDeviceLinkRecord(properties));
            }
            else
            {
                return false;
            }

            byte[] message2 = { 0x6A };
            status = network.Messenger.TrySendReceive(message2, false, 0x57, out properties);
            while (status == EchoStatus.ACK)
            {
                list.Add(new InsteonDeviceLinkRecord(properties));
                status = network.Messenger.TrySendReceive(message2, false, 0x57, out properties);
            }

            if (status != EchoStatus.NAK)
                return false;

            links = list.ToArray();
            return true;
        }

        /// <summary>
        /// Sends an INSTEON group broadcast command to the controller.
        /// This method is a non-blocking operation.
        /// Status changed events will be invoked for each INSTEON device linked within the specified group that responds to the command.
        /// </summary>
        /// <param name="command">Specifies the INSTEON controller group command to be invoked.</param>
        /// <param name="group">Specifies the group number for the command.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryGroupCommand(InsteonControllerGroupCommands command, byte group)
        {
            if (command == InsteonControllerGroupCommands.StopDimming)
                return false;
            return TryGroupCommand(command, group, 0);
        }

        /// <summary>
        /// Sends an INSTEON group broadcast command to the controller.
        /// This method is a non-blocking operation.
        /// Status changed events will be invoked for each INSTEON device linked within the specified group that responds to the command.
        /// </summary>
        /// <param name="command">Specifies the INSTEON controller group command to be invoked.</param>
        /// <param name="group">Specifies the group number for the command.</param>
        /// <param name="value">A parameter value required by some group commands.</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryGroupCommand(InsteonControllerGroupCommands command, byte group, byte value)
        {
            byte cmd = (byte)command;
            byte[] message = { 0x61, group, cmd, value };
            return network.Messenger.TrySend(message) == EchoStatus.ACK;
        }
    }
}
