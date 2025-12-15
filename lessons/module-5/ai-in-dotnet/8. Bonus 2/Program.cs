using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Text;
using System.Text.RegularExpressions;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// === Настройка клиента ===
var endpoint = "http://localhost:1234/v1";
var client = new OpenAIClient(
    new ApiKeyCredential("not-needed"),
    new OpenAIClientOptions { Endpoint = new Uri(endpoint) }
);

var chatClient = client.GetChatClient("qwen/qwen3-4b-2507");

// === Создание агентов-переговорщиков ===
var buyerAgent = chatClient.CreateAIAgent(
    instructions: @"Ты профессиональный закупщик (Buyer) корпоративного ПО. Твоя задача:

БЮДЖЕТ: $5000 (максимум, но никому не говори точную цифру)
ЦЕЛЕВАЯ ЦЕНА: $3500-$4000 (идеальный диапазон)

СТРАТЕГИЯ ПЕРЕГОВОРОВ:
1. Начни с низкой цены ($3000-$3200)
2. Постепенно повышай предложение, но маленькими шагами ($200-$300)
3. Используй аргументы: конкуренты, бюджет, долгосрочное партнёрство
4. Если продавец близко к $4200 - соглашайся
5. Если цена выше $5000 - отказывайся и предлагай альтернативы

ФОРМАТ ОТВЕТА:
OFFER: $XXXX
COMMENT: [твоё обоснование в 1-2 предложениях]
ACTION: [CONTINUE / ACCEPT / REJECT]

ВАЖНО: 
- Веди себя как опытный переговорщик
- Не раскрывай свой максимум сразу
- Ищи компромисс, но защищай интересы компании",
    name: "Buyer"
);

var sellerAgent = chatClient.CreateAIAgent(
    instructions: @"Ты менеджер по продажам (Seller) корпоративного ПО. Твоя задача:

МИНИМАЛЬНАЯ ЦЕНА: $4000 (ниже не можешь, потеряешь прибыль)
ЖЕЛАЕМАЯ ЦЕНА: $5500-$6000 (цель продаж)
НАЧАЛЬНАЯ ЦЕНА: $6500 (начни с этого)

СТРАТЕГИЯ ПЕРЕГОВОРОВ:
1. Начни с высокой цены, но покажи ценность продукта
2. Делай уступки медленно ($300-$500 за раунд)
3. Используй аргументы: качество, поддержка, уникальность, ROI
4. Если покупатель предлагает $4500+ - серьёзно рассмотри
5. Не опускайся ниже $4000 - лучше отказаться

ФОРМАТ ОТВЕТА:
OFFER: $XXXX
COMMENT: [твоё обоснование в 1-2 предложениях]
ACTION: [CONTINUE / ACCEPT / REJECT]

ВАЖНО:
- Подчёркивай ценность, не только цену
- Создавай срочность (ограниченное предложение, другие клиенты)
- Будь настойчив, но профессионален",
    name: "Seller"
);

// === Функция для извлечения предложения из ответа агента ===
(decimal? offer, string comment, string action) ParseAgentResponse(string response)
{
    decimal? offer = null;
    string comment = "";
    string action = "CONTINUE";

    // Извлекаем цену
    var offerMatch = Regex.Match(response, @"OFFER:\s*\$?(\d{1,3}(?:,?\d{3})*(?:\.\d{2})?)", RegexOptions.IgnoreCase);
    if (offerMatch.Success)
    {
        var priceStr = offerMatch.Groups[1].Value.Replace(",", "");
        decimal.TryParse(priceStr, out decimal price);
        offer = price;
    }

    // Если не нашли через OFFER:, ищем любое число похожее на цену
    if (offer == null)
    {
        var priceMatches = Regex.Matches(response, @"\$?(\d{1,3}(?:,?\d{3})*(?:\.\d{2})?)");
        foreach (Match match in priceMatches)
        {
            var priceStr = match.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(priceStr, out decimal price) && price >= 1000 && price <= 10000)
            {
                offer = price;
                break;
            }
        }
    }

    // Извлекаем комментарий
    var commentMatch = Regex.Match(response, @"COMMENT:\s*(.+?)(?=ACTION:|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    if (commentMatch.Success)
        comment = commentMatch.Groups[1].Value.Trim();

    // Извлекаем действие
    if (response.Contains("ACCEPT", StringComparison.OrdinalIgnoreCase) ||
        response.Contains("AGREE", StringComparison.OrdinalIgnoreCase) ||
        response.Contains("DEAL", StringComparison.OrdinalIgnoreCase))
        action = "ACCEPT";
    else if (response.Contains("REJECT", StringComparison.OrdinalIgnoreCase) ||
             response.Contains("DECLINE", StringComparison.OrdinalIgnoreCase))
        action = "REJECT";

    return (offer, comment, action);
}

