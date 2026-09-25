using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using negosuite_api.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace negosuite_api.Controllers
{
    [Authorize]
    [TypeFilter(typeof(ConfigUuidFilter))]
    [Route("api/ai")]
    [ApiController]
    public class AIController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _deepSeekApiKey;
        private readonly negosuiteContext _context;
        private readonly string _aiModel = "deepseek-chat";

        private static string MESSAGETYPE_USER = "User";
        private static string MESSAGETYPE_AI = "AI";
        //private static string MESSAGETYPE_SYSTEM = "System";

        public static short STATUS_ACTIVE = 1;
        public static short STATUS_INACTIVE = 2;
        public static short STATUS_DELETED = -1;

        public AIController(
            IHttpClientFactory httpClientFactory,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            negosuiteContext context)
        {
            _httpClientFactory = httpClientFactory;
            _deepSeekApiKey = configuration["DeepSeek:ApiKey"];
            _context = context;
        }


        [HttpGet("test")]
        public async Task<IActionResult> TestApiConnection()
        {
            var client = _httpClientFactory.CreateClient("DeepSeekClient");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_deepSeekApiKey}");

            var requestBody = new
            {
                model = "deepseek-chat",
                messages = new[]
                {
            new { role = "system", content = "You are a helpful assistant." },
            new { role = "user", content = "Just respond with 'Hello world'" }
        },
                temperature = 0.7
            };

            var response = await client.PostAsync(
                "v1/chat/completions",
                new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"));

            if (response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                return BadRequest($@"
        
                    API requires payment setup.Please:

                    1.Login to DeepSeek portal
        
                    2.Verify your account
        
                    3.Check free tier eligibility
        
                    4.Add payment method if required
                   ");
            }

            var content = await response.Content.ReadAsStringAsync();
            return Ok(content);
        }



        [HttpPost("query")]
        public async Task<ActionResult> ProcessNaturalLanguageQuery(Message message)
        {
            try
            {
                var question = message.Content;
                var userId = message.UserId;

                if (string.IsNullOrWhiteSpace(question))
                {
                    return BadRequest("Question cannot be empty");
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null || user.ConfigId == null)
                {
                    return BadRequest("Invalid user configuration");
                }


                // Check if new chat session
                ChatSession chatSession;
                if (String.IsNullOrEmpty(message.ChatSessionId))
                {
                    chatSession = await CreateNewChatSession(message);
                } 
                else
                {
                    chatSession = await _context.ChatSessions.FindAsync(message.ChatSessionId);
                    var userMessage = new ChatMessage
                    {
                        ChatSessionId = chatSession.Id,
                        Content = message.Content,
                        IsJsonContent = false,
                        ModelUsed = _aiModel,
                        MessageType = MESSAGETYPE_USER,
                        SentAt = DateTime.UtcNow,
                        TokensUsed = 0,
                        PromptTokens = 0,
                        CompletionTokens = 0
                    };
                    chatSession.LastActivityAt = new DateTime();
                    chatSession.ChatMessages.Add(userMessage);
                    _context.Entry(chatSession).State = EntityState.Modified;
                }

                // Step 1: Generate SQL
                var dbSchema = AIDbSchema.GetDbSchema();
                var generatedResult = await GenerateSqlFromQuestion(question, dbSchema, (int)user.ConfigId);

                if (!String.IsNullOrEmpty(generatedResult.offTopic))
                {
                    // Update chat session and record AI response as chat messages //
                    var newMessage = new ChatMessage
                    {
                        ChatSessionId = chatSession.Id,
                        Content = generatedResult.offTopic,
                        IsJsonContent = false,
                        ModelUsed = _aiModel,
                        MessageType = MESSAGETYPE_AI,
                        SentAt = DateTime.UtcNow,
                        PromptTokens = generatedResult.usage.PromptTokens,
                        CompletionTokens = generatedResult.usage.CompletionTokens,
                        TokensUsed = generatedResult.usage.TokensUsed,
                    };

                    chatSession.LastActivityAt = DateTime.UtcNow;
                    chatSession.ChatMessages.Add(newMessage);
                    _context.Entry(chatSession).State = EntityState.Modified;

                    await _context.SaveChangesAsync();

                    var ret = new
                    {
                        ChatSessionId = chatSession.Id,
                        ChatSessionName = chatSession.SessionName,
                        Question = question,
                        OffTopic = generatedResult.offTopic
                    };
                    return Ok(ret);
                }

                if (!String.IsNullOrEmpty(generatedResult.message) || String.IsNullOrEmpty(generatedResult.sqlQuery) )
                {
                    // Update chat session and record AI response as chat messages //
                    var newMessage = new ChatMessage
                    {
                        ChatSessionId = chatSession.Id,
                        Content = generatedResult.message,
                        IsJsonContent = false,
                        ModelUsed = _aiModel,
                        MessageType = MESSAGETYPE_AI,
                        SentAt = DateTime.UtcNow,
                        PromptTokens = generatedResult.usage.PromptTokens,
                        CompletionTokens = generatedResult.usage.CompletionTokens,
                        TokensUsed = generatedResult.usage.TokensUsed,
                    };

                    chatSession.LastActivityAt = DateTime.UtcNow;
                    chatSession.ChatMessages.Add(newMessage);
                    _context.Entry(chatSession).State = EntityState.Modified;

                    await _context.SaveChangesAsync();

                    var ret = new
                    {
                        ChatSessionId = chatSession.Id,
                        ChatSessionName = chatSession.SessionName,
                        question = question,
                        suggestion = generatedResult.message
                    };
                    return Ok(ret);
                }


                var generatedSql = generatedResult.sqlQuery;

                // Step 2: Validate SQL
                if (!IsQuerySafe(generatedSql))
                {
                     return BadRequest("I can not respond to your command.");
                }

                // Step 3: Execute query using Entity Framework
                var queryResults = await ExecuteSqlQuery(generatedSql);

                // Step 4: Format results
                var formattedResponse = await FormatResultsWithAi(question, queryResults);

                // Update chat session and record AI response as chat messages //
                var chatMessage = new ChatMessage
                {
                    ChatSessionId = chatSession.Id,
                    Content = System.Text.Json.JsonSerializer.Serialize(formattedResponse),
                    IsJsonContent = true,
                    ModelUsed = _aiModel,
                    MessageType = MESSAGETYPE_AI,
                    SentAt = DateTime.UtcNow,
                    PromptTokens = formattedResponse.usage.PromptTokens,
                    CompletionTokens = formattedResponse.usage.CompletionTokens,
                    TokensUsed = formattedResponse.usage.TokensUsed,
                    Data = System.Text.Json.JsonSerializer.Serialize(queryResults)
                };

                if (String.IsNullOrEmpty(message.ChatSessionId)) chatSession.SessionName = formattedResponse.sessionName;
                chatSession.LastActivityAt = DateTime.UtcNow;
                chatSession.ChatMessages.Add(chatMessage);
                _context.Entry(chatSession).State = EntityState.Modified;

                await _context.SaveChangesAsync();

                var result = new
                {
                    Analysis = formattedResponse,
                    Message = chatMessage,
                    Data = queryResults
                };

                return Ok(result);

            }
            catch (DbUpdateException)
            {
                throw;
                //return StatusCode((int)HttpStatusCode.BadRequest, "Database server error.");
            }
            catch (HttpRequestException)
            {
                return StatusCode((int)HttpStatusCode.BadGateway, "AI service is busy, Please try again later.");
            }
            catch (Exception)
            {
                //throw;
                return StatusCode((int)HttpStatusCode.InternalServerError, "AI service is busy, Please try again later.");
            }
        }


        private async Task<ChatSession> CreateNewChatSession(Message message)
        {
            var chatMessage = new ChatMessage
            {
                //ChatSessionId = chatSession.Id,
                Content = message.Content,
                IsJsonContent = false,
                ModelUsed = _aiModel,
                MessageType = MESSAGETYPE_USER,
                SentAt = DateTime.UtcNow,
                TokensUsed = 0,
                PromptTokens = 0,
                CompletionTokens = 0
            };

            var chatSession = new ChatSession
            {
                UserId = message.UserId,
                SessionName = message.Content.Length > 50 ? message.Content.Substring(0, 47) + "..." : message.Content,
                Status = STATUS_ACTIVE,
                CreatedAt = DateTime.UtcNow,  // Use UtcNow instead of new DateTime()
                LastActivityAt = DateTime.UtcNow,
                ChatMessages = { chatMessage }
            };

            _context.ChatSessions.Add(chatSession);
            await _context.SaveChangesAsync();

            return await _context.ChatSessions.FirstOrDefaultAsync(s => s.Id == chatSession.Id);
        }


        private async Task<List<Dictionary<string, object>>> ExecuteSqlQuery(string sql)
        {
            var result = new List<Dictionary<string, object>>();

            await using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = sql;
                await _context.Database.OpenConnectionAsync();

                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object>();
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            row.Add(reader.GetName(i), reader.IsDBNull(i) ? null : reader.GetValue(i));
                        }
                        result.Add(row);
                    }
                }
            }

            return result;
        }

        private async Task<GenerateQueryResult> GenerateSqlFromQuestion(string question, string dbSchema, int userConfigId)
        {
            // Use named client configured in Startup
            var client = _httpClientFactory.CreateClient("DeepSeekClient");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_deepSeekApiKey}");

            var prompt = $@"
            TOPIC: COMPANY SALES ANALYSIS

            Question: {question}

            IF QUESTION IS OFF TOPIC:
                Return a comprehensive answer to the question that is formated as stringified inner HTML code. 
                Use Tailwind CSS utility classes for styles.The outermost <div> container should have no padding and border.
                The HTML code should include formats and styling similar to DeepSeek chat response. 
                You may include image or other media type if applicable, to illustrate the response.

            IF QUESTION IS WITHIN TOPIC:
                Generate a MySQL compatible query to answer the question.
                The SQL query is limited to the provided database schema.
                The SQL query should not be a Create, Update or Delete statement.
                If the SQL query is supposed to include Create, Update or Delete statement, return ""SELECT 'Invalid request';"" instead.

            Database schema (View onlly):
            {dbSchema}
            
            If the generated SQL includes aggregate function such as MIN(), MAX(), COUNT(), SUM() or AVG(): 
                Make sure that the user question includes date period specification
                Examples are: This year, this quarter, last month, etc..
                If period cannot be derived from the question, return a message to the user to provide the requred period

            If period is provided, generate a MySQL compatible query to answer this question. 
                Very important note: If no period is provided, return empty string as sqlQuery.
                Otherwise if period is provided, Return ONLY the SQL query, no explanations.
                Strictly match the case from db schema object name
                SQL query general condition: UserConfigId = {userConfigId}
                Make sure that the number of rows return does not exceed 100.

            RESPONSE FORMAT (JSON ONLY):
                {{
                    ""offTopic"": ""answer to off topics question"",
                    ""sqlQuery"": ""generated sql query"",
                    ""message"": ""suggestion to provide period"",
                }}

            ";

            var requestBody = new
            {
                model = "deepseek-chat",
                messages = new[]
                {
                    new { role = "system", content = "If question is within topic, you are a SQL expert assistant and return only SQL. However, if question is off topic, act as a general AI assistant." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.3,
                response_format = new { type = "json_object" }
            };

            // Now using relative path since base address is configured
            var response = await client.PostAsync(
                "v1/chat/completions",
                new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var jsonDoc = await JsonDocument.ParseAsync(responseStream);
            /*
            var normalizedSql = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var usage = jsonDoc.RootElement.GetProperty("usage");

            // Remove Markdown code fences if present
            var cleanSql = Regex.Replace(normalizedSql, "```SQL", "", RegexOptions.IgnoreCase).Replace("```", "").Trim();

            return cleanSql;
            */

            var content = jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var ret = System.Text.Json.JsonSerializer.Deserialize<GenerateQueryResult>(content);
            ret.usage = jsonDoc.RootElement.GetProperty("usage").Deserialize<Usage>();

            return ret;

        }


        private bool IsQuerySafe(string cleanSql)
        {
            if (string.IsNullOrWhiteSpace(cleanSql))
                return false;

            // Only allow SELECT queries
            if (!cleanSql.StartsWith("SELECT"))
                return false;

            // Block dangerous keywords
            var forbiddenKeywords = new[]
            {
                "INSERT", "UPDATE", "DELETE", "DROP",
                "ALTER", "CREATE", "TRUNCATE", "EXEC",
                "EXECUTE", "DECLARE", "XP_", "SP_",
                "--", "/*", "*/", "WITH(NOLOCK)"
            };

            foreach (var keyword in forbiddenKeywords)
            {
                if (cleanSql.ToUpper().Contains(keyword))
                    return false;
            }

            return true;
        }


        private async Task<AIFormattedResponse> FormatResultsWithAi(string question, object queryResults)
        {
            var client = _httpClientFactory.CreateClient("DeepSeekClient");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_deepSeekApiKey}");

            string prompt = $@"
                ANALYSIS REQUEST
                ----------------
                Question: {question}

                DATA TO ANALYZE:
                {System.Text.Json.JsonSerializer.Serialize(queryResults)}

                RESPONSE REQUIREMENTS
                1. Provide 3-4 key insights as list
                2. Use icons or avatars for the insight list
                3. Generate COMPLETE ApexCharts configuration in stringified Javascript Object
                4. Use this exact structure for the chart config:
                    ```javascript
                    {{
                        chart: {{ type: 'bar' }},
                        series: [{{ name: 'Sales', data: [...] }}],
                        xaxis: {{ categories: [...] }},
                        // Include all necessary options except 
                    }}
                5. Write a 2-3 sentence summary
                6. Amount for sales, cost, rate, etc.. should be rounded to 2 decimal places. Currencly sysmbol is Php.

                RESPONSE FORMAT (JSON ONLY):
                {{
                    ""sessionName"": ""generated title"",
                    ""insights"": [""insight1"", ""insight2"", ""insight3"", ""insight4"", ...],
                    ""visualizationType"": ""chart_type"",
                    ""apexChartConfig"": ""ApexChart chart configuration"",
                    ""summary"": ""concise summary text""
                }}";


            var requestBody = new
            {
                model = "deepseek-chat",
                messages = new[]
                {
                    new { role = "system", content = "You are an analytics assistant that returns complete ApexCharts configurations." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.3,  // Lower for more consistent output
                response_format = new { type = "json_object" }
            };


            try
            {
                var response = await client.PostAsync(
                "v1/chat/completions",
                new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"));

                response.EnsureSuccessStatusCode();

                // Debug the raw response if needed
                var responseString = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"API Response: {responseString}");

                using var jsonDoc = JsonDocument.Parse(responseString);
                var content = jsonDoc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                var ret = System.Text.Json.JsonSerializer.Deserialize<AIFormattedResponse>(content);
                // Get the usage object directly
                ret.usage = jsonDoc.RootElement.GetProperty("usage").Deserialize<Usage>();

                return ret;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing response: {ex.Message}");
                /*
                return new AIFormattedResponse
                {
                    insights = (new[] { "Error occurred during analysis" }).ToList(),
                    visualizationType = "table",
                    summary = "Could not generate insights due to technical error"
                };
                */
                //return BadRequest(ex.Message);
                throw;
            }
        }


        private async Task<AIFormattedResponse> FormatResultsWithAi2(string question, object queryResults)
        {
            var client = _httpClientFactory.CreateClient("DeepSeekClient");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_deepSeekApiKey}");


            var prompt = $@"
                **Data Analysis Request**
                Question: {question}

                **Data to Visualize**:
                {System.Text.Json.JsonSerializer.Serialize(queryResults)}

                **Response Requirements**:
                1. Provide 3 key insights
                2. Recommend visualization type
                3. Generate COMPLETE ApexCharts configuration
                   - Must be valid JSON (not stringified)
                   - Include all required options
                   - Format like this example:

                {{
                  ""chart"": {{ ""type"": ""bar"" }},
                  ""series"": [{{ ""name"": ""Revenue"", ""data"": [100, 200, 300] }}],
                  ""xaxis"": {{ ""categories"": [""Q1"", ""Q2"", ""Q3""] }}
                }}

                **Important**:
                - Return the chart config as a JSON object (not string)
                - Escape only quotes within strings
                - Include responsive configuration";

            var requestBody = new
            {
                model = "deepseek-chat",
                messages = new[]
                {
                    new {
                        role = "system",
                        content = "You are an analytics assistant that returns properly formatted JSON with ApexCharts configuration as a direct object (not stringified)."
                    },
                    new { role = "user", content = prompt }
                },
                temperature = 0.3,
                response_format = new { type = "json_object" }
            };

            var response = await client.PostAsync(
                "v1/chat/completions",
                new StringContent(System.Text.Json.JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"));

            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(responseString);
            var contentElement = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content");

            // Check if content is already parsed (ValueKind = Object)
            if (contentElement.ValueKind == JsonValueKind.String)
            {
                // Parse the string content if needed
                var docString = contentElement.GetString();
                using var contentDoc = JsonDocument.Parse(docString);
                contentElement = contentDoc.RootElement;
            }

            var res = new AIFormattedResponse
            {
                insights = contentElement.GetProperty("insights")
                                       .EnumerateArray()
                                       .Select(x => x.GetString())
                                       .ToList(),
                visualizationType = contentElement.GetProperty("recommendation").GetString(),
                summary = "Analysis complete",
                apexChartConfig = contentElement.GetProperty("chartConfig").GetString()
            };

            return res;
        }

    }


    public class Message
    {
        public string ChatSessionId { get; set; }
        public string MessageType { get; set; }
        public string Content { get; set; }
        public int UserId { get; set; }
    }

    public class AIFormattedResponse
    {
        public string sessionName { get; set; }
        public List<string> insights { get; set; }
        public string visualizationType { get; set; }
        public string apexChartConfig { get; set; }
        public string summary { get; set; }
        public Usage usage { get; set; }
        public string suggestion { get; set; }
    }

    public class Usage
    {
        [JsonPropertyName("total_tokens")]
        public int TokensUsed { get; set; }

        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }

    public class GenerateQueryResult
    {
        public string offTopic { get; set; }
        public string sqlQuery { get; set; }
        public string message { get; set; }
        public Usage usage { get; set; }
    }

}