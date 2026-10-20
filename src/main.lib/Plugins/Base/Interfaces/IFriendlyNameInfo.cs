namespace PKISharp.WACS.Plugins.Interfaces
{
    public interface IFriendlyNameInfo
    {
        /// <summary>
        /// Get the intermediate friendly name, which is a combination 
        /// of the fixed base (which may be overruled by the caller) 
        /// and the optional OrderPart. 
        /// </summary>
        /// <param name="baseName"></param>
        /// <returns></returns>
        public string GetIntermediate(string? baseName = null);
    }
}
