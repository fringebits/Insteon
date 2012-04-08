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

            availableListBox.Items.SortDescriptions.Add(new SortDescription("Content", ListSortDirection.Ascending));
            AddSerialConnections();
            if (InsteonService.Network.Connection != null && InsteonService.Network.Connection.Type == InsteonConnectionType.Net)
                AddNetworkConnection(InsteonService.Network.Connection, true);

            worker.DoWork += worker_DoWork;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;
            worker.RunWorkerAsync();
        }

        private void AcceptDialog()
        {
            if (availableRadioButton.IsChecked.Value)
            {
                if (availableListBox.SelectedItem != null)
                {
                    ListBoxItem item = availableListBox.SelectedItem as ListBoxItem;
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
                    SelectedConnection = item.Tag as InsteonConnection;
            }

            DialogResult = SelectedConnection != null;
            Close();
        }

        private void AddNetworkConnection(InsteonConnection connection, bool select)
        {
            ListBoxItem item = new ListBoxItem();
            item.Content = connection.Name;
            item.ToolTip = InsteonService.GetConnectionInfo(connection);
            item.Tag = connection;
            availableListBox.Items.Add(item);
            if (select)
            {
                item.IsSelected = true;
                availableListBox.Focus();
            }
        }

        private void AddSerialConnections()
        {
            InsteonConnection[] connections = InsteonService.Network.GetAvailableSerialConnections();
            foreach (InsteonConnection connection in connections)
            {
                serialRadioButton.IsEnabled = true;
                ComboBoxItem item = new ComboBoxItem();
                item.Content = connection.Name;
                item.Tag = connection;
                serialComboBox.Items.Add(item);
                if (connection.Equals(InsteonService.Network.Connection))
                {
                    item.IsSelected = true;
                    serialRadioButton.IsChecked = true;
                }
                else if (serialComboBox.Items.Count == 1)
                {
                    item.IsSelected = true;
                }
            }
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            AcceptDialog();
        }

        private void connectionsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (availableListBox.SelectedItem != null) 
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
                    if (InsteonService.Network.Connection == null || !InsteonService.Network.Connection.Equals(connection)) // active connection already added to list on load
                        AddNetworkConnection(connection, false);
                
                connectButton.IsEnabled = availableRadioButton.IsChecked.Value;
            }
        }

        private void detectedRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (availableListBox == null)
                return;
            connectButton.IsEnabled = availableListBox.Items.Count > 0;
            availableListBox.IsEnabled = true;
            networkTextBox.IsEnabled = false;
            serialComboBox.IsEnabled = false;
            availableListBox.Focus();
        }

        private void networkRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            connectButton.IsEnabled = true;
            availableListBox.IsEnabled = false;
            networkTextBox.IsEnabled = true;
            serialComboBox.IsEnabled = false;
            networkTextBox.Focus();
        }

        private void serialRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            connectButton.IsEnabled = true;
            availableListBox.IsEnabled = false;
            networkTextBox.IsEnabled = false;
            serialComboBox.IsEnabled = true;
            serialComboBox.Focus();
        }
    }
}
