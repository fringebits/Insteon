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
using System.Timers;
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
    public partial class NewReactionPage : UserControl
    {
        public NewReactionPage()
        {
            InitializeComponent();            
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            EnterLinkMode();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            if (InsteonService.Network.Controller.IsInLinkingMode)
                InsteonService.Network.Controller.TryCancelLinkMode();
        }

        private void EnterLinkMode()
        {
            InsteonReactionConfig config = UIHelper.FindParent<InsteonReactionConfig>(this);
            try
            {
                config.DataItem.Group = InsteonService.GetAvailableGroup();
            }
            catch (OutOfMemoryException)
            {
                captionTextBlock.Text = "Sorry, no more devices can be added.\r\n\r\nIf there is another event or reaction that may no longer be needed, remove it and try again.";
                captionTextBlock.Visibility = Visibility.Visible;
                animation.Visibility = Visibility.Hidden;
                return;
            }

            if (!InsteonService.Network.Controller.TryEnterLinkMode(InsteonLinkMode.Controller, config.DataItem.Group))
            {
                captionTextBlock.Text = "Sorry, there was a problem communicating with the INSTEON controller.\r\n\r\nIf this problem persists, please try unplugging your INSTEON controller and plugging it back in.";
                captionTextBlock.Visibility = Visibility.Visible;
                animation.Visibility = Visibility.Hidden;
                return;
            }

            InsteonService.Network.Controller.DeviceLinked += PlmDevice_DeviceLinked;
            InsteonService.Network.Controller.DeviceLinkTimeout += PlmDevice_DeviceLinkTimeout;
        }

        private void OnDeviceLinked(string address)
        {
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;
            InsteonService.Network.Controller.DeviceLinkTimeout -= PlmDevice_DeviceLinkTimeout;

            InsteonReactionConfig config = UIHelper.FindParent<InsteonReactionConfig>(this);
            config.DataItem.Device = address;
            config.DataItem.DeviceStatus = InsteonDeviceStatus.On;
            config.CanSave = true;

            Panel parent = this.VisualParent as Panel;
            parent.Children.Remove(this);
            UserControl page = new ManageReactionPage();
            parent.Children.Add(page);
            parent.Height = page.Height;
        }

        void OnDeviceLinkTimeout()
        {
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;
            InsteonService.Network.Controller.DeviceLinkTimeout -= PlmDevice_DeviceLinkTimeout;

            captionTextBlock.Visibility = Visibility.Visible;
            retryButton.Visibility = Visibility.Visible;
            animation.Visibility = Visibility.Hidden;
        }

        private void PlmDevice_DeviceLinked(object sender, InsteonDeviceEventArgs data)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.OnDeviceLinked(data.Device.Address.ToString())), null);
        }

        void PlmDevice_DeviceLinkTimeout(object sender, EventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.OnDeviceLinkTimeout()), null);
        }

        private void retryButton_Click(object sender, RoutedEventArgs e)
        {
            captionTextBlock.Visibility = Visibility.Hidden;
            retryButton.Visibility = Visibility.Hidden;
            animation.Visibility = Visibility.Visible;
            EnterLinkMode();
        }
    }
}
