using ColossalFramework.IO;
using System.IO;
using System.Xml.Serialization;


namespace TerraformTool
{
    public class ConfigData
    {
        public int MoneyModifer = 500;
        public bool Free;
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
            var xmlSerializer = new XmlSerializer(typeof(ConfigData));
            using (var streamWriter = new StreamWriter(ConfigData.GetConfigPath()))
            {
                xmlSerializer.Serialize(streamWriter, config);
            }
        }
        public static ConfigData Deserialize()
        {
            var xmlSerializer = new XmlSerializer(typeof(ConfigData));
            ConfigData result;
            try
            {
                using (var streamReader = new StreamReader(ConfigData.GetConfigPath()))
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
