using ColossalFramework.IO;
using System;
using System.IO;
using System.Xml.Serialization;
namespace TerraformTool
{
    public class ConfigData
    {
        public int MoneyModifer = 500;
        public bool Free = false;
        public static string GetConfigPath()
        {            
            string text = Path.Combine(DataLocation.modsPath, "TerraformTool\\TerraformTool.xml");
            if (!Directory.Exists(Path.GetDirectoryName(text)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(text));
            }
            return text;
        }
        public static void Serialize(ConfigData config)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ConfigData));
            using (StreamWriter streamWriter = new StreamWriter(ConfigData.GetConfigPath()))
            {
                xmlSerializer.Serialize(streamWriter, config);
            }
        }
        public static ConfigData Deserialize()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ConfigData));
            ConfigData result;
            try
            {
                using (StreamReader streamReader = new StreamReader(ConfigData.GetConfigPath()))
                {
                    result = (ConfigData)xmlSerializer.Deserialize(streamReader);
                }
            }
            catch
            {
                result = null;
            }
            return result;
        }
    }
}
