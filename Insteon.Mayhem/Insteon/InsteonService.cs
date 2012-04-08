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
using System.IO;
using System.Text;
using System.Threading;
using System.Windows;
using Insteon.Network;

namespace Insteon.Mayhem
{
    internal static class InsteonService
    {
        public static event EventHandler ConnectionFailed;

        private static string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"Mayhem\Insteon.Connection.txt");
        private static AutoResetEvent wait = new AutoResetEvent(false);

        public static InsteonNetwork Network { get; private set; }
        public static InsteonConnection SpecificConnection { get; set; }
        public static bool Connecting { get; private set; }

        static InsteonService()
        {
            InsteonNetwork.SetLogPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Mayhem"));
            Network = new InsteonNetwork();
            Application.Current.Exit += Application_Exit;
        }

        private static void Application_Exit(object sender, ExitEventArgs e)
        {
            if (Network.IsConnected)
            {
                SaveSettings();
                Network.Close();
            }
        }

        private static void SaveSettings()
        {
            if (Network.IsConnected)
                try
                {
                    using (TextWriter w = File.CreateText(path))
                    {
                        w.Write(Network.Connection.ToString());
                    }
                }
                catch (IOException)
                {
                }
        }

        private static void LoadSettings()
        {
            try
            {
                if (File.Exists(path))
                    using (TextReader r = File.OpenText(path))
                    {
                        string text = r.ReadToEnd().Trim();
                        InsteonConnection connection;
                        if (InsteonConnection.TryParse(text, out connection))
                            SpecificConnection = connection;
                        else
                            SpecificConnection = null;                        
                    }
            }
            catch (IOException)
            {
            }
        }

        public static bool WaitUntilConnected()
        {
            if (!Network.IsConnected && Connecting)
                wait.WaitOne();
            return Network.IsConnected;
        }

        public static void StartNetwork()
        {
            if (!Network.IsConnected && !Connecting)
            {
                Connecting = true;
                ThreadPool.QueueUserWorkItem(ConnectThreadProc, null);
            }
        }

        public static void StartNetwork(InsteonConnection connection)
        {
            Connecting = true;
            ThreadPool.QueueUserWorkItem(ConnectThreadProc, connection);
        }

        public static string GetConnectionInfo(InsteonConnection connection)
        {
            StringBuilder sb = new StringBuilder();
            if (connection.Type == InsteonConnectionType.Net && connection.Name != connection.Value)
                sb.AppendFormat("Network Address: {0}", connection.Value);
            if (!connection.Address.IsEmpty)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.AppendFormat("INSTEON Address: {0}", connection.Address.ToString());
            }
            if (sb.Length > 0)
                return sb.ToString();
            else
                return null;
        }

        private static void ConnectThreadProc(object obj)
        {
            InsteonConnection connection = obj as InsteonConnection;
            if (connection != null)
            {
                SpecificConnection = connection;
                if (!connection.Equals(Network.Connection))
                    Network.Close();
            }

            if (SpecificConnection == null)
                LoadSettings();

            if (SpecificConnection != null)
            {                
                Network.TryConnect(SpecificConnection);
            }
            else
            {
                Network.TryConnect();
            }
            if (!Network.IsConnected)
            {
                OnConnectionFailed();
            }
            Connecting = false;
            wait.Set();
        }

        private static void OnConnectionFailed()
        {
            if (ConnectionFailed != null)
                ConnectionFailed(null, EventArgs.Empty);
        }

        public static bool TryGetAvailableGroup(out byte group, out string message)
        {
            group = 0;
            message = null;

            if (Network == null || !Network.IsConnected)
            {
                message = "Sorry, there was a problem communicating with the INSTEON controller.\r\n\r\nIf this problem persists, please try unplugging your INSTEON controller from the wall and plugging it back in.";
                return false;
            }
            
            InsteonDeviceLinkRecord[] links;
            if (!Network.Controller.TryGetLinks(out links))
            {
                message = "Sorry, there was a problem communicating with the INSTEON controller.\r\n\r\nIf this problem persists, please try unplugging your INSTEON controller from the wall and plugging it back in.";
                return false;
            }

            bool[] used = new bool[256];
            foreach (InsteonDeviceLinkRecord link in links)
                used[link.Group] = true;
            
            for (byte i = 255; i > 0; --i)
                if (!used[i])
                {
                    group = i;
                    return true;
                }
            
            message = "Sorry, no more devices can be added.\r\n\r\nIf there is another event or reaction that may no longer be needed, remove it and try again.";
            return false;
        }


        public static void UnlinkDevice(byte group, string device)
        {
            if (group != 0 && !string.IsNullOrEmpty(device))
            {
                InsteonAddress address;
                if (InsteonAddress.TryParse(device, out address))
                {
                    if (InsteonService.Network.Devices.ContainsKey(address))
                    {
                        InsteonService.Network.Devices.Find(address).TryUnlink(group);
                    }
                }
            }
        }
    }
}
