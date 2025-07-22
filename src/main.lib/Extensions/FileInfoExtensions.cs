using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Extensions
{
    public static class FileInfoExtensions
    {  
        public static async Task SafeWrite(string path, string? content)
        {
            var newFile = new FileInfo(path);
            await newFile.SafeWrite(content);
        }

        /// <summary>
        /// Safely write a file to disk
        /// </summary>
        /// <param name="file"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public static async Task SafeWrite(this FileInfo file, string? content)
        {
            var newFile = new FileInfo(file.FullName + ".new");
            var previousFile = new FileInfo(file.FullName + ".previous");
            // Detect previously failed writes and abort on incomplete transaction
            if (previousFile.Exists || newFile.Exists)
            {
                throw new InvalidOperationException($"Write to {file.FullName} aborted because an earlier failure. Please check the file integrity and delete .new and/or .previous version to continue.");
            }

            string? verify;
            file.Refresh();
            if (file.Exists)
            {
                await WriteToDisk(newFile.FullName, content);
                verify = await File.ReadAllTextAsync(newFile.FullName);
                if (verify != content)
                {
                    throw new InvalidOperationException($"Write to {file.FullName} failed. Please check file integrity and delete the .new version to continue.");
                }
                File.Replace(newFile.FullName, file.FullName, previousFile.FullName, true);
            }
            else
            {
                await WriteToDisk(file.FullName, content);
            }

            // Verify if written content can be read back
            verify = await File.ReadAllTextAsync(file.FullName);
            if (verify == content)
            {
                // Delete backup
                File.Delete(file.FullName + ".previous");
                return;
            }

            // Verification failed
            if (file.Exists)
            {
                throw new InvalidOperationException($"Overwrite of {file.FullName} failed. A backup should be available in .previous version.");
            }
            else
            {
                throw new InvalidOperationException($"Write to {file.FullName} failed");
            }
        }

        /// <summary>
        /// Make sure file is fully flushed to disk
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileContent"></param>
        /// <returns></returns>
        private static async Task WriteToDisk(string filePath, string? fileContent)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            var data = fileContent != null ? new UTF8Encoding(true).GetBytes(fileContent) : [];
            await fileStream.WriteAsync(data);
            await fileStream.FlushAsync();
            await fileStream.DisposeAsync();
        }
    }
}
