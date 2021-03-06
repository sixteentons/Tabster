﻿#region

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Tabster.Core.Types;

#endregion

namespace Tabster.Utilities
{
    internal enum TabsterEnvironmentDirectory
    {
        ApplicatonData,
        UserData
    }

    internal class TabsterEnvironment
    {
        private static readonly string ApplicationDataDirectory;
        private static readonly string UserDataDirectory;
        private static TabsterVersion _version;

        static TabsterEnvironment()
        {
#if PORTABLE
            ApplicationDataDirectory = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "AppData");
            UserDataDirectory = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath), "UserData");
#else
            ApplicationDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Tabster");
            UserDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Tabster");
#endif
        }

        /// <summary>
        ///     Gets or sets whether the splash screen should be displayed.
        /// </summary>
        public static bool NoSplash { get; set; }

        /// <summary>
        ///     Gets or sets whether the application is currently running in 'safe mode'.
        /// </summary>
        public static bool SafeMode { get; set; }

        public static TabsterVersion GetVersion()
        {
            if (_version == null)
            {
                var attribute =
                    (AssemblyInformationalVersionAttribute) Assembly.GetExecutingAssembly()
                        .GetCustomAttributes(typeof (AssemblyInformationalVersionAttribute), false).FirstOrDefault();
                _version = attribute != null ? new TabsterVersion(attribute.InformationalVersion) : new TabsterVersion(Application.ProductVersion);
            }

            return _version;
        }

        /// <summary>
        ///     Returns the absolute path of the supplied environment directory.
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static string GetEnvironmentDirectoryPath(TabsterEnvironmentDirectory directory)
        {
            switch (directory)
            {
                case TabsterEnvironmentDirectory.ApplicatonData:
                    return ApplicationDataDirectory;
                case TabsterEnvironmentDirectory.UserData:
                    return UserDataDirectory;
            }

            return null;
        }

        /// <summary>
        ///     Creates the directory path by combining the environement directory variable and the relative path.
        /// </summary>
        /// <param name="directory">Environement directory.</param>
        /// <param name="path">Relative path.</param>
        public static string CreateEnvironmentDirectoryPath(TabsterEnvironmentDirectory directory, string path)
        {
            var combined = Path.Combine(GetEnvironmentDirectoryPath(directory), path);

            if (!Directory.Exists(combined))
                Directory.CreateDirectory(combined);

            return combined;
        }

        /// <summary>
        ///     Creates environment directories if they do not already exist.
        /// </summary>
        public static void CreateDirectories()
        {
            if (!Directory.Exists(ApplicationDataDirectory))
                Directory.CreateDirectory(ApplicationDataDirectory);

            if (!Directory.Exists(UserDataDirectory))
                Directory.CreateDirectory(UserDataDirectory);

            if (!Directory.Exists(UserDataDirectory))
                Directory.CreateDirectory(UserDataDirectory);
        }
    }
}