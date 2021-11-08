using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nibbler.Utils
{
    /// <summary>
    /// https://docs.docker.com/registry/spec/auth/scope/
    /// </summary>
    public class ResourceScope
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public IEnumerable<string> Actions { get; set; }

        public override string ToString()
        {
            return $"{Type}:{Name}:{string.Join(",", Actions)}";
        }

        public bool IsPullOnly()
        {
            return Actions != null
                && Actions.Count() == 1
                && Actions.First() == "pull";
        }

        public void SetPullPush()
        {
            Actions = new[] { "pull", "push" };
        }

        public static ResourceScope TryParse(string scope)
        {
            // only support parsing single scope, so skip if space 
            if (scope.Contains(" "))
            {
                return null;
            }

            int firstSectionIndex = scope.IndexOf(":");
            int lastSectionIndex = scope.LastIndexOf(":");


            // if indexes are different, there should be more than two and a valid scope
            if (firstSectionIndex == lastSectionIndex)
            {
                return null;
            }


            var type = scope.Substring(0, firstSectionIndex);
            var name = scope.Substring(firstSectionIndex + 1, lastSectionIndex - firstSectionIndex - 1);
            var actionsString = scope.Substring(lastSectionIndex + 1);

            var actions = actionsString.Split(',');

            return new ResourceScope
            {
                Actions = actions,
                Name = name,
                Type = type,
            };
        }
    }
}
