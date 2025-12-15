using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var endpoint = "http://localhost:1234/v1";
var client = new OpenAIClient(
    new ApiKeyCredential("not-needed"),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
);

var chatClient = client.GetChatClient("qwen/qwen3-4b-2507");

// === Создание игровых агентов ===
var playerX = chatClient.CreateAIAgent(
    instructions: @"Ты играешь в крестики-нолики за X. Правила:
- Доска 3x3, позиции пронумерованы 1-9 (слева направо, сверху вниз)
- Твоя цель: собрать 3 X в ряд (горизонталь, вертикаль, диагональ)
- ВСЕГДА отвечай ТОЛЬКО числом от 1 до 9 на свободную позицию
- Анализируй доску: сначала ищи возможность выиграть, потом блокируй O
- НЕ пиши объяснений, только номер хода",
    name: "PlayerX"
);

var playerO = chatClient.CreateAIAgent(
    instructions: @"Ты играешь в крестики-нолики за O. Правила:
- Доска 3x3, позиции пронумерованы 1-9 (слева направо, сверху вниз)
- Твоя цель: собрать 3 O в ряд (горизонталь, вертикаль, диагональ)
- ВСЕГДА отвечай ТОЛЬКО числом от 1 до 9 на свободную позицию
- Анализируй доску: сначала ищи возможность выиграть, потом блокируй X
- НЕ пиши объяснений, только номер хода",
    name: "PlayerO"
);


// === Запуск игры ===

Console.WriteLine("=== 🎮 Крестики-Нолики: Agent vs Agent ===\n");
Console.WriteLine("Демонстрация agent-to-agent взаимодействия через shared game state\n");

var game = new TicTacToeGame();
var threadX = playerX.GetNewThread();
var threadO = playerO.GetNewThread();

var currentPlayer = playerX;
var currentThread = threadX;
var currentSymbol = 'X';
var moveCount = 0;

while (game.CheckWinner() == null && moveCount < 9)
{
    Console.WriteLine($"\n{'='} Ход #{++moveCount} - Игрок {currentSymbol} {'='}'");
    Console.WriteLine(game.GetBoardState());

    var availableMoves = game.GetAvailableMoves();
    Console.WriteLine($"Доступные позиции: {string.Join(", ", availableMoves)}");

    Console.Write($"\n{currentPlayer.Name} думает... ");
    var move = await GetAgentMove(currentPlayer, game.GetBoardState(), currentThread);

    // Валидация хода
    int attempts = 0;
    while (!game.MakeMove(move, currentSymbol) && attempts < 3)
    {
        Console.Write($"❌ Некорректный ход ({move}), повтор... ");
        move = await GetAgentMove(
            currentPlayer,
            game.GetBoardState() + $"\nВНИМАНИЕ: Ход {move} недоступен! Выбери из: {string.Join(",", availableMoves)}",
            currentThread
        );
        attempts++;
    }

    if (attempts >= 3)
    {
        // Запасной вариант - случайный ход
        move = availableMoves[Random.Shared.Next(availableMoves.Count)];
        game.MakeMove(move, currentSymbol);
        Console.WriteLine($"⚠️ Выбран случайный ход: {move}");
    }
    else
    {
        Console.WriteLine($"✅ Ход: {move}");
    }

    // Переключение игрока
    if (currentPlayer == playerX)
    {
        currentPlayer = playerO;
        currentThread = threadO;
        currentSymbol = 'O';
    }
    else
    {
        currentPlayer = playerX;
        currentThread = threadX;
        currentSymbol = 'X';
    }

    await Task.Delay(800); // Пауза для читаемости
}

// === Результат ===
Console.WriteLine("\n" + new string('=', 50));
Console.WriteLine(game.GetBoardState());

var winner = game.CheckWinner();
if (winner == 'D')
    Console.WriteLine("🤝 Ничья! Оба агента сыграли хорошо.");
else
    Console.WriteLine($"🏆 Победитель: Игрок {winner}!");

Console.WriteLine($"\n📊 Всего ходов: {moveCount}");

async Task<int> GetAgentMove(AIAgent agent, string boardState, AgentThread thread)
{
    var prompt = $@"Текущая доска:
{boardState}

Твой ход (введи число 1-9):";

    var response = new StringBuilder();
    await foreach (var chunk in agent.RunStreamingAsync(prompt, thread))
    {
        response.Append(chunk.Text);
    }

    var moveText = new string(response.ToString().Where(char.IsDigit).ToArray());
    return int.TryParse(moveText.Length > 0 ? moveText[0].ToString() : "0", out int move)
        ? move : 0;
}
