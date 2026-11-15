
namespace PKISharp.WACS.Services.Serialization  
{
    public class ProtectedStringFactory(IProtector protector, ILogService log)
    {
        public ProtectedString Parse(string anyValue) => new(anyValue, log, protector);
        public ProtectedString Create(string? clearValue) => new(clearValue, protector);
        public ProtectedString? Protect(string? clearValue, bool allowEmtpy = false)
        {
            if (string.IsNullOrWhiteSpace(clearValue) && !allowEmtpy)
            {
                return null;
            }
            return new ProtectedString(clearValue, protector);
        }
    }
}
