﻿using System;
using System.Drawing;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;

namespace Gravatar
{
    public interface IImageCache
    {
        /// <summary>
        /// Occurs whenever the cache is invalidated.
        /// </summary>
        event EventHandler Invalidated;

        /// <summary>
        /// Adds the image to the cache from the supplied stream.
        /// </summary>
        /// <param name="imageFileName">The image file name.</param>
        /// <param name="imageStream">The stream which contains the image.</param>
        Task AddImageAsync(string imageFileName, Stream imageStream);

        /// <summary>
        /// Clears the cache by deleting all images.
        /// </summary>
        Task ClearAsync();

        /// <summary>
        /// Deletes the specified image from the cache.
        /// </summary>
        /// <param name="imageFileName">The image file name.</param>
        Task DeleteImageAsync(string imageFileName);

        /// <summary>
        /// Retrieves the image from the cache.
        /// </summary>
        /// <param name="imageFileName">The image file name.</param>
        Image GetImage(string imageFileName);

        /// <summary>
        /// Retrieves the image from the cache.
        /// </summary>
        /// <param name="imageFileName">The image file name.</param>
        Task<Image> GetImageAsync(string imageFileName);
    }

    public sealed class DirectoryImageCache : IImageCache
    {
        private const int DefaultCacheDays = 30;
        private readonly string _cachePath;
        private readonly int _cacheDays;
        private readonly IFileSystem _fileSystem;

        public DirectoryImageCache(string cachePath, int cacheDays, IFileSystem fileSystem)
        {
            _cachePath = cachePath;
            _fileSystem = fileSystem;
            _cacheDays = cacheDays;
            if (_cacheDays < 1)
            {
                _cacheDays = DefaultCacheDays;
            }
        }

        public DirectoryImageCache(string cachePath, int cacheDays)
            : this(cachePath, cacheDays, new FileSystem())
        {
        }

        public event EventHandler Invalidated;

        public async Task AddImageAsync(string imageFileName, Stream imageStream)
        {
            if (string.IsNullOrWhiteSpace(imageFileName) || imageStream == null)
            {
                return;
            }

            if (!_fileSystem.Directory.Exists(_cachePath))
            {
                _fileSystem.Directory.CreateDirectory(_cachePath);
            }

            try
            {
                string file = Path.Combine(_cachePath, imageFileName);
                using (var output = new FileStream(file, FileMode.Create))
                {
                    await imageStream.CopyToAsync(output);
                }
            }
            catch
            {
                // do nothing
            }

            Invalidated?.Invoke(this, EventArgs.Empty);
        }

        public async Task ClearAsync()
        {
            if (!_fileSystem.Directory.Exists(_cachePath))
            {
                return;
            }

            await Task.Run(() =>
            {
                foreach (var file in _fileSystem.Directory.GetFiles(_cachePath))
                {
                    try
                    {
                        _fileSystem.File.Delete(file);
                    }
                    catch
                    {
                        // do nothing
                    }
                }
            });

            Invalidated?.Invoke(this, EventArgs.Empty);
        }

        public async Task DeleteImageAsync(string imageFileName)
        {
            if (string.IsNullOrWhiteSpace(imageFileName))
            {
                return;
            }

            string file = Path.Combine(_cachePath, imageFileName);
            if (!_fileSystem.File.Exists(file))
            {
                return;
            }

            try
            {
                await Task.Run(() => _fileSystem.File.Delete(file));
            }
            catch
            {
                // do nothing
            }

            Invalidated?.Invoke(this, EventArgs.Empty);
        }

        public Image GetImage(string imageFileName)
        {
            if (string.IsNullOrWhiteSpace(imageFileName))
            {
                return null;
            }

            string file = Path.Combine(_cachePath, imageFileName);
            try
            {
                if (HasExpired(file))
                {
                    return null;
                }

                using (Stream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                {
                    return Image.FromStream(fileStream);
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<Image> GetImageAsync(string imageFileName)
        {
            return await Task.Run(() => GetImage(imageFileName));
        }

        private bool HasExpired(string fileName)
        {
            var file = _fileSystem.FileInfo.FromFileName(fileName);
            if (!file.Exists)
            {
                return true;
            }

            return file.LastWriteTime < DateTime.Now.AddDays(-_cacheDays);
        }
    }
}
