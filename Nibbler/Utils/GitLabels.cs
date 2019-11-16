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

            var repo = new Repo(gitRepoPath);

            var result = new Dictionary<string, string>();

            var gitInfo = repo.GetGitInfo();

            result.Add("nibbler.git.commit.id", gitInfo.CommitId);
            result.Add("nibbler.git.commit.message", gitInfo.CommitMessage);
            result.Add("nibbler.git.commit.author", gitInfo.CommitAuthor);
            result.Add("nibbler.git.commit.date", gitInfo.CommitDate);
            result.Add("nibbler.git.commit.ref", $"{gitInfo.Branch ?? Environment.GetEnvironmentVariable("GIT_BRANCH")}, {gitInfo.CommitDescription}");
            result.Add("nibbler.git.url", gitInfo.OriginUrl);

            return result;
        }
    }
}
