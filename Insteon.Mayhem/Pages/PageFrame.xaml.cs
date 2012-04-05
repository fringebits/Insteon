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
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Insteon.Mayhem
{
    public partial class PageFrame : UserControl
    {
        public PageFrame()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, EventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            InsteonService.ConnectionFailed += InsteonService_ConnectionFailed;
            InsteonService.Network.Connected += Network_Connected;
            InsteonService.Network.Disconnected += Network_Disconnected;
            InsteonService.Network.ConnectProgress += Network_ConnectProgress;

            UpdateIcons();
            if (!InsteonService.Network.IsConnected)
                InsteonService.StartNetwork();
        }

        private void UpdateIcons()
        {
            Window window = Window.GetWindow(this);
            if (InsteonService.Network.IsConnected)
            {
                statusTextBlock.Text = string.Format("Connected to {0}", InsteonService.Network.Connection.Name);
                iconViewbox.Child = new GlowingIcon();
                statusTextBlock.Cursor = Cursors.Hand;
                iconViewbox.Cursor = Cursors.Hand;
                progressBar.Visibility = Visibility.Hidden;
                if (window != null)
                    window.Cursor = Cursors.Arrow;
            }
            else if (InsteonService.Connecting)
            {
                statusTextBlock.Text = "Connecting...";
                iconViewbox.Child = new SpinningIcon();
                statusTextBlock.Cursor = Cursors.AppStarting;
                iconViewbox.Cursor = Cursors.AppStarting;
                progressBar.Visibility = Visibility.Visible;
                if (window != null)
                    window.Cursor = Cursors.AppStarting;
            }
            else
            {
                statusTextBlock.Text = "Not connected";
                iconViewbox.Child = new StopIcon();
                statusTextBlock.Cursor = Cursors.Hand;
                iconViewbox.Cursor = Cursors.Hand;
                progressBar.Visibility = Visibility.Hidden;
                if (window != null)
                    window.Cursor = Cursors.Arrow;
            }
            iconViewbox.Cursor = statusTextBlock.Cursor;
        }

        private void Network_ConnectProgress(object sender, ProgressChangedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => progressBar.Value = e.ProgressPercentage), null);
        }

        public void ShowConnectionDialog()
        {
            ConnectionDialog dialog = new ConnectionDialog()
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();

            if (dialog.SelectedConnection != null)
            {
                InsteonService.Network.Close();

                pagePanel.Children.RemoveRange(0, pagePanel.Children.Count);
                pagePanel.Children.Add(new ConnectionPage());

                InsteonService.ConnectionFailed += InsteonService_ConnectionFailed;
                InsteonService.Network.Connected += Network_Connected;
                UpdateIcons();

                InsteonService.Connection = dialog.SelectedConnection;
                if (!InsteonService.Network.IsConnected)
                    InsteonService.StartNetwork();
            }
        }

        void Network_Connected(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.UpdateIcons()), null);
        }

        void InsteonService_ConnectionFailed(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.UpdateIcons()), null);
        }

        void Network_Disconnected(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.UpdateIcons()), null);
        }

        private void StatusControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!InsteonService.Connecting)
                ShowConnectionDialog();
        }

        private void insteonLogo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start("http://www.insteon.net");
        }
    }
}
