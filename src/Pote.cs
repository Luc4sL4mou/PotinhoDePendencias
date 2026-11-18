using System.Text.Json;
using System.Text.Json.Serialization;

namespace Potinho;

public class Tarefa
{
    public string Texto { get; set; } = "";
    public int Xp { get; set; }
    public DateTime Criada { get; set; } = DateTime.Now;
    public DateTime? Concluida { get; set; }

    public override string ToString() =>
        Concluida is { } d ? $"{d:dd/MM/yyyy}   +{Xp} XP   {Texto}" : $"[{Xp} XP]   {Texto}";
}

public class Pote
{
    public static readonly string CaminhoPadrao = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PotinhoDePendencias", "dados.json");

    public List<Tarefa> Pendentes { get; set; } = new();
    public List<Tarefa> Arquivo { get; set; } = new();

    [JsonIgnore] public int XpTotal => Arquivo.Sum(t => t.Xp);

    // Subir do nível N para o N+1 custa 100*N XP (1→2: 100, 2→3: 200, ...).
    public static (int nivel, int xpNoNivel, int xpParaProximo) Nivel(int xp)
    {
        int nivel = 1;
        while (xp >= 100 * nivel) { xp -= 100 * nivel; nivel++; }
        return (nivel, xp, 100 * nivel);
    }

    public Tarefa? Sortear(Random rng) => Pendentes.Count == 0 ? null : Pendentes[rng.Next(Pendentes.Count)];

    public void Concluir(Tarefa t)
    {
        if (!Pendentes.Remove(t)) return;
        t.Concluida = DateTime.Now;
        Arquivo.Insert(0, t);
    }

    public static Pote Carregar(string caminho) =>
        File.Exists(caminho) ? JsonSerializer.Deserialize<Pote>(File.ReadAllText(caminho)) ?? new() : new();

    // Grava num .tmp e troca, para não corromper o arquivo se o app cair no meio.
    public void Salvar(string caminho)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);
        var tmp = caminho + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, caminho, overwrite: true);
    }
}
