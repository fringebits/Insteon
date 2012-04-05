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
    /// <summary>
    /// Represents an INSTEON connection.
    /// </summary>
    public class InsteonConnection
    {
        /// <summary>
        /// The display name for the connection string.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The type of connection.
        /// </summary>
        public InsteonConnectionType Type { get; private set; }
        
        /// <summary>
        /// Value that specifies the network address or serial port.
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Initializes a new connection string instance.
        /// </summary>
        /// <param name="type">Type type of connection.</param>
        /// <param name="value">The connection value.</param>
        public InsteonConnection(InsteonConnectionType type, string value)
        : this(type, value, value)
        {
        }

        /// <summary>
        /// Initializes a new connection string instance.
        /// </summary>
        /// <param name="type">Type type of connection.</param>
        /// <param name="value">The connection value.</param>
        /// <param name="Name">The display name for the connection string.</param>
        public InsteonConnection(InsteonConnectionType type, string value, string name)
        {
            this.Type = type;
            this.Value = value;
            if (!string.IsNullOrEmpty(name) && name.Trim().Length > 0)
                this.Name = name;
            else
                this.Name = value;
        }

        /// <summary>
        /// Parses a string into a connection string object.
        /// </summary>
        /// <param name="text">The specified connection string.</param>
        /// <returns>Returns the connection string object.</returns>
        public static InsteonConnection Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentNullException();

            int i = text.IndexOf(':');
            int j = text.IndexOf(',');

            InsteonConnectionType type;
            string typeValue = text.Substring(0, i).Trim();
            if (string.Equals(typeValue, "Net", StringComparison.InvariantCultureIgnoreCase))
                type = InsteonConnectionType.Net;
            else if (string.Equals(typeValue, "Serial", StringComparison.InvariantCultureIgnoreCase))
                type = InsteonConnectionType.Serial;
            else
                throw new FormatException();

            string value = (j >= 0) ? text.Substring(i + 1, j - i - 1).Trim() : text.Substring(i + 1).Trim();
            if (string.IsNullOrEmpty(value))
                throw new FormatException();

            string name = (j >= 0 && j < text.Length) ? text.Substring(j + 1).Trim() : value;
            if (string.IsNullOrEmpty(name))            
                return new InsteonConnection(type, value);
            else
                return new InsteonConnection(type, value, name);
        }

        /// <summary>
        /// Parses a string into a connection string object.
        /// </summary>
        /// <param name="text">The specified connection string.</param>
        /// <param name="connection">The returned connection string object.</param>
        /// <returns>Returns true if the string could be parsed.</returns>
        public static bool TryParse(string text, out InsteonConnection connection)
        {
            try
            {
                connection = Parse(text);
                return true;
            }
            catch (ArgumentException)
            {
                connection = null;
                return false;
            }
            catch (FormatException)
            {
                connection = null;
                return false;
            }
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Name) || string.Equals(Name, Value, StringComparison.InvariantCulture))
                return string.Format("{0}: {1}", Type, Value);
            else
                return string.Format("{0}: {1}, {2}", Type, Value, Name);
        }
    }
}
