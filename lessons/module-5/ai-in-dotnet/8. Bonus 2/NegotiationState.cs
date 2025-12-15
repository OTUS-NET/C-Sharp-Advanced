
// === Класс для отслеживания состояния переговоров ===
class NegotiationState
{
    public decimal? CurrentOffer { get; set; }
    public decimal? CounterOffer { get; set; }
    public int Round { get; set; } = 0;
    public bool DealClosed { get; set; } = false;
    public string? FinalPrice { get; set; }
    public List<string> History { get; set; } = new();

    public void LogOffer(string agent, decimal price, string comment = "")
    {
        Round++;
        var log = $"Раунд {Round} - {agent}: ${price:N0}";
        if (!string.IsNullOrEmpty(comment))
            log += $" ({comment})";
        History.Add(log);
    }
}
