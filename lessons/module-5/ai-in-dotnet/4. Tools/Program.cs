using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Подключение к LM Studio
var endpoint = "http://localhost:1234/v1";
var client = new OpenAIClient(
    new ApiKeyCredential("not-needed"),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
);

var chatClient = client.GetChatClient("qwen/qwen3-4b-2507");

// Создание агента
var agent = chatClient.CreateAIAgent(
    instructions: "Ты полезный помощник для разработчиков C#",
    name: "DevAssistant"
);

// Создаём thread для хранения истории
var thread = agent.GetNewThread();

// === Tools ===
var tools = new List<AIFunction>
{
    AIFunctionFactory.Create(GetCurrentTime, name: "current_time"),
    AIFunctionFactory.Create(GetWeather, name: "get_weather"),
    AIFunctionFactory.Create((int a, int b) => a + b, name: "add")
}.Cast<AITool>().ToList();

// === Диалог ===
while (true)
{
    Console.Write("\nВы: ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input)) break;

    var runOptions = new ChatClientAgentRunOptions
    {
        ChatOptions = new ChatOptions
        {
            Tools = tools,
        }
    };

    var responseStream = agent.RunStreamingAsync(
        input,
        thread,
        runOptions
    );

    await foreach (var response in responseStream)
    {
        Console.Write(response.Text);
    }
}

string GetCurrentTime() => DateTime.Now.ToString("HH:mm:ss");
string GetWeather(string city) => $"В {city} сейчас -10°C";