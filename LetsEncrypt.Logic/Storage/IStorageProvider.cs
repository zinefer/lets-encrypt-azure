﻿using System.Threading;
using System.Threading.Tasks;

namespace LetsEncrypt.Logic.Storage
{
    public interface IStorageProvider
    {
        /// <summary>
        /// Returns the escape fileName for the specific desired filename.
        /// Some providers may not support all characters and will encode them accordingly.
        /// E.g. blob @ -> "%40".
        /// </summary>
        string Escape(string fileName);

        /// <summary>
        /// Checks if a file with the given name exists.
        /// </summary>
        Task<bool> ExistsAsync(string fileName, CancellationToken cancellationToken);

        Task<string[]> ListAsync(string prefix, CancellationToken cancellationToken);

        /// <summary>
        /// Reads the content of an existing file.
        /// </summary>
        Task<string> GetAsync(string fileName, CancellationToken cancellationToken);

        /// <summary>
        /// Creates or updates the file with the content.
        /// </summary>
        Task SetAsync(string fileName, string content, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a file if it exists.
        /// </summary>
        Task DeleteAsync(string fileName, CancellationToken cancellationToken);
    }
}
