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

// === Диалог ===
while (true)
{
    Console.Write("\nВы: ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input) || input == "exit") break;

    var response = await agent.RunAsync(
        input
    );

    Console.WriteLine($"AI: {response.Text}");
}