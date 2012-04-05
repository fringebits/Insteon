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
using System.IO;
using System.IO.Ports;
using System.Text;

namespace Insteon.Network
{
    /// <summary>
    /// Represents the top level INSTEON network, including the set of known INSTEON devices and the controller device.
    /// </summary>
    public class InsteonNetwork
    {
        internal InsteonMessenger Messenger { get; private set; }
        private List<InsteonConnection> connections = null;

        /// <summary>
        /// Invoked when a connection to an INSTEON network is established.
        /// </summary>
        public event EventHandler Connected;

        /// <summary>
        /// Communicates progress status during the sometimes lengthy process of connecting to a network.
        /// </summary>
        public event ProgressChangedEventHandler ConnectProgress;

        /// <summary>
        /// Invoked when the INSTEON network is shutting down.
        /// </summary>
        public event EventHandler Closing;

        /// <summary>
        /// Invoked when the connection to an INSTEON network is terminated.
        /// </summary>
        public event EventHandler Disconnected;
        
        /// <summary>
        /// A collection of known INSTEON devices linked to the network.
        /// </summary>
        public InsteonDeviceList Devices { get; private set; }
        
        /// <summary>
        /// The INSTEON controller device which interfaces to the various other INSTEON devices on the network.
        /// </summary>
        public InsteonController Controller { get; private set; }

        /// <summary>
        /// Initializes a new instance of the INSTEON network class.
        /// </summary>
        public InsteonNetwork()
        {
            Devices = new InsteonDeviceList(this);
            Messenger = new InsteonMessenger(this);
        }

        /// <summary>
        /// Determines whether devices are automatically added to the device collection when a message is received from a device not already in the device collection.
        /// </summary>
        public bool AutoAdd { get; set; }

        /// <summary>
        /// Connects to an INSTEON network using the specified connection.
        /// </summary>
        /// <param name="connection">Specifies the connection to the INSTEON powerline controller device, which can accessed serially or over the network. Examples: "serial: COM1" or "net: 192.168.2.5".</param>
        public void Connect(InsteonConnection connection)
        {
            Messenger.Connect(connection);
            Connection = connection;
            Controller = new InsteonController(this);
            OnConnected();
        }

        /// <summary>
        /// Disconnects from the active INSTEON network and closes all open connections.
        /// </summary>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public void Close()
        {
            Log.WriteLine("Closing INSTEON network...");
            OnClosing();
            Messenger.Close();
            Connection = null;
            Log.WriteLine("INSTEON network closed");
        }

        /// <summary>
        /// <summary>
        /// Returns the INSTEON network connection string, or null if the network is not connected. This string can be used later to reconnect to the same network.
        /// </summary>
        public InsteonConnection Connection { get; private set; }

        internal void Disconnect()
        {
            Connection = null;
            OnDisconnected();
        }

        /// <summary>
        /// Returns the available connections.
        /// </summary>
        /// <param name="refresh">Specifies whether to refresh the list. If called after TryConnectNet with false the list found by TryConnectNet will be returned.</param>
        /// <returns>An array of objects representing each available connection.</returns>
        public InsteonConnection[] GetAvailableConnections(bool refresh)
        {
            List<InsteonConnection> list = new List<InsteonConnection>();
            list.AddRange(GetAvailableNetworkConnections(refresh));
            list.AddRange(GetAvailableSerialConnections());
            return list.ToArray();
        }

        /// <summary>
        /// Returns the available network connections.
        /// </summary>
        /// <param name="refresh">Specifies whether to refresh the list. If called after TryConnectNet with false the list found by TryConnectNet will be returned.</param>
        /// <returns>An array of objects representing each available network connection.</returns>
        public InsteonConnection[] GetAvailableNetworkConnections(bool refresh)
        {
            if (connections == null || refresh)
            {
                connections = new List<InsteonConnection>();
                List<SmartLincInfo> list = SmartLincFinder.GetRegisteredSmartLincs();
                foreach (SmartLincInfo item in list)
                {
                    string name = SmartLincFinder.GetSmartLincName(item.Uri.AbsoluteUri);
                    connections.Add(new InsteonConnection(InsteonConnectionType.Net, item.Uri.Host, name));
                }
            }
            return connections.ToArray();
        }

