namespace Potinho;

public class MainForm : Form
{
    readonly Pote pote;
    readonly string caminho;
    readonly Random rng = new();

    readonly Label lblNivel = new() { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold) };
    readonly ProgressBar barra = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 22 };
    readonly Label lblXp = new() { AutoSize = true, Margin = new Padding(3, 4, 3, 12) };
    readonly TextBox txtTarefa = new() { Anchor = AnchorStyles.Left | AnchorStyles.Right, PlaceholderText = "Escreva uma pendência e aperte Enter", AccessibleName = "Nova pendência" };
    readonly NumericUpDown numXp = new() { Minimum = 1, Maximum = 1000, Value = 20, Increment = 5, Width = 70, AccessibleName = "XP da pendência" };
    readonly PoteView potinho;
    readonly ListBox lstArquivo = new() { Dock = DockStyle.Fill, IntegralHeight = false, AccessibleName = "Tarefas concluídas" };
    readonly TabPage abaPote = new(), abaArquivo = new();

    public MainForm(Pote pote, string caminho)
    {
        this.pote = pote;
        this.caminho = caminho;
        potinho = new PoteView(pote) { Dock = DockStyle.Fill };
        Text = "Potinho de Pendências";
        Font = new Font("Segoe UI", 10);
        ClientSize = new Size(560, 720);
        MinimumSize = new Size(440, 440);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        var btnAdd = Botao("Pôr no pote", Adicionar);
        AcceptButton = btnAdd;

        var linhaAdd = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 8) };
        linhaAdd.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) linhaAdd.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        linhaAdd.Controls.AddRange([txtTarefa, new Label { Text = "XP:", AutoSize = true, Anchor = AnchorStyles.Right }, numXp, btnAdd]);

        var botoes = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 6, 0, 0) };
        botoes.Controls.AddRange([Botao("Sortear!", Sortear), Botao("Concluir", Concluir), Botao("Devolver ao pote", Devolver), Botao("Remover", Remover)]);
        abaPote.Controls.Add(Coluna(potinho, botoes));
        abaArquivo.Controls.Add(lstArquivo);
        abaPote.Padding = abaArquivo.Padding = new Padding(6);

        var abas = new TabControl { Dock = DockStyle.Fill };
        abas.TabPages.AddRange([abaPote, abaArquivo]);

        var raiz = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
        for (int i = 0; i < 4; i++) raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        raiz.Controls.AddRange([lblNivel, barra, lblXp, linhaAdd, abas]);
        Controls.Add(raiz);

        Atualizar();
    }

    static Button Botao(string texto, Action acao)
    {
        var b = new Button { Text = texto, AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
        b.Click += (_, _) => acao();
        return b;
    }

    static TableLayoutPanel Coluna(Control cima, Control baixo)
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1 };
        t.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.AddRange([cima, baixo]);
        return t;
    }

    void Atualizar()
    {
        var (nivel, xp, prox) = Pote.Nivel(pote.XpTotal);
        lblNivel.Text = $"Nível {nivel}";
        barra.Maximum = prox;
        barra.Value = xp;
        lblXp.Text = $"{xp} / {prox} XP para o nível {nivel + 1}   ·   {pote.XpTotal} XP no total";
        potinho.Invalidate();
        lstArquivo.Items.Clear();
        lstArquivo.Items.AddRange(pote.Arquivo.ToArray());
        abaPote.Text = $"Pote ({pote.Pendentes.Count})";
        abaArquivo.Text = $"Arquivo ({pote.Arquivo.Count})";
    }

    bool Salvar()
    {
        try { pote.Salvar(caminho); return true; }
        catch (Exception ex)
        {
            MessageBox.Show($"Não consegui salvar em {caminho}:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    void Adicionar()
    {
        var texto = txtTarefa.Text.Trim();
        if (texto == "") { txtTarefa.Focus(); return; }
        var t = new Tarefa { Texto = texto, Xp = (int)numXp.Value };
        pote.Pendentes.Add(t);
        Salvar();
        potinho.Jogar(t);
        txtTarefa.Clear();
        txtTarefa.Focus();
        Atualizar();
    }

    void Sortear()
    {
        if (potinho.Ocupado) return;
        var resto = pote.Pendentes.Where(x => x != potinho.NaMao).ToList();
        if (resto.Count == 0)
        {
            MessageBox.Show(pote.Pendentes.Count == 0 ? "O pote está vazio. Escreva uma pendência lá em cima!" : "Só tem esse papel, e ele já está na sua mão.", Text);
            return;
        }
        potinho.Pegar(resto[rng.Next(resto.Count)]);
    }

    // Concluir, devolver e remover agem sobre o papel que saiu do pote.
    Tarefa? PapelNaMao()
    {
        if (potinho.Ocupado) return null;
        if (potinho.NaMao == null) MessageBox.Show("Tire um papel do pote primeiro: clique em Sortear! ou num papelzinho.", Text);
        return potinho.NaMao;
    }

    void Devolver()
    {
        if (PapelNaMao() is { } t) potinho.Jogar(t);
    }

    void Concluir()
    {
        if (PapelNaMao() is not { } t) return;
        if (MessageBox.Show($"Concluir \"{t.Texto}\" e ganhar {t.Xp} XP?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        int antes = Pote.Nivel(pote.XpTotal).nivel;
        pote.Concluir(t);
        potinho.Soltar();
        Salvar();
        Atualizar();
        int depois = Pote.Nivel(pote.XpTotal).nivel;
        MessageBox.Show(depois > antes ? $"+{t.Xp} XP!\n\nSUBIU PARA O NÍVEL {depois}!" : $"+{t.Xp} XP! Arquivada.", Text);
    }

    void Remover()
    {
        if (PapelNaMao() is not { } t) return;
        if (MessageBox.Show($"Tirar \"{t.Texto}\" do pote sem ganhar XP?", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        pote.Pendentes.Remove(t);
        potinho.Soltar();
        Salvar();
        Atualizar();
    }
}
