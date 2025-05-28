using System;

namespace PKISharp.WACS.Configuration.Settings
{
    [AttributeUsage(AttributeTargets.Property)]
    class SettingsValueAttribute() : Attribute 
    {
        public string? Description { get; set; }
        public string? NullBehaviour { get; set; }
        public string? SubType { get; set; }
        public string? Name { get; set; }
        public string? Default { get; set; }
        public bool Hidden { get; set; } = false;
    }
}
