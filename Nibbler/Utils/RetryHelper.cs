using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Nibbler.Utils
{
    public static class RetryHelper
    {
        public static async Task Retry(int times, bool debug, Func<Task> action)
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
                        if (debug)
                        {
                            var msg = JsonConvert.SerializeObject(ex.Message);
                            Console.WriteLine($"debug: will retry error: {msg}");
                        }

                        tries++;
                    }
                }
            }
        }
    }
}
