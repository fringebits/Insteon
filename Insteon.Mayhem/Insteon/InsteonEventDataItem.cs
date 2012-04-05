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
using Insteon.Network;

namespace Insteon.Mayhem
{
    public class InsteonEventDataItem
    {
        public string Device { get; set; }
        public InsteonDeviceStatus DeviceStatus { get; set; }
        public byte Group { get; set; }
        public bool Zero { get { return Group == 0; } }
        public override string ToString()
        {
            return string.Format("{0} {1}", Device, DeviceStatus.ToString());
        }
    }
}
