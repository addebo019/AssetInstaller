using Microsoft.Win32;
using System;

namespace AssetInstaller.Utils
{
    class RegistryUtils
    {
        private static string GetBaseRegistryPath()
        {
            if (Environment.Is64BitOperatingSystem)
            {
                return "Software\\Wow6432Node\\Auran\\Products\\TrainzSimulator";
            }
            else
            {
                return "Software\\Auran\\Products\\TrainzSimulator";
            }
        }

        private static string GetBaseVirtualRegistryPath()
        {
            if (Environment.Is64BitOperatingSystem)
            {
                return "Software\\Classes\\VirtualStore\\Machine\\Software\\Wow6432Node\\Auran\\Products\\TrainzSimulator";
            }
            else
            {
                return "Software\\Classes\\VirtualStore\\Machine\\Software\\Auran\\Products\\TrainzSimulator";
            }
        }

        public static string FindTrainzInstallation()
        {
            using (RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(GetBaseRegistryPath()))
            {
                if (registryKey != null)
                {
                    foreach (string keyName in registryKey.GetSubKeyNames())
                    {
                        using (RegistryKey subKey = registryKey.OpenSubKey(keyName))
                        {
                            if (subKey.GetValue("ProductBuild") != null)
                            {
                                int buildNumber = Convert.ToInt32(subKey.GetValue("ProductBuild"));

                                if (buildNumber >= 37625 && buildNumber <= 44653)
                                {
                                    return subKey.GetValue("ProductInstallPath") as string;
                                }
                            }
                        }
                    }
                }
            }

            return FindTrainzUserInstallation();
        }

        public static string FindTrainzUserInstallation()
        {
            using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(GetBaseVirtualRegistryPath()))
            {
                if (registryKey != null)
                {
                    foreach (string keyName in registryKey.GetSubKeyNames())
                    {
                        using (RegistryKey subKey = registryKey.OpenSubKey(keyName))
                        {
                            if (subKey.GetValue("ProductBuild") != null)
                            {
                                int buildNumber = Convert.ToInt32(subKey.GetValue("ProductBuild"));

                                if (buildNumber >= 37625 && buildNumber <= 44653)
                                {
                                    return subKey.GetValue("ProductInstallPath") as string;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}
