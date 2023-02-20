using System.Collections.Generic;
using System.Management;

namespace AssetInstaller
{
    class SystemGpuInfo
    {
        public static bool IsNvidia
        {
            get
            { 
                foreach (string graphicsCard in GetGraphicsCards())
                {
                    if (graphicsCard.ToLower().Contains("nvidia"))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static List<string> GetGraphicsCards()
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            List<string> graphicsCards = new List<string>();

            foreach (ManagementObject obj in searcher.Get())
            {
                foreach (PropertyData property in obj.Properties)
                {
                    if (property.Name == "Description")
                    {
                        graphicsCards.Add(property.Value.ToString());
                    }
                }
            }

            return graphicsCards;
        }
    }
}
