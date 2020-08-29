using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using isRock.LineBot;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace NbaLineBot.Controllers
{
    [ApiController]
    public class ScoreboardController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _clientFactory;

        public ScoreboardController(
            IConfiguration config,
            IHttpClientFactory clientFactory)
        {
            _config = config;
            _clientFactory = clientFactory;
        }

        [HttpPost("api/scoreboard")]
        public async Task<IActionResult> PostAsync()
        {
            var token = _config.GetSection("ChannelAccessToken");
            var adminUserId = _config.GetSection("AdminUserId");
            var body = "";
            var bot = new Bot(token.Value);
            MessageBase responseMsg = null;
            var responseMsgs = new List<MessageBase>();

            try
            {
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync();
                }

                var receivedMessage = Utility.Parsing(body);

                var lineEvent = receivedMessage.events.FirstOrDefault();

                if (lineEvent.type.ToLower() == "message" && lineEvent.message.type.ToLower() == "text")
                {
                    var scoreboard = await GetScoreboardAsync(lineEvent.message.text);
                    responseMsg = new TextMessage(scoreboard);
                    responseMsgs.Add(responseMsg);
                }
                else
                {
                    responseMsg = new TextMessage($"None handled event type : {lineEvent.message.type}");
                    responseMsgs.Add(responseMsg);
                }

                bot.ReplyMessage(lineEvent.replyToken, responseMsgs);
                return Ok();
            }
            catch (Exception ex)
            {
                bot.PushMessage(adminUserId.Value, "Exception : \n" + ex.Message);
            }
            return Ok();
        }

        private async Task<string> GetScoreboardAsync(string dateString)
        {
            var client = _clientFactory.CreateClient();

            string requestUrl = $"https://data.nba.net/prod/v2/{dateString}/scoreboard.json";
            var response = await client.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                using (var responseStream = await response.Content.ReadAsStreamAsync())
                using (var document = await JsonDocument.ParseAsync(responseStream))
                {
                    dynamic scoreboard = new JsonObject { Element = document.RootElement };
                    var numGames = scoreboard.numGames;
                    if (numGames == 0)
                    {
                        return "No games are scheduled on this day.";
                    }
                    var games = scoreboard.games;
                    var result = new List<string>();
                    foreach (var game in games)
                    {
                        var vTriCode = game.vTeam.triCode;
                        var vScore = game.vTeam.score;
                        var hTriCode = game.hTeam.triCode;
                        var hScore = game.hTeamscore;

                        var isGameActivated = game.isGameActivated;
                        var clock = game.clock;
                        var current = game.period.current;
                        var maxRegular = game.period.maxRegular;
                        var isHalftime = game.period.isHalftime;
                        var isEndOfPeriod = game.period.isEndOfPeriod;

                        string title = string.Empty;
                        if (isGameActivated)
                        {
                            if (isHalftime)
                            {
                                title = "Halftime";
                            }
                            else if (isEndOfPeriod)
                            {
                                if (current <= maxRegular)
                                {
                                    title = $"End of Q{current}";
                                }
                                else
                                {
                                    if (current - maxRegular == 1)
                                    {
                                        title = "End of OT";
                                    }
                                    else
                                    {
                                        title = $"End of OT{current - maxRegular}";
                                    }
                                }
                            }
                            else
                            {
                                if (current <= maxRegular)
                                {
                                    if (string.IsNullOrEmpty(clock))
                                    {
                                        title = $"Pregame";
                                    }
                                    else
                                    {
                                        title = $"Q{current} {clock}";
                                    }
                                }
                                else
                                {
                                    if (current - maxRegular == 1)
                                    {
                                        title = $"OT {clock}";
                                    }
                                    else
                                    {
                                        title = $"OT{current - maxRegular} {clock}";
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (current != 0)
                            {
                                if (current <= maxRegular)
                                {
                                    title = "Final";
                                }
                                else
                                {
                                    if (current - maxRegular == 1)
                                    {
                                        title = "Final / OT";
                                    }
                                    else
                                    {
                                        title = $"Final / OT{current - maxRegular}";
                                    }

                                }
                            }
                        }
                        result.Add($"{vTriCode} {vScore} : {hScore} {hTriCode}\n{title}");
                    };
                    return string.Join("\n---------------\n", result);
                }
            }
            else
            {
                return "Bad Request";
            }
        }
    }

    public class JsonObject : DynamicObject
    {
        public JsonElement Element { get; set; }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            var hasProperty = Element.TryGetProperty(binder.Name, out var data);
            result = null;
            if (!hasProperty) return true;
            switch (data.ValueKind)
            {
                case JsonValueKind.Undefined:
                    result = null;
                    break;
                case JsonValueKind.Object:
                    result = new JsonObject
                    {
                        Element = data
                    };
                    break;
                case JsonValueKind.Array:
                    result = data.EnumerateArray().Select(x => new JsonObject { Element = x }).ToArray();
                    break;
                case JsonValueKind.String:
                    result = data.GetString();
                    break;
                case JsonValueKind.Number:
                    result = data.GetDouble();
                    break;
                case JsonValueKind.True:
                    result = true;
                    break;
                case JsonValueKind.False:
                    result = false;
                    break;
                case JsonValueKind.Null:
                    result = null;
                    break;
            }

            return true;
        }
    }
}
