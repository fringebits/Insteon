// <copyright company="INSTEON">
// Copyright (c) 2012 All Right Reserved, http://www.insteon.net
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
using System.Text;
using System.Runtime.Serialization;
using System.Windows.Forms;
using MayhemCore;
using MayhemWpf;
using MayhemWpf.ModuleTypes;
using MayhemWpf.UserControls;
using Insteon.Network;

namespace Insteon.Mayhem
{
    [DataContract]
    [MayhemModule("INSTEON Command", "Sends a command to an INSTEON device")]
    public class InsteonReaction : ReactionBase, IWpfConfigurable
    {
        [DataMember]
        private InsteonReactionDataItem data = new InsteonReactionDataItem();
        private InsteonDevice device = null;

        public InsteonReaction()
        {
        }
        public WpfConfiguration ConfigurationControl
        {
            get { return new InsteonReactionConfig(data); }
        }
        public string GetConfigString()
        {
            return data.ToString();
        }
        public void OnSaved(WpfConfiguration control)
        {
            InsteonReactionConfig config = control as InsteonReactionConfig;
            data = config.DataItem;
        }
        protected override void OnDeleted()
        {
//          InsteonService.UnlinkDevice(data.Group, data.Device);
        }
        protected override void OnEnabling(EnablingEventArgs e)
        {
            InsteonAddress address;
            if (!InsteonAddress.TryParse(data.Device, out address))
            {
                ErrorLog.AddError(ErrorType.Failure, string.Format("Invalid INSTEON address '{0}'", data.Device));
                e.Cancel = true;
                return;
            }

            if (!InsteonService.VerifyConnection())
            {
                ErrorLog.AddError(ErrorType.Failure, string.Format("Lost connection to INSTEON network. {0}", InsteonService.SpecificConnection != null ? InsteonService.SpecificConnection.ToString() : string.Empty));
                e.Cancel = true;
                return;
            }

            if (!InsteonService.Network.Devices.ContainsKey(address))
                device = InsteonService.Network.Devices.Add(address, new InsteonIdentity());
            else
                device = InsteonService.Network.Devices.Find(address);
        }
        public override void Perform()
        {
            if (device == null)
                return;

            InsteonService.WaitUntilConnected();
            if (InsteonService.Network.IsConnected)
            {
                InsteonDeviceCommands command;
                switch (data.DeviceStatus)
                {
                    case InsteonDeviceStatus.On:
                        command = InsteonDeviceCommands.On;
                        break;
                    case InsteonDeviceStatus.Off:
                        command = InsteonDeviceCommands.Off;
                        break;
                    case InsteonDeviceStatus.FastOn:
                        command = InsteonDeviceCommands.FastOn;
                        break;
                    case InsteonDeviceStatus.FastOff:
                        command = InsteonDeviceCommands.FastOff;
                        break;
                    case InsteonDeviceStatus.Brighten:
                        command = InsteonDeviceCommands.Brighten;
                        break;
                    case InsteonDeviceStatus.Dim:
                        command = InsteonDeviceCommands.Dim;
                        break;
                    default:
                        return;
                }
                if (!device.TryCommand(command))
                    ErrorLog.AddError(ErrorType.Failure, string.Format("Could not send INSTEON command {0} to device {1} due to a problem communicating with the INSTEON controller.", command.ToString(), data.Device));

                /*
                InsteonControllerGroupCommands command;
                switch (data.DeviceStatus)
                {
                    case InsteonDeviceStatus.On:
                        command = InsteonControllerGroupCommands.On;
                        break;
                    case InsteonDeviceStatus.Off:
                        command = InsteonControllerGroupCommands.Off;
                        break;
                    case InsteonDeviceStatus.FastOn:
                        command = InsteonControllerGroupCommands.FastOn;
                        break;
                    case InsteonDeviceStatus.FastOff:
                        command = InsteonControllerGroupCommands.FastOff;
                        break;
                    case InsteonDeviceStatus.Brighten:
                        command = InsteonControllerGroupCommands.Brighten;
                        break;
                    case InsteonDeviceStatus.Dim:
                        command = InsteonControllerGroupCommands.Dim;
                        break;
                    default:
                        return;
                }
                if (!InsteonService.Network.Controller.TryGroupCommand(command, 0xFF))
                    ErrorLog.AddError(ErrorType.Failure, string.Format("Could not send INSTEON command {0} to device {1} due to a problem communicating with the INSTEON controller.", command.ToString(), data.Device));
                */
            }
        }
    }
}
