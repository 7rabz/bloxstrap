﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

using Bloxstrap.Models;
using Bloxstrap.Enums;
using Bloxstrap.Helpers;
using Bloxstrap.Views;

namespace Bloxstrap
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static readonly CultureInfo CultureFormat = CultureInfo.InvariantCulture;

        public const string ProjectName = "Bloxstrap";
        public const string ProjectRepository = "pizzaboxer/bloxstrap";

        public static string BaseDirectory = null!;
        public static bool IsSetupComplete { get; set; } = true;
        public static bool IsFirstRun { get; private set; } = false;
        public static bool IsQuiet { get; private set; } = false;
        public static bool IsUninstall { get; private set; } = false;
        public static bool IsNoLaunch { get; private set; } = false;
        public static bool IsUpgrade { get; private set; } = false;
        public static string[] LaunchArgs { get; private set; } = null!;


        public static string Version = Assembly.GetExecutingAssembly().GetName().Version!.ToString()[..^2];

        public static readonly JsonManager<Settings> Settings = new();
        public static readonly HttpClient HttpClient = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All });

        // shorthand
        public static MessageBoxResult ShowMessageBox(string message, MessageBoxImage icon = MessageBoxImage.None, MessageBoxButton buttons = MessageBoxButton.OK)
        {
            if (IsQuiet)
                return MessageBoxResult.None;

            return MessageBox.Show(message, ProjectName, buttons, icon);
        }

        public static void Terminate(int code = Bootstrapper.ERROR_SUCCESS)
        {
            Settings.Save();
            Environment.Exit(code);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            LaunchArgs = e.Args;

            HttpClient.Timeout = TimeSpan.FromMinutes(5);
            HttpClient.DefaultRequestHeaders.Add("User-Agent", ProjectRepository);

            if (LaunchArgs.Length > 0)
            {
                if (Array.IndexOf(LaunchArgs, "-quiet") != -1)
                    IsQuiet = true;

                if (Array.IndexOf(LaunchArgs, "-uninstall") != -1)
                    IsUninstall = true;

                if (Array.IndexOf(LaunchArgs, "-nolaunch") != -1)
                    IsNoLaunch = true;

                if (Array.IndexOf(LaunchArgs, "-upgrade") != -1)
                    IsUpgrade = true;
            }

            // check if installed
            RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey($@"Software\{ProjectName}");

            if (registryKey is null)
            {
                IsFirstRun = true;
                BaseDirectory = Path.Combine(Directories.LocalAppData, ProjectName);

                if (!IsQuiet)
                {
                    IsSetupComplete = false;
                    new MainWindow().ShowDialog();
                }
            }
            else
            {
                BaseDirectory = (string)registryKey.GetValue("InstallLocation")!;
                registryKey.Close();
            }

            // preferences dialog was closed, and so base directory was never set
            if (!IsSetupComplete)
                Environment.Exit(Bootstrapper.ERROR_INSTALL_USEREXIT);

            Directories.Initialize(BaseDirectory);

            //Settings.FileLocation = Path.Combine(Directories.Base, "Settings.json");

            // we shouldn't save settings on the first run until the first installation is finished,
            // just in case the user decides to cancel the install
            if (!IsFirstRun)
            {
                Settings.Load();
                Settings.ShouldSave = true;
            }

#if !DEBUG
            if (!IsUninstall && !IsFirstRun)
                Updater.CheckInstalledVersion();
#endif

            string commandLine = "";

            if (LaunchArgs.Length > 0)
            {
                if (LaunchArgs[0] == "-preferences")
                {
#if !DEBUG
                    if (Process.GetProcessesByName(ProjectName).Length > 1)
                    {
                        ShowMessageBox($"{ProjectName} is currently running. Please close any currently open Bloxstrap or Roblox window before opening the menu.", MessageBoxImage.Error);
                        Environment.Exit(0);
                    }
#endif

                    //new Preferences().ShowDialog();
                    new MainWindow().ShowDialog();
                }
                else if (LaunchArgs[0].StartsWith("roblox-player:"))
                {
                    commandLine = Protocol.ParseUri(LaunchArgs[0]);
                }
                else if (LaunchArgs[0].StartsWith("roblox:"))
                {
                    commandLine = $"--app --deeplink {LaunchArgs[0]}";
                }
                else
                {
                    commandLine = "--app";
                }
            }
            else
            {
                commandLine = "--app";
            }

            if (!String.IsNullOrEmpty(commandLine))
            {
                DeployManager.Channel = Settings.Prop.Channel;
                Settings.Prop.BootstrapperStyle.Show(new Bootstrapper(commandLine));
            }

            Terminate();
        }
    }
}