// === Функция для выполнения хода агента ===
async Task<string> ExecuteAgentTurn(
    AIAgent agent,
    string prompt,
    AgentThread thread,
    string emoji)
{
    Console.Write($"\n{emoji} [{agent.Name}] обдумывает предложение");

    var response = new StringBuilder();
    var dotCount = 0;

    var responseTask = Task.Run(async () =>
    {
        await foreach (var chunk in agent.RunStreamingAsync(prompt, thread))
        {
            response.Append(chunk.Text);
        }
    });

    // Анимация ожидания
    while (!responseTask.IsCompleted)
    {
        Console.Write(".");
        dotCount++;
        if (dotCount > 3)
        {
            Console.Write("\r" + new string(' ', 80) + "\r");
            Console.Write($"{emoji} [{agent.Name}] обдумывает предложение");
            dotCount = 0;
        }
        await Task.Delay(300);
    }

    Console.WriteLine("\r" + new string(' ', 80) + "\r");
    return response.ToString();
}

// === Основной воркфлоу переговоров ===
Console.WriteLine("╔════════════════════════════════════════════════════╗");
Console.WriteLine("║   🤝 Agent-to-Agent: Корпоративные переговоры     ║");
Console.WriteLine("╚════════════════════════════════════════════════════╝\n");

Console.WriteLine("📋 Сценарий: Покупка корпоративного ПО");
Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

var state = new NegotiationState();
var sharedThread = buyerAgent.GetNewThread();

const int MAX_ROUNDS = 8;

