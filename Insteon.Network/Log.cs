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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Insteon.Network
{
    internal static class Log
    {
        private static StreamWriter w = null;

        public static void Open(string path)
        {
            Close();            
            string fullPath = null;
            for (int i = 0; i < 10000; ++i)
            {
                string fileName = string.Format(@"{0}.{1:0000}.log", Assembly.GetExecutingAssembly().GetName().Name, i);
                fullPath = Path.Combine(path, fileName);

                if (!File.Exists(fullPath))
                    break;
            }
            if (!string.IsNullOrEmpty(fullPath))
                w = new StreamWriter(fullPath);
            if (w != null)
                w.WriteLine(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff"));
        }

        public static void WriteLine(string message)
        {
#if INSTEON_DEBUG
            Debug.WriteLine(message);
#endif
            if (w != null)
            {
                w.Write(DateTime.Now.ToString("HH:mm:ss.fff"));
                w.Write(" ");
                w.Write(string.Format(""));
                w.WriteLine(message);
                w.Flush();
            }
        }

        public static void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(format, args));
        }

        public static void Close()
        {
            if (w != null)
                w.Close();
        }
    }
}
