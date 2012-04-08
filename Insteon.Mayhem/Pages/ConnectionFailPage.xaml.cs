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

namespace Insteon.Mayhem
{
    public partial class ConnectionFailPage : UserControl
    {
        public ConnectionFailPage()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
                return;

            if (InsteonService.SpecificConnection != null)
            {
                StringBuilder sb = new StringBuilder();
                if (InsteonService.SpecificConnection.Name == InsteonService.SpecificConnection.Value)
                    sb.AppendFormat("Sorry, unable to connect to '{0}'", InsteonService.SpecificConnection.Name);
                else
                    sb.AppendFormat("Sorry, unable to connect to '{0}' at '{1}'", InsteonService.SpecificConnection.Name, InsteonService.SpecificConnection.Value);
                if (!InsteonService.SpecificConnection.Address.IsEmpty)
                    sb.AppendFormat("  ({0})", InsteonService.SpecificConnection.Address.ToString());
                CaptionTextBlock.Text = sb.ToString();
            }

            PageFrame frame = UIHelper.FindParent<PageFrame>(this);
            if (frame != null)
                frame.StatusControlsVisible = false;
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            PageFrame frame = UIHelper.FindParent<PageFrame>(this);
            if (frame != null)
                frame.SetPage(new ConnectionPage());
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            PageFrame frame = UIHelper.FindParent<PageFrame>(this);
            frame.ShowConnectionDialog();
        }

        private void hyperlinkTextBlock_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Process.Start(hyperlinkTextBlock.ToolTip as string);
        }
    }
}
