using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wicker;

namespace WickerREST
{
    internal static class Utilities
    {
        internal static async System.Threading.Tasks.Task EnsureFileExists(string filePath, string url, bool isBinary = false)
        {
            if (!File.Exists(filePath))
            {
                try
                {
                    using (var httpClient = new HttpClient())
                    {
                        if (isBinary)
                        {
                            WickerServer.Instance.LogMessage($"Attempting binary download of {url}", 1);
                            var response = await httpClient.GetAsync(url);
                            if (response.IsSuccessStatusCode)
                            {
                                var contentBytes = await response.Content.ReadAsByteArrayAsync();
                                await File.WriteAllBytesAsync(filePath, contentBytes);
                                WickerServer.Instance.LogMessage($"Downloaded binary file to {filePath}", 1);
                            }
                            else
                            {
                                WickerServer.Instance.LogMessage($"Failed to download binary file. Status code: {response.StatusCode}", 1);
                            }
                        }
                        else
                        {
                            var contentString = await httpClient.GetStringAsync(url);
                            await File.WriteAllTextAsync(filePath, contentString);
                            WickerServer.Instance.LogMessage($"Downloaded text file to {filePath}", 1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WickerServer.Instance.LogMessage($"Exception during file download: {ex.Message}", 1);
                }
            }
        }

        public static string EnsureUniqueKey(IEnumerable<string> existingKeys, string originalKey)
        {
            string uniqueKey = originalKey;
            int counter = 2;
            while (existingKeys.Contains(uniqueKey))
            {
                uniqueKey = $"{originalKey}_{counter}";
                counter++;
            }
            return uniqueKey;
        }
    }
}
