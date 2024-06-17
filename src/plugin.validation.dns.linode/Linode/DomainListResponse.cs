namespace PKISharp.WACS.Plugins.ValidationPlugins.Linode
{
    internal class DomainListResponse
    {
        public int Page { get; set; }
        public int Pages { get; set; }
        public int Results { get; set; }

        public List<DomainItem>? Data { get; set; }
    }

    internal class DomainItem
    {
        public int Id { get; set; }
        public string? Domain { get; set; }
    }
}