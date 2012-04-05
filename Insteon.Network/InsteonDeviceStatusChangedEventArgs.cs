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
using System.Text;

namespace Insteon.Network
{
    public class InsteonDeviceStatusChangedEventArgs
    {
        public InsteonDevice Device { get; private set; }
        public InsteonDeviceStatus DeviceStatus { get; private set; }
        internal InsteonDeviceStatusChangedEventArgs(InsteonDevice device, InsteonDeviceStatus status)
        {
            this.Device = device;
            this.DeviceStatus = status;
        }
    }
    public delegate void InsteonDeviceStatusChangedEventHandler(object sender, InsteonDeviceStatusChangedEventArgs data);
}
