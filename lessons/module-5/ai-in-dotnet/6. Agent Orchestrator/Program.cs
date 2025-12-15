using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// === Настройка клиента ===
var endpoint = "http://localhost:1234/v1";
var client = new OpenAIClient(
    new ApiKeyCredential("not-needed"),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
);

var chatClient = client.GetChatClient("qwen/qwen3-4b-2507");

// === Создание специализированных агентов ===
var analystAgent = chatClient.CreateAIAgent(
    instructions: @"Ты агент-аналитик кода. Анализируй C# код кратко и возвращай структуру:
- Назначение кода
- Основные классы
- Публичные методы
- Используемые библиотеки",
    name: "CodeAnalyst"
);

var writerAgent = chatClient.CreateAIAgent(
    instructions: @"Ты агент-писатель документации. Создавай краткий README.md в Markdown формате.
Включай секции: Overview, Features, Usage.",
    name: "DocWriter"
);

var reviewerAgent = chatClient.CreateAIAgent(
    instructions: @"Ты агент-ревьюер. Проверяй документацию кратко и возвращай:
- Оценку (1-10)
- 2-3 конкретных улучшения",
    name: "DocReviewer"
);

// === Обёртка агентов как async функций-инструментов со streaming ===
async Task<string> AnalyzeCode(string codeContent)
{
    Console.WriteLine("\n\n🔍 [TOOL EXECUTION] Запуск агента-аналитика...");
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("─".PadRight(80, '─'));
    Console.ResetColor();

    var thread = analystAgent.GetNewThread();
    var result = new StringBuilder();

    await foreach (var chunk in analystAgent.RunStreamingAsync(
        $"Проанализируй этот код кратко:\n {codeContent}",
        thread))
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(chunk.Text);
        Console.ResetColor();
        result.Append(chunk.Text);
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n" + "─".PadRight(80, '─'));
    Console.ResetColor();
    Console.WriteLine("✅ [TOOL COMPLETED] Анализ завершён\n");

    return result.ToString();
}

async Task<string> WriteDocumentation(string analysis, string code)
{
    Console.WriteLine("\n\n📝 [TOOL EXECUTION] Запуск агента-писателя...");
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("─".PadRight(80, '─'));
    Console.ResetColor();

    var thread = writerAgent.GetNewThread();
    var result = new StringBuilder();

    await foreach (var chunk in writerAgent.RunStreamingAsync(
        $"Создай краткий README на основе анализа:\n\nАНАЛИЗ:\n{analysis}",
        thread))
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(chunk.Text);
        Console.ResetColor();
        result.Append(chunk.Text);
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("\n" + "─".PadRight(80, '─'));
    Console.ResetColor();
    Console.WriteLine("✅ [TOOL COMPLETED] Документация создана\n");

    return result.ToString();
}

async Task<string> ReviewDocumentation(string documentation, string analysis)
{
    Console.WriteLine("\n\n✅ [TOOL EXECUTION] Запуск агента-ревьюера...");
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine("─".PadRight(80, '─'));
    Console.ResetColor();

    var thread = reviewerAgent.GetNewThread();
    var result = new StringBuilder();

    await foreach (var chunk in reviewerAgent.RunStreamingAsync(
        $"Проверь документацию кратко:\n\nДОКУМЕНТАЦИЯ:\n{documentation}",
        thread))
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write(chunk.Text);
        Console.ResetColor();
        result.Append(chunk.Text);
    }

    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine("\n" + "─".PadRight(80, '─'));
    Console.ResetColor();
    Console.WriteLine("✅ [TOOL COMPLETED] Ревью завершено\n");

    return result.ToString();
}

string ReadFile(string filePath)
{
    Console.WriteLine($"\n📂 [TOOL EXECUTION] Чтение файла: {filePath}");
    Console.ForegroundColor = ConsoleColor.Gray;

    var content = File.Exists(filePath)
        ? File.ReadAllText(filePath)
        : $"Файл {filePath} не найден";

    // Показываем превью первых 200 символов
    var preview = content.Length > 200
        ? content.Substring(0, 200) + "..."
        : content;
    Console.WriteLine($"\nПревью:\n{preview}\n");
    Console.ResetColor();

    Console.WriteLine($"✅ [TOOL COMPLETED] Прочитано {content.Length} символов\n");
    return content;
}

