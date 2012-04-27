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
    public partial class ConnectionPage : UserControl, IPage
    {
        private bool stopping = false;

        public ConnectionPage()
        {            
            InitializeComponent();
        }
        
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {            
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            stopping = false;
            InsteonService.Network.Connected += Network_Connected;
            InsteonService.ConnectionFailed += InsteonService_ConnectionFailed;
            InsteonService.Network.ConnectProgress += Network_ConnectProgress;

            if (!InsteonService.Network.IsConnected)
            {
                InsteonService.StartNetwork();

                // Only show progress controls when trying multiple connections, not when connecting to a specific connection...
                if (InsteonService.SpecificConnection != null)
                    HideProgressControls();

                // If there's a last connection status then use it to set the initial on-screen progress
                if (InsteonService.Network.LastConnectStatus != null) 
                {
                    progressBar.Value = InsteonService.Network.LastConnectStatus.ProgressPercentage;
                    statusTextBlock.Text = InsteonService.Network.LastConnectStatus.Status;
                }
            }
            else
            {
                InsteonService.Network.VerifyConnection(); // note: page frame will react on disconnect event if verification fails
                ShowNextPage();
            }
        }

        public void Close()
        {
            InsteonService.Network.Connected -= Network_Connected;
            InsteonService.ConnectionFailed -= InsteonService_ConnectionFailed;
            InsteonService.Network.ConnectProgress -= Network_ConnectProgress;
        }

        private void HideProgressControls()
        {
            progressBar.Visibility = Visibility.Hidden;
            statusTextBlock.Visibility = Visibility.Hidden;
            stopButton.Visibility = Visibility.Hidden;
        }

        private void ShowNextPage()
        {
            PageFrame frame = UIHelper.FindParent<PageFrame>(this);
            if (frame != null)
            {
                InsteonEventConfig eventConfig = UIHelper.FindParent<InsteonEventConfig>(this);
                if (eventConfig != null)
                {
                    if (eventConfig.DataItem.IsEmpty)
                        frame.SetPage(new NewEventPage());
                    else
                        frame.SetPage(new ManageEventPage());
                    return;
                }

                InsteonReactionConfig reactionConfig = UIHelper.FindParent<InsteonReactionConfig>(this);
                if (reactionConfig != null)
                {
                    if (reactionConfig.DataItem.IsEmpty)
                        frame.SetPage(new NewReactionPage());
                    else
                        frame.SetPage(new ManageReactionPage());
                    return;
                }
            }
        }

        private void ShowFailPage()
        {
            PageFrame frame = UIHelper.FindParent<PageFrame>(this);
            if (frame != null)
                frame.SetPage(new ConnectionFailPage());
        }

        private void InsteonService_ConnectionFailed(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (stopping)
                {
                    stopping = false;
                    HideProgressControls(); // hidden so they don't distract the user from the connection dialog that pops up over the page

                    PageFrame frame = UIHelper.FindParent<PageFrame>(this);
                    if (frame != null)
                    {
                        frame.StatusControlsVisible = false;
                        if (frame.ShowConnectionDialog())
                            return; // if show dialog returns true then page frame has already switched to the next page, otherwise fall thru and show the fail page
                    }
                }
                this.ShowFailPage();
            }
            ), null);
        }

        private void Network_Connected(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.ShowNextPage()), null);
        }

        private void Network_ConnectProgress(object sender, ConnectProgressChangedEventArgs e)
        {
            if (stopping)
                e.Cancel = true;

            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                progressBar.Value = e.ProgressPercentage;
                statusTextBlock.Text = e.Status;
            }
            ), null);
        }

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            stopping = true;
            stopButton.IsEnabled = false;
            statusTextBlock.Text = "Stopping...";
        }
    }
}
