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
    public partial class NewReactionPage : UserControl, IPage
    {
        private InsteonReactionConfig config = null;

        public NewReactionPage()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            config = UIHelper.FindParent<InsteonReactionConfig>(this);
            InsteonService.GetAvailableGroupCompleted += InsteonService_GetAvailableGroupCompleted;
            InsteonService.BeginGetAvailableGroup();
        }

        public void Close()
        {
            InsteonService.GetAvailableGroupCompleted -= InsteonService_GetAvailableGroupCompleted;
            if (InsteonService.Network.Controller.IsInLinkingMode)
                InsteonService.Network.Controller.TryCancelLinkMode();
        }

        private void EnterLinkMode()
        {
            if (!InsteonService.Network.Controller.TryEnterLinkMode(InsteonLinkMode.Controller, config.DataItem.Group))
            {
                SetError("Sorry, there was a problem communicating with the INSTEON controller.\r\n\r\nIf this problem persists, please try unplugging your INSTEON controller from the wall and plugging it back in.");
                return;
            }

            InsteonService.Network.Controller.DeviceLinked += PlmDevice_DeviceLinked;
            InsteonService.Network.Controller.DeviceLinkTimeout += PlmDevice_DeviceLinkTimeout;
        }

        private void SetError(string message)
        {
            captionTextBlock.Text = message;
            animation.Visibility = Visibility.Hidden;
        }

        private void OnDeviceLinked(string address)
        {
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;
            InsteonService.Network.Controller.DeviceLinkTimeout -= PlmDevice_DeviceLinkTimeout;

            config.DataItem.Device = address;
            config.DataItem.DeviceStatus = InsteonDeviceStatus.On;
            config.CanSave = true;

            PageFrame frame = UIHelper.FindParent<PageFrame>(this);
            if (frame != null)
                frame.SetPage(new ManageReactionPage());
        }

        void OnDeviceLinkTimeout()
        {
            InsteonService.Network.Controller.DeviceLinked -= PlmDevice_DeviceLinked;
            InsteonService.Network.Controller.DeviceLinkTimeout -= PlmDevice_DeviceLinkTimeout;

            captionTextBlock.Text = "Are you still there?";
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

        private void InsteonService_GetAvailableGroupCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.UserState != null)
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    config.DataItem.Group = (byte)e.UserState;
                    captionTextBlock.Text = string.Empty;
                    animation.Visibility = Visibility.Visible;
                    EnterLinkMode();
                }), null);
            }
            else if (e.Error != null)
            {
                this.Dispatcher.BeginInvoke(new Action(() => SetError(e.Error.Message)), null);
            }
        }

        private void retryButton_Click(object sender, RoutedEventArgs e)
        {
            captionTextBlock.Text = string.Empty;
            retryButton.Visibility = Visibility.Hidden;
            animation.Visibility = Visibility.Visible;
            EnterLinkMode();
        }
    }
}
