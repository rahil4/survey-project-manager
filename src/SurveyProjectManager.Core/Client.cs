namespace SurveyProjectManager.Core;

/// <summary>متقاضی: کسی که سفارش می‌دهد (ممکن است مالک ملک نباشد).</summary>
public sealed class Client
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "حقیقی";
    public string Phone { get; set; } = "";
    public string Note { get; set; } = "";

    public static readonly IReadOnlyList<string> Kinds = new[] { "حقیقی", "حقوقی", "نماینده" };
}

public static class ClientStore
{
    public const string CsvName = "clients.csv";
    private static readonly string[] Header = { "id", "name", "kind", "phone", "note" };

    public static string CsvPath(string root) => Path.Combine(root, CsvName);

    public static List<Client> Load(string root) =>
        Csv.ReadTable(CsvPath(root)).Select(r => new Client
        {
            Id = r.GetValueOrDefault("id", ""), Name = r.GetValueOrDefault("name", ""),
            Kind = r.GetValueOrDefault("kind", "حقیقی"), Phone = r.GetValueOrDefault("phone", ""),
            Note = r.GetValueOrDefault("note", ""),
        }).Where(c => c.Id.Length > 0).ToList();

    public static void Save(string root, IEnumerable<Client> clients) =>
        Csv.WriteTable(CsvPath(root), Header, clients.Select(c => new[] { c.Id, c.Name, c.Kind, c.Phone, c.Note }));

    public static string NextId(IEnumerable<Client> clients)
    {
        var max = 0;
        foreach (var c in clients)
            if (c.Id.StartsWith('c') && int.TryParse(c.Id[1..], out var n)) max = Math.Max(max, n);
        return "c" + (max + 1);
    }
}

/// <summary>پیشنهاد متقاضی هنگام تایپ نام و تشخیص نام‌های تکراری.</summary>
public static class ClientMatcher
{
    public static List<Client> Suggest(IEnumerable<Client> clients, string text, int max = 4)
    {
        var q = TextNorm.StripHonorific(TextNorm.Normalize(text));
        if (q.Length == 0) return new List<Client>();
        var qTokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 2).ToArray();
        var scored = new List<(Client C, int Score)>();
        foreach (var c in clients)
        {
            var n = TextNorm.StripHonorific(TextNorm.Normalize(c.Name));
            int score = 0;
            if (n == q) score = 4;
            else if (n.Contains(q, StringComparison.Ordinal)) score = 3;
            else if (q.Contains(n, StringComparison.Ordinal) && n.Length > 2) score = 2;
            else if (qTokens.Any(t => n.Contains(t, StringComparison.Ordinal))) score = 1;
            if (score > 0) scored.Add((c, score));
        }
        return scored.OrderByDescending(x => x.Score).ThenBy(x => x.C.Name, StringComparer.Ordinal).Take(max).Select(x => x.C).ToList();
    }

    public static Client? FindExact(IEnumerable<Client> clients, string name)
    {
        var q = TextNorm.StripHonorific(TextNorm.Normalize(name));
        return q.Length == 0 ? null : clients.FirstOrDefault(c => TextNorm.StripHonorific(TextNorm.Normalize(c.Name)) == q);
    }
}
