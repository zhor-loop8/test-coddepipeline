using System;
using System.Collections.Generic;

#nullable disable

namespace WebAPI.DataModels
{
    public partial class Demo
    {
        public string PhoneToken { get; set; }
        public string ExtensionToken { get; set; }
        public int Id { get; set; }
        public string DataVersion { get; set; }
        public string DeviceOs { get; set; }
    }
}
