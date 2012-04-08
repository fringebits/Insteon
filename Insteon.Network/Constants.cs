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
using System.Text;

namespace Insteon.Network
{
    internal static class Constants
    {
        public const int echoTimeout = 1000; // time to wait for echo response after sending data (milliseconds)
        public const int openTimeout = 1000; // time to wait for initial response after opening port (milliseconds)
        public const int messageTimeout = 1000; // timeout to receive expected reply from a sent insteon message
        public const int readTime = 100; // maximum amount of time to wait for additional data on a read
        public const int retryCount = 5; // number of retries when sending a command before failing
        public const int retryTime = 5; // time to wait between retries when reading data (milliseconds)
        public const int webRequestTimeout = 5000; // timeout for web requests to smartlinc.smarthome.com and for accessing SmartLinc devices over the local network (milliseconds)
    }
}