        /// <summary>
        /// Returns the available serial connections.
        /// </summary>
        /// <returns>An array of objects representing each available serial connection.</returns>
        public InsteonConnection[] GetAvailableSerialConnections()
        {
            List<InsteonConnection> list = new List<InsteonConnection>();
            string[] ports = SerialPort.GetPortNames();
            foreach (string port in ports)
                list.Add(new InsteonConnection(InsteonConnectionType.Serial, port));
            return list.ToArray();
        }

        ///<summary>
        /// Determines whether the connection to the INSTEON network is active.
        /// </summary>
        public bool IsConnected { get { return Connection != null; } }

        private void OnConnected()
        {
            if (Connected != null)
                Connected(this, EventArgs.Empty);
        }

        private void OnConnectProgress(int progressPercentage)
        {
            if (ConnectProgress != null)
                ConnectProgress(this, new ProgressChangedEventArgs(progressPercentage, null));
        }

        private void OnDisconnected()
        {
            if (Disconnected != null)
                Disconnected(this, EventArgs.Empty);
        }

        private void OnClosing()
        {
            if (Closing != null)
                Closing(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets the folder path where INSTEON log files will be written.
        /// </summary>
        /// <param name="path">Full path to the specified folder.</param>
        /// <remarks>Folder must exist or an error will occur.</remarks>
        public static void SetLogPath(string path)
        {
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException();
            Log.Open(path);
        }

        /// <summary>
        /// Attempts to find an INSTEON controller interface first by using the Smarthome web service to connect over the network, and then by searching the serial ports for a compatible controller device.
        /// </summary>
        /// <returns>Returns true if a connection was successfully made, or false if unable to find a connection.</returns>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryConnect()
        {          
            if (TryConnectAll())
            {
                Controller = new InsteonController(this);
                OnConnected();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Connects to an INSTEON network using the specified connection.
        /// </summary>
        /// <param name="connection">Specifies the connection to the INSTEON controller device, which can accessed serially or over the network. Examples: "serial: COM1" or "net: 192.168.2.5".</param>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryConnect(InsteonConnection connection)
        {
            if (!Messenger.TryConnect(connection))
                return false;

            Connection = connection;
            Controller = new InsteonController(this);
            OnConnected();

            return true;
        }

        /// <summary>
        /// Connects to an INSTEON network using the Smarthome web service. Some devices such as the SmartLinc self-register with this service each time they are powered up.
        /// </summary>
        /// <returns>Returns true if a connection is established.</returns>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryConnectNet()
        {
            OnConnectProgress(0);
            List<SmartLincInfo> list = SmartLincFinder.GetRegisteredSmartLincs();
            OnConnectProgress(10);
            connections = new List<InsteonConnection>();
            foreach (SmartLincInfo item in list)
            {
                string name = SmartLincFinder.GetSmartLincName(item.Uri.AbsoluteUri);
                connections.Add(new InsteonConnection(InsteonConnectionType.Net, item.Uri.Host, name));
                Log.WriteLine("Registered SmartLinc url='{0}' name='{1}' address='{2}'", item.Uri.AbsoluteUri, name, item.InsteonAddress.ToString());
                OnConnectProgress(90 * list.IndexOf(item) / list.Count + 10);
            }

            foreach (InsteonConnection connection in connections)
                if (Messenger.TryConnect(connection))
                {
                    this.Connection = connection;
                    return true;
                }

            OnConnectProgress(100);
            this.Connection = null;
            return false;
        }

        /// <summary>
        /// Connects to an INSTEON network using a local serial port.
        /// </summary>
        /// <returns>Returns true if a connection is established.</returns>
        /// <remarks>
        /// This method does not throw an exception.
        /// </remarks>
        public bool TryConnectSerial()
        {
            string[] list = SerialPort.GetPortNames();
            foreach (string item in list)
            {
                Connection = new InsteonConnection(InsteonConnectionType.Net, item);
                if (Messenger.TryConnect(Connection))
                    return true;
            }

            Connection = null;
            return false;
        }

        private bool TryConnectAll()
        {
            if (TryConnectNet())
                return true;
            else if (TryConnectSerial())
                return true;
            else
                return false;
        }
    }
}
