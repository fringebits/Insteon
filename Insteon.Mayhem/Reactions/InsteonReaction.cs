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
    [MayhemModule("INSTEON Command", "Sends a command to an INSTEON device to turn it on or off or set the dim level")]
    public class InsteonReaction : ReactionBase, IWpfConfigurable
    {
        [DataMember]
        private InsteonReactionDataItem data = new InsteonReactionDataItem();

        public InsteonReaction()
        {
        }
        public WpfConfiguration ConfigurationControl
        {
            get { return new InsteonReactionConfig(data); }
        }
        public void OnSaved(WpfConfiguration control)
        {
            InsteonReactionConfig config = control as InsteonReactionConfig;
            data = config.DataItem;
        }
        protected override void OnLoadDefaults()
        {
            InsteonService.StartNetwork();
        }
        protected override void OnAfterLoad()
        {
            InsteonService.StartNetwork();
        }
        protected override void OnDeleted()
        {
            InsteonService.UnlinkDevice(data.Group, data.Device);
        }
        public string GetConfigString()
        {
            return data.ToString();
        }
        public override void Perform()
        {
            InsteonService.WaitUntilConnected();
            if (InsteonService.Network.IsConnected)
            {
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
                if (!InsteonService.Network.Controller.TryGroupCommand(command, data.Group))
                    ErrorLog.AddError(ErrorType.Failure, string.Format("Could not issue INSTEON command {0} to group {1}, device {2} due to a problem communicating with the INSTEON controller.", command.ToString(), data.Group, data.Device));
            }
        }
    }
}