string SaveFile(string content, string filePath)
{
    Console.WriteLine($"\n💾 [TOOL EXECUTION] Сохранение в файл: {filePath}");
    Console.ForegroundColor = ConsoleColor.Gray;

    File.WriteAllText(filePath, content);
    Console.WriteLine($"Сохранено {content.Length} символов");
    Console.ResetColor();

    Console.WriteLine($"✅ [TOOL COMPLETED] Файл сохранён: {filePath}\n");
    return $"Успешно сохранено в {filePath}";
}

// === Главный агент-координатор ===
var orchestratorAgent = chatClient.CreateAIAgent(
    instructions: @"Ты главный координатор процесса документирования. 
ОБЯЗАТЕЛЬНО выполни ВСЕ шаги последовательно:

1. read_file - прочитай код
2. analyze_code - проанализируй прочитанный код (передай содержимое файла)
3. write_documentation - создай документацию (передай результат анализа и код)
4. review_documentation - проверь документацию (передай документацию и анализ)
5. save_file - сохрани финальную документацию (передай документацию и путь)

После каждого шага кратко комментируй (1-2 предложения) что сделано и что делаешь дальше.
Не останавливайся пока не выполнишь все 5 шагов!",
    name: "OrchestratorAgent"
);

// === Регистрация всех функций как инструментов ===
var tools = new List<AIFunction>
{
    AIFunctionFactory.Create(ReadFile, name: "read_file",
        description: "Читает содержимое файла по указанному пути"),
    AIFunctionFactory.Create(AnalyzeCode, name: "analyze_code",
        description: "Анализирует C# код и выделяет компоненты, принимает codeContent"),
    AIFunctionFactory.Create(WriteDocumentation, name: "write_documentation",
        description: "Создаёт README.md, принимает analysis и code"),
    AIFunctionFactory.Create(ReviewDocumentation, name: "review_documentation",
        description: "Проверяет качество документации, принимает documentation и analysis"),
    AIFunctionFactory.Create(SaveFile, name: "save_file",
        description: "Сохраняет content в файл filePath")
}.Cast<AITool>().ToList();

// === Запуск оркестрации ===
Console.WriteLine("=== Agent-as-Tools: Генерация документации ===\n");
Console.Write("Введите путь к файлу C# (Default: Program.cs): ");
var filePath = Console.ReadLine();
filePath = string.IsNullOrEmpty(filePath) ? "../../../Program.cs" : filePath;

var orchestratorThread = orchestratorAgent.GetNewThread();

var runOptions = new ChatClientAgentRunOptions
{
    ChatOptions = new ChatOptions
    {
        Tools = tools,
        ToolMode = ChatToolMode.Auto
    }
};

var outputPath = Path.ChangeExtension(filePath, ".README.md");
var userRequest = $@"Создай полную техническую документацию для файла '{filePath}'.
Сохрани результат в '{outputPath}'.
Выполни ОБЯЗАТЕЛЬНО все 5 шагов последовательно!";

Console.WriteLine("\n🤖 Главный агент-оркестратор начинает работу...\n");
Console.WriteLine("=".PadRight(80, '='));

// === Обработка streaming с отображением размышлений оркестратора ===
var orchestratorThoughts = new StringBuilder();

await foreach (var update in orchestratorAgent.RunStreamingAsync(
    userRequest,
    orchestratorThread,
    runOptions))
{
    // Показываем размышления оркестратора между вызовами тулов
    if (!string.IsNullOrEmpty(update.Text))
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write(update.Text);
        Console.ResetColor();
        orchestratorThoughts.Append(update.Text);
    }
}

Console.WriteLine("\n" + "=".PadRight(80, '='));
Console.WriteLine("\n✨ Процесс завершён!");
Console.WriteLine($"\n📊 Комментарии оркестратора:\n{orchestratorThoughts}");
