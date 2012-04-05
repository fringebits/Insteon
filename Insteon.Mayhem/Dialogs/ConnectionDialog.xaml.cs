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
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Insteon.Network;

namespace Insteon.Mayhem
{
    public partial class ConnectionDialog : Window
    {
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private InsteonConnection[] availableConnections = null;

        public InsteonConnection SelectedConnection { get; private set; }

        public ConnectionDialog()
        {
            InitializeComponent();
        }

        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            foreach (string port in SerialPort.GetPortNames())
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = port;
                serialComboBox.Items.Add(item);
            }
            serialRadioButton.IsEnabled = serialComboBox.Items.Count > 0;
            serialComboBox.IsEnabled = serialComboBox.Items.Count > 0;

            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        private void AcceptDialog()
        {
            if (detectedRadioButton.IsChecked.Value)
            {
                if (detectedListBox.SelectedItem != null)
                {
                    ListBoxItem item = detectedListBox.SelectedItem as ListBoxItem;
                    if (item != null)
                        SelectedConnection = item.Tag as InsteonConnection;
                }
            }
            else if (networkRadioButton.IsChecked.Value)
            {
                if (string.IsNullOrWhiteSpace(networkTextBox.Text))
                {
                    MessageBox.Show(this, "Please specify a valid network connection.", "Mayhem", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                SelectedConnection = new InsteonConnection(InsteonConnectionType.Net, networkTextBox.Text);
            }
            else if (serialRadioButton.IsChecked.Value)
            {
                ComboBoxItem item = serialComboBox.SelectedItem as ComboBoxItem;
                if (item != null)
                    SelectedConnection = new InsteonConnection(InsteonConnectionType.Serial, item.Content as string);
            }


            DialogResult = SelectedConnection != null;
            Close();
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            AcceptDialog();
        }

        private void connectionsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            AcceptDialog();
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            bool refresh = !InsteonService.Network.IsConnected; // if connected don't refresh
            availableConnections = InsteonService.Network.GetAvailableNetworkConnections(refresh);
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            spinnerIcon.Visibility = Visibility.Collapsed;
            if (availableConnections.Length > 0)
            {
                foreach (InsteonConnection connection in availableConnections)
                {
                    ListBoxItem item = new ListBoxItem();
                    item.Content = connection.Name;
                    if (connection.Name != connection.Value)                            
                        item.ToolTip = connection.Value;
                    item.Tag = connection;
                    detectedListBox.Items.Add(item);
                }
                connectButton.IsEnabled = detectedRadioButton.IsChecked.Value;
            }
        }

        private void detectedRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (detectedListBox == null)
                return;
            connectButton.IsEnabled = detectedListBox.Items.Count > 0;
            detectedListBox.IsEnabled = true;
            networkTextBox.IsEnabled = false;
            serialComboBox.IsEnabled = false;
        }

        private void networkRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            connectButton.IsEnabled = true;
            detectedListBox.IsEnabled = false;
            networkTextBox.IsEnabled = true;
            serialComboBox.IsEnabled = false;            
        }

        private void serialRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            connectButton.IsEnabled = true;
            detectedListBox.IsEnabled = false;
            networkTextBox.IsEnabled = false;
            serialComboBox.IsEnabled = true;
            serialComboBox.SelectedIndex = 0;
        }
    }
}
