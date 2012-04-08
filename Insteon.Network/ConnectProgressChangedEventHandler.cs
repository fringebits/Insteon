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
using System.ComponentModel;
using System.Text;

namespace Insteon.Network
{
    public class ConnectProgressChangedEventArgs : EventArgs 
    {
        public ConnectProgressChangedEventArgs(int progressPercentage, string status)
        {
            this.Cancel = false;
            this.ProgressPercentage = progressPercentage;
            this.Status = status;
        }

        public bool Cancel { get; set; }
        public int ProgressPercentage { get; private set; }
        public string Status { get; private set; }
    }
    public delegate void ConnectProgressChangedEventHandler(object sender, ConnectProgressChangedEventArgs data);
}
