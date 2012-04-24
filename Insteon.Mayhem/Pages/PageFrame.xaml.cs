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
using Insteon.Network;

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

            if (!InsteonService.Network.IsConnected)
                InsteonService.StartNetwork();

            UpdateStatus();
        }

        public void UpdateStatus()
        {
            Type pageType = pagePanel.Children.Count > 0 ? pagePanel.Children[0].GetType() : null;

            if (InsteonService.Network.IsConnected)
            {
                statusTextBlock.Text = string.Format("Connected to '{0}'", InsteonService.Network.Connection.Name);
                statusTextBlock.ToolTip = InsteonService.GetConnectionInfo(InsteonService.Network.Connection);
                iconViewbox.Child = new GlowingIcon();
                statusTextBlock.Cursor = Cursors.Hand;
                iconViewbox.Cursor = Cursors.Hand;
            }
            else if (InsteonService.Connecting || pageType == typeof(ConnectionPage))
            {
                if (InsteonService.SpecificConnection == null)
                {
                    statusTextBlock.Text = "Searching...";
                    statusTextBlock.ToolTip = null;
                }
                else
                {
                    statusTextBlock.Text = string.Format("Connecting to '{0}'...", InsteonService.SpecificConnection.Name);
                    statusTextBlock.ToolTip = InsteonService.GetConnectionInfo(InsteonService.SpecificConnection);
                }
                iconViewbox.Child = new SpinningIcon();
                statusTextBlock.Cursor = Cursors.Arrow;
                iconViewbox.Cursor = Cursors.Arrow;
            }
            else
            {
                if (InsteonService.SpecificConnection == null)
                {
                    statusTextBlock.Text = "Not connected";
                    statusTextBlock.ToolTip = null;
                }
                else
                {
                    statusTextBlock.Text = string.Format("Lost connection to '{0}'", InsteonService.SpecificConnection.Name);
                    statusTextBlock.ToolTip = InsteonService.GetConnectionInfo(InsteonService.SpecificConnection);
                }
                iconViewbox.Child = new StopIcon();
                statusTextBlock.Cursor = Cursors.Hand;
                iconViewbox.Cursor = Cursors.Hand;
            }
            iconViewbox.Cursor = statusTextBlock.Cursor;
            iconViewbox.ToolTip = statusTextBlock.ToolTip;
        }

        public void SetPage(UserControl page)
        {
            pagePanel.Children.Clear();
            pagePanel.Children.Add(page);
            pagePanel.Height = page.Height;
            StatusControlsVisible = true;
            UpdateStatus();
        }

        public bool ShowConnectionDialog()
        {
            ConnectionDialog dialog = new ConnectionDialog()
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();

            if (dialog.SelectedConnection != null)
            {
                if (!dialog.SelectedConnection.Equals(InsteonService.Network.Connection)) // only take action if the selected connection DOES NOT match the active connection, otherwise do nothing
                {
                    StatusControlsVisible = false;
                    UIHelper.RefreshElement(this);
                    InsteonService.StartNetwork(dialog.SelectedConnection);
                    SetPage(new ConnectionPage());
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool StatusControlsVisible
        {
            get
            {
                return iconViewbox.Visibility == Visibility.Visible;
            }
            set
            {
                iconViewbox.Visibility = value ? Visibility.Visible : Visibility.Hidden;
                statusTextBlock.Visibility = value ? Visibility.Visible : Visibility.Hidden;
            }
        }

        void Network_Connected(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.UpdateStatus()), null);
        }

        void Network_Disconnected(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (InsteonService.SpecificConnection == null)
                {
                    statusTextBlock.Text = "Lost connection";
                    statusTextBlock.ToolTip = null;
                }
                else
                {
                    statusTextBlock.Text = string.Format("Lost connection to '{0}'", InsteonService.SpecificConnection.Name);
                    statusTextBlock.ToolTip = InsteonService.GetConnectionInfo(InsteonService.SpecificConnection);
                }
                iconViewbox.Child = new StopIcon();
                statusTextBlock.Cursor = Cursors.Hand;
                iconViewbox.Cursor = Cursors.Hand;
            }), null);
        }

        void Network_ConnectProgress(object sender, ConnectProgressChangedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.UpdateStatus()), null);
        }

        void InsteonService_ConnectionFailed(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.UpdateStatus()), null);
        }

        private void StatusControl_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!InsteonService.Connecting)
                ShowConnectionDialog();
        }

        private void insteonLogo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(insteonLogo.ToolTip as string);
        }
    }
}
