namespace Potinho;

// Rodar: PotinhoDePendencias.exe --teste  (código de saída 0 = tudo passou; falhas vão para o stderr)
static class Testes
{
    public static int Rodar()
    {
        try
        {
            Checar(Pote.Nivel(0) == (1, 0, 100), "0 XP = nível 1");
            Checar(Pote.Nivel(99) == (1, 99, 100), "99 XP = nível 1");
            Checar(Pote.Nivel(100) == (2, 0, 200), "100 XP = nível 2");
            Checar(Pote.Nivel(350) == (3, 50, 300), "350 XP = nível 3 com 50");

            var pote = new Pote();
            Checar(pote.Sortear(new Random(1)) == null, "pote vazio não sorteia");
            var a = new Tarefa { Texto = "A", Xp = 30 };
            var b = new Tarefa { Texto = "B", Xp = 80 };
            pote.Pendentes.AddRange([a, b]);
            for (int i = 0; i < 20; i++) Checar(pote.Sortear(new Random(i)) is { } s && pote.Pendentes.Contains(s), "sorteio vem do pote");

            pote.Concluir(a); pote.Concluir(a);
            Checar(pote.Pendentes.Count == 1 && pote.Arquivo.Count == 1 && a.Concluida != null, "concluir move para o arquivo");
            pote.Concluir(b);
            Checar(pote.XpTotal == 110 && Pote.Nivel(pote.XpTotal).nivel == 2, "XP soma e sobe de nível");

            var arq = Path.Combine(Path.GetTempPath(), $"potinho-teste-{Guid.NewGuid()}.json");
            pote.Salvar(arq);
            var lido = Pote.Carregar(arq);
            File.Delete(arq);
            Checar(lido.Arquivo.Count == 2 && lido.XpTotal == 110 && lido.Arquivo[0].Texto == "B", "salvar e carregar");
            Checar(Pote.Carregar(arq).Pendentes.Count == 0, "arquivo inexistente = pote novo");

            var cheio = new Pote();
            for (int i = 0; i < 60; i++) cheio.Pendentes.Add(new Tarefa { Texto = $"T{i}", Criada = new DateTime(2026, 1, 1).AddMinutes(i) });
            using var view = new PoteView(cheio);
            foreach (var t in cheio.Pendentes)
            {
                var (c, _) = view.Vaga(t);
                Checar(c.X > 70 && c.X < 250 && c.Y > 200 && c.Y < 420, "papelzinho fica dentro do pote");
            }
            var ultimo = cheio.Pendentes[^1];
            Checar(view.PapelEm(view.Vaga(ultimo).centro) == ultimo, "clique pega o papel de cima");
            Checar(view.PapelEm(new PointF(160, 60)) == null, "clique fora do pote não pega nada");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FALHOU: " + ex.Message); return 1; }
    }

    static void Checar(bool ok, string nome) { if (!ok) throw new Exception(nome); }
}
