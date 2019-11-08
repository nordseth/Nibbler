using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nibbler.Utils;

namespace Nibbler.Test
{
    [TestClass]
    public class GitLabelTest
    {
        [TestMethod]
        [DataRow("../../../../")]
        public void Git_Read_Repo(string repoPath)
        {
            using (var repo = new Repository(repoPath))
            {
                var RFC2822Format = "ddd dd MMM HH:mm:ss yyyy K";

                foreach (Commit c in repo.Commits.Take(15))
                {
                    Console.WriteLine(string.Format("commit {0}", c.Id));

                    if (c.Parents.Count() > 1)
                    {
                        Console.WriteLine("Merge: {0}",
                            string.Join(" ", c.Parents.Select(p => p.Id.Sha.Substring(0, 7)).ToArray()));
                    }

                    Console.WriteLine(string.Format("Author: {0} <{1}>", c.Author.Name, c.Author.Email));
                    Console.WriteLine("Date:   {0}", c.Author.When.ToString(RFC2822Format, CultureInfo.InvariantCulture));
                    Console.WriteLine();
                    Console.WriteLine(c.Message);
                    Console.WriteLine();
                }
            }
        }

        [TestMethod]
        [DataRow("../../../../")]
        public void Git_GetLabels(string repoPath)
        {
            var labels = GitLabels.GetLabels(repoPath);
            foreach (var l in labels)
            {
                Console.WriteLine($"{l.Key} = {l.Value}");
            }
        }
    }
}
