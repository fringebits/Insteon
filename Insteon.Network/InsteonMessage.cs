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
    // Represents a structured view of an INSTEON message, as produced by the message processor.
    internal class InsteonMessage
    {
        public InsteonMessage(int messageId, InsteonMessageType messageType, Dictionary<PropertyKey, int> properties)
        {
            this.MessageId = messageId;
            this.MessageType = messageType;
            this.Properties = properties;
        }
        public int MessageId { get; private set; }
        public InsteonMessageType MessageType { get; private set; }        
        public Dictionary<PropertyKey, int> Properties { get; private set; }
        public override string ToString()
        {
            return MessageType.ToString();
        }
    }
}
