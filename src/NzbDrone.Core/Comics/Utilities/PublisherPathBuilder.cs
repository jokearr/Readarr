using System;
using System.IO;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Comics
{
    public interface IBuildPublisherPaths
    {
        string BuildPath(Publisher publisher, bool useExistingRelativeFolder);
    }

    public class PublisherPathBuilder : IBuildPublisherPaths
    {
        private readonly IBuildFileNames _fileNameBuilder;
        private readonly IRootFolderService _rootFolderService;

        public PublisherPathBuilder(IBuildFileNames fileNameBuilder, IRootFolderService rootFolderService)
        {
            _fileNameBuilder = fileNameBuilder;
            _rootFolderService = rootFolderService;
        }

        public string BuildPath(Publisher publisher, bool useExistingRelativeFolder)
        {
            if (publisher.RootFolderPath.IsNullOrWhiteSpace())
            {
                throw new ArgumentException("Root folder was not provided", nameof(publisher));
            }

            if (useExistingRelativeFolder && publisher.Path.IsNotNullOrWhiteSpace())
            {
                var relativePath = GetExistingRelativePath(publisher);
                return Path.Combine(publisher.RootFolderPath, relativePath);
            }

            return Path.Combine(publisher.RootFolderPath, _fileNameBuilder.GetPublisherFolder(publisher));
        }

        private string GetExistingRelativePath(Publisher publisher)
        {
            var rootFolderPath = _rootFolderService.GetBestRootFolderPath(publisher.Path);

            return rootFolderPath.GetRelativePath(publisher.Path);
        }
    }
}
