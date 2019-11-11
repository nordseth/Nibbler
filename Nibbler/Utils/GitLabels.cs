using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibGit2Sharp;

namespace Nibbler.Utils
{
    public static class GitLabels
    {
        //label nibbler.git.commit.id \"\$(git --no-pager show -s --format=%h)"
        //label nibbler.git.commit.message \"\$(git --no-pager show -s --format=%B)"
        //label nibbler.git.commit.ref \"\$GIT_BRANCH, \$(git describe --always)"
        //label nibbler.git.commit.author \"\$(git log -1 --pretty=%an) <\$(git log -1 --pretty=%ae)>"
        //label nibbler.git.commit.date \"\$(git log -1 --pretty=%ai)"
        //label nibbler.git.url \"\$GIT_REPO" 
        public static IDictionary<string, string> GetLabels(string repoPath)
        {
            string gitRepoPath;
            if (!string.IsNullOrEmpty(repoPath))
            {
                gitRepoPath = System.IO.Path.GetFullPath(repoPath);
            }
            else
            {
                gitRepoPath = System.IO.Directory.GetCurrentDirectory();
            }

            var result = new Dictionary<string, string>();
            using (var repo = new Repository(gitRepoPath))
            {
                var commit = repo.Head.Tip;
                string description = repo.Describe(commit, new DescribeOptions { UseCommitIdAsFallback = true });
                string branch = GetBranch(repo.Head);

                result.Add("nibbler.git.commit.id", commit.Id.ToString());
                result.Add("nibbler.git.commit.message", commit.MessageShort);
                result.Add("nibbler.git.commit.author", $"{commit.Author.Name} <{commit.Author.Email}>");
                result.Add("nibbler.git.commit.date", commit.Author.When.ToString("u"));
                result.Add("nibbler.git.commit.ref", $"{branch}, {description}");
                result.Add("nibbler.git.url", repo.Network.Remotes.FirstOrDefault(r => r.Name == "origin")?.Url);
            }

            return result;
        }

        private static string GetBranch(Branch head)
        {
            // try to fallback to env var $GIT_BRANCH, if detached head
            if (head.CanonicalName == "(no branch)")
            {
                string branchVar = Environment.GetEnvironmentVariable("GIT_BRANCH");
                if (!string.IsNullOrEmpty(branchVar))
                {
                    return branchVar;
                }
            }

            return head.FriendlyName;
        }
    }
}
