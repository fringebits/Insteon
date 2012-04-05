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
    public partial class ConnectionPage : UserControl
    {
        public ConnectionPage()
        {            
            InitializeComponent();
        }
        
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {            
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            if (!InsteonService.Network.IsConnected)
            {
                InsteonService.StartNetwork();
                InsteonService.Network.Connected += Network_Connected;
                InsteonService.ConnectionFailed += InsteonService_ConnectionFailed;
            }
            else
            {
                ShowNextPage();
            }
        }

        private void Network_Connected(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.ShowNextPage()), null);
        }

        private void InsteonService_ConnectionFailed(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.ShowFailPage()), null);
        }

        private void ShowNextPage()
        {
            InsteonService.Network.Connected -= Network_Connected;

            InsteonEventConfig eventConfig = UIHelper.FindParent<InsteonEventConfig>(this);
            if (eventConfig != null)
            {
                ShowNextPage(eventConfig);
                return;
            }

            InsteonReactionConfig reactionConfig = UIHelper.FindParent<InsteonReactionConfig>(this);
            if (reactionConfig != null)
            {
                ShowNextPage(reactionConfig);
                return;
            }
        }

        private void ShowNextPage(InsteonEventConfig config)
        {
            Panel parent = this.VisualParent as Panel;
            if (parent != null)
            {
                parent.Children.Remove(this);
                UserControl page;
                if (string.IsNullOrEmpty(config.DataItem.Device))
                    page = new NewEventPage();
                else
                    page = new ManageEventPage();
                parent.Children.Add(page);
                parent.Height = page.Height;
            }
        }

        private void ShowNextPage(InsteonReactionConfig config)
        {
            Panel parent = this.VisualParent as Panel;
            if (parent != null)
            {
                parent.Children.Remove(this);
                UserControl page;
                if (config.DataItem.Group == 0)
                    page = new NewReactionPage();
                else
                    page = new ManageReactionPage();
                parent.Children.Add(page);
                parent.Height = page.Height;
            }
        }

        private void ShowFailPage()
        {
            InsteonService.Network.Connected -= Network_Connected;
            Panel parent = this.VisualParent as Panel;
            if (parent != null)
            {
                parent.Children.Remove(this);
                UserControl page = new ConnectionFailPage();
                parent.Children.Add(page);
                parent.Height = page.Height;
            }
        }
    }
}
