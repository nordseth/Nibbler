using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Nibbler.Command
{
    public class RegistryBaseAndDestValidator : IOptionValidator
    {
        public ValidationResult GetValidationResult(CommandOption option, ValidationContext context)
        {
            // todo: verify that baseImage and destination is the same registry
            return ValidationResult.Success;
        }
    }
}
