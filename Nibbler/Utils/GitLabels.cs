using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nordseth.Git;

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
        public static IDictionary<string, string> GetLabels(string repoPath, string prefix = "nibbler.git")
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

            var repo = new Repo(gitRepoPath);

            var result = new Dictionary<string, string>();

            var gitInfo = repo.GetGitInfo();

            result.Add($"{prefix}.commit.id", gitInfo.CommitId);
            result.Add($"{prefix}.commit.message", gitInfo.CommitMessage);
            result.Add($"{prefix}.commit.author", gitInfo.CommitAuthor);
            result.Add($"{prefix}.commit.date", gitInfo.CommitDate);
            result.Add($"{prefix}.commit.ref", $"{gitInfo.Branch ?? Environment.GetEnvironmentVariable("GIT_BRANCH")}, {gitInfo.CommitDescription}");
            result.Add($"{prefix}.url", gitInfo.OriginUrl);

            return result;
        }
    }
}