try
{
    // === Начальное предложение от продавца ===
    Console.WriteLine("🎬 Продавец открывает переговоры...\n");

    var sellerOpening = await ExecuteAgentTurn(
        sellerAgent,
        @"Ты встречаешься с потенциальным покупателем корпоративного ПО. 
Представь продукт и назови начальную цену. Используй формат с OFFER, COMMENT, ACTION.",
        sharedThread,
        "💼"
    );

    Console.WriteLine($"💼 [Seller]:\n{sellerOpening}\n");

    var (sellerOffer, sellerComment, sellerAction) = ParseAgentResponse(sellerOpening);
    if (sellerOffer.HasValue)
    {
        state.CurrentOffer = sellerOffer.Value;
        state.LogOffer("Seller", sellerOffer.Value, sellerComment);
    }

    // === Раунды переговоров ===
    var currentAgent = buyerAgent;
    var currentEmoji = "🛒";
    var otherAgent = sellerAgent;
    var otherEmoji = "💼";

    while (state.Round < MAX_ROUNDS && !state.DealClosed)
    {
        Console.WriteLine($"\n{'━'} Раунд {state.Round + 1} {'━'}\n");

        var contextPrompt = state.Round == 0
            ? $"Продавец предложил ${state.CurrentOffer:N0}. Сделай своё контрпредложение."
            : "Продолжай переговоры на основе последнего предложения оппонента.";

        var response = await ExecuteAgentTurn(
            currentAgent,
            contextPrompt,
            sharedThread,
            currentEmoji
        );

        Console.WriteLine($"{currentEmoji} [{currentAgent.Name}]:\n{response}\n");

        var (offer, comment, action) = ParseAgentResponse(response);

        if (offer.HasValue)
        {
            state.LogOffer(currentAgent.Name, offer.Value, comment);
            state.CounterOffer = offer.Value;
        }

        // Проверка на завершение
        if (action == "ACCEPT")
        {
            state.DealClosed = true;
            state.FinalPrice = offer?.ToString("N0") ?? state.CurrentOffer?.ToString("N0") ?? "Unknown";
            Console.WriteLine($"\n✅ {currentAgent.Name} принимает предложение!");
            break;
        }
        else if (action == "REJECT")
        {
            state.DealClosed = true;
            Console.WriteLine($"\n❌ {currentAgent.Name} отказывается от сделки!");
            break;
        }

        // Проверка на сближение позиций (автоматическое завершение)
        if (state.CurrentOffer.HasValue && state.CounterOffer.HasValue)
        {
            var difference = Math.Abs(state.CurrentOffer.Value - state.CounterOffer.Value);
            if (difference <= 100)
            {
                state.DealClosed = true;
                state.FinalPrice = ((state.CurrentOffer.Value + state.CounterOffer.Value) / 2).ToString("N0");
                Console.WriteLine($"\n🎯 Позиции сблизились! Компромисс: ${state.FinalPrice}");
                break;
            }

            state.CurrentOffer = state.CounterOffer;
        }

        // Переключение агентов
        (currentAgent, otherAgent) = (otherAgent, currentAgent);
        (currentEmoji, otherEmoji) = (otherEmoji, currentEmoji);

    }

    // === Результаты ===
    Console.WriteLine("\n╔════════════════════════════════════════════════════╗");
    Console.WriteLine("║                  📊 ИТОГИ ПЕРЕГОВОРОВ             ║");
    Console.WriteLine("╚════════════════════════════════════════════════════╝\n");

    Console.WriteLine($"Раундов проведено: {state.Round}");
    Console.WriteLine($"Статус: {(state.DealClosed ? "✅ Сделка закрыта" : "⏸️ Переговоры продолжаются")}\n");

    if (state.DealClosed && state.FinalPrice != null)
    {
        Console.WriteLine($"💰 Финальная цена: ${state.FinalPrice}\n");
    }

    Console.WriteLine("📜 История переговоров:");
    Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    foreach (var entry in state.History)
    {
        Console.WriteLine($"  {entry}");
    }

    // === Анализ эффективности ===
    if (state.History.Count >= 2)
    {
        Console.WriteLine("\n🎯 Анализ переговоров:");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        var firstBuyerOffer = state.History
            .FirstOrDefault(h => h.Contains("Buyer"))
            ?.Split('$')[1]?.Split(' ')[0]?.Replace(",", "");

        var lastSellerOffer = state.History
            .LastOrDefault(h => h.Contains("Seller"))
            ?.Split('$')[1]?.Split(' ')[0]?.Replace(",", "");

        if (decimal.TryParse(firstBuyerOffer, out decimal firstBuyer) &&
            decimal.TryParse(lastSellerOffer, out decimal lastSeller) &&
            state.FinalPrice != null &&
            decimal.TryParse(state.FinalPrice.Replace(",", ""), out decimal final))
        {
            var buyerSavings = lastSeller - final;
            var sellerProfit = final - 4000; // Минимум продавца

            Console.WriteLine($"  🛒 Покупатель сэкономил: ${buyerSavings:N0}");
            Console.WriteLine($"  💼 Продавец заработал: ${sellerProfit:N0} выше минимума");
            Console.WriteLine($"  📈 Диапазон переговоров: ${firstBuyer:N0} - ${lastSeller:N0}");

            var winWin = buyerSavings > 0 && sellerProfit > 0;
            Console.WriteLine($"\n  {(winWin ? "🤝 Win-Win сделка!" : "⚖️ Компромисс достигнут")}");
        }
    }

    Console.WriteLine("\n" + new string('═', 52));
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Ошибка: {ex.Message}");
}

Console.WriteLine("\n💡 Нажмите Enter для выхода...");
Console.ReadLine();
