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
    internal static class Utilities
    {
        public static byte[] ArraySubset(byte[] data, int offset)
        {
            return ArraySubset(data, offset, data.Length - offset);
        }

        public static byte[] ArraySubset(byte[] data, int offset, int count)
        {
            if (count > data.Length - offset)
                count = data.Length - offset;
            var result = new byte[count];
            Array.Copy(data, offset, result, 0, count);
            return result;
        }

        public static bool ArraySequenceEquals(byte[] a, byte[] b)
        {
            if (a != null && b != null && a.Length == b.Length)
                for (var i = 0; i < a.Length; ++i)
                    if (a[i] != b[i])
                        return false;
            return true;
        }

        public static string ByteArrayToString(byte[] data)
        {
            return ByteArrayToString(data, 0, data.Length);
        }

        public static string ByteArrayToString(byte[] data, int offset)
        {
            return ByteArrayToString(data, offset, data.Length - offset);
        }

        public static string ByteArrayToString(byte[] data, int offset, int count)
        {
            var list = new List<string>();
            
            for (var ii = offset; ii < Math.Max(offset + count, data.Length); ++ii)
            {
                if (ii == 0 && data[ii] == 0x02)
                {
                    list.Add("STX");
                }
                else
                {
                    list.Add($"{data[ii]:X2}");
                }
            }

            return string.Join(" ", list.ToArray());
        }

        public static string FormatHex(int value)
        {
            if (value <= 0xFF)
                return string.Format("{0:X2}", value);
            else if (value <= 0xFFFF)
                return string.Format("{0:X4}", value);
            else if (value <= 0xFFFFFF)
                return string.Format("{0:X6}", value);
            else
                return string.Format("{0:X8}", value);
        }

        public static string FormatProperties(Dictionary<PropertyKey, int> properties, bool multiline, bool filterMessageFlags)
        {
            var first = true;
            var sb = new StringBuilder();
            foreach (var item in properties)
            {
                if (!filterMessageFlags || (item.Key != PropertyKey.MessageFlagsRemainingHops && item.Key != PropertyKey.MessageFlagsMaxHops))
                {
                    if (multiline)
                    {
                        sb.AppendLine();
                        sb.Append("  ");
                    }
                    else
                    {
                        if (!first)
                            sb.Append(" ");
                    }
                    sb.AppendFormat("{0}={1}", item.Key, Utilities.FormatHex(item.Value));
                    first = false;
                }                
            }
            return sb.ToString();
        }
    }
}
