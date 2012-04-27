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
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using MayhemCore;
using MayhemWpf;
using MayhemWpf.ModuleTypes;
using MayhemWpf.UserControls;
using Insteon.Network;

namespace Insteon.Mayhem
{
    [DataContract]
    [MayhemModule("INSTEON Event", "Triggers on an INSTEON device event")]
    public class InsteonEvent : EventBase, IWpfConfigurable
    {
        [DataMember]
        private InsteonEventDataItem data = new InsteonEventDataItem();
        private InsteonDevice device = null;

        public InsteonEvent()
        {
        }
        public WpfConfiguration ConfigurationControl
        {
            get { return new InsteonEventConfig(data); }
        }
        public string GetConfigString()
        {
            return data.ToString();
        }
        public void OnSaved(WpfConfiguration control)
        {
            InsteonEventConfig config = control as InsteonEventConfig;
            data = config.DataItem;
        }
        protected override void OnDeleted()
        {
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
            
            InsteonService.StartNetwork();
/*
            if (!InsteonService.VerifyConnection())
            {
                ErrorLog.AddError(ErrorType.Failure, string.Format("Lost connection to INSTEON network. {0}", InsteonService.SpecificConnection != null ? InsteonService.SpecificConnection.ToString() : string.Empty));
                e.Cancel = true;
                return;
            }
*/
            if (!InsteonService.Network.Devices.ContainsKey(address))
                device = InsteonService.Network.Devices.Add(address, new InsteonIdentity());
            else
                device = InsteonService.Network.Devices.Find(address);
            device.DeviceStatusChanged += device_DeviceStatusChanged;
        }
        protected override void OnDisabled(DisabledEventArgs e)
        {            
            if (device != null)
                device.DeviceStatusChanged -= device_DeviceStatusChanged;
        }
        void device_DeviceStatusChanged(object sender, InsteonDeviceStatusChangedEventArgs data)
        {
            if (this.IsEnabled && this.data.DeviceStatus == data.DeviceStatus)
                Trigger();
        }
    }
}
