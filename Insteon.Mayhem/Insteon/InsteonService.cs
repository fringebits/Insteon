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
using System.Diagnostics;
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
        public static event AsyncCompletedEventHandler GetAvailableGroupCompleted;

        private static string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"Mayhem\Insteon.Connection.txt");
        private static AutoResetEvent wait = new AutoResetEvent(false);
        private static Stopwatch verifyStopwatch = new Stopwatch();

        public static InsteonNetwork Network { get; private set; }
        public static InsteonConnection SpecificConnection { get; set; }
        public static bool Connecting { get; private set; }

        static InsteonService()
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), @"Mayhem\Logs");
            if (Directory.Exists(logPath))
                InsteonNetwork.SetLogPath(logPath);
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
                SpecificConnection = null;
                Network.Close();
                Network.TryConnect(connection);
            }
            else
            {
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
            }

            if (!Network.IsConnected)
                OnConnectionFailed();

            Connecting = false;
            wait.Set();
        }

        private static void OnConnectionFailed()
        {
            if (ConnectionFailed != null)
                ConnectionFailed(null, EventArgs.Empty);
        }

        private static void OnGetAvailableGroupCompleted(byte group)
        {
            if (GetAvailableGroupCompleted != null)
                GetAvailableGroupCompleted(null, new AsyncCompletedEventArgs(null, false, group));
        }

        public static void BeginGetAvailableGroup()
        {
            OnGetAvailableGroupCompleted(0xFF); // Mayhem always uses group 255
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

        public static bool VerifyConnection()
        {
            bool result;

            if (Network.IsConnected)
            {
                if (verifyStopwatch.IsRunning && verifyStopwatch.ElapsedMilliseconds < 10000)
                    return true;
                result = Network.VerifyConnection();
            }
            else
            {
                StartNetwork();
                WaitUntilConnected();
                result = Network.IsConnected;
            }

            verifyStopwatch.Reset();
            if (result)
                verifyStopwatch.Start();

            return result;
        }

        public static string GetDeviceStatusDisplayName(InsteonDeviceStatus status)
        {
            switch (status)
            {
                case InsteonDeviceStatus.On:        return "On";
                case InsteonDeviceStatus.Off:       return "Off";
                case InsteonDeviceStatus.FastOn:    return "Fast On";
                case InsteonDeviceStatus.FastOff:   return "Fast Off";
                case InsteonDeviceStatus.Brighten:  return "Brighten";
                case InsteonDeviceStatus.Dim:       return "Dim";
                default:                            return "Unknown";
            }            
        }
    }
}
