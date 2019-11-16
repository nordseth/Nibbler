using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nibbler.Utils;

namespace Nibbler.Test
{
    [TestClass]
    public class GitLabelTest
    {
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
