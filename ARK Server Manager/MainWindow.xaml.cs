﻿using ARK_Server_Manager.Lib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ARK_Server_Manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly DependencyProperty IsIpValidProperty = DependencyProperty.Register("IsIpValid", typeof(bool), typeof(MainWindow));
        public static readonly DependencyProperty CurrentConfigProperty = DependencyProperty.Register("CurrentConfig", typeof(Config), typeof(MainWindow));

        public static MainWindow Instance
        {
            get;
            private set;
        }

        public bool IsIpValid
        {
            get { return (bool)GetValue(IsIpValidProperty); }
            set { SetValue(IsIpValidProperty, value); }
        }

        public Config CurrentConfig
        {
            get { return GetValue(CurrentConfigProperty) as Config; }
            set { SetValue(CurrentConfigProperty, value); }
        }

        private object defaultTab;

        public MainWindow()
        {
            Config.Default.Reload();
            this.CurrentConfig = Config.Default;

            InitializeComponent();
            MainWindow.Instance = this;
            this.DataContext = this;
            this.defaultTab = this.Tabs.Items[0];
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //AddDefaultServerTab();            

            // We need to load the set of existing servers, or create a blank one if we don't have any...
            var tabAdded = false;
            foreach(var profile in Directory.EnumerateFiles(Config.Default.ConfigDirectory, "*" + Config.Default.ProfileExtension))
            {
                var settings = ServerSettings.LoadFrom(profile);
                AddNewServerTab(settings);
                tabAdded = true;
            }

            if (!tabAdded)
            {
                AddNewServerTab(new ServerSettings());
            }

            Tabs.SelectedIndex = 0;
        }

        private int AddNewServerTab(ServerSettings settings)
        {
            var newTab = new TabItem();
            var control = new ServerSettingsControl(settings);
            newTab.Content = control;
            newTab.DataContext = control;
            newTab.SetBinding(TabItem.HeaderProperty, new Binding("Settings." + ServerSettings.ProfileNameProperty));
            this.Tabs.Items.Insert(this.Tabs.Items.Count - 1, newTab);
            return this.Tabs.Items.Count - 2;            
        }

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                var tabControl = sender as TabControl;
                if (tabControl != null)
                {
                    if (tabControl.SelectedItem == this.defaultTab)
                    {
                        tabControl.SelectedIndex = AddNewServerTab(new ServerSettings());
                    }
                }
            }
        }

        public void CloseTab_Click(object sender, RoutedEventArgs args)
        {
            var button = sender as Button;
            if(button != null)
            {   
                var context = button.DataContext as ServerSettingsControl;
                var result = MessageBox.Show("Are you sure you want to delete this profile?\r\n\r\nNOE: This will only delete the profile, not the installation directory, save games or settings files contained therein.", String.Format("Delete {0}?", context.Settings.ProfileName), MessageBoxButton.YesNo);
                if(result == MessageBoxResult.Yes)
                {

                }
            }
        }
    }

    public class IpValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (IpValidationRule.ValidateIP((string)value))
            {
                return ValidationResult.ValidResult;
            }
            else
            {
                return new ValidationResult(false, "Invalid IP address or host name");
            }
        }

        private static bool ValidateIP(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }
            else
            {
                IPAddress ipAddress;
                if (IPAddress.TryParse(source, out ipAddress))
                {
                    return true;
                }
                else
                {
                    // Try DNS resolution
                    try
                    {
                        var addresses = Dns.GetHostAddresses(source);
                        var ip4Address = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                        if (ip4Address != null)
                        {
                            Debug.WriteLine(String.Format("Resolved address {0} to {1}", source, ip4Address.ToString()));
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
        }

    }
}
