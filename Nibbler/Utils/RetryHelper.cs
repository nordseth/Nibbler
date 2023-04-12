using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nibbler.Utils
{
    public static class RetryHelper
    {
        public static async Task Retry(int times, ILogger logger, Func<Task> action)
        {
            int tries = 0;
            while (true)
            {
                try
                {
                    await action();
                    return;
                }
                catch (Exception ex)
                {
                    if (tries >= times)
                    {
                        throw;
                    }
                    else
                    {
                        var msg = JsonSerializer.Serialize(ex.Message, JsonContext.Default.String);
                        logger.LogWarning($"failed, but will retry! error: {msg}");

                        tries++;
                    }
                }
            }
        }
    }
}
