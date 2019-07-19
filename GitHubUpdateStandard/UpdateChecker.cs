using System;
using System.Linq;
using Octokit;
using Semver;
using System.Threading.Tasks;

namespace GitHubUpdate
{
    public enum UpdateType
    {
        None,
        Major,
        Minor,
        Patch,
        Fail
    }

    public class UpdateChecker
    {
        private IReleasesClient _releaseClient;
        internal GitHubClient Github;

        internal SemVersion CurrentVersion;
        internal string RepositoryOwner;
        public string RepositoryName { get; internal set; }
        internal string RepostoryBranch;
        internal Release LatestRelease;

        public string ErrorMessage = "";

        void Init(string owner, string name, SemVersion version, string branch)
        {
            Github = new GitHubClient(new ProductHeaderValue(name + @"-UpdateCheck"));
            _releaseClient = Github.Repository.Release;

            RepositoryOwner = owner;
            RepositoryName = name;
            CurrentVersion = version;
            RepostoryBranch = branch;
        }        

        public UpdateChecker(string owner, string name, string version, string branch = "master")
        {
            Helper.ArgumentNotNullOrEmptyString(owner, @"owner");
            Helper.ArgumentNotNullOrEmptyString(name, @"name");
            Helper.ArgumentNotNullOrEmptyString(version, @"version");
            Helper.ArgumentNotNullOrEmptyString(version, @"branch");

            version = version.Substring(0, version.LastIndexOf('.'));

            Init(owner, name, version, branch);
        }

        public async Task<UpdateType> CheckUpdate(UpdateType locked = UpdateType.None)
        {
            System.Collections.Generic.IReadOnlyList<Release> releases;
            try
            {
                releases = await _releaseClient.GetAll(RepositoryOwner, RepositoryName);
            }
            catch(Exception ex)
            {
                ErrorMessage = ex.Message;
                return UpdateType.Fail;
            }

            SemVersion lockedVersion;
            switch (locked)
            {
                case UpdateType.Major:
                    lockedVersion = new SemVersion(CurrentVersion.Major + 1);
                    LatestRelease = releases.FirstOrDefault(
                        release => !release.Prerelease &&
                        release.TargetCommitish == RepostoryBranch &&
                        Helper.StripInitialV(release.TagName) > CurrentVersion &&
                        Helper.StripInitialV(release.TagName) < lockedVersion
                    );
                    break;
                case UpdateType.Minor:
                    lockedVersion = new SemVersion(CurrentVersion.Major, CurrentVersion.Minor + 1);
                    LatestRelease = releases.FirstOrDefault(
                        release => !release.Prerelease &&
                        release.TargetCommitish == RepostoryBranch &&
                        Helper.StripInitialV(release.TagName) > CurrentVersion &&
                        Helper.StripInitialV(release.TagName) < lockedVersion
                    );
                    break;
                default:
                    LatestRelease = releases.FirstOrDefault(
                        release => !release.Prerelease &&
                        release.TargetCommitish == RepostoryBranch);
                    break;
            }

            if (LatestRelease == null) return UpdateType.None;

            var tagName = LatestRelease.TagName;
            var latestVersion = Helper.StripInitialV(tagName);

            if (latestVersion.Major > CurrentVersion.Major)
                return UpdateType.Major;
            if (latestVersion.Minor > CurrentVersion.Minor)
                return UpdateType.Minor;
            if (latestVersion.Patch > CurrentVersion.Patch)
                return UpdateType.Patch;

            return UpdateType.None;
        }

        public async Task<string> RenderReleaseNotes()
        {
            if (LatestRelease == null)
                throw new InvalidOperationException();
            return LatestRelease.Body;
            //return await Github.Miscellaneous.RenderRawMarkdown(LatestRelease.Body);
        }        
    }
}
