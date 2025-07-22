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
        public string? DefaultExtra { get; set; }
        public string? Warning { get; set; }
        public string? Tip { get; set; }
        public bool Split { get; set; } = false;
        public bool Hidden { get; set; } = false;
    }
}
