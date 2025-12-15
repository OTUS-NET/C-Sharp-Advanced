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
    instructions: @"Ты агент-аналитик кода. Твоя задача:
1. Прочитать предоставленный C# код
2. Выделить основные компоненты: классы, методы, зависимости
3. Определить назначение кода и его архитектуру
4. Вернуть структурированный анализ в формате JSON с полями:
   - purpose: назначение кода
   - mainClasses: список основных классов
   - publicMethods: список публичных методов с описанием
   - dependencies: используемые библиотеки
   - patterns: обнаруженные паттерны проектирования",
    name: "CodeAnalyst"
);

var writerAgent = chatClient.CreateAIAgent(
    instructions: @"Ты агент-писатель технической документации. Твоя задача:
1. На основе анализа кода создать профессиональный README.md
2. Включить секции: Overview, Features, Usage, API Reference
3. Добавить примеры использования кода
4. Использовать Markdown форматирование
5. Писать понятно для разработчиков разного уровня",
    name: "DocWriter"
);

var reviewerAgent = chatClient.CreateAIAgent(
    instructions: @"Ты агент-ревьюер документации. Твоя задача:
1. Проверить документацию на полноту (все ли компоненты описаны)
2. Проверить техническую точность описаний
3. Оценить качество примеров кода
4. Проверить структуру и читаемость
5. Вернуть оценку (1-10) и список конкретных улучшений",
    name: "DocReviewer"
);

// === Тулы для работы с файлами ===
var tools = new List<AIFunction>
{
    AIFunctionFactory.Create(ReadFile, name: "read_file", description: "Читает содержимое файла"),
    AIFunctionFactory.Create(WriteFile, name: "write_file", description: "Сохраняет данные в файл")
}.Cast<AITool>().ToList();

// === Основной воркфлоу ===
Console.WriteLine("=== Мультиагентная генерация документации ===\n");
Console.Write("Введите путь к файлу C# для документирования (Default: Program.cs): ");
var filePath = Console.ReadLine();
filePath = string.IsNullOrEmpty(filePath) ? "../../../Program.cs" : filePath;

try
{
    var codeContent = File.ReadAllText(filePath);

    // === Этап 1: Анализ кода ===
    Console.WriteLine("\n🔍 Этап 1: Анализ кода (Агент-аналитик)...");
    var analystThread = analystAgent.GetNewThread();

    var analysisPrompt = $@"Проанализируй следующий C# код и верни структурированный анализ:

{codeContent}";

    var analysisResult = new StringBuilder();
    await foreach (var chunk in analystAgent.RunStreamingAsync(analysisPrompt, analystThread))
    {
        Console.Write(chunk.Text);
        analysisResult.Append(chunk.Text);
    }
    
    var analysis = analysisResult.ToString();

    // === Этап 2: Написание документации ===
    Console.WriteLine("\n\n📝 Этап 2: Создание документации (Агент-писатель)...");
    var writerThread = writerAgent.GetNewThread();

    var writerPrompt = $@"На основе следующего анализа кода создай полный README.md:

АНАЛИЗ:
{analysis}

ИСХОДНЫЙ КОД:
{codeContent}";

    var documentationResult = new StringBuilder();
    await foreach (var chunk in writerAgent.RunStreamingAsync(writerPrompt, writerThread))
    {
        Console.Write(chunk.Text);
        documentationResult.Append(chunk.Text);
    }

    var documentation = documentationResult.ToString();

    // === Этап 3: Ревью документации ===
    Console.WriteLine("\n\n✅ Этап 3: Проверка документации (Агент-ревьюер)...");
    var reviewerThread = reviewerAgent.GetNewThread();

    var reviewPrompt = $@"Проверь качество следующей документации:

ДОКУМЕНТАЦИЯ:
{documentation}

ИСХОДНЫЙ АНАЛИЗ КОДА:
{analysis}

ИСХОДНЫЙ КОД:
{codeContent}

Оцени полноту, точность и качество. Предложи конкретные улучшения.";

    var reviewResult = new StringBuilder();
    var reviewerStream = reviewerAgent.RunStreamingAsync(reviewPrompt, reviewerThread);
    await foreach (var chunk in reviewerStream)
    {
        Console.Write(chunk.Text);
        reviewResult.Append(chunk.Text);
    }

    // === Сохранение результата ===
    Console.WriteLine("\n\n💾 Сохранение документации...");
    var outputPath = Path.ChangeExtension(filePath, ".README.md");
    File.WriteAllText(outputPath, documentation);

    Console.WriteLine($"\n✨ Готово! Документация сохранена в: {outputPath}");
    Console.WriteLine($"📊 Результаты ревью доступны выше");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Ошибка: {ex.Message}");
}

// === Функции тулов ===
string ReadFile(string path)
{
    if (!File.Exists(path))
        return $"Файл {path} не найден";

    return File.ReadAllText(path);
}

string WriteFile(string content, string outputPath)
{
    File.WriteAllText(outputPath, content);
    return $"Файл сохранен в {outputPath}";
}
