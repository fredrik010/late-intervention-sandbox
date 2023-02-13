using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace PDFSecuReader
{
    //WSB XML file schema
    public class Configuration
    {
        public string? VGpu;
        public string? Networking;
        public string? ProtectedClient;
        public MappedFolder[]? MappedFolders;

        [XmlArray("LogonCommand")]
        [XmlArrayItem("Command")]
        public List<LogonCommand>? LogonCommand;
    }

    public class LogonCommand
    {
        [XmlText]
        public string? Command;
    }

    public class MappedFolder
    {
        public string? HostFolder;
        public string? SandboxFolder;
        public bool? ReadOnly;
    }
}
