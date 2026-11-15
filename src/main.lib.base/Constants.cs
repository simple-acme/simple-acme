using ACMESharp.Authorizations;

namespace PKISharp.WACS
{
    public static class Constants
    {
        public const string VaultPrefix = "vault://";
        public const int MaxCommonName = 64;
        public const string Dns01ChallengeType = Dns01ChallengeValidationDetails.Dns01ChallengeType;
        public const string Http01ChallengeType = Http01ChallengeValidationDetails.Http01ChallengeType;
        public const string TlsAlpn01ChallengeType = TlsAlpn01ChallengeValidationDetails.TlsAlpn01ChallengeType;
        public const string DefaultChallengeType = Http01ChallengeType;
    }
}
