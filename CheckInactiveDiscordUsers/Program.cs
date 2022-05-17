using Newtonsoft.Json.Linq;
using RestSharp;

namespace CheckInactiveDiscordUsers
{
    internal class Program
    {
        static private string logPath = $"{Environment.CurrentDirectory}/log.txt";

        static async Task Main(string[] args)
        {
            try
            {
                var launchConfiguration = JObject.Parse(await File.ReadAllTextAsync($@"{Environment.CurrentDirectory}/launchConfiguration.json"));

                var serverId = long.Parse(launchConfiguration["serverId"].ToString());
                var checkerToken = launchConfiguration["tokenChecker"].ToString();
                var countDays = int.Parse(launchConfiguration["countDays"].ToString());
                var discordAccountIds = await File.ReadAllLinesAsync(launchConfiguration["discordAccountIdsPath"].ToString());
                var failChechedDiscordAccountIdsPath = launchConfiguration["failChechedDiscordAccountIdsPath"].ToString();
                var restClient = new RestClient();
                var discordRequest = new RestRequest();
                discordRequest.AddHeader("authorization", checkerToken);
                discordRequest.AddHeader("tts", false);

                List<string> failChechedDiscordAccountIds = new List<string>();
                if (File.Exists(failChechedDiscordAccountIdsPath))
                    failChechedDiscordAccountIds = (await File.ReadAllLinesAsync(failChechedDiscordAccountIdsPath)).ToList();

                foreach (var discordAccountId in discordAccountIds)
                {
                    if (failChechedDiscordAccountIds.Contains(discordAccountId))
                        continue;

                    discordRequest.Resource = $"https://discord.com/api/v9/guilds/{serverId}/messages/search?author_id={discordAccountId}&limit=1";
                    var lastDiscordAccountMessageRespounce = await restClient.ExecuteGetAsync(discordRequest);
                    if (!lastDiscordAccountMessageRespounce.IsSuccessful)
                    {
                        if (lastDiscordAccountMessageRespounce.Content.Contains("rate"))
                        {
                            var rateDelay = int.Parse(Math.Ceiling(double.Parse(JObject.Parse(lastDiscordAccountMessageRespounce.Content)["retry_after"].ToString())).ToString());
                            await Task.Delay(rateDelay * 1000);
                        }

                        Console.WriteLine("Возникла ошибка при полученном ответе: " + lastDiscordAccountMessageRespounce.ErrorException);
                    }

                    var lastDiscordAccountMessage = JArray.Parse(JObject.Parse(lastDiscordAccountMessageRespounce.Content)["messages"].ToString()).First.First;
                    var dateLastMessage = DateTime.Parse(lastDiscordAccountMessage["timestamp"].ToString());
                    if (dateLastMessage.AddDays(countDays) < DateTime.Now)
                    {
                        Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] {discordAccountId} - Не прошел проверку");
                        await File.AppendAllTextAsync(failChechedDiscordAccountIdsPath, discordAccountId + Environment.NewLine);
                        await File.AppendAllTextAsync(logPath, $"[{DateTime.Now.ToLongTimeString()}] {discordAccountId} - Не прошел проверку" + Environment.NewLine);
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] {discordAccountId} - Успешно прошел проверку");
                        await File.AppendAllTextAsync(logPath, $"[{DateTime.Now.ToLongTimeString()}] {discordAccountId} - Успешно прошел проверку" + Environment.NewLine);
                    }

                    await Task.Delay(1000);
                }

                Console.WriteLine($"[{DateTime.Now.ToLongTimeString()}] Программа успешно завершила свою работу!");
                await File.AppendAllTextAsync(logPath, $"[{DateTime.Now.ToLongTimeString()}] Программа успешно завершила свою работу!" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Возникла непредвиденные ошибка: " + ex.Message);
                await File.AppendAllTextAsync(logPath, $"[{DateTime.Now.ToLongTimeString()}] Возникла непредвиденные ошибка: " + ex.Message + Environment.NewLine);
            }
        }
    }
}