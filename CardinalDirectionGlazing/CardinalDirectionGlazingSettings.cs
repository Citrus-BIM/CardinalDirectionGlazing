using System.IO;
using System.Xml.Serialization;

namespace CardinalDirectionGlazing
{
    public class CardinalDirectionGlazingSettings
    {
        public string SelectedRevitLinkInstanceName { get; set; } = string.Empty;
        public string SpacesForProcessingButtonName { get; set; } = string.Empty;
        public string SpacesOrRoomsForProcessingButtonName { get; set; } = string.Empty;

        /// <summary>Для режима «Помещения»: учитывать ли окна и остекление из связанного файла.</summary>
        public bool UseLinkedFileForRooms { get; set; }

        public static CardinalDirectionGlazingSettings? GetSettings()
        {
            CardinalDirectionGlazingSettings? cardinalDirectionGlazingSettings = null;
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            const string fileName = "CardinalDirectionGlazingSettings.xml";
            string? dir = Path.GetDirectoryName(assemblyPathAll);
            if (string.IsNullOrEmpty(dir))
                return null;

            string assemblyPath = Path.Combine(dir, fileName);

            if (File.Exists(assemblyPath))
            {
                using (FileStream fs = new FileStream(assemblyPath, FileMode.Open))
                {
                    XmlSerializer xSer = new XmlSerializer(typeof(CardinalDirectionGlazingSettings));
                    cardinalDirectionGlazingSettings = xSer.Deserialize(fs) as CardinalDirectionGlazingSettings;
                }
            }

            return cardinalDirectionGlazingSettings;
        }

        public void SaveSettings()
        {
            string assemblyPathAll = System.Reflection.Assembly.GetExecutingAssembly().Location;
            const string fileName = "CardinalDirectionGlazingSettings.xml";
            string? dir = Path.GetDirectoryName(assemblyPathAll);
            if (string.IsNullOrEmpty(dir))
                return;

            string assemblyPath = Path.Combine(dir, fileName);

            if (File.Exists(assemblyPath))
            {
                File.Delete(assemblyPath);
            }

            using (FileStream fs = new FileStream(assemblyPath, FileMode.Create))
            {
                XmlSerializer xSer = new XmlSerializer(typeof(CardinalDirectionGlazingSettings));
                xSer.Serialize(fs, this);
            }
        }
    }
}
