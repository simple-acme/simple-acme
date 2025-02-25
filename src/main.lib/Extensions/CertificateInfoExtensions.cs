using Microsoft.VisualBasic;
using Org.BouncyCastle.Security;
using PKISharp.WACS.DomainObjects;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace PKISharp.WACS.Extensions
{
    public static class CertificateInfoExtensions
    {
        /// <summary>
        /// Get PFX archive as MemoryStream
        /// </summary>
        /// <param name="ci"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static MemoryStream PfxStream(this ICertificateInfo ci, string? password = null)
        {
            var stream = new MemoryStream();
            ci.Collection.Store.Save(stream, (password ?? "").ToCharArray(), new SecureRandom());
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        /// <summary>
        /// Get PFX archive as byte array
        /// </summary>
        /// <param name="ci"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static byte[] PfxBytes(this ICertificateInfo ci, string? password = null) => PfxStream(ci, password).ToArray();

        /// <summary>
        /// Save PFX archive to disk location
        /// </summary>
        /// <param name="ci"></param>
        /// <param name="path"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        public static async Task<FileInfo> PfxSave(this ICertificateInfo ci, string path, string? password = null)
        {
            var fi = new FileInfo(path);
            using var fs = fi.Open(FileMode.Create);
            using var stream = PfxStream(ci, password);
            await stream.CopyToAsync(fs);
            await fs.FlushAsync();
            fi.Refresh();
            return fi;
        }

        /// <summary>
        /// Get archive as .NET object
        /// </summary>
        /// <param name="ci"></param>
        /// <returns></returns>
        public static X509Certificate2Collection AsCollection(this ICertificateInfo ci, X509KeyStorageFlags flags, string? password = null)
        {
            var ret = X509CertificateLoader.LoadPkcs12Collection(ci.PfxBytes(password), password, flags);
            if (OperatingSystem.IsWindows())
            {
                ret.First(x => x.Thumbprint == ci.Thumbprint).FriendlyName = ci.FriendlyName;
            }
            return ret;
        }

    }
}